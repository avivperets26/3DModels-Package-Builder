[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$UnityExecutable,
    [string]$BlenderExecutable,
    [string]$ResultPointerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$commonScript = Join-Path $PSScriptRoot 'UnityCleanReimport.Common.ps1'
. $commonScript

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..'
}
$repositoryRootPath = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$templateRoot = Join-Path $repositoryRootPath 'engine-templates\unity\6000.3'
$toolsRoot = Join-Path $repositoryRootPath 'tools'
if ([string]::IsNullOrWhiteSpace($UnityExecutable)) {
    $UnityExecutable = 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe'
}
if ([string]::IsNullOrWhiteSpace($BlenderExecutable)) {
    $BlenderExecutable = Join-Path $toolsRoot 'blender\5.0.0\blender.exe'
}
$unityPath = [IO.Path]::GetFullPath($UnityExecutable)
$blenderPath = [IO.Path]::GetFullPath($BlenderExecutable)
if (-not (Test-Path -LiteralPath $unityPath -PathType Leaf)) {
    throw "Approved Unity executable is unavailable: $unityPath"
}
if (-not (Test-Path -LiteralPath $blenderPath -PathType Leaf) -or
    -not $blenderPath.StartsWith(
        $toolsRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Approved repository-contained Blender executable is unavailable: $blenderPath"
}

& (Join-Path $repositoryRootPath 'scripts\Test-UnityProductPolicies.ps1') `
    -RepositoryRoot $repositoryRootPath

$runId = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runRoot = Join-Path $repositoryRootPath "artifacts\u\$runId"
$cloneRoot = Join-Path $runRoot 'p'
$cleanCloneRoot = Join-Path $runRoot 'm'
$productRoot = Join-Path $cloneRoot 'Assets\PBMultiClipTests'
$fixturePath = Join-Path $productRoot 'Source\MultiClipProp.fbx'
$blenderLogPath = Join-Path $runRoot 'blender-multi-clip.log'
$unityLogPath = Join-Path $runRoot 'unity-multi-clip.log'
$packagePath = Join-Path $cloneRoot 'PackageBuilderExports\MultiClipProp.unitypackage'
$packageManifestPath = Join-Path $runRoot 'multi-clip-unitypackage-assets.txt'
$cleanImportLogPath = Join-Path $runRoot 'unity-multi-clip-clean-import.log'
$cleanValidationLogPath = Join-Path $runRoot 'unity-multi-clip-clean-validation.log'
$cleanResultPath = Join-Path $runRoot 'unity-multi-clip-clean-result.json'
$cacheRoot = Join-Path $repositoryRootPath 'runtime-data\unity\6000.3.10f1'
$temporaryRoot = Join-Path $cacheRoot 'temp'
$upmCacheRoot = Join-Path $cacheRoot 'upm-cache'
$upmConfigRoot = Join-Path $cacheRoot 'upm-config'

# Keep the generated Unity project below legacy Mono path limits used by pinned Unity packages.
$representativePackagePath = Join-Path $cloneRoot `
    'Library\PackageCache\com.unity.collections@000000000000\Unity.Collections.LowLevel.ILSupport\source~\Unity.Collections.LowLevel.ILSupport.CodeGen\Unity.Collections.LowLevel.ILSupport.CodeGen.asmdef.meta'
if ($representativePackagePath.Length -gt 248) {
    throw 'The multi-clip Unity integration clone is too deep for legacy package assembly APIs.'
}

New-Item -ItemType Directory -Path $runRoot, $cloneRoot, $temporaryRoot,
    $upmCacheRoot, $upmConfigRoot -Force | Out-Null
foreach ($rootName in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $templateRoot $rootName) -Destination $cloneRoot -Recurse
}
foreach ($folderName in @('Animations', 'Controllers', 'Documentation', 'Materials', 'Meshes',
        'Prefabs', 'Scenes', 'Scripts', 'Source', 'Textures')) {
    New-Item -ItemType Directory -Path (Join-Path $productRoot $folderName) -Force | Out-Null
}

$blenderArguments = @(
    '--background', '--factory-startup',
    '--python', (Join-Path $repositoryRootPath `
        'tests\blender\engine\pb0701_generate_rig_fbx.py'),
    '--', $fixturePath, 'animated'
)
$blenderProcess = Start-Process -FilePath $blenderPath -ArgumentList $blenderArguments `
    -Wait -PassThru -NoNewWindow -RedirectStandardOutput $blenderLogPath `
    -RedirectStandardError (Join-Path $runRoot 'blender-multi-clip-error.log')
if ($null -eq $blenderProcess.ExitCode -or [int]$blenderProcess.ExitCode -ne 0 -or
    -not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) {
    throw "The redistribution-safe animated FBX fixture could not be generated. See $blenderLogPath"
}

$originalEnvironment = @{}
$environment = [ordered]@{
    TEMP = $temporaryRoot
    TMP = $temporaryRoot
    UPM_NPM_CACHE_PATH = $upmCacheRoot
    UPM_CACHE_PATH = $upmCacheRoot
    UPM_CONFIG_PATH = $upmConfigRoot
    PACKAGEBUILDER_UNITY_MULTI_CLIP_PACKAGE_OUTPUT = $packagePath
    PACKAGEBUILDER_UNITY_MULTI_CLIP_PACKAGE_MANIFEST = $packageManifestPath
}
try {
    foreach ($entry in $environment.GetEnumerator()) {
        $originalEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    $unityArguments = @(
        '-batchmode', '-nographics', '-projectPath', $cloneRoot,
        '-executeMethod', 'PackageBuilder.UnityWorker.Editor.UnityMultiClipIntegration.Run',
        '-logFile', $unityLogPath
    )
    $unityProcess = Start-Process -FilePath $unityPath -ArgumentList $unityArguments `
        -Wait -PassThru -NoNewWindow
}
finally {
    foreach ($entry in $originalEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
}

$unityLog = if (Test-Path -LiteralPath $unityLogPath) {
    Get-Content -LiteralPath $unityLogPath -Raw -Encoding UTF8
}
else {
    throw 'Unity multi-clip integration log is missing.'
}
if ($null -eq $unityProcess.ExitCode -or [int]$unityProcess.ExitCode -ne 0 -or
    -not $unityLog.Contains('PACKAGEBUILDER_UNITY_MULTI_CLIP_PASS') -or
    $unityLog -match '(?m)(error CS\d+|PACKAGEBUILDER_UNITY_MULTI_CLIP_FAIL)') {
    $tail = (@(Get-Content -LiteralPath $unityLogPath -Tail 180) -join [Environment]::NewLine)
    throw "Unity multi-clip integration failed with exit code $($unityProcess.ExitCode).`n$tail"
}
if ($unityLog -match '(?im)(warning CS\d+|\bPackageBuilder[^\r\n]*\bwarning\b)') {
    throw 'Unity multi-clip integration log contains a package-caused warning.'
}
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $packageManifestPath -PathType Leaf)) {
    throw 'Multi-clip package or exact package manifest is missing.'
}
$manifestEntries = @(Get-Content -LiteralPath $packageManifestPath -Encoding UTF8 |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($manifestEntries.Count -eq 0 -or @($manifestEntries | Where-Object {
            $_ -ne 'Assets/PBMultiClipTests' -and
            -not $_.StartsWith('Assets/PBMultiClipTests/', [StringComparison]::Ordinal)
        }).Count -ne 0) {
    throw 'The exact multi-clip package plan contains unrelated assets.'
}

$cleanResult = Invoke-CleanUnityPackageValidation `
    -TemplateRoot $templateRoot -CleanCloneRoot $cleanCloneRoot -UnityPath $unityPath `
    -PackagePath $packagePath -ImportLogPath $cleanImportLogPath `
    -ValidationLogPath $cleanValidationLogPath -ResultPath $cleanResultPath `
    -ProductRootReference 'Assets/PBMultiClipTests' `
    -PrefabReference 'Assets/PBMultiClipTests/Prefabs/P_MultiClipProp.prefab' `
    -ValidationMode 'multi-clip-animated' -AddTemplatePreviewForValidation `
    -RequiredImportedAssets @(
        'Assets/PBMultiClipTests/Source/MultiClipProp.fbx',
        'Assets/PBMultiClipTests/Animations/A_MultiClipProp_Attack.anim',
        'Assets/PBMultiClipTests/Animations/A_MultiClipProp_BendLoop.anim',
        'Assets/PBMultiClipTests/Controllers/AC_MultiClipProp.controller',
        'Assets/PBMultiClipTests/Prefabs/P_MultiClipProp.prefab')
if ($cleanResult.schemaVersion -ne 1 -or -not $cleanResult.passed -or
    $cleanResult.validationMode -ne 'multi-clip-animated' -or
    $cleanResult.rendererCount -ne 1 -or $cleanResult.skinnedRendererCount -ne 1 -or
    $cleanResult.boneCount -ne 2 -or $cleanResult.animationClipCount -ne 2 -or
    $cleanResult.loopingClipCount -ne 1 -or $cleanResult.nonLoopingClipCount -ne 1 -or
    $cleanResult.controllerStateCount -ne 2 -or $cleanResult.animatorCount -ne 1 -or
    -not $cleanResult.animationMotionVerified -or @($cleanResult.findings).Count -ne 0) {
    throw 'Multi-clip clean reimport returned invalid structured evidence.'
}

$result = [ordered]@{
    schemaVersion = 1
    runRoot = $runRoot
    project = $cloneRoot
    cleanProject = $cleanCloneRoot
    package = $packagePath
    packageManifest = $packageManifestPath
    fixture = $fixturePath
    clips = @(
        Join-Path $productRoot 'Animations\A_MultiClipProp_Attack.anim'
        Join-Path $productRoot 'Animations\A_MultiClipProp_BendLoop.anim'
    )
    controller = Join-Path $productRoot 'Controllers\AC_MultiClipProp.controller'
    prefab = Join-Path $productRoot 'Prefabs\P_MultiClipProp.prefab'
    integrationLog = $unityLogPath
    cleanValidationLog = $cleanValidationLogPath
    cleanValidationResult = $cleanResultPath
}
$defaultResultPointerPath = Join-Path $runRoot 'integration-result.json'
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $defaultResultPointerPath -Encoding UTF8
if (-not [string]::IsNullOrWhiteSpace($ResultPointerPath)) {
    $resolvedPointerPath = [IO.Path]::GetFullPath($ResultPointerPath)
    New-Item -ItemType Directory -Path (Split-Path $resolvedPointerPath -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $defaultResultPointerPath -Destination $resolvedPointerPath -Force
}

Write-Host 'Unity multi-clip import, mixed loop settings, controller, and motion validation: passed'
Write-Host 'Unity multi-clip exact package and isolated clean reimport: passed'
Write-Host "Manual Unity project: $cloneRoot"
Write-Host "Clean reimport Unity project: $cleanCloneRoot"
Write-Host "Retained integration evidence: $runRoot"
