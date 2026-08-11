[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$PrivateFixtureRoot,
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
$privateFixturesRoot = Join-Path $repositoryRootPath 'runtime-data\fixtures\private'
if ([string]::IsNullOrWhiteSpace($PrivateFixtureRoot)) {
    $PrivateFixtureRoot = Join-Path $privateFixturesRoot 'Silverwing_Talonbow'
}
$privateRootPath = [IO.Path]::GetFullPath($PrivateFixtureRoot).TrimEnd([char[]]'\/')
if (-not $privateRootPath.StartsWith(
        $privateFixturesRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $privateRootPath -PathType Container)) {
    throw 'The Silverwing fixture must remain beneath runtime-data/fixtures/private.'
}
if ((Get-Item -LiteralPath $privateRootPath -Force).Attributes -band
    [IO.FileAttributes]::ReparsePoint) {
    throw 'The private Silverwing fixture root must not be a reparse point.'
}
$reparsePoints = @(Get-ChildItem -LiteralPath $privateRootPath -Recurse -Force |
    Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })
if ($reparsePoints.Count -ne 0) {
    throw 'The private Silverwing fixture must not contain reparse points.'
}
& git -C $repositoryRootPath check-ignore -q -- $privateRootPath
if ($LASTEXITCODE -ne 0) {
    throw 'The private Silverwing fixture is not protected by repository ignore policy.'
}

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

$blendSource = Join-Path $privateRootPath 'rigged_glb_bow_visible_curve.blend'
$textureSourceRoot = Join-Path $privateRootPath 'Silverwing_Talonbow_fbx'
$textureSources = [ordered]@{
    'T_SilverwingTalonbow_Albedo.png' = Join-Path $textureSourceRoot `
        'T_SilverwingTalonbow_Albeado.png'
    'T_SilverwingTalonbow_Emission.png' = Join-Path $textureSourceRoot `
        'T_SilverwingTalonbow_Emission.png'
    'T_SilverwingTalonbow_Metallic.png' = Join-Path $textureSourceRoot `
        'T_SilverwingTalonbow_Metallic.png'
    'T_SilverwingTalonbow_Normal.png' = Join-Path $textureSourceRoot `
        'T_SilverwingTalonbow_Normal.png'
    'T_SilverwingTalonbow_Roughness.png' = Join-Path $textureSourceRoot `
        'T_SilverwingTalonbow_Roughness.png'
}
$consumedPrivateFiles = @($blendSource) + @($textureSources.Values)
foreach ($sourcePath in $consumedPrivateFiles) {
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Required private Silverwing input is missing: $([IO.Path]::GetFileName($sourcePath))"
    }
}
$sourceHashesBefore = [ordered]@{}
foreach ($sourcePath in $consumedPrivateFiles) {
    $sourceHashesBefore[$sourcePath] = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
}

$runId = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runRoot = Join-Path $repositoryRootPath "artifacts\u\$runId"
$cloneRoot = Join-Path $runRoot 'p'
$cleanCloneRoot = Join-Path $runRoot 's'
$preparedRoot = Join-Path $runRoot 'prepared'
$preparedFbx = Join-Path $preparedRoot 'SilverwingTalonbow.fbx'
$preparationReportPath = Join-Path $preparedRoot 'silverwing-prepare-report.json'
$blenderLogPath = Join-Path $runRoot 'blender-silverwing.log'
$unityLogPath = Join-Path $runRoot 'unity-silverwing.log'
$packagePath = Join-Path $cloneRoot 'PackageBuilderExports\SilverwingTalonbow.unitypackage'
$packageManifestPath = Join-Path $runRoot 'silverwing-unitypackage-assets.txt'
$cleanImportLogPath = Join-Path $runRoot 'unity-silverwing-clean-import.log'
$cleanValidationLogPath = Join-Path $runRoot 'unity-silverwing-clean-validation.log'
$cleanResultPath = Join-Path $runRoot 'unity-silverwing-clean-result.json'
$extractRoot = Join-Path $runRoot 'silverwing-unitypackage-extracted'
$cacheRoot = Join-Path $repositoryRootPath 'runtime-data\unity\6000.3.10f1'
$temporaryRoot = Join-Path $cacheRoot 'temp'
$upmCacheRoot = Join-Path $cacheRoot 'upm-cache'
$upmConfigRoot = Join-Path $cacheRoot 'upm-config'
New-Item -ItemType Directory -Path $runRoot, $cloneRoot, $preparedRoot,
    $temporaryRoot, $upmCacheRoot, $upmConfigRoot -Force | Out-Null

$blenderArguments = @(
    '--background', $blendSource, '--disable-autoexec',
    '--python', (Join-Path $repositoryRootPath `
        'tests\blender\engine\pb0712_prepare_silverwing.py'),
    '--', $repositoryRootPath, $preparedFbx, $preparationReportPath
)
$blenderProcess = Start-Process -FilePath $blenderPath -ArgumentList $blenderArguments `
    -Wait -PassThru -NoNewWindow -RedirectStandardOutput $blenderLogPath `
    -RedirectStandardError (Join-Path $runRoot 'blender-silverwing-error.log')
if ($null -eq $blenderProcess.ExitCode -or [int]$blenderProcess.ExitCode -ne 0 -or
    -not (Test-Path -LiteralPath $preparedFbx -PathType Leaf) -or
    -not (Test-Path -LiteralPath $preparationReportPath -PathType Leaf)) {
    throw "Silverwing Blender normalization failed. See $blenderLogPath"
}
$preparationReport = Get-Content -LiteralPath $preparationReportPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ($preparationReport.schemaVersion -ne 1 -or $preparationReport.actionName -ne 'Bow_Shot' -or
    $preparationReport.armatureCount -ne 1 -or $preparationReport.boneCount -ne 38 -or
    @($preparationReport.meshNames).Count -ne 2 -or
    @($preparationReport.rootBoneNames).Count -ne 1) {
    throw 'Silverwing Blender normalization returned invalid structured evidence.'
}

foreach ($rootName in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $templateRoot $rootName) -Destination $cloneRoot -Recurse
}
$productRoot = Join-Path $cloneRoot 'Assets\PBSilverwingTests'
foreach ($folderName in @('Animations', 'Controllers', 'Documentation', 'Materials', 'Meshes',
        'Prefabs', 'Scenes', 'Scripts', 'Source', 'Textures')) {
    New-Item -ItemType Directory -Path (Join-Path $productRoot $folderName) -Force | Out-Null
}
Copy-Item -LiteralPath $preparedFbx -Destination (Join-Path $productRoot `
        'Source\SilverwingTalonbow.fbx')
foreach ($entry in $textureSources.GetEnumerator()) {
    Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $productRoot `
            ('Textures\' + $entry.Key))
}

$previewTemplateRoot = Join-Path $cloneRoot 'Assets\PackageBuilder\Preview'
$productScriptsRoot = Join-Path $productRoot 'Scripts'
foreach ($fileName in @(
        'PackageBuilder.Preview.asmdef',
        'PackageBuilder.Preview.asmdef.meta',
        'PackageBuilderPreviewController.cs',
        'PackageBuilderPreviewController.cs.meta',
        'PackageBuilderAnimationTransport.cs',
        'PackageBuilderAnimationTransport.cs.meta')) {
    Move-Item -LiteralPath (Join-Path $previewTemplateRoot $fileName) `
        -Destination (Join-Path $productScriptsRoot $fileName)
}

$originalEnvironment = @{}
$environment = [ordered]@{
    TEMP = $temporaryRoot
    TMP = $temporaryRoot
    UPM_NPM_CACHE_PATH = $upmCacheRoot
    UPM_CACHE_PATH = $upmCacheRoot
    UPM_CONFIG_PATH = $upmConfigRoot
    PACKAGEBUILDER_UNITY_SILVERWING_PACKAGE_OUTPUT = $packagePath
    PACKAGEBUILDER_UNITY_SILVERWING_PACKAGE_MANIFEST = $packageManifestPath
}
try {
    foreach ($entry in $environment.GetEnumerator()) {
        $originalEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    $unityArguments = @(
        '-batchmode', '-nographics', '-projectPath', $cloneRoot,
        '-executeMethod', 'PackageBuilder.UnityWorker.Editor.UnitySilverwingIntegration.Run',
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
    throw 'Unity Silverwing integration log is missing.'
}
if ($null -eq $unityProcess.ExitCode -or [int]$unityProcess.ExitCode -ne 0 -or
    -not $unityLog.Contains('PACKAGEBUILDER_UNITY_SILVERWING_PASS') -or
    $unityLog -match '(?m)(error CS\d+|PACKAGEBUILDER_UNITY_SILVERWING_FAIL)') {
    $tail = (@(Get-Content -LiteralPath $unityLogPath -Tail 180) -join [Environment]::NewLine)
    throw "Unity Silverwing integration failed with exit code $($unityProcess.ExitCode).`n$tail"
}
if ($unityLog -match '(?im)(warning CS\d+|\bPackageBuilder[^\r\n]*\bwarning\b)') {
    throw 'Unity Silverwing integration log contains a package-caused warning.'
}
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $packageManifestPath -PathType Leaf)) {
    throw 'Silverwing package or exact package manifest is missing.'
}

New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null
& tar.exe -xzf $packagePath -C $extractRoot
if ($LASTEXITCODE -ne 0) {
    throw 'Silverwing package archive extraction failed.'
}
$packageEntries = @(Get-ChildItem -LiteralPath $extractRoot -Recurse -File -Filter pathname |
    ForEach-Object { (Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8).Trim() } |
    Sort-Object -Unique)
$expectedEntries = @(Get-Content -LiteralPath $packageManifestPath -Encoding UTF8 |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
if (@(Compare-Object -ReferenceObject $expectedEntries -DifferenceObject $packageEntries).Count -ne 0 -or
    @($packageEntries | Where-Object {
            $_ -ne 'Assets/PBSilverwingTests' -and
            -not $_.StartsWith('Assets/PBSilverwingTests/', [StringComparison]::Ordinal)
        }).Count -ne 0) {
    throw 'Silverwing package archive differs from its exact contained plan.'
}

$cleanResult = Invoke-CleanUnityPackageValidation `
    -TemplateRoot $templateRoot -CleanCloneRoot $cleanCloneRoot -UnityPath $unityPath `
    -PackagePath $packagePath -ImportLogPath $cleanImportLogPath `
    -ValidationLogPath $cleanValidationLogPath -ResultPath $cleanResultPath `
    -ProductRootReference 'Assets/PBSilverwingTests' `
    -PrefabReference 'Assets/PBSilverwingTests/Prefabs/P_SilverwingTalonbow.prefab' `
    -SceneReference 'Assets/PBSilverwingTests/Scenes/S_SilverwingTalonbow_Overview.unity' `
    -ValidationMode 'silverwing-animated' -RequiredImportedAssets @(
        'Assets/PBSilverwingTests/Source/SilverwingTalonbow.fbx',
        'Assets/PBSilverwingTests/Animations/A_SilverwingTalonbow_Bow_Shot.anim',
        'Assets/PBSilverwingTests/Controllers/AC_SilverwingTalonbow.controller',
        'Assets/PBSilverwingTests/Prefabs/P_SilverwingTalonbow.prefab',
        'Assets/PBSilverwingTests/Scenes/S_SilverwingTalonbow_Overview.unity',
        'Assets/PBSilverwingTests/Materials/M_SilverwingTalonbow_URP.mat')
if ($cleanResult.schemaVersion -ne 1 -or -not $cleanResult.passed -or
    $cleanResult.validationMode -ne 'silverwing-animated' -or
    $cleanResult.rendererCount -ne 2 -or $cleanResult.skinnedRendererCount -ne 2 -or
    $cleanResult.boneCount -ne 38 -or $cleanResult.animationClipCount -ne 1 -or
    $cleanResult.animatorCount -ne 1 -or -not $cleanResult.synchronizedRendererMotionVerified -or
    @($cleanResult.clipNames).Count -ne 1 -or
    $cleanResult.clipNames[0] -ne 'A_SilverwingTalonbow_Bow_Shot' -or
    @($cleanResult.findings).Count -ne 0) {
    throw 'Silverwing clean reimport returned invalid structured evidence.'
}

foreach ($sourcePath in $consumedPrivateFiles) {
    $after = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    if ($after -ne $sourceHashesBefore[$sourcePath]) {
        throw "Private Silverwing input changed during validation: $([IO.Path]::GetFileName($sourcePath))"
    }
}

$result = [ordered]@{
    schemaVersion = 1
    runRoot = $runRoot
    project = $cloneRoot
    cleanProject = $cleanCloneRoot
    package = $packagePath
    packageManifest = $packageManifestPath
    scene = Join-Path $productRoot 'Scenes\S_SilverwingTalonbow_Overview.unity'
    prefab = Join-Path $productRoot 'Prefabs\P_SilverwingTalonbow.prefab'
    clip = Join-Path $productRoot 'Animations\A_SilverwingTalonbow_Bow_Shot.anim'
    preparationReport = $preparationReportPath
    integrationLog = $unityLogPath
    cleanValidationLog = $cleanValidationLogPath
    cleanValidationResult = $cleanResultPath
    privateInputsUnchanged = $true
}
$defaultResultPointerPath = Join-Path $runRoot 'integration-result.json'
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $defaultResultPointerPath -Encoding UTF8
if (-not [string]::IsNullOrWhiteSpace($ResultPointerPath)) {
    $resolvedPointerPath = [IO.Path]::GetFullPath($ResultPointerPath)
    New-Item -ItemType Directory -Path (Split-Path $resolvedPointerPath -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $defaultResultPointerPath -Destination $resolvedPointerPath -Force
}

Write-Host 'Silverwing normalized private-source preparation: passed'
Write-Host 'Silverwing Generic rig, textures, Bow_Shot, body/string motion, and package export: passed'
Write-Host 'Silverwing isolated clean package reimport: passed'
Write-Host "Manual Unity project: $cloneRoot"
Write-Host "Clean reimport Unity project: $cleanCloneRoot"
Write-Host "Retained integration evidence: $runRoot"
