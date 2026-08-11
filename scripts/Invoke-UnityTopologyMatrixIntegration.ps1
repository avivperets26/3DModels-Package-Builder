[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$UnityExecutable,
    [string]$BlenderExecutable,
    [string]$ResultPointerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'UnityCleanReimport.Common.ps1')

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
$cleanCloneRoot = Join-Path $runRoot 't'
$generatedRoot = Join-Path $runRoot 'g'
$productRoot = Join-Path $cloneRoot 'Assets\PBTopologyMatrixTests'
$blenderLogPath = Join-Path $runRoot 'blender-topology-matrix.log'
$unityLogPath = Join-Path $runRoot 'unity-topology-matrix.log'
$packagePath = Join-Path $cloneRoot 'PackageBuilderExports\GenericTopologyMatrix.unitypackage'
$packageManifestPath = Join-Path $runRoot 'topology-unitypackage-assets.txt'
$cleanImportLogPath = Join-Path $runRoot 'unity-topology-clean-import.log'
$cleanValidationLogPath = Join-Path $runRoot 'unity-topology-clean-validation.log'
$cleanResultPath = Join-Path $runRoot 'unity-topology-clean-result.json'
$cacheRoot = Join-Path $repositoryRootPath 'runtime-data\unity\6000.3.10f1'
$temporaryRoot = Join-Path $cacheRoot 'temp'
$upmCacheRoot = Join-Path $cacheRoot 'upm-cache'
$upmConfigRoot = Join-Path $cacheRoot 'upm-config'

$representativePackagePath = Join-Path $cloneRoot `
    'Library\PackageCache\com.unity.collections@000000000000\Unity.Collections.LowLevel.ILSupport\source~\Unity.Collections.LowLevel.ILSupport.CodeGen\Unity.Collections.LowLevel.ILSupport.CodeGen.asmdef.meta'
if ($representativePackagePath.Length -gt 248) {
    throw 'The topology-matrix Unity integration clone is too deep for legacy package APIs.'
}

New-Item -ItemType Directory -Path $runRoot, $cloneRoot, $generatedRoot,
    $temporaryRoot, $upmCacheRoot, $upmConfigRoot -Force | Out-Null
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
        'tests\blender\engine\pb0714_generate_topology_matrix.py'),
    '--', $generatedRoot
)
$blenderProcess = Start-Process -FilePath $blenderPath -ArgumentList $blenderArguments `
    -Wait -PassThru -NoNewWindow -RedirectStandardOutput $blenderLogPath `
    -RedirectStandardError (Join-Path $runRoot 'blender-topology-matrix-error.log')
if ($null -eq $blenderProcess.ExitCode -or [int]$blenderProcess.ExitCode -ne 0) {
    throw "The procedural topology matrix could not be generated. See $blenderLogPath"
}
$expectedModels = @(
    'BipedTail.fbx',
    'InvalidMultiRoot.fbx',
    'MechanicalBow.fbx',
    'MechanicalVehicle.fbx',
    'QuadrupedTail.fbx',
    'WingedCreature.fbx'
)
$actualModels = @(Get-ChildItem -LiteralPath $generatedRoot -File -Filter '*.fbx' |
    Select-Object -ExpandProperty Name | Sort-Object)
if (-not ($actualModels -join "`n").Equals(($expectedModels -join "`n"),
        [StringComparison]::Ordinal) -or
    -not (Test-Path -LiteralPath (Join-Path $generatedRoot 'topology-matrix.txt') -PathType Leaf)) {
    throw 'The generated topology-matrix inventory is not exact.'
}
foreach ($model in $expectedModels) {
    Copy-Item -LiteralPath (Join-Path $generatedRoot $model) `
        -Destination (Join-Path $productRoot "Source\$model")
}
Copy-Item -LiteralPath (Join-Path $generatedRoot 'topology-matrix.txt') `
    -Destination (Join-Path $productRoot 'Documentation\topology-matrix.txt')

$originalEnvironment = @{}
$environment = [ordered]@{
    TEMP = $temporaryRoot
    TMP = $temporaryRoot
    UPM_NPM_CACHE_PATH = $upmCacheRoot
    UPM_CACHE_PATH = $upmCacheRoot
    UPM_CONFIG_PATH = $upmConfigRoot
    PACKAGEBUILDER_UNITY_TOPOLOGY_MATRIX_PACKAGE_OUTPUT = $packagePath
    PACKAGEBUILDER_UNITY_TOPOLOGY_MATRIX_PACKAGE_MANIFEST = $packageManifestPath
}
try {
    foreach ($entry in $environment.GetEnumerator()) {
        $originalEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable(
            $entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    $unityArguments = @(
        '-batchmode', '-nographics', '-projectPath', $cloneRoot,
        '-executeMethod', 'PackageBuilder.UnityWorker.Editor.UnityTopologyMatrixIntegration.Run',
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
    throw 'Unity topology-matrix integration log is missing.'
}
if ($null -eq $unityProcess.ExitCode -or [int]$unityProcess.ExitCode -ne 0 -or
    -not $unityLog.Contains('PACKAGEBUILDER_UNITY_TOPOLOGY_MATRIX_PASS') -or
    $unityLog -match '(?m)(error CS\d+|PACKAGEBUILDER_UNITY_TOPOLOGY_MATRIX_FAIL)') {
    $tail = (@(Get-Content -LiteralPath $unityLogPath -Tail 200) -join [Environment]::NewLine)
    throw "Unity topology-matrix integration failed with exit code $($unityProcess.ExitCode).`n$tail"
}
if ($unityLog -match '(?im)(warning CS\d+|\bPackageBuilder[^\r\n]*\bwarning\b)') {
    throw 'Unity topology-matrix integration log contains a package-caused warning.'
}
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $packageManifestPath -PathType Leaf)) {
    throw 'Topology-matrix package or exact package manifest is missing.'
}
$manifestEntries = @(Get-Content -LiteralPath $packageManifestPath -Encoding UTF8 |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($manifestEntries.Count -eq 0 -or @($manifestEntries | Where-Object {
            $_ -ne 'Assets/PBTopologyMatrixTests' -and
            -not $_.StartsWith('Assets/PBTopologyMatrixTests/', [StringComparison]::Ordinal)
        }).Count -ne 0) {
    throw 'The exact topology package plan contains unrelated assets.'
}

$assetIds = @('BipedTail', 'MechanicalBow', 'MechanicalVehicle',
    'QuadrupedTail', 'WingedCreature')
$requiredAssets = @(
    'Assets/PBTopologyMatrixTests/Documentation/topology-matrix.txt',
    'Assets/PBTopologyMatrixTests/Documentation/README.txt',
    'Assets/PBTopologyMatrixTests/Source/InvalidMultiRoot.fbx'
)
foreach ($assetId in $assetIds) {
    $requiredAssets += @(
        "Assets/PBTopologyMatrixTests/Source/$assetId.fbx",
        "Assets/PBTopologyMatrixTests/Animations/A_${assetId}_Deform.anim",
        "Assets/PBTopologyMatrixTests/Controllers/AC_$assetId.controller",
        "Assets/PBTopologyMatrixTests/Prefabs/P_$assetId.prefab"
    )
}
$cleanResult = Invoke-CleanUnityPackageValidation `
    -TemplateRoot $templateRoot -CleanCloneRoot $cleanCloneRoot -UnityPath $unityPath `
    -PackagePath $packagePath -ImportLogPath $cleanImportLogPath `
    -ValidationLogPath $cleanValidationLogPath -ResultPath $cleanResultPath `
    -ProductRootReference 'Assets/PBTopologyMatrixTests' `
    -PrefabReference 'Assets/PBTopologyMatrixTests/Prefabs/P_MechanicalBow.prefab' `
    -ValidationMode 'generic-topology-matrix' -AddTemplatePreviewForValidation `
    -RequiredImportedAssets $requiredAssets
$expectedCategories = @(
    'articulated-mechanical-bow',
    'articulated-mechanical-vehicle',
    'non-humanoid-biped-with-tail',
    'quadruped-with-tail',
    'winged-creature'
)
if ($cleanResult.schemaVersion -ne 1 -or -not $cleanResult.passed -or
    $cleanResult.validationMode -ne 'generic-topology-matrix' -or
    $cleanResult.topologyCaseCount -ne 5 -or $cleanResult.animationClipCount -ne 5 -or
    $cleanResult.controllerStateCount -ne 5 -or -not $cleanResult.genericTopologyVerified -or
    -not $cleanResult.topologyHierarchyVerified -or
    -not $cleanResult.topologySkinWeightsVerified -or
    -not $cleanResult.animationMotionVerified -or
    -not $cleanResult.topologyNegativeFindingsVerified -or
    -not ((@($cleanResult.topologyCategories) -join "`n").Equals(
        ($expectedCategories -join "`n"), [StringComparison]::Ordinal)) -or
    @($cleanResult.findings).Count -ne 0) {
    throw 'Topology-matrix clean reimport returned invalid structured evidence.'
}

$result = [ordered]@{
    schemaVersion = 1
    runRoot = $runRoot
    project = $cloneRoot
    cleanProject = $cleanCloneRoot
    package = $packagePath
    packageManifest = $packageManifestPath
    fixtureManifest = Join-Path $generatedRoot 'topology-matrix.txt'
    validFixtureCount = 5
    invalidFixtureCount = 1
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

Write-Host 'Unity Generic topology matrix, skin, hierarchy, clips, and motion: passed'
Write-Host 'Unity topology stable negative findings and isolated clean reimport: passed'
Write-Host "Manual Unity project: $cloneRoot"
Write-Host "Clean reimport Unity project: $cleanCloneRoot"
Write-Host "Retained integration evidence: $runRoot"
