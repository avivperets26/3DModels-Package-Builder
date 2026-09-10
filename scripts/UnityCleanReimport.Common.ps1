Set-StrictMode -Version Latest

function Invoke-CleanUnityPackageValidation {
    <#
    .SYNOPSIS
    Imports one exact package into a fresh Unity clone and runs the selected structured validator.
    #>
    param(
        [Parameter(Mandatory)][string]$TemplateRoot,
        [Parameter(Mandatory)][string]$CleanCloneRoot,
        [Parameter(Mandatory)][string]$UnityPath,
        [Parameter(Mandatory)][string]$PackagePath,
        [Parameter(Mandatory)][string]$ImportLogPath,
        [Parameter(Mandatory)][string]$ValidationLogPath,
        [Parameter(Mandatory)][string]$ResultPath,
        [Parameter(Mandatory)][string]$ProductRootReference,
        [Parameter(Mandatory)][string]$PrefabReference,
        [Parameter(Mandatory)][string]$ValidationMode,
        [string]$SceneReference = '',
        [switch]$AddTemplatePreviewForValidation,
        [Parameter(Mandatory)][string[]]$RequiredImportedAssets
    )

    New-Item -ItemType Directory -Path $CleanCloneRoot -Force | Out-Null
    foreach ($rootName in @('Assets', 'Packages', 'ProjectSettings')) {
        Copy-Item -LiteralPath (Join-Path $TemplateRoot $rootName) -Destination $CleanCloneRoot -Recurse
    }
    $cleanWorkerPackageRoot = Join-Path $CleanCloneRoot 'Packages\com.packagebuilder.worker'
    if (Test-Path -LiteralPath $cleanWorkerPackageRoot) {
        Remove-Item -LiteralPath $cleanWorkerPackageRoot -Recurse -Force
    }
    $cleanPreviewRoot = Join-Path $CleanCloneRoot 'Assets\PackageBuilder'
    if (Test-Path -LiteralPath $cleanPreviewRoot) {
        Remove-Item -LiteralPath $cleanPreviewRoot -Recurse -Force
    }
    $cleanPreviewMetaPath = "$cleanPreviewRoot.meta"
    if (Test-Path -LiteralPath $cleanPreviewMetaPath) {
        Remove-Item -LiteralPath $cleanPreviewMetaPath -Force
    }
    $physicalProductRoot = Join-Path $CleanCloneRoot ($ProductRootReference -replace '/', '\')
    if (Test-Path -LiteralPath $physicalProductRoot) {
        throw "The clean Unity clone unexpectedly contains product assets: $ProductRootReference"
    }

    $cleanImportArguments = @(
        '-batchmode', '-nographics', '-quit', '-projectPath', $CleanCloneRoot,
        '-importPackage', $PackagePath, '-logFile', $ImportLogPath
    )
    $cleanImportProcess = Start-Process -FilePath $UnityPath `
        -ArgumentList $cleanImportArguments -PassThru -WindowStyle Hidden
    $cleanImportProcess.WaitForExit()
    $cleanImportLog = if (Test-Path -LiteralPath $ImportLogPath) {
        Get-Content -LiteralPath $ImportLogPath -Raw -Encoding UTF8
    }
    else {
        throw 'Unity clean package import log is missing.'
    }
    if ($null -eq $cleanImportProcess.ExitCode -or [int]$cleanImportProcess.ExitCode -ne 0 -or
        $cleanImportLog -match '(?m)(error CS\d+|Aborting batchmode due to failure)') {
        $tail = (@(Get-Content -LiteralPath $ImportLogPath -Tail 160) -join `
                [Environment]::NewLine)
        $exitDisplay = if ($null -eq $cleanImportProcess.ExitCode) { 'unavailable' } else {
            $cleanImportProcess.ExitCode
        }
        throw "Unity clean package import failed with exit code $exitDisplay.`n$tail"
    }
    foreach ($requiredImportedAsset in $RequiredImportedAssets) {
        $requiredPath = Join-Path $CleanCloneRoot ($requiredImportedAsset -replace '/', '\')
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Unity clean package import omitted required asset: $requiredImportedAsset"
        }
    }

    if ($AddTemplatePreviewForValidation) {
        Copy-Item -LiteralPath (Join-Path $TemplateRoot 'Assets\PackageBuilder') `
            -Destination (Join-Path $CleanCloneRoot 'Assets') -Recurse
        Copy-Item -LiteralPath (Join-Path $TemplateRoot 'Assets\PackageBuilder.meta') `
            -Destination (Join-Path $CleanCloneRoot 'Assets\PackageBuilder.meta')
    }
    Copy-Item -LiteralPath (Join-Path $TemplateRoot 'Packages\com.packagebuilder.worker') `
        -Destination (Join-Path $CleanCloneRoot 'Packages') -Recurse
    [Environment]::SetEnvironmentVariable(
        'PACKAGEBUILDER_UNITY_REIMPORT_RESULT', $ResultPath, 'Process')
    [Environment]::SetEnvironmentVariable(
        'PACKAGEBUILDER_UNITY_REIMPORT_MODE', $ValidationMode, 'Process')
    [Environment]::SetEnvironmentVariable(
        'PACKAGEBUILDER_UNITY_PRODUCT_ROOT', $ProductRootReference, 'Process')
    [Environment]::SetEnvironmentVariable(
        'PACKAGEBUILDER_UNITY_PRODUCT_PREFAB', $PrefabReference, 'Process')
    [Environment]::SetEnvironmentVariable(
        'PACKAGEBUILDER_UNITY_OVERVIEW_SCENE', $SceneReference, 'Process')

    $cleanValidationArguments = @(
        '-batchmode', '-nographics', '-projectPath', $CleanCloneRoot,
        '-executeMethod', 'PackageBuilder.UnityWorker.Editor.UnityCleanReimportIntegration.Run',
        '-logFile', $ValidationLogPath
    )
    $cleanValidationProcess = Start-Process -FilePath $UnityPath `
        -ArgumentList $cleanValidationArguments -PassThru -WindowStyle Hidden
    $cleanValidationProcess.WaitForExit()
    $cleanValidationLog = if (Test-Path -LiteralPath $ValidationLogPath) {
        Get-Content -LiteralPath $ValidationLogPath -Raw -Encoding UTF8
    }
    else {
        throw 'Unity clean package validation log is missing.'
    }
    $cleanValidationExitCode = $cleanValidationProcess.ExitCode
    if ($null -eq $cleanValidationExitCode -or [int]$cleanValidationExitCode -ne 0 -or
        -not $cleanValidationLog.Contains('PACKAGEBUILDER_UNITY_CLEAN_REIMPORT_PASS') -or
        $cleanValidationLog -match '(?m)(error CS\d+|PACKAGEBUILDER_UNITY_CLEAN_REIMPORT_FAIL)') {
        $tail = (@(Get-Content -LiteralPath $ValidationLogPath -Tail 160) -join `
                [Environment]::NewLine)
        $exitDisplay = if ($null -eq $cleanValidationExitCode) { 'unavailable' } else {
            $cleanValidationExitCode
        }
        throw "Unity clean package validation failed with exit code $exitDisplay.`n$tail"
    }
    if (-not (Test-Path -LiteralPath $ResultPath -PathType Leaf)) {
        throw 'Unity clean package validation did not write its structured result.'
    }
    return Get-Content -LiteralPath $ResultPath -Raw -Encoding UTF8 | ConvertFrom-Json
}
