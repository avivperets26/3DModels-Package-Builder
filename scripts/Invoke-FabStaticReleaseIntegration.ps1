[CmdletBinding()]
param([string]$UnityResultPointer, [switch]$KeepArtifacts)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd([char[]]'\/')
. (Join-Path $PSScriptRoot 'Enter-PackageBuilderEnvironment.ps1')
. (Join-Path $PSScriptRoot 'UnityTestArtifacts.Common.ps1')
. (Join-Path $PSScriptRoot 'UnityCleanReimport.Common.ps1')
$evidence = Join-Path $repository 'artifacts/PB-1010'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$portableWorkspace = $null
$unity = $null
$process = $null
$oldEnvironment = @{}
try {
    if (-not $UnityResultPointer) {
        $UnityResultPointer = Join-Path $evidence ('unity-' + [Guid]::NewGuid().ToString('N') + '.json')
        & (Join-Path $PSScriptRoot 'Invoke-UnityProductIntegration.ps1') -RepositoryRoot $repository `
            -ResultPointerPath $UnityResultPointer -KeepArtifacts
    }
    $pointer = [IO.Path]::GetFullPath($UnityResultPointer)
    if (-not $pointer.StartsWith($evidence + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Existing Unity pointer must belong to PB-1010.'
    }
    $unity = Get-Content -LiteralPath $pointer -Raw | ConvertFrom-Json
    $run = [IO.Path]::GetFullPath($unity.runRoot)
    $project = [IO.Path]::GetFullPath($unity.cleanProject)
    if ((Split-Path $run -Parent) -ne (Join-Path $repository 'artifacts/u') -or
        (Split-Path $run -Leaf) -cnotmatch '^[0-9a-f]{8}$' -or $project -ne (Join-Path $run 'r')) {
        throw 'Unity result must refer to an owned clean integration clone.'
    }
    if (-not (Get-Content -LiteralPath $unity.cleanReimportResult -Raw | ConvertFrom-Json).passed) {
        throw 'Fresh clean-import evidence failed.'
    }
    # Prepare a disposable fixture, then inspect only its newly exported and clean-imported bytes.
    Copy-Item -LiteralPath (Join-Path $repository 'engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityFabReleaseInspection.cs') `
        -Destination (Join-Path $project 'Packages/com.packagebuilder.worker/Editor/UnityFabReleaseInspection.cs') -Force
    $cache = Join-Path $repository 'runtime-data/unity/6000.3.10f1'
    $environment = @{ TEMP = "$cache/temp"; TMP = "$cache/temp"; UPM_CACHE_PATH = "$cache/upm-cache";
        UPM_NPM_CACHE_PATH = "$cache/upm-cache"; UPM_CONFIG_PATH = "$cache/upm-config" }
    foreach ($key in $environment.Keys) {
        $oldEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $environment[$key], 'Process')
    }
    $unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe'
    $process = Start-Process -FilePath $unityPath -WindowStyle Hidden -PassThru `
        -ArgumentList @('-batchmode', '-nographics', '-projectPath', ('"' + $project + '"'), '-executeMethod',
            'PackageBuilder.UnityWorker.Editor.UnityFabReleaseInspection.Prepare', '-logFile', ('"' + "$run/fab-prepare.log" + '"'))
    if (-not $process.WaitForExit(900000)) { Stop-Process -Id $process.Id -Force; $process.WaitForExit(); throw 'Fab preparation timed out.' }
    if ($process.ExitCode -ne 0) { throw "Fab preparation failed; see $run/fab-prepare.log" }
    $newPackage = Join-Path $project 'PackageBuilderExports/StoneArchFab.unitypackage'
    $project = Join-Path $run 's'
    if (Test-Path -LiteralPath $project) { throw 'Fresh Fab import destination already exists.' }
    foreach ($key in @('PACKAGEBUILDER_UNITY_REIMPORT_RESULT', 'PACKAGEBUILDER_UNITY_REIMPORT_MODE',
            'PACKAGEBUILDER_UNITY_PRODUCT_ROOT', 'PACKAGEBUILDER_UNITY_PRODUCT_PREFAB', 'PACKAGEBUILDER_UNITY_OVERVIEW_SCENE')) {
        $oldEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
    }
    Invoke-CleanUnityPackageValidation -TemplateRoot (Join-Path $repository 'engine-templates/unity/6000.3') `
        -CleanCloneRoot $project -UnityPath $unityPath -PackagePath $newPackage `
        -ImportLogPath "$run/fab-import.log" -ValidationLogPath "$run/fab-clean-validation.log" `
        -ResultPath "$run/fab-clean-reimport.json" -ProductRootReference 'Assets/PBModelTests' `
        -PrefabReference 'Assets/PBModelTests/Prefabs/P_StoneArch.prefab' -ValidationMode 'overview' `
        -SceneReference 'Assets/PBModelTests/Scenes/S_StoneArch_Overview.unity' `
        -RequiredImportedAssets @('Assets/PBModelTests/Source/StoneArch.fbx', 'Assets/PBModelTests/Documentation/README.txt',
            'Assets/PBModelTests/Prefabs/P_StoneArch.prefab')
    $unity.cleanProject = $project
    $unity.package = $newPackage
    $unity.packageManifest = Join-Path $run 'fab-package-assets.txt'
    $unity.cleanReimportResult = Join-Path $run 'fab-clean-reimport.json'
    $pointer = Join-Path $evidence 'prepared-result.json'
    $unity | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $pointer -Encoding UTF8
    $process = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe' -WindowStyle Hidden -PassThru `
        -ArgumentList @('-batchmode', '-projectPath', ('"' + $project + '"'), '-executeMethod',
            'PackageBuilder.UnityWorker.Editor.UnityFabReleaseInspection.Run', '-logFile', ('"' + "$run/fab-inspection.log" + '"'))
    if (-not $process.WaitForExit(900000)) { Stop-Process -Id $process.Id -Force; $process.WaitForExit(); throw 'Fab inspection exceeded 15 minutes.' }
    if ($process.ExitCode -ne 0) { throw "Fab Unity inspection failed; see $run/fab-inspection.log" }
    $portable = @{}
    $latest = Join-Path $repository 'artifacts/PB-0507/manual/latest.txt'
    $previousPointer = if (Test-Path -LiteralPath $latest) { Get-Content -LiteralPath $latest -Raw } else { '' }
    try { & (Join-Path $PSScriptRoot 'Invoke-PB0507PortableStaticManualTest.ps1') }
    finally {
        # Capture only this invocation's workspace, including failure after its pointer was written.
        if ((Test-Path -LiteralPath $latest) -and (Get-Content -LiteralPath $latest -Raw) -ne $previousPointer) {
            foreach ($line in Get-Content -LiteralPath $latest) {
                $parts = $line.Split('=', 2); if ($parts.Length -eq 2) { $portable[$parts[0]] = $parts[1] }
            }
            $portableWorkspace = [IO.Path]::GetFullPath($portable.workspace)
        }
    }
    if (-not $portableWorkspace) { throw 'Portable test did not produce a fresh workspace.' }
    if ((Split-Path $portableWorkspace -Parent) -ne (Join-Path $repository 'artifacts/PB-0507/manual') -or
        (Split-Path $portableWorkspace -Leaf) -notmatch '^run-\d{8}-\d{6}-[0-9a-f]{32}$') { throw 'Unexpected portable workspace.' }
    $fbx = @(Get-ChildItem -LiteralPath (Join-Path $portableWorkspace 'inspection') -Filter '*.fbx' -Recurse -File)
    if ($fbx.Count -ne 1) { throw 'Expected one static FBX.' }
    & (Join-Path $repository 'tools/blender/5.0.0/blender.exe') --background --factory-startup `
        --python (Join-Path $repository 'tests/blender/engine/pb1010_observe_static_fbx.py') -- `
        $fbx[0].FullName (Join-Path $run 'fab-portable-reimport.json')
    if ($LASTEXITCODE -ne 0) { throw 'Portable FBX reimport failed.' }
    $oldEnvironment['PB_FAB_REAL_RELEASE_POINTER'] = [Environment]::GetEnvironmentVariable('PB_FAB_REAL_RELEASE_POINTER', 'Process')
    $env:PB_FAB_REAL_RELEASE_POINTER = $pointer
    & dotnet test (Join-Path $repository 'tests/PackageBuilder.App.Wpf.Tests/PackageBuilder.App.Wpf.Tests.csproj') `
        -c Release --no-restore --filter 'FullyQualifiedName~ReviewedCandidateRequiresCompatibilityAndApprovalBeforeRelease' `
        --logger 'trx;LogFileName=fab-release.trx' --results-directory $run
    if ($LASTEXITCODE -ne 0) { throw "Real Fab release validation failed; see $run" }
    $result = Get-Content -LiteralPath (Join-Path $run 'fab-release-result.json') -Raw | ConvertFrom-Json
    if (-not $result.passed -or -not $result.realEngineRun) { throw 'Missing real release pass receipt.' }
    Copy-Item -LiteralPath (Join-Path $run 'fab-release-result.json') -Destination $evidence -Force
    Write-Host "PASS: real static Fab release; evidence $run"
}
finally {
    foreach ($key in $oldEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key, $oldEnvironment[$key], 'Process') }
    try {
        if ($null -ne $process -and $process.HasExited) {
            Stop-CompletedUnityTestHub -RepositoryRoot $repository -ProjectPath $project -CompletedEditorProcessId $process.Id
        }
    }
    finally {
        try {
            if ($null -ne $unity) { Remove-UnityTestArtifacts -RepositoryRoot $repository -RunRoot $unity.runRoot -KeepArtifacts:$KeepArtifacts }
        }
        finally {
            if ($KeepArtifacts) { Write-Host "Retained for active diagnosis: $portableWorkspace; clean this exact run after inspection." }
            if ($portableWorkspace -and -not $KeepArtifacts) {
                # Exact generated run, verified again before recursive removal; compact evidence survives separately.
                if ((Split-Path $portableWorkspace -Parent) -ne (Join-Path $repository 'artifacts/PB-0507/manual') -or
                    (Split-Path $portableWorkspace -Leaf) -notmatch '^run-\d{8}-\d{6}-[0-9a-f]{32}$') { throw 'Unsafe portable cleanup root.' }
                $ancestor = $portableWorkspace
                while ($ancestor) {
                    if ((Test-Path -LiteralPath $ancestor) -and
                        ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe portable cleanup ancestor.' }
                    $ancestor = Split-Path $ancestor -Parent
                }
                $items = @(Get-Item -LiteralPath $portableWorkspace) + @(Get-ChildItem -LiteralPath $portableWorkspace -Force -Recurse)
                if ($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Portable cleanup encountered a reparse point.' }
                foreach ($record in @(@('report', 'portable-validation-report.json'), @('log', 'portable-job.log'))) {
                    if ($portable.ContainsKey($record[0]) -and (Test-Path -LiteralPath $portable[$record[0]])) {
                        Copy-Item -LiteralPath $portable[$record[0]] -Destination (Join-Path $evidence $record[1]) -Force
                    }
                }
                Remove-Item -LiteralPath $portableWorkspace -Recurse -Force
                if (Test-Path -LiteralPath $portableWorkspace) { throw 'Portable test artifacts remain.' }
            }
        }
    }
}
