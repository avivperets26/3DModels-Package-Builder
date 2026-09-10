[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$UnityExecutable,
    [string]$BlenderExecutable,
    [string]$ResultPointerPath,
    [switch]$KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$commonScript = Join-Path $PSScriptRoot 'UnityCleanReimport.Common.ps1'
. $commonScript

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
. (Join-Path $PSScriptRoot 'UnityTestArtifacts.Common.ps1')
$integrationSucceeded = $false
try {
    $cloneRoot = Join-Path $runRoot 'p'
    $cleanCloneRoot = Join-Path $runRoot 'r'
    $rigCleanCloneRoot = Join-Path $runRoot 'g'
    $logPath = Join-Path $runRoot 'unity-product-tests.log'
    $playModeLogPath = Join-Path $runRoot 'unity-overview-playmode.log'
    $cleanImportLogPath = Join-Path $runRoot 'unity-clean-import.log'
    $cleanReimportLogPath = Join-Path $runRoot 'unity-clean-reimport.log'
    $cleanReimportResultPath = Join-Path $runRoot 'unity-clean-reimport-result.json'
    $rigCleanImportLogPath = Join-Path $runRoot 'unity-rig-clean-import.log'
    $rigCleanReimportLogPath = Join-Path $runRoot 'unity-rig-clean-reimport.log'
    $rigCleanReimportResultPath = Join-Path $runRoot 'unity-rig-clean-reimport-result.json'
    $reopenLogPath = Join-Path $runRoot 'unity-product-reopen.log'
    $reopenRetryLogPath = Join-Path $runRoot 'unity-product-reopen-retry.log'
    $packageOutputPath = Join-Path $cloneRoot 'PackageBuilderExports\StoneArch.unitypackage'
    $packageManifestPath = Join-Path $runRoot 'unitypackage-assets.txt'
    $rigPackageOutputPath = Join-Path $cloneRoot 'PackageBuilderExports\RiggedProp.unitypackage'
    $rigPackageManifestPath = Join-Path $runRoot 'rig-unitypackage-assets.txt'
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
            'PackageBuilderPreviewController.cs.meta',
            'PackageBuilderAnimationTransport.cs',
            'PreviewSelectionPolicy.cs',
            'PreviewSelectionPolicy.cs.meta',
            'PackageBuilderItemSelector.cs',
            'PackageBuilderItemSelector.cs.meta',
            'PackageBuilderAnimationTransport.cs.meta')) {
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
        PACKAGEBUILDER_ITEM_OWNERSHIP_PLAN = (Join-Path $repositoryRootPath 'tests\fixtures\manifests\item-prefab-ownership.json')
        PACKAGEBUILDER_SET_PLAN = (Join-Path $repositoryRootPath 'tests\fixtures\manifests\assembled-set-plan.json')
        PACKAGEBUILDER_ITEM_PACKAGE_OUTPUT = (Join-Path $runRoot 'items.unitypackage')
        PACKAGEBUILDER_ATTACHMENT_PLAN = (Join-Path $repositoryRootPath 'tests/fixtures/manifests/set-attachments.json')
        PACKAGEBUILDER_EQUIPMENT_OUTPUT = (Join-Path $runRoot 'equipment')
        PACKAGEBUILDER_TWELVE_OUTPUT = (Join-Path $runRoot 'twelve')
        PACKAGEBUILDER_TWELVE_SOURCE = (Join-Path $repositoryRootPath 'tests/fixtures/portable/twelve-item-collection/source')
        PACKAGEBUILDER_TWELVE_ASSETS = (Join-Path $runRoot 'twelve-package-assets.txt')
        PACKAGEBUILDER_SELECTOR_SCENE = 'Assets/PBTwelveTests/Scenes/S_TwelveColumns_Overview.unity'
        PACKAGEBUILDER_EQUIPMENT_SOURCE = (Join-Path $repositoryRootPath 'tests/fixtures/portable/equipment-set/source')
        PACKAGEBUILDER_COLLECTION_PLAN = (Join-Path $repositoryRootPath 'tests/fixtures/manifests/collection-plan.json')
        PACKAGEBUILDER_COLLECTION_PACKAGE_OUTPUT = (Join-Path $cloneRoot 'PackageBuilderExports/ExampleCollection.unitypackage')
        PACKAGEBUILDER_UNITYPACKAGE_OUTPUT = $packageOutputPath
        PACKAGEBUILDER_UNITYPACKAGE_MANIFEST = $packageManifestPath
        PACKAGEBUILDER_UNITY_RIG_PACKAGE_OUTPUT = $rigPackageOutputPath
        PACKAGEBUILDER_UNITY_RIG_PACKAGE_MANIFEST = $rigPackageManifestPath
        PACKAGEBUILDER_UNITY_REIMPORT_RESULT = $cleanReimportResultPath
        PACKAGEBUILDER_UNITY_REIMPORT_MODE = 'overview'
        PACKAGEBUILDER_UNITY_PRODUCT_ROOT = 'Assets/PBModelTests'
        PACKAGEBUILDER_UNITY_PRODUCT_PREFAB = 'Assets/PBModelTests/Prefabs/P_StoneArch.prefab'
        PACKAGEBUILDER_UNITY_OVERVIEW_SCENE = 'Assets/PBModelTests/Scenes/S_StoneArch_Overview.unity'
    }
    try {
        foreach ($entry in $environment.GetEnumerator()) {
            $originalEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
            [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
        }

        . (Join-Path $repositoryRootPath 'scripts/Enter-PackageBuilderEnvironment.ps1')
        dotnet test (Join-Path $repositoryRootPath 'tests/PackageBuilder.Targets.Portable.Tests/PackageBuilder.Targets.Portable.Tests.csproj') --no-restore --filter 'FullyQualifiedName~EquipmentSetEndToEndTests' -v minimal
        if ($LASTEXITCODE -ne 0) { throw 'Equipment portable archive/application plan generation failed.' }
        $equipmentBlender = Start-Process -FilePath $blenderPath -ArgumentList @(
            '--background', '--factory-startup', '--python',
            (Join-Path $repositoryRootPath 'tests/blender/engine/pb0810_equipment_fixture.py'), '--', 'verify',
            (Join-Path $runRoot 'equipment/EquipmentSet_FBX.zip'), (Join-Path $runRoot 'equipment-import')) `
            -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runRoot 'equipment-blender.log') `
            -RedirectStandardError (Join-Path $runRoot 'equipment-blender-error.log')
        $equipmentBlender.WaitForExit()
        $equipmentPortableResult = Join-Path $runRoot 'equipment-import/reimport-result.json'
        if ($equipmentBlender.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $equipmentPortableResult) -or
            -not (Get-Content -LiteralPath $equipmentPortableResult -Raw | ConvertFrom-Json).passed) {
            throw 'Equipment ZIP clean Blender reimport failed; inspect equipment-blender logs.'
        }
        Copy-Item -LiteralPath $equipmentPortableResult -Destination (Join-Path $runRoot 'equipment-portable-reimport.json')

        dotnet test (Join-Path $repositoryRootPath 'tests/PackageBuilder.Targets.Portable.Tests/PackageBuilder.Targets.Portable.Tests.csproj') --no-restore --filter 'FullyQualifiedName~CollectionEndToEndTests' -v minimal
        if ($LASTEXITCODE -ne 0) { throw 'Twelve-item portable archive/application plan generation failed.' }
        $twelveBlender = Start-Process -FilePath $blenderPath -ArgumentList @(
            '--background', '--factory-startup', '--python-exit-code', '1', '--python',
            (Join-Path $repositoryRootPath 'tests/blender/engine/pb0811_collection_fixture.py'), '--', 'verify',
            (Join-Path $runRoot 'twelve/TwelveColumns_FBX.zip'), (Join-Path $runRoot 'twelve-import'),
            (Join-Path $repositoryRootPath 'tests/fixtures/portable/twelve-item-collection/expectations.json')) `
            -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runRoot 'twelve-blender.log') `
            -RedirectStandardError (Join-Path $runRoot 'twelve-blender-error.log')
        if (-not $twelveBlender.WaitForExit(120000)) { $twelveBlender.Kill(); $twelveBlender.WaitForExit(); throw 'Collection Blender verification timed out.' }
        $twelvePortableResult = Join-Path $runRoot 'twelve-import/reimport-result.json'
        if ($twelveBlender.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $twelvePortableResult) -or
            -not (Get-Content -LiteralPath $twelvePortableResult -Raw | ConvertFrom-Json).passed) {
            throw 'Twelve-item ZIP clean Blender reimport failed.'
        }
        Copy-Item -LiteralPath $twelvePortableResult -Destination (Join-Path $runRoot 'twelve-portable-reimport.json')

        $arguments = @(
            '-batchmode',
            '-nographics',
            '-projectPath', $cloneRoot,
            '-executeMethod', 'PackageBuilder.UnityWorker.Editor.UnityProductEditorIntegrationTests.Run',
            '-logFile', $logPath
        )
        # Wait for the Editor itself, not its persistent compiler-server descendants.
        $process = Start-Process -FilePath $unityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
        $process.WaitForExit()

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

        # Import and validation intentionally use separate Editor processes. The shared helper creates
        # a fresh approved template clone for every product case and adds the Editor-only validator only
        # after the customer package has compiled without worker or template-preview dependencies.
        $cleanReimportResult = Invoke-CleanUnityPackageValidation `
            -TemplateRoot $templateRoot -CleanCloneRoot $cleanCloneRoot -UnityPath $unityPath `
            -PackagePath $packageOutputPath -ImportLogPath $cleanImportLogPath `
            -ValidationLogPath $cleanReimportLogPath -ResultPath $cleanReimportResultPath `
            -ProductRootReference 'Assets/PBModelTests' `
            -PrefabReference 'Assets/PBModelTests/Prefabs/P_StoneArch.prefab' `
            -SceneReference 'Assets/PBModelTests/Scenes/S_StoneArch_Overview.unity' `
            -ValidationMode 'overview' -RequiredImportedAssets @(
                'Assets/PBModelTests/Scripts/PackageBuilder.Preview.asmdef',
                'Assets/PBModelTests/Scripts/PackageBuilderPreviewController.cs',
                'Assets/PBModelTests/Scripts/PackageBuilderAnimationTransport.cs',
                'Assets/PBModelTests/Scenes/S_StoneArch_Overview.unity',
                'Assets/PBModelTests/Prefabs/P_StoneArch.prefab')
        if ($cleanReimportResult.schemaVersion -ne 1 -or -not $cleanReimportResult.passed -or
            $cleanReimportResult.rendererCount -lt 1 -or $cleanReimportResult.materialCount -lt 1 -or
            $cleanReimportResult.textureCount -lt 1 -or @($cleanReimportResult.findings).Count -ne 0) {
            throw 'Unity clean package reimport returned an invalid or failing structured result.'
        }

        $itemReimportResult = Invoke-CleanUnityPackageValidation `
            -TemplateRoot $templateRoot -CleanCloneRoot (Join-Path $runRoot 'i') -UnityPath $unityPath `
            -PackagePath (Join-Path $runRoot 'items.unitypackage') `
            -ImportLogPath (Join-Path $runRoot 'item-import.log') `
            -ValidationLogPath (Join-Path $runRoot 'item-validation.log') `
            -ResultPath (Join-Path $runRoot 'item-reimport.json') `
            -ProductRootReference 'Assets/PBItemTests' -PrefabReference 'Assets/PBItemTests/Prefabs/P_Alpha.prefab' `
            -ValidationMode 'item-and-set-prefabs' -AddTemplatePreviewForValidation -RequiredImportedAssets @(
                'Assets/PBItemTests/Prefabs/P_Alpha.prefab', 'Assets/PBItemTests/Prefabs/P_Zed.prefab',
                'Assets/PBSetTests/Prefabs/P_ExampleSet_Assembled.prefab', 'Assets/PBSetTests/Documentation/SET_ExampleSet.json')
        if (-not $itemReimportResult.passed -or @($itemReimportResult.findings).Count -ne 0) {
            throw 'Item prefab clean reimport validation failed.'
        }

        $collectionReimportResult = Invoke-CleanUnityPackageValidation `
            -TemplateRoot $templateRoot -CleanCloneRoot (Join-Path $runRoot 'c') -UnityPath $unityPath `
            -PackagePath (Join-Path $cloneRoot 'PackageBuilderExports/ExampleCollection.unitypackage') `
            -ImportLogPath (Join-Path $runRoot 'collection-import.log') `
            -ValidationLogPath (Join-Path $runRoot 'collection-validation.log') `
            -ResultPath (Join-Path $runRoot 'collection-reimport.json') `
            -ProductRootReference 'Assets/PBItemTests' -PrefabReference 'Assets/PBItemTests/Prefabs/P_Alpha.prefab' `
            -SceneReference 'Assets/PBItemTests/Scenes/S_ExampleCollection_Overview.unity' `
            -ValidationMode 'collection-overview' -RequiredImportedAssets @(
                'Assets/PBItemTests/Prefabs/P_Alpha.prefab', 'Assets/PBItemTests/Prefabs/P_Zed.prefab',
                'Assets/PBItemTests/Scenes/S_ExampleCollection_Overview.unity',
                'Assets/PBItemTests/Scripts/PackageBuilderPreviewController.cs',
                'Assets/PBItemTests/Documentation/COLLECTION_ExampleCollection.json')
        if (-not $collectionReimportResult.passed -or @($collectionReimportResult.findings).Count -ne 0) {
            throw 'Collection overview clean reimport validation failed.'
        }

        $equipmentReimportResult = Invoke-CleanUnityPackageValidation `
            -TemplateRoot $templateRoot -CleanCloneRoot (Join-Path $runRoot 'e') -UnityPath $unityPath `
            -PackagePath (Join-Path $cloneRoot 'PackageBuilderExports/EquipmentSet.unitypackage') `
            -ImportLogPath (Join-Path $runRoot 'equipment-import.log') `
            -ValidationLogPath (Join-Path $runRoot 'equipment-validation.log') `
            -ResultPath (Join-Path $runRoot 'equipment-reimport.json') `
            -ProductRootReference 'Assets/PBEquipmentTests' -PrefabReference 'Assets/PBEquipmentTests/Prefabs/P_EquipmentSet_Assembled.prefab' `
            -SceneReference 'Assets/PBEquipmentTests/Scenes/S_EquipmentSet_Overview.unity' `
            -ValidationMode 'equipment-set' -RequiredImportedAssets @(
                'Assets/PBEquipmentTests/Prefabs/P_Helmet.prefab', 'Assets/PBEquipmentTests/Prefabs/P_Armour.prefab',
                'Assets/PBEquipmentTests/Scripts/PackageBuilderItemSelector.cs',
                'Assets/PBEquipmentTests/Documentation/SET_EquipmentSet.json')
        if (-not $equipmentReimportResult.passed -or @($equipmentReimportResult.findings).Count -ne 0) {
            throw 'Equipment set clean Unity package reimport failed.'
        }

        $twelveArchiveReport = Join-Path $runRoot 'twelve-archive-verification.json'
        $twelveArchiveCheck = Start-Process -FilePath $blenderPath -ArgumentList @(
            '--background', '--factory-startup', '--python-exit-code', '1', '--python',
            (Join-Path $repositoryRootPath 'tests/blender/engine/pb0811_collection_fixture.py'), '--', 'verify-unity',
            (Join-Path $cloneRoot 'PackageBuilderExports/TwelveColumns.unitypackage'),
            (Join-Path $runRoot 'twelve-package-assets.txt'), $twelveArchiveReport) `
            -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runRoot 'twelve-archive-check.log') `
            -RedirectStandardError (Join-Path $runRoot 'twelve-archive-check-error.log')
        if (-not $twelveArchiveCheck.WaitForExit(120000)) { $twelveArchiveCheck.Kill(); $twelveArchiveCheck.WaitForExit(); throw 'Collection archive check timed out.' }
        if ($twelveArchiveCheck.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $twelveArchiveReport) -or
            -not (Get-Content -LiteralPath $twelveArchiveReport -Raw | ConvertFrom-Json).passed) {
            throw 'Twelve-item Unity archive inventory differs from its export plan.'
        }
        $twelveReimportResult = Invoke-CleanUnityPackageValidation `
            -TemplateRoot $templateRoot -CleanCloneRoot (Join-Path $runRoot 'v') -UnityPath $unityPath `
            -PackagePath (Join-Path $cloneRoot 'PackageBuilderExports/TwelveColumns.unitypackage') `
            -ImportLogPath (Join-Path $runRoot 'twelve-import.log') `
            -ValidationLogPath (Join-Path $runRoot 'twelve-validation.log') `
            -ResultPath (Join-Path $runRoot 'twelve-reimport.json') `
            -ProductRootReference 'Assets/PBTwelveTests' -PrefabReference 'Assets/PBTwelveTests/Prefabs/P_Column12.prefab' `
            -SceneReference 'Assets/PBTwelveTests/Scenes/S_TwelveColumns_Overview.unity' `
            -ValidationMode 'twelve-item-collection' -RequiredImportedAssets @(
                'Assets/PBTwelveTests/Prefabs/P_Column01.prefab', 'Assets/PBTwelveTests/Prefabs/P_Column12.prefab',
                'Assets/PBTwelveTests/Scripts/PackageBuilderItemSelector.cs',
                'Assets/PBTwelveTests/Documentation/COLLECTION_TwelveColumns.json')
        if (-not $twelveReimportResult.passed -or @($twelveReimportResult.findings).Count -ne 0 -or
            @($twelveReimportResult.collectionItems).Count -ne 12 -or
            @($twelveReimportResult.collectionItems.prefabGuid | Sort-Object -Unique).Count -ne 12) {
            throw 'Twelve-item collection clean Unity import failed.'
        }
        # Compare independently measured engine results in Unity's Y-up axis convention.
        $portableItems = (Get-Content -LiteralPath (Join-Path $runRoot 'twelve-portable-reimport.json') -Raw | ConvertFrom-Json).items
        foreach ($item in $twelveReimportResult.collectionItems) {
            $portableItem = @($portableItems | Where-Object id -CEQ $item.itemId)
            if ($portableItem.Count -ne 1 -or $portableItem[0].triangles -ne $item.triangles -or
                $portableItem[0].materials -ne $item.materials -or $portableItem[0].textures -ne $item.textures) {
                throw "Collection per-item metrics differ between targets: $($item.itemId)"
            }
            foreach ($axis in @('x', 'y', 'z')) {
                if ([Math]::Abs($portableItem[0].dimensions.$axis - $item.dimensions.$axis) -gt 0.0001) {
                    throw "Collection dimensions differ between targets: $($item.itemId), $axis"
                }
            }
        }

        # IMGUI input requires the Editor event loop; exercise the twelve-item picker after clean import.
        $selectorUiLog = Join-Path $runRoot 'selector-ui.log'
        $selectorUiProcess = Start-Process -FilePath $unityPath -ArgumentList @(
            '-projectPath', (Join-Path $runRoot 'v'),
            '-executeMethod', 'PackageBuilder.UnityWorker.Editor.UnitySelectorInteractionTests.Run',
            '-logFile', $selectorUiLog) -PassThru -WindowStyle Hidden
        $selectorUiProcess.WaitForExit()
        Stop-CompletedUnityTestHub -RepositoryRoot $repositoryRootPath `
            -ProjectPath (Join-Path $runRoot 'v') -CompletedEditorProcessId $selectorUiProcess.Id
        $selectorUiText = Get-Content -LiteralPath $selectorUiLog -Raw
        if ($selectorUiProcess.ExitCode -ne 0 -or -not $selectorUiText.Contains('PACKAGEBUILDER_SELECTOR_UI_PASS') -or
            $selectorUiText.Contains('PACKAGEBUILDER_SELECTOR_UI_FAIL')) {
            throw "Clean-package selector pointer/keyboard validation failed. See $selectorUiLog"
        }

        $rigCleanReimportResult = Invoke-CleanUnityPackageValidation `
            -TemplateRoot $templateRoot -CleanCloneRoot $rigCleanCloneRoot -UnityPath $unityPath `
            -PackagePath $rigPackageOutputPath -ImportLogPath $rigCleanImportLogPath `
            -ValidationLogPath $rigCleanReimportLogPath -ResultPath $rigCleanReimportResultPath `
            -ProductRootReference 'Assets/PBRigPolicyTests' `
            -PrefabReference 'Assets/PBRigPolicyTests/Prefabs/P_RiggedProp.prefab' `
            -ValidationMode 'rigged-no-animation' -AddTemplatePreviewForValidation `
            -RequiredImportedAssets @(
                'Assets/PBRigPolicyTests/Source/RiggedProp.fbx',
                'Assets/PBRigPolicyTests/Prefabs/P_RiggedProp.prefab',
                'Assets/PBRigPolicyTests/Documentation/SKEL_RiggedProp.json')
        if ($rigCleanReimportResult.schemaVersion -ne 1 -or
            -not $rigCleanReimportResult.passed -or
            $rigCleanReimportResult.validationMode -ne 'rigged-no-animation' -or
            $rigCleanReimportResult.rendererCount -ne 1 -or
            $rigCleanReimportResult.skinnedRendererCount -ne 1 -or
            $rigCleanReimportResult.boneCount -ne 2 -or
            $rigCleanReimportResult.animationClipCount -ne 0 -or
            $rigCleanReimportResult.animatorCount -ne 0 -or
            @($rigCleanReimportResult.findings).Count -ne 0) {
            throw 'Unity rigged-no-animation clean reimport returned invalid evidence.'
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
            -PassThru -WindowStyle Hidden
        $playModeProcess.WaitForExit()
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
            -PassThru -WindowStyle Hidden
        $reopenProcess.WaitForExit()
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
                    -PassThru -WindowStyle Hidden
                $reopenProcess.WaitForExit()
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
    Write-Host 'Unity clip loop, compression, and root-motion policy tests: passed'
    Write-Host 'Unity Animator Controller generation tests: passed'
    Write-Host 'Unity animated prefab flow tests: passed'
    Write-Host 'Unity animation preview transport controls and source immutability tests: passed'
    Write-Host 'Unity animation metadata, binding, bone, renderer, and duration tests: passed'
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
    Write-Host 'Unity rigged-no-animation clean package reimport validation: passed'
    Write-Host 'Generated test assets are removed after validation unless -KeepArtifacts is supplied.'
    if ($KeepArtifacts) { Write-Host "Manual Unity project: $cloneRoot" }
    if ($KeepArtifacts) { Write-Host "Clean reimport Unity project: $cleanCloneRoot" }
    Write-Host "Retained integration evidence: $runRoot"

    $resultPointer = [ordered]@{
        schemaVersion = 1
        runRoot = $runRoot
        artifactsRetained = [bool]$KeepArtifacts
        cleanupReport = (Join-Path $runRoot 'cleanup-result.json')
        project = $cloneRoot
        cleanProject = $cleanCloneRoot
        itemCleanProject = (Join-Path $runRoot 'i')
        itemPackage = (Join-Path $runRoot 'items.unitypackage')
        assembledSetPrefab = (Join-Path $cloneRoot 'Assets\PBSetTests\Prefabs\P_ExampleSet_Assembled.prefab')
        setDocumentation = (Join-Path $cloneRoot 'Assets\PBSetTests\Documentation\SET_ExampleSet.json')
        itemCleanReimportResult = (Join-Path $runRoot 'item-reimport.json')
        selectorUiLog = (Join-Path $runRoot 'selector-ui.log')
        equipmentCleanProject = (Join-Path $runRoot 'e')
        equipmentPortableArchive = (Join-Path $runRoot 'equipment/EquipmentSet_FBX.zip')
        equipmentPortableReimportResult = (Join-Path $runRoot 'equipment-portable-reimport.json')
        equipmentUnityPackage = (Join-Path $cloneRoot 'PackageBuilderExports/EquipmentSet.unitypackage')
        equipmentUnityReimportResult = (Join-Path $runRoot 'equipment-reimport.json')
        collectionCleanProject = (Join-Path $runRoot 'c')
        twelveCleanProject = (Join-Path $runRoot 'v')
        twelveCleanReimportResult = (Join-Path $runRoot 'twelve-reimport.json')
        twelvePortableReimportResult = (Join-Path $runRoot 'twelve-portable-reimport.json')
        collectionPackage = (Join-Path $cloneRoot 'PackageBuilderExports/ExampleCollection.unitypackage')
        collectionCleanReimportResult = (Join-Path $runRoot 'collection-reimport.json')
        itemPrefabs = @('P_Alpha.prefab', 'P_Zed.prefab') | ForEach-Object {
            Join-Path $cloneRoot "Assets\PBItemTests\Prefabs\$_"
        }
        rigCleanProject = $rigCleanCloneRoot
        package = $packageOutputPath
        packageManifest = $packageManifestPath
        scene = Join-Path $cloneRoot 'Assets\PBModelTests\Scenes\S_StoneArch_Overview.unity'
        rigFixture = Join-Path $cloneRoot 'Assets\PBRigPolicyTests\Source\RiggedProp.fbx'
        riggedPrefab = Join-Path $cloneRoot 'Assets\PBRigPolicyTests\Prefabs\P_RiggedProp.prefab'
        skeletonMetadata = Join-Path $cloneRoot `
            'Assets\PBRigPolicyTests\Documentation\SKEL_RiggedProp.json'
        animatedFixture = Join-Path $cloneRoot 'Assets\PBAnimationTests\Source\AnimatedProp.fbx'
        animationClips = @(
            Join-Path $cloneRoot 'Assets\PBAnimationTests\Animations\A_AnimatedProp_Attack.anim'
            Join-Path $cloneRoot 'Assets\PBAnimationTests\Animations\A_AnimatedProp_BendLoop.anim'
        )
        animatorController = Join-Path $cloneRoot `
            'Assets\PBAnimationTests\Controllers\AC_AnimatedProp.controller'
        animatedPrefab = Join-Path $cloneRoot `
            'Assets\PBAnimationTests\Prefabs\P_AnimatedProp.prefab'
        integrationLog = $logPath
        cleanReimportLog = $cleanReimportLogPath
        cleanReimportResult = $cleanReimportResultPath
        rigPackage = $rigPackageOutputPath
        rigPackageManifest = $rigPackageManifestPath
        rigCleanReimportLog = $rigCleanReimportLogPath
        rigCleanReimportResult = $rigCleanReimportResultPath
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
    $integrationSucceeded = $true
}
finally {
    # Failure paths also discard partial packages; retain only successful runs requested for review.
    Remove-UnityTestArtifacts -RepositoryRoot $repositoryRootPath -RunRoot $runRoot `
        -KeepArtifacts:($KeepArtifacts -and $integrationSucceeded)
}
