[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$UnityExecutable,
    [string]$BlenderExecutable,
    [string]$ResultPointerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $scriptPath = [string]$MyInvocation.MyCommand.Path
    if ([string]::IsNullOrWhiteSpace($scriptPath)) {
        throw 'RepositoryRoot was not supplied and the executing script path is unavailable.'
    }

    $RepositoryRoot = Join-Path (Split-Path $scriptPath -Parent) '..'
}

$repositoryRootPath = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$templateRoot = Join-Path $repositoryRootPath 'engine-templates\unity\6000.3'
if ([string]::IsNullOrWhiteSpace($UnityExecutable)) {
    $UnityExecutable = 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe'
}
$unityPath = [IO.Path]::GetFullPath($UnityExecutable)
if (-not (Test-Path -LiteralPath $unityPath -PathType Leaf)) {
    throw "Approved Unity executable is unavailable: $unityPath"
}
$toolsRoot = Join-Path $repositoryRootPath 'tools'
if ([string]::IsNullOrWhiteSpace($BlenderExecutable)) {
    $BlenderExecutable = Join-Path $toolsRoot 'blender\5.0.0\blender.exe'
}
$blenderPath = [IO.Path]::GetFullPath($BlenderExecutable)
if (-not (Test-Path -LiteralPath $blenderPath -PathType Leaf) -or
    -not $blenderPath.StartsWith($toolsRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "The approved repository-contained Blender executable is unavailable: $blenderPath"
}

& (Join-Path $repositoryRootPath 'scripts\Test-UnityProductPolicies.ps1') `
    -RepositoryRoot $repositoryRootPath

$runId = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runRoot = Join-Path $repositoryRootPath "artifacts\u\$runId"
$cloneRoot = Join-Path $runRoot 'p'
$cleanCloneRoot = Join-Path $runRoot 'r'
$logPath = Join-Path $runRoot 'unity-product-tests.log'
$playModeLogPath = Join-Path $runRoot 'unity-overview-playmode.log'
$cleanImportLogPath = Join-Path $runRoot 'unity-clean-import.log'
$cleanReimportLogPath = Join-Path $runRoot 'unity-clean-reimport.log'
$cleanReimportResultPath = Join-Path $runRoot 'unity-clean-reimport-result.json'
$reopenLogPath = Join-Path $runRoot 'unity-product-reopen.log'
$reopenRetryLogPath = Join-Path $runRoot 'unity-product-reopen-retry.log'
$packageOutputPath = Join-Path $cloneRoot 'PackageBuilderExports\StoneArch.unitypackage'
$packageManifestPath = Join-Path $runRoot 'unitypackage-assets.txt'
$packageExtractRoot = Join-Path $runRoot 'unitypackage-extracted'
$cacheRoot = Join-Path $repositoryRootPath 'runtime-data\unity\6000.3.10f1'
$temporaryRoot = Join-Path $cacheRoot 'temp'
$upmCacheRoot = Join-Path $cacheRoot 'upm-cache'
$upmConfigRoot = Join-Path $cacheRoot 'upm-config'

# Unity 6000.3 still reaches package assemblies through Mono APIs that can fail at legacy Windows
# path lengths even when the operating-system long-path policy is enabled. Keep the generated
# project short enough for the longest reviewed path in the pinned package graph.
$maximumLegacyCompatiblePathLength = 248
$representativePackagePath = Join-Path $cloneRoot `
    'Library\PackageCache\com.unity.collections@000000000000\Unity.Collections.LowLevel.ILSupport\source~\Unity.Collections.LowLevel.ILSupport.CodeGen\Unity.Collections.LowLevel.ILSupport.CodeGen.asmdef.meta'
if ($representativePackagePath.Length -gt $maximumLegacyCompatiblePathLength) {
    throw "The Unity integration clone is too deep for legacy package assembly APIs: $($representativePackagePath.Length) characters."
}

New-Item -ItemType Directory -Path $runRoot, $cloneRoot, $temporaryRoot, $upmCacheRoot, $upmConfigRoot -Force |
    Out-Null
foreach ($rootName in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $templateRoot $rootName) -Destination $cloneRoot -Recurse
}

# Use the repository-authored normalized FBX rather than a mock ModelImporter input. Its camera,
# light, two meshes, and embedded materials make the PB-0609 through PB-0611 checks observable.
$modelTestRoot = Join-Path $cloneRoot 'Assets\PBModelTests'
foreach ($folderName in @('Documentation', 'Source', 'Meshes', 'Materials', 'Textures',
        'Prefabs', 'Scenes', 'Scripts')) {
    New-Item -ItemType Directory -Path (Join-Path $modelTestRoot $folderName) -Force | Out-Null
}
$staticFbxFixture = Join-Path $repositoryRootPath `
    'tests\fixtures\portable\static-vertical-slice\source\StoneArch.fbx'
if (-not (Test-Path -LiteralPath $staticFbxFixture -PathType Leaf)) {
    throw "Static Unity FBX fixture is missing: $staticFbxFixture"
}
Copy-Item -LiteralPath $staticFbxFixture -Destination (Join-Path $modelTestRoot 'Source\StoneArch.fbx')

# Generate a real armature-and-skin FBX so PB-0701 verifies Generic avatar, hierarchy, motion-root,
# optimization, and exposed-transform behavior against Unity's FBX importer rather than a mock.
$rigProductRoot = Join-Path $cloneRoot 'Assets\PBRigPolicyTests'
$rigTestRoot = Join-Path $rigProductRoot 'Source'
$rigFbxPath = Join-Path $rigTestRoot 'RiggedProp.fbx'
$animatedProductRoot = Join-Path $cloneRoot 'Assets\PBAnimationTests'
$animatedSourceRoot = Join-Path $animatedProductRoot 'Source'
$animatedFbxPath = Join-Path $animatedSourceRoot 'AnimatedProp.fbx'
$rigGenerationLog = Join-Path $runRoot 'blender-rig-fixture.log'
# Case 2 intentionally has no Animations or Controllers output. The animated fixture receives
# those folders separately so an empty animation surface cannot leak into rigged-only packages.
foreach ($folderName in @('Documentation', 'Source', 'Meshes', 'Materials', 'Textures',
        'Prefabs', 'Scenes', 'Scripts')) {
    New-Item -ItemType Directory -Path (Join-Path $rigProductRoot $folderName) -Force | Out-Null
}
foreach ($folderName in @('Documentation', 'Source', 'Meshes', 'Materials', 'Textures',
        'Prefabs', 'Scenes', 'Scripts', 'Animations', 'Controllers')) {
    New-Item -ItemType Directory -Path (Join-Path $animatedProductRoot $folderName) -Force | Out-Null
}
$blenderArguments = @(
    '--background',
    '--factory-startup',
    '--python', (Join-Path $repositoryRootPath 'tests\blender\engine\pb0701_generate_rig_fbx.py'),
    '--', $rigFbxPath
)
$blenderProcess = Start-Process -FilePath $blenderPath -ArgumentList $blenderArguments `
    -Wait -PassThru -NoNewWindow -RedirectStandardOutput $rigGenerationLog `
    -RedirectStandardError (Join-Path $runRoot 'blender-rig-fixture-error.log')
if ($blenderProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $rigFbxPath -PathType Leaf)) {
    throw "The real PB-0701 rig fixture could not be generated. See $rigGenerationLog"
}

# Reuse the same topology and weighting fixture with one sampled Blender action for PB-0705.
$animatedBlenderArguments = @(
    '--background',
    '--factory-startup',
    '--python', (Join-Path $repositoryRootPath 'tests\blender\engine\pb0701_generate_rig_fbx.py'),
    '--', $animatedFbxPath, 'animated'
)
$animatedBlenderProcess = Start-Process -FilePath $blenderPath -ArgumentList $animatedBlenderArguments `
    -Wait -PassThru -NoNewWindow -RedirectStandardOutput `
    (Join-Path $runRoot 'blender-animation-fixture.log') `
    -RedirectStandardError (Join-Path $runRoot 'blender-animation-fixture-error.log')
if ($animatedBlenderProcess.ExitCode -ne 0 -or
    -not (Test-Path -LiteralPath $animatedFbxPath -PathType Leaf)) {
    throw 'The real PB-0705 animation fixture could not be generated.'
}

# Move the generic controller source and assembly definition into the generated product Scripts
# folder before Unity imports the clone. Moving both assets with their metadata preserves stable
# script identity, keeps the worker package Editor-only, and makes the saved customer scene
# independent of com.packagebuilder.worker.
$previewTemplateRoot = Join-Path $cloneRoot 'Assets\PackageBuilder\Preview'
$productScriptsRoot = Join-Path $modelTestRoot 'Scripts'
foreach ($fileName in @(
        'PackageBuilder.Preview.asmdef',
        'PackageBuilder.Preview.asmdef.meta',
        'PackageBuilderPreviewController.cs',
        'PackageBuilderPreviewController.cs.meta')) {
    $sourcePath = Join-Path $previewTemplateRoot $fileName
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Unity preview-controller template file is missing: $sourcePath"
    }
    Move-Item -LiteralPath $sourcePath -Destination (Join-Path $productScriptsRoot $fileName)
}

$originalEnvironment = @{}
$environment = [ordered]@{
    TEMP = $temporaryRoot
    TMP = $temporaryRoot
    UPM_NPM_CACHE_PATH = $upmCacheRoot
    UPM_CACHE_PATH = $upmCacheRoot
    UPM_CONFIG_PATH = $upmConfigRoot
    PACKAGEBUILDER_RETAIN_UNITY_TEST_ASSETS = '1'
    PACKAGEBUILDER_UNITYPACKAGE_OUTPUT = $packageOutputPath
    PACKAGEBUILDER_UNITYPACKAGE_MANIFEST = $packageManifestPath
    PACKAGEBUILDER_UNITY_REIMPORT_RESULT = $cleanReimportResultPath
}
try {
    foreach ($entry in $environment.GetEnumerator()) {
        $originalEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $cloneRoot,
        '-executeMethod', 'PackageBuilder.UnityWorker.Editor.UnityProductEditorIntegrationTests.Run',
        '-logFile', $logPath
    )
    $process = Start-Process -FilePath $unityPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow

    $log = if (Test-Path -LiteralPath $logPath) {
        Get-Content -LiteralPath $logPath -Raw -Encoding UTF8
    }
    else {
        throw 'Unity product integration log is missing.'
    }

    if ($process.ExitCode -ne 0 -or -not $log.Contains('PACKAGEBUILDER_UNITY_PRODUCT_TESTS_PASS')) {
        $tail = (@(Get-Content -LiteralPath $logPath -Tail 120) -join [Environment]::NewLine)
        throw "Unity product integration failed with exit code $($process.ExitCode).`n$tail"
    }
    if ($log -match '(?m)(error CS\d+|PACKAGEBUILDER_UNITY_PRODUCT_TESTS_FAIL)') {
        throw 'Unity product integration log contains a compilation or test failure.'
    }
    if ($log -match '(?im)(warning CS\d+|\bPackageBuilder[^\r\n]*\bwarning\b)') {
        throw 'Unity product integration log contains a package-caused warning.'
    }

    if (-not (Test-Path -LiteralPath $packageOutputPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $packageManifestPath -PathType Leaf)) {
        throw 'Exact Unity package or its expected inventory evidence is missing.'
    }
    if ($null -eq (Get-Command tar.exe -ErrorAction SilentlyContinue)) {
        throw 'The exact Unity package verifier requires the Windows tar.exe archive reader.'
    }
    New-Item -ItemType Directory -Path $packageExtractRoot -Force | Out-Null
    & tar.exe -xzf $packageOutputPath -C $packageExtractRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Unity package archive extraction failed with exit code $LASTEXITCODE."
    }
    $packageEntries = @(Get-ChildItem -LiteralPath $packageExtractRoot -Recurse -File -Filter pathname |
        ForEach-Object { (Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8).Trim() } |
        Sort-Object -Unique)
    $expectedEntries = @(Get-Content -LiteralPath $packageManifestPath -Encoding UTF8 |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique)
    $archiveDifferences = @(Compare-Object -ReferenceObject $expectedEntries `
        -DifferenceObject $packageEntries)
    if ($archiveDifferences.Count -gt 0) {
        throw "Unity package archive differs from its exact plan: $($archiveDifferences.InputObject -join ', ')."
    }
    foreach ($entry in $packageEntries) {
        if ($entry -ne 'Assets/PBModelTests' -and
            -not $entry.StartsWith('Assets/PBModelTests/', [StringComparison]::Ordinal)) {
            throw "Unity package contains an unrelated asset: $entry"
        }
        if ($entry -match '(?i)(_Template|PBOverviewTemplate|PBTextureTests|PBMaterialTests|Assets/PackageBuilder)') {
            throw "Unity package contains template or test-only content: $entry"
        }
    }
    foreach ($entryFolder in Get-ChildItem -LiteralPath $packageExtractRoot -Directory) {
        foreach ($requiredFile in @('asset.meta', 'pathname')) {
            if (-not (Test-Path -LiteralPath (Join-Path $entryFolder.FullName $requiredFile) -PathType Leaf)) {
                throw "Unity package entry is missing $requiredFile`: $($entryFolder.Name)"
            }
        }
        $pathname = (Get-Content -LiteralPath (Join-Path $entryFolder.FullName 'pathname') `
            -Raw -Encoding UTF8).Trim()
        $isFolderRecord = @($expectedEntries | Where-Object {
                $_.StartsWith($pathname + '/', [StringComparison]::Ordinal)
            }).Count -gt 0
        if (-not $isFolderRecord -and
            -not (Test-Path -LiteralPath (Join-Path $entryFolder.FullName 'asset') -PathType Leaf)) {
            throw "Unity package file entry is missing asset payload: $($entryFolder.Name)"
        }
    }

    # PB-0617 imports the completed package into a second clone that contains only the approved
    # Unity template. Removing the template preview source proves that the exported product owns
    # every runtime script, scene, material, texture, mesh, prefab, and metadata dependency it uses.
    New-Item -ItemType Directory -Path $cleanCloneRoot -Force | Out-Null
    foreach ($rootName in @('Assets', 'Packages', 'ProjectSettings')) {
        Copy-Item -LiteralPath (Join-Path $templateRoot $rootName) -Destination $cleanCloneRoot -Recurse
    }
    $cleanWorkerPackageRoot = Join-Path $cleanCloneRoot 'Packages\com.packagebuilder.worker'
    if (Test-Path -LiteralPath $cleanWorkerPackageRoot) {
        Remove-Item -LiteralPath $cleanWorkerPackageRoot -Recurse -Force
    }
    $cleanPreviewRoot = Join-Path $cleanCloneRoot 'Assets\PackageBuilder'
    if (Test-Path -LiteralPath $cleanPreviewRoot) {
        Remove-Item -LiteralPath $cleanPreviewRoot -Recurse -Force
    }
    $cleanPreviewMetaPath = "$cleanPreviewRoot.meta"
    if (Test-Path -LiteralPath $cleanPreviewMetaPath) {
        Remove-Item -LiteralPath $cleanPreviewMetaPath -Force
    }
    if (Test-Path -LiteralPath (Join-Path $cleanCloneRoot 'Assets\PBModelTests')) {
        throw 'The clean Unity clone unexpectedly contains product assets before package import.'
    }

    # Unity resolves and compiles the clean template before processing -importPackage. Import and
    # validation therefore use separate Editor processes: the first completes the package import
    # and domain reload, and the second executes the structured validator against that settled
    # customer project. Combining both operations can compile the Editor worker before the
    # package-owned runtime assembly exists, which hides the behavior PB-0617 must prove.
    $cleanImportArguments = @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', $cleanCloneRoot,
        '-importPackage', $packageOutputPath,
        '-logFile', $cleanImportLogPath
    )
    # Unity is a GUI-subsystem executable and can transfer work to a child Editor process. The
    # timed Process.WaitForExit overload observes only the original launcher on some hosts and can
    # leave ExitCode unavailable even though the Editor completed successfully. Start-Process
    # -Wait follows the process tree and returns the actual batch-mode exit code.
    $cleanImportProcess = Start-Process -FilePath $unityPath `
        -ArgumentList $cleanImportArguments -Wait -PassThru -NoNewWindow
    $cleanImportLog = if (Test-Path -LiteralPath $cleanImportLogPath) {
        Get-Content -LiteralPath $cleanImportLogPath -Raw -Encoding UTF8
    }
    else {
        throw 'Unity clean package import log is missing.'
    }
    if ($null -eq $cleanImportProcess.ExitCode -or [int]$cleanImportProcess.ExitCode -ne 0 -or
        $cleanImportLog -match '(?m)(error CS\d+|Aborting batchmode due to failure)') {
        $tail = (@(Get-Content -LiteralPath $cleanImportLogPath -Tail 160) -join `
                [Environment]::NewLine)
        $exitDisplay = if ($null -eq $cleanImportProcess.ExitCode) { 'unavailable' } else {
            $cleanImportProcess.ExitCode
        }
        throw "Unity clean package import failed with exit code $exitDisplay.`n$tail"
    }
    foreach ($requiredImportedAsset in @(
            'Assets\PBModelTests\Scripts\PackageBuilder.Preview.asmdef',
            'Assets\PBModelTests\Scripts\PackageBuilderPreviewController.cs',
            'Assets\PBModelTests\Scenes\S_StoneArch_Overview.unity',
            'Assets\PBModelTests\Prefabs\P_StoneArch.prefab')) {
        if (-not (Test-Path -LiteralPath (Join-Path $cleanCloneRoot $requiredImportedAsset) -PathType Leaf)) {
            throw "Unity clean package import omitted required asset: $requiredImportedAsset"
        }
    }

    # The import process deliberately contains no Package Builder worker. Add the Editor-only
    # validator only after the customer package and its runtime assembly have compiled, then use a
    # fresh process for the structured inspection. This prevents the test harness from satisfying
    # any customer runtime dependency while still allowing deep Editor API validation afterward.
    Copy-Item -LiteralPath (Join-Path $templateRoot 'Packages\com.packagebuilder.worker') `
        -Destination (Join-Path $cleanCloneRoot 'Packages') -Recurse

    $cleanReimportArguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $cleanCloneRoot,
        '-executeMethod', 'PackageBuilder.UnityWorker.Editor.UnityCleanReimportIntegration.Run',
        '-logFile', $cleanReimportLogPath
    )
    $cleanReimportProcess = Start-Process -FilePath $unityPath `
        -ArgumentList $cleanReimportArguments -Wait -PassThru -NoNewWindow
    $cleanReimportLog = if (Test-Path -LiteralPath $cleanReimportLogPath) {
        Get-Content -LiteralPath $cleanReimportLogPath -Raw -Encoding UTF8
    }
    else {
        throw 'Unity clean package reimport log is missing.'
    }
    $cleanReimportExitCode = $cleanReimportProcess.ExitCode
    if ($null -eq $cleanReimportExitCode -or [int]$cleanReimportExitCode -ne 0 -or
        -not $cleanReimportLog.Contains('PACKAGEBUILDER_UNITY_CLEAN_REIMPORT_PASS') -or
        $cleanReimportLog -match '(?m)(error CS\d+|PACKAGEBUILDER_UNITY_CLEAN_REIMPORT_FAIL)') {
        $tail = (@(Get-Content -LiteralPath $cleanReimportLogPath -Tail 160) -join `
                [Environment]::NewLine)
        $exitDisplay = if ($null -eq $cleanReimportExitCode) { 'unavailable' } else {
            $cleanReimportExitCode
        }
        throw "Unity clean package reimport failed with exit code $exitDisplay.`n$tail"
    }
    if (-not (Test-Path -LiteralPath $cleanReimportResultPath -PathType Leaf)) {
        throw 'Unity clean package reimport did not write its structured result.'
    }
    $cleanReimportResult = Get-Content -LiteralPath $cleanReimportResultPath -Raw -Encoding UTF8 |
        ConvertFrom-Json
    if ($cleanReimportResult.schemaVersion -ne 1 -or -not $cleanReimportResult.passed -or
        $cleanReimportResult.rendererCount -lt 1 -or $cleanReimportResult.materialCount -lt 1 -or
        $cleanReimportResult.textureCount -lt 1 -or @($cleanReimportResult.findings).Count -ne 0) {
        throw 'Unity clean package reimport returned an invalid or failing structured result.'
    }

    # Open the composed scene in a real Play mode cycle. This process intentionally omits -quit:
    # the Editor callback exits only after EnteredPlayMode and EnteredEditMode both complete.
    $playModeArguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $cloneRoot,
        '-executeMethod', 'PackageBuilder.UnityWorker.Editor.UnityOverviewPlayModeSmokeTest.Run',
        '-logFile', $playModeLogPath
    )
    $playModeProcess = Start-Process -FilePath $unityPath -ArgumentList $playModeArguments `
        -Wait -PassThru -NoNewWindow
    $playModeLog = if (Test-Path -LiteralPath $playModeLogPath) {
        Get-Content -LiteralPath $playModeLogPath -Raw -Encoding UTF8
    }
    else {
        throw 'Unity overview Play mode log is missing.'
    }
    $playModeExitCode = $playModeProcess.ExitCode
    $playModeHasNonZeroExit = $null -ne $playModeExitCode -and [int]$playModeExitCode -ne 0
    if ($playModeHasNonZeroExit -or
        -not $playModeLog.Contains('PACKAGEBUILDER_UNITY_OVERVIEW_PLAYMODE_PASS') -or
        $playModeLog -match '(?m)(error CS\d+|PACKAGEBUILDER_UNITY_OVERVIEW_PLAYMODE_FAIL)') {
        $tail = (@(Get-Content -LiteralPath $playModeLogPath -Tail 120) -join [Environment]::NewLine)
        $exitDisplay = if ($null -eq $playModeExitCode) { 'unavailable' } else { $playModeExitCode }
        throw "Unity overview Play mode validation failed with exit code $exitDisplay.`n$tail"
    }

    # Match the project marker to the actual upgrader inventory in the pinned package. A stale
    # marker opens a modal URP material-upgrade dialog during the first interactive Editor frame,
    # even when the project contains no customer material assets.
    $urpProjectSettingsPath = Join-Path $cloneRoot 'ProjectSettings\URPProjectSettings.asset'
    $materialPostprocessorPaths = @(Get-ChildItem -LiteralPath (Join-Path $cloneRoot `
                'Library\PackageCache') -Recurse -File -Filter 'MaterialPostprocessor.cs' |
        Where-Object {
            $_.FullName -match '[\\/]com\.unity\.render-pipelines\.universal@[^\\/]+[\\/]Editor[\\/]AssetPostProcessors[\\/]MaterialPostprocessor\.cs$'
        })
    if ($materialPostprocessorPaths.Count -ne 1) {
        throw "Expected one pinned URP MaterialPostprocessor.cs; found $($materialPostprocessorPaths.Count)."
    }
    $materialPostprocessorSource = Get-Content -LiteralPath $materialPostprocessorPaths[0].FullName `
        -Raw -Encoding UTF8
    $upgraderDeclaration = [regex]::Match($materialPostprocessorSource,
        'k_Upgraders\s*=\s*\{(?<upgraders>[^}]*)\}')
    if (-not $upgraderDeclaration.Success) {
        throw 'Pinned URP material upgrader inventory could not be read.'
    }
    $upgraderCount = @([regex]::Matches($upgraderDeclaration.Groups['upgraders'].Value,
            '\bUpgradeV\d+\b')).Count
    $urpProjectSettings = Get-Content -LiteralPath $urpProjectSettingsPath -Raw -Encoding UTF8
    $materialVersionMatch = [regex]::Match($urpProjectSettings,
        '(?m)^  m_LastMaterialVersion: (?<version>\d+)$')
    if (-not $materialVersionMatch.Success -or
        [int]$materialVersionMatch.Groups['version'].Value -ne $upgraderCount) {
        throw "URP material version marker does not match the pinned package upgrader count ($upgraderCount)."
    }

    # Reopen the populated project to catch package-cache and assembly-validation defects that are
    # invisible during the first import. This is the automated equivalent of the manual GUI reopen.
    $reopenArguments = @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', $cloneRoot,
        '-logFile', $reopenLogPath
    )
    $reopenProcess = Start-Process -FilePath $unityPath -ArgumentList $reopenArguments `
        -Wait -PassThru -NoNewWindow
    if ($reopenProcess.ExitCode -ne 0) {
        $firstReopenLog = if (Test-Path -LiteralPath $reopenLogPath) {
            Get-Content -LiteralPath $reopenLogPath -Raw -Encoding UTF8
        }
        else {
            ''
        }
        $knownNativeStartupRace = $firstReopenLog.Contains(
            "Assertion failed on expression: 'CurrentThread::IsMainThread()'") -and
            $firstReopenLog -notmatch '(?m)(DirectoryNotFoundException|Could not find a part of the path|error CS\d+)'
        if ($knownNativeStartupRace) {
            # Unity can tear down its licensing/windowing threads after the process exits. Retain
            # the failed native log, allow that teardown to settle, and require one fresh process
            # to prove the populated project itself reopens cleanly.
            Start-Sleep -Seconds 2
            $reopenLogPath = $reopenRetryLogPath
            $reopenArguments = @(
                '-batchmode',
                '-nographics',
                '-quit',
                '-projectPath', $cloneRoot,
                '-logFile', $reopenLogPath
            )
            $reopenProcess = Start-Process -FilePath $unityPath -ArgumentList $reopenArguments `
                -Wait -PassThru -NoNewWindow
        }
    }
}
finally {
    foreach ($entry in $originalEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
}

$reopenLog = if (Test-Path -LiteralPath $reopenLogPath) {
    Get-Content -LiteralPath $reopenLogPath -Raw -Encoding UTF8
}
else {
    throw 'Unity product reopen log is missing.'
}
if ($reopenProcess.ExitCode -ne 0) {
    $tail = (@(Get-Content -LiteralPath $reopenLogPath -Tail 120) -join [Environment]::NewLine)
    throw "Unity product reopen failed with exit code $($reopenProcess.ExitCode).`n$tail"
}
if ($reopenLog -match '(?m)(DirectoryNotFoundException|Could not find a part of the path|error CS\d+)') {
    throw 'Unity product reopen log contains a path, assembly, or compilation failure.'
}

Write-Host 'Unity product folder Editor tests: passed'
Write-Host 'Unity TextureImporter Editor tests: passed'
Write-Host 'Unity metallic-smoothness exact pixel tests: passed'
Write-Host 'Unity URP/Lit material compiler tests: passed'
Write-Host 'Unity static ModelImporter Editor tests: passed'
Write-Host 'Unity Generic and optional Humanoid rig importer Editor tests: passed'
Write-Host 'Unity skin and skeleton validation Editor tests: passed'
Write-Host 'Unity rigged-no-animation prefab and skeleton metadata tests: passed'
Write-Host 'Unity exact animation clip extraction Editor tests: passed'
Write-Host 'Unity standalone mesh extraction Editor tests: passed'
Write-Host 'Unity static prefab generation Editor tests: passed'
Write-Host 'Unity generic overview scene template tests: passed'
Write-Host 'Unity bounds-only preview controller tests: passed'
Write-Host 'Unity product overview composition tests: passed'
Write-Host 'Unity exact package archive and metadata validation: passed'
Write-Host 'Unity logs, references, GUIDs, duplicates, and path validation: passed'
Write-Host 'Unity overview Play mode smoke test: passed'
Write-Host 'Unity URP material upgrader marker validation: passed'
Write-Host 'Unity populated-project reopen validation: passed'
Write-Host 'Unity clean package reimport, scene, prefab, material, texture, and render validation: passed'
Write-Host 'Generated folder, texture, material, model, mesh, and prefab assets retained for manual Unity inspection.'
Write-Host "Manual Unity project: $cloneRoot"
Write-Host "Clean reimport Unity project: $cleanCloneRoot"
Write-Host "Retained integration evidence: $runRoot"

$resultPointer = [ordered]@{
    schemaVersion = 1
    runRoot = $runRoot
    project = $cloneRoot
    cleanProject = $cleanCloneRoot
    package = $packageOutputPath
    packageManifest = $packageManifestPath
    scene = Join-Path $cloneRoot 'Assets\PBModelTests\Scenes\S_StoneArch_Overview.unity'
    rigFixture = Join-Path $cloneRoot 'Assets\PBRigPolicyTests\Source\RiggedProp.fbx'
    riggedPrefab = Join-Path $cloneRoot 'Assets\PBRigPolicyTests\Prefabs\P_RiggedProp.prefab'
    skeletonMetadata = Join-Path $cloneRoot `
        'Assets\PBRigPolicyTests\Documentation\SKEL_RiggedProp.json'
    animatedFixture = Join-Path $cloneRoot 'Assets\PBAnimationTests\Source\AnimatedProp.fbx'
    animationClip = Join-Path $cloneRoot 'Assets\PBAnimationTests\Animations\A_AnimatedProp_Bend.anim'
    integrationLog = $logPath
    cleanReimportLog = $cleanReimportLogPath
    cleanReimportResult = $cleanReimportResultPath
}
$defaultResultPointerPath = Join-Path $runRoot 'integration-result.json'
$resultPointer | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $defaultResultPointerPath `
    -Encoding UTF8
if (-not [string]::IsNullOrWhiteSpace($ResultPointerPath)) {
    $resolvedPointerPath = [IO.Path]::GetFullPath($ResultPointerPath)
    $pointerDirectory = Split-Path $resolvedPointerPath -Parent
    New-Item -ItemType Directory -Path $pointerDirectory -Force | Out-Null
    Copy-Item -LiteralPath $defaultResultPointerPath -Destination $resolvedPointerPath -Force
}
