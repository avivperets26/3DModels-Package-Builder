[CmdletBinding()]
param(
    [string]$RepositoryRoot
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
$editorRoot = Join-Path $repositoryRootPath `
    'engine-templates\unity\6000.3\Packages\com.packagebuilder.worker\Editor'
$folderSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityProductFolderGenerator.cs') `
    -Raw -Encoding UTF8
$textureSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityTextureImporterPolicy.cs') `
    -Raw -Encoding UTF8
$packingSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityMetallicSmoothnessPacker.cs') `
    -Raw -Encoding UTF8
$materialSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityUrpLitMaterialCompiler.cs') `
    -Raw -Encoding UTF8
$modelImporterSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityStaticModelImporterPolicy.cs') -Raw -Encoding UTF8
$rigImporterSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityRigModelImporterPolicy.cs') -Raw -Encoding UTF8
$skinValidatorSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnitySkinSkeletonValidator.cs') -Raw -Encoding UTF8
$riggedPrefabSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityRiggedPrefabGenerator.cs') -Raw -Encoding UTF8
$animationClipSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityAnimationClipImporter.cs') -Raw -Encoding UTF8
$animationValidatorSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityAnimationMotionValidator.cs') -Raw -Encoding UTF8
$prefabHierarchySource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityPrefabHierarchyUtility.cs') -Raw -Encoding UTF8
$animatorControllerSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityAnimatorControllerGenerator.cs') -Raw -Encoding UTF8
$animatedPrefabSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityAnimatedPrefabGenerator.cs') -Raw -Encoding UTF8
$meshExtractorSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityMeshAssetExtractor.cs') -Raw -Encoding UTF8
$prefabSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityPrefabGenerator.cs') `
    -Raw -Encoding UTF8
$overviewSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityOverviewScenePipeline.cs') `
    -Raw -Encoding UTF8
$playModeSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityOverviewPlayModeSmokeTest.cs') `
    -Raw -Encoding UTF8
$packageExporterSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityPackageExporter.cs') `
    -Raw -Encoding UTF8
$packageValidatorSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityPackageValidator.cs') `
    -Raw -Encoding UTF8
$cleanReimportSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityCleanReimportIntegration.cs') -Raw -Encoding UTF8
$multiClipSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityMultiClipIntegration.cs') -Raw -Encoding UTF8
$topologyValidatorSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityGenericTopologyMatrixValidator.cs') -Raw -Encoding UTF8
$topologyIntegrationSource = Get-Content -LiteralPath (Join-Path $editorRoot `
        'UnityTopologyMatrixIntegration.cs') -Raw -Encoding UTF8
$controllerSource = Get-Content -LiteralPath (Join-Path $repositoryRootPath `
        'engine-templates\unity\6000.3\Assets\PackageBuilder\Preview\PackageBuilderPreviewController.cs') `
    -Raw -Encoding UTF8
$transportSource = Get-Content -LiteralPath (Join-Path $repositoryRootPath `
        'engine-templates\unity\6000.3\Assets\PackageBuilder\Preview\PackageBuilderAnimationTransport.cs') `
    -Raw -Encoding UTF8
$testSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityProductEditorIntegrationTests.cs') `
    -Raw -Encoding UTF8
$integrationSource = Get-Content -LiteralPath (Join-Path $repositoryRootPath `
        'scripts\Invoke-UnityProductIntegration.ps1') -Raw -Encoding UTF8
$integrationSource += Get-Content -LiteralPath (Join-Path $repositoryRootPath `
        'scripts\UnityCleanReimport.Common.ps1') -Raw -Encoding UTF8
$staticSliceSource = Get-Content -LiteralPath (Join-Path $repositoryRootPath `
        'scripts\Invoke-UnityStaticVerticalSlice.ps1') -Raw -Encoding UTF8
$multiClipHarnessSource = Get-Content -LiteralPath (Join-Path $repositoryRootPath `
        'scripts\Invoke-UnityMultiClipIntegration.ps1') -Raw -Encoding UTF8
$topologyGeneratorSource = Get-Content -LiteralPath (Join-Path $repositoryRootPath `
        'tests\blender\engine\pb0714_generate_topology_matrix.py') -Raw -Encoding UTF8
$topologyHarnessSource = Get-Content -LiteralPath (Join-Path $repositoryRootPath `
        'scripts\Invoke-UnityTopologyMatrixIntegration.ps1') -Raw -Encoding UTF8
$script:PassCount = 0
$script:FailureCount = 0

function Invoke-Check {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Check
    )

    try {
        & $Check
        $script:PassCount++
        Write-Host "[PASS] $Name" -ForegroundColor Green
    }
    catch {
        $script:FailureCount++
        Write-Host "[FAIL] $Name" -ForegroundColor Red
        Write-Host "       $($_.Exception.Message)" -ForegroundColor Red
    }
}

Invoke-Check 'Unity product folder policy covers every approved product case' {
    foreach ($value in @('static', 'rigged', 'rigged-animated', 'item-set', 'item-collection')) {
        if (-not $folderSource.Contains('"' + $value + '"')) {
            throw "Missing product case: $value"
        }
    }
}

Invoke-Check 'Unity base product folder inventory is exact and template-free' {
    foreach ($value in @('Source', 'Meshes', 'Materials', 'Textures', 'Prefabs',
            'Documentation', 'Scenes', 'Scripts')) {
        if (-not $folderSource.Contains('"' + $value + '"')) {
            throw "Missing base folder: $value"
        }
    }
    if ($folderSource.Contains('"_Template"')) {
        throw 'The production folder plan must not contain _Template.'
    }
}

Invoke-Check 'Animation folders are gated to the animated product case' {
    foreach ($value in @('Animations', 'Controllers', 'rigged-animated')) {
        if (-not $folderSource.Contains('"' + $value + '"')) {
            throw "Missing animated folder policy token: $value"
        }
    }
}

Invoke-Check 'Folder creation uses AssetDatabase and rejects existing product roots' {
    foreach ($value in @('AssetDatabase.CreateFolder', 'AssetDatabase.IsValidFolder',
            'UNITY_PRODUCT_FOLDER_COLLISION', 'AssetDatabase.DeleteAsset', 'AssetDatabase.SaveAssets')) {
        if (-not $folderSource.Contains($value)) {
            throw "Missing safe folder behavior: $value"
        }
    }
}

Invoke-Check 'Unity texture roles have explicit colour-space and type policy' {
    foreach ($value in @('albedo', 'emission', 'normal', 'metallic', 'roughness',
            'ambient-occlusion', 'opacity', 'height', 'TextureImporterType.NormalMap',
            'sRGBTexture')) {
        if (-not $textureSource.Contains($value)) {
            throw "Missing texture policy token: $value"
        }
    }
}

Invoke-Check 'Unity texture alpha handling is explicit and reapplied synchronously' {
    foreach ($value in @('TextureImporterAlphaSource.FromInput', 'TextureImporterAlphaSource.None',
            'alphaIsTransparency', 'SaveAndReimport')) {
        if (-not $textureSource.Contains($value)) {
            throw "Missing alpha/import behavior: $value"
        }
    }
}

Invoke-Check 'Unity metallic-smoothness packing is exact, dimension-safe, and source-restoring' {
    foreach ($value in @('UNITY_METALLIC_SMOOTHNESS_DIMENSION_MISMATCH',
            'metallicPixels[index].r', 'byte.MaxValue - roughnessPixels[index].r',
            'TextureImporterCompression.Uncompressed', 'finally', 'Restore(metallicImporter)',
            'Restore(roughnessImporter)', 'TextureImporterAlphaSource.FromInput')) {
        if (-not $packingSource.Contains($value)) {
            throw "Missing metallic-smoothness packing behavior: $value"
        }
    }
}

Invoke-Check 'Unity URP/Lit compiler maps canonical textures, factors, surfaces, and culling' {
    foreach ($value in @('Universal Render Pipeline/Lit', '_BaseMap', '_BumpMap',
            '_MetallicGlossMap', '_EmissionMap', '_OcclusionMap', '_AlphaClip', '_Cutoff',
            '_Cull', '_Surface', '_Smoothness', '1f - request.RoughnessFactor')) {
        if (-not $materialSource.Contains($value)) {
            throw "Missing URP/Lit material mapping: $value"
        }
    }
}

Invoke-Check 'Unity URP/Lit emission intent is explicit before keyword canonicalization' {
    foreach ($value in @('MaterialGlobalIlluminationFlags.BakedEmissive',
            'MaterialGlobalIlluminationFlags.EmissiveIsBlack', 'maxColorComponent')) {
        if (-not $materialSource.Contains($value)) {
            throw "Missing emission/GI behavior: $value"
        }
    }
}

Invoke-Check 'Unity URP/Lit compiler delegates keyword and render-state canonicalization to URP' {
    foreach ($value in @('BaseShaderGUI.SetMaterialKeywords(candidate, LitGUI.SetMaterialKeywords)',
            'BaseShaderGUI.SetupMaterialBlendMode(candidate)', 'SynchronizeUrpState',
            'Unity.RenderPipelines.Universal.Editor')) {
        $found = $materialSource.Contains($value) -or
            (Get-Content -LiteralPath (Join-Path $editorRoot `
                    'PackageBuilder.UnityWorker.Editor.asmdef') -Raw -Encoding UTF8).Contains($value)
        if (-not $found) {
            throw "Missing URP canonicalization behavior: $value"
        }
    }
}

Invoke-Check 'Unity static ModelImporter policy is complete and material remaps are exact' {
    foreach ($value in @('ModelImporterAnimationType.None', 'importAnimation = false',
            'importCameras = false', 'importLights = false', 'importVisibility = false',
            'importBlendShapes = false',
            'ModelImporterNormals.Import', 'ModelImporterTangents.CalculateMikk', 'globalScale',
            'preserveHierarchy', 'ModelImporterMaterialLocation.InPrefab',
            'ModelImporterMaterialImportMode.ImportViaMaterialDescription', 'GetExternalObjectMap',
            'RemoveRemap', 'AddRemap', 'StringComparer.Ordinal', 'SaveAndReimport',
            'UNITY_STATIC_MODEL_MATERIAL_PLAN_INCOMPLETE', 'SourceAssetIdentifier',
            'plannedNames.SetEquals(sourceNames)')) {
        if (-not $modelImporterSource.Contains($value)) {
            throw "Missing static ModelImporter behavior: $value"
        }
    }
}

Invoke-Check 'Unity standalone mesh extraction is transactional, stable, and deduplicated' {
    foreach ($value in @('UNITY_MESH_EXTRACTION_OUTPUT_COLLISION', 'MS_', '.ToString("D2")',
            'TryGetGUIDAndLocalFileIdentifier', 'Dictionary<long, SourceMesh>',
            'vertexCount != source.vertexCount', 'subMeshCount != source.subMeshCount',
            'AssetDatabase.DeleteAsset', 'finally', '/Meshes')) {
        if (-not $meshExtractorSource.Contains($value)) {
            throw "Missing safe mesh extraction behavior: $value"
        }
    }
}

Invoke-Check 'Unity prefab policy resets hierarchy and verifies mesh and material references' {
    foreach ($value in @('P_Model', '/Prefabs/P_', 'PrefabUtility.InstantiatePrefab',
            'PrefabUtility.SaveAsPrefabAsset', 'ResetTransform', 'Vector3.zero',
            'Quaternion.identity', 'Vector3.one', 'ReplaceMeshes', 'HasCompleteMaterials',
            'UNITY_PREFAB_REFERENCE_INVALID', 'UNITY_PREFAB_ROOT_NAME_VERIFY_FAILED',
            'UNITY_PREFAB_ROOT_TRANSFORM_VERIFY_FAILED', 'UNITY_PREFAB_CHILD_COUNT_VERIFY_FAILED',
            'UNITY_PREFAB_MODEL_NAME_VERIFY_FAILED', 'UNITY_PREFAB_MODEL_TRANSFORM_VERIFY_FAILED',
            'UNITY_PREFAB_MATERIAL_VERIFY_FAILED', 'UNITY_PREFAB_MESH_VERIFY_FAILED',
            'UNITY_PREFAB_MISSING_SCRIPT_VERIFY_FAILED')) {
        if (-not $prefabSource.Contains($value)) {
            throw "Missing prefab generation behavior: $value"
        }
    }
}

Invoke-Check 'Unity overview template is product-free with URP lighting, camera, and controller references' {
    foreach ($value in @('PackageBuilderOverview', 'PreviewTarget', 'Main Camera', 'Background',
            'Key Light', 'Fill Light', 'Universal Render Pipeline/Unlit', 'previewTarget.childCount == 0',
            'controller.PreviewTarget == previewTarget', 'controller.PreviewCamera == previewCamera',
            'UNITY_OVERVIEW_TEMPLATE_CONTENT_INVALID')) {
        if (-not $overviewSource.Contains($value)) {
            throw "Missing generic overview template behavior: $value"
        }
    }
}

Invoke-Check 'Unity preview controller frames by bounds and never scales product assets' {
    foreach ($value in @('TryGetProductBounds', 'Renderer[] renderers', 'bounds.Encapsulate',
            'Quaternion.Inverse', 'Vector3.Scale', 'SetPositionAndRotation', 'AutoFrame()',
            'Orbit(float yawDegrees', 'Zoom(float normalizedDelta)',
            'Product transforms are never modified')) {
        if (-not $controllerSource.Contains($value)) {
            throw "Missing bounds-only preview behavior: $value"
        }
    }
    if ($controllerSource -match 'previewTarget\.(localScale|position|rotation)\s*=') {
        throw 'Preview navigation must not mutate the preview target transform.'
    }
    if (-not $testSource.Contains('AreBoundsInsideViewport')) {
        throw 'Real Unity integration must prove every product-bounds corner is inside the viewport.'
    }
}

Invoke-Check 'Unity preview implements the shared interactive dark-studio contract' {
    foreach ($value in @('ContractVersion = "1"', 'EventType.MouseDrag',
            'EventType.ScrollWheel', 'EventType.KeyDown', 'KeyCode.R', 'KeyCode.H', 'KeyCode.L',
            'SetControlsVisible', 'RestoreControls', 'RestoreControlsRect', 'Show Controls',
            'Restore the 3D preview controls (H)', 'SetKeyLightDirection', 'ResetKeyLight',
            'MinimumPitchDegrees = -80f', 'MaximumPitchDegrees = 80f', 'OnGUI()',
            'Reset View', 'Reset Light', 'StudioBackground')) {
        if (-not $controllerSource.Contains($value)) {
            throw "Missing interactive preview behavior: $value"
        }
    }
    if ($controllerSource.Contains('Input.')) {
        throw 'Interactive preview must not call the backend-specific UnityEngine.Input API.'
    }
    foreach ($value in @('CreateRadialBackgroundTexture', 'T_OverviewBackground.png',
            'Universal Render Pipeline/Unlit', 'TextureWrapMode.Clamp',
            'OutputBackgroundTextureReference', 'controller.KeyLight == FindUniqueChild',
            'controller.StudioBackground == background')) {
        if (-not $overviewSource.Contains($value)) {
            throw "Missing dark-studio scene behavior: $value"
        }
    }
}

Invoke-Check 'Unity animation preview maps shared transport semantics to pointer and keyboard controls' {
    foreach ($value in @('Select(int index)', 'Play()', 'Pause()', 'Replay()',
            'Scrub(float timeSeconds)', 'SetLoop(bool enabled)', 'Tick(float deltaSeconds)',
            'PackageBuilderPlaybackState.Completed', 'clips[index].isLooping',
            'animator.Play', 'animator.Update(0f)', 'KeyboardTimelineStepSeconds = 0.1f')) {
        if (-not $transportSource.Contains($value)) {
            throw "Missing Unity animation transport behavior: $value"
        }
    }
    foreach ($value in @('GUI.SelectionGrid', 'GUI.HorizontalSlider', 'GUI.Toggle',
            'AnimationSelectorControl', 'PlayPauseControl', 'ReplayControl', 'TimelineControl',
            'LoopControl', 'KeyCode.Tab', 'KeyCode.Space', 'GUI.FocusControl',
            'CurrentTimeSeconds', 'DurationSeconds')) {
        if (-not $controllerSource.Contains($value)) {
            throw "Missing accessible animation control behavior: $value"
        }
    }
    if ($transportSource.Contains('AssetDatabase') -or $transportSource.Contains('UnityEditor')) {
        throw 'Customer animation transport must not depend on Editor APIs.'
    }
}

Invoke-Check 'Unity overview composition saves one intended prefab under the publisher product root' {
    foreach ($value in @('previewTarget.childCount != 1', 'PrefabUtility.InstantiatePrefab',
            'product.transform.SetParent(previewTarget, false)', 'S_" + request.AssetId + "_Overview.unity',
            '/Scripts/', 'AssetDatabase.GetAssetPath(source) == request.ProductPrefabReference',
            'GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) == 0')) {
        if (-not $overviewSource.Contains($value)) {
            throw "Missing overview composition behavior: $value"
        }
    }
}

Invoke-Check 'Unity overview scene has a real bounded Play mode smoke test' {
    foreach ($value in @('EditorApplication.EnterPlaymode()', 'EnteredPlayMode', 'EnteredEditMode',
            'PACKAGEBUILDER_UNITY_OVERVIEW_PLAYMODE_PASS', 'controller.AutoFrame()',
            'controller.Orbit(20f, 8f)', 'controller.Zoom(0.2f)',
            'Camera navigation changed a product transform')) {
        if (-not $playModeSource.Contains($value)) {
            throw "Missing overview Play mode assertion: $value"
        }
    }
}

Invoke-Check 'Unity package export is exact, dependency-closed, and collision-safe' {
    foreach ($value in @('AssetDatabase.GetAllAssetPaths()', 'AssetDatabase.GetDependencies',
            'UNITY_PACKAGE_DEPENDENCY_OUTSIDE_PRODUCT', 'UNITY_PACKAGE_PATH_INVALID',
            'ExportPackageOptions.Default', 'UNITY_PACKAGE_EXPORT_OUTPUT_COLLISION',
            'OutputPackagePath', 'AllowedProductFolders')) {
        if (-not $packageExporterSource.Contains($value)) {
            throw "Missing exact Unity package export behavior: $value"
        }
    }
    if ($packageExporterSource.Contains('ExportPackageOptions.IncludeDependencies') -or
        $packageExporterSource.Contains('ExportPackageOptions.Recurse')) {
        throw 'Unity package export must not use implicit recursive or dependency inclusion.'
    }
}

Invoke-Check 'Unity animated package paths are placed in their exact product folders' {
    foreach ($value in @('".anim" => "Animations/"',
            '".controller" => "Controllers/"')) {
        if (-not $packageValidatorSource.Contains($value)) {
            throw "Missing animated package path policy: $value"
        }
    }
}

Invoke-Check 'Unity release validation blocks logs, references, GUIDs, duplicates, and paths' {
    foreach ($value in @('UNITY_VALIDATION_MISSING_SCRIPT', 'UNITY_VALIDATION_MISSING_MATERIAL',
            'UNITY_VALIDATION_MISSING_TEXTURE', 'UNITY_VALIDATION_COMPILATION_FAILED',
            'UNITY_VALIDATION_PACKAGE_WARNING', 'UNITY_VALIDATION_PACKAGE_ERROR',
            'UNITY_VALIDATION_BROKEN_GUID', 'UNITY_VALIDATION_DUPLICATE_PATH',
            'UNITY_VALIDATION_DUPLICATE_GUID', 'UNITY_VALIDATION_PATH_INVALID',
            'UNITY_VALIDATION_DEPENDENCY_OUTSIDE_PRODUCT', 'GetMonoBehavioursWithMissingScriptCount',
            'AssetPathToGUID', 'GUIDToAssetPath')) {
        if (-not $packageValidatorSource.Contains($value)) {
            throw "Missing Unity release validator behavior: $value"
        }
    }
}

Invoke-Check 'Editor integration tests cover all cases, roles, collisions, and unsafe inputs' {
    foreach ($value in @('PACKAGEBUILDER_UNITY_PRODUCT_TESTS_PASS',
            'UNITY_PRODUCT_FOLDER_COLLISION', 'orm', '../outside.png',
            'TextureImporterType.NormalMap', 'AssetDatabase.IsValidFolder',
            'Texture2D(4, 4', 'new Color[16]', 'TestMetallicSmoothnessPacking',
            'UNITY_METALLIC_SMOOTHNESS_DIMENSION_MISMATCH', 'Metallic red channel is incorrect',
            'Smoothness alpha must equal one minus roughness', 'TestUrpLitMaterialCompilation',
            'VerifyUrpStateIsStable', '_METALLICSPECGLOSSMAP', '_SURFACE_TYPE_TRANSPARENT',
            'TestStaticModelImportMeshExtractionAndPrefab', 'ModelImporterAnimationType.None',
            'Material remapping changed when request order changed',
            'UNITY_MESH_EXTRACTION_OUTPUT_COLLISION', 'P_StoneArch.prefab',
            'The product root or P_Model transform is not reset',
            'TestOverviewTemplateControllerAndComposition', 'Overview auto-frame failed',
            'Overview camera navigation changed a product transform')) {
        if (-not $testSource.Contains($value)) {
            throw "Missing Editor integration assertion: $value"
        }
    }
    foreach ($value in @('TestExactPackageExportAndValidation',
            'UNITY_VALIDATION_PACKAGE_WARNING', 'UNITY_VALIDATION_COMPILATION_FAILED',
            'UNITY_PACKAGE_PATH_INVALID', 'UNITY_PACKAGE_EXPORT_OUTPUT_COLLISION')) {
        if (-not $testSource.Contains($value)) {
            throw "Missing package export/validation integration assertion: $value"
        }
    }
}

Invoke-Check 'Unity integration uses a legacy-safe short clone and validates a clean reopen' {
    foreach ($value in @('artifacts\u', "Substring(0, 8)",
            '$maximumLegacyCompatiblePathLength = 248', 'unity-product-reopen.log',
            'unity-product-reopen-retry.log', 'CurrentThread::IsMainThread()',
            '$knownNativeStartupRace', "'-quit'", 'DirectoryNotFoundException',
            'tests\fixtures\portable\static-vertical-slice\source\StoneArch.fbx',
            'Unity static ModelImporter Editor tests',
            'Unity standalone mesh extraction Editor tests',
            'Unity static prefab generation Editor tests',
            'Unity generic overview scene template tests',
            'Unity overview Play mode smoke test',
            'PackageBuilderPreviewController.cs.meta',
            'PackageBuilderAnimationTransport.cs.meta',
            'PACKAGEBUILDER_UNITYPACKAGE_OUTPUT', 'unitypackage-extracted', 'pathname',
            'asset.meta', 'Unity exact package archive and metadata validation',
            'Unity populated-project reopen validation')) {
        if (-not $integrationSource.Contains($value)) {
            throw "Missing Unity reopen/path safeguard: $value"
        }
    }
}

Invoke-Check 'Unity integration rejects a stale URP material upgrader marker' {
    foreach ($value in @('URPProjectSettings.asset', 'MaterialPostprocessor.cs',
            'm_LastMaterialVersion', 'upgraderCount',
            'Unity URP material upgrader marker validation')) {
        if (-not $integrationSource.Contains($value)) {
            throw "Missing URP material upgrader safeguard: $value"
        }
    }
}

Invoke-Check 'Unity package clean-reimport validation is isolated and structured' {
    foreach ($value in @('UnityCleanReimportResult', 'schemaVersion = 1',
            'UNITY_REIMPORT_PRODUCT_ROOT_MISSING', 'UNITY_REIMPORT_SCENE_REFERENCES_INVALID',
            'UNITY_REIMPORT_MISSING_SCRIPT', 'UNITY_REIMPORT_MATERIAL_OUTSIDE_PRODUCT',
            'UNITY_REIMPORT_TEXTURE_OUTSIDE_PRODUCT', 'UNITY_REIMPORT_PREFAB_NOT_RENDERABLE',
            'PACKAGEBUILDER_UNITY_CLEAN_REIMPORT_PASS', 'JsonUtility.ToJson')) {
        if (-not $cleanReimportSource.Contains($value)) {
            throw "Missing clean-reimport validator behavior: $value"
        }
    }
    foreach ($value in @('$cleanCloneRoot', "'-importPackage'",
            'PACKAGEBUILDER_UNITY_REIMPORT_RESULT', 'unity-clean-reimport-result.json',
            'The clean Unity clone unexpectedly contains product assets',
            'Unity clean package reimport, scene, prefab, material, texture, and render validation')) {
        if (-not $integrationSource.Contains($value)) {
            throw "Missing clean-reimport harness behavior: $value"
        }
    }
}

Invoke-Check 'Unity rigged-no-animation package is validated in a separate clean project' {
    foreach ($value in @('rigged-no-animation', 'UNITY_REIMPORT_RIG_ANIMATION_FOLDER_PRESENT',
            'UNITY_REIMPORT_RIG_ANIMATION_ASSET_PRESENT', 'UNITY_REIMPORT_RIG_IMPORT_POLICY_INVALID',
            'UNITY_REIMPORT_RIG_PREFAB_INVALID', 'animationClipCount', 'animatorCount',
            'UniqueBoneCount')) {
        if (-not $cleanReimportSource.Contains($value)) {
            throw "Missing rigged clean-reimport behavior: $value"
        }
    }
    foreach ($value in @('$rigCleanCloneRoot', '$rigPackageOutputPath',
            'PACKAGEBUILDER_UNITY_RIG_PACKAGE_OUTPUT', 'Invoke-CleanUnityPackageValidation',
            'Unity rigged-no-animation clean package reimport validation')) {
        if (-not $integrationSource.Contains($value)) {
            throw "Missing rigged clean-reimport harness behavior: $value"
        }
    }
}

Invoke-Check 'Unity static vertical slice composes both targets and promotes atomically' {
    foreach ($value in @('Invoke-PB0507PortableStaticManualTest.ps1',
            'Invoke-UnityProductIntegration.ps1', 'sourceSha256',
            'Portable\Stone_Arch_FBX.zip', 'Unity\StoneArch.unitypackage',
            'unity-clean-reimport-result.json', '[IO.Directory]::Move',
            "status = 'passed'", 'validation-report.json')) {
        if (-not $staticSliceSource.Contains($value)) {
            throw "Missing Unity static vertical-slice behavior: $value"
        }
    }
}

Invoke-Check 'Unity Generic rig importer policy is explicit and deterministic' {
    foreach ($value in @('ModelImporterAnimationType.Generic',
            'ModelImporterAvatarSetup.CreateFromThisModel', 'motionNodeName',
            'preserveHierarchy', 'optimizeGameObjects', 'extraExposedTransformPaths',
            'StringComparer.Ordinal', 'UNITY_GENERIC_RIG_VERIFY_FAILED')) {
        if (-not $rigImporterSource.Contains($value)) {
            throw "Missing Generic rig importer behavior: $value"
        }
    }
}

Invoke-Check 'Unity Humanoid import is opt-in, validated, and never silently downgraded' {
    foreach ($value in @('HumanoidRequestedByManifest', 'HumanoidBoneMappings',
            'HumanTrait.BoneName', 'avatar.isValid', 'avatar.isHuman',
            'UNITY_HUMANOID_MANIFEST_OPT_IN_REQUIRED', 'UNITY_HUMANOID_MAPPING_INVALID',
            'UNITY_HUMANOID_AVATAR_INVALID', 'ApproveGenericFallback',
            'UsedApprovedFallback', 'Restore(request.ModelAssetReference, snapshot)')) {
        if (-not $rigImporterSource.Contains($value)) {
            throw "Missing Humanoid validation behavior: $value"
        }
    }
    foreach ($value in @('humanoidWithoutManifestApproval', 'humanoidWithoutFallbackApproval',
            'ApproveGenericFallback = true', 'TestGenericAndHumanoidRigImporterPolicies')) {
        if (-not $testSource.Contains($value)) {
            throw "Missing real Unity rig policy test: $value"
        }
    }
}

Invoke-Check 'Unity skin and skeleton validation is complete and finding-driven' {
    foreach ($value in @('SkinnedMeshRenderer', 'rootBone', 'bindposes',
            'GetBonesPerVertex', 'GetAllBoneWeights', 'UNITY_SKIN_BONE_MISSING',
            'UNITY_SKIN_BINDPOSE_COUNT_INVALID', 'UNITY_SKIN_UNWEIGHTED_VERTICES',
            'UNITY_SKIN_MAXIMUM_INFLUENCES_EXCEEDED', 'StringComparer.Ordinal')) {
        if (-not $skinValidatorSource.Contains($value)) {
            throw "Missing skin/skeleton validation behavior: $value"
        }
    }
    foreach ($value in @('TestSkinRiggedPrefabAndAnimationClips',
            'UNITY_SKIN_ROOT_BONE_MISSING', 'UNITY_SKIN_BONE_MISSING',
            'UnweightedVertexCount == 0', 'MaximumInfluences <= 4')) {
        if (-not $testSource.Contains($value)) {
            throw "Missing real Unity skin validation assertion: $value"
        }
    }
}

Invoke-Check 'Unity rigged-no-animation prefab flow preserves skin without empty outputs' {
    foreach ($value in @('P_Model', '/Prefabs/P_', '/Documentation/SKEL_',
            'hasAnimationClips = false', 'RemoveEmptyAnimationComponents',
            'UnitySkinSkeletonValidator.Validate', 't:AnimationClip', 't:AnimatorController',
            'UNITY_RIGGED_PREFAB_EMPTY_ANIMATION_OUTPUT')) {
        if (-not $riggedPrefabSource.Contains($value)) {
            throw "Missing rigged-no-animation prefab behavior: $value"
        }
    }
    foreach ($value in @('P_RiggedProp.prefab', 'SKEL_RiggedProp.json',
            '!AssetDatabase.IsValidFolder(RigPolicyTestRoot + "/Animations")',
            '!AssetDatabase.IsValidFolder(RigPolicyTestRoot + "/Controllers")')) {
        if (-not $testSource.Contains($value)) {
            throw "Missing real Unity rigged prefab assertion: $value"
        }
    }
}

Invoke-Check 'Unity action import creates exact deterministic A_ clips' {
    foreach ($value in @('importedTakeInfos', 'clipAnimations', 'firstFrame', 'lastFrame',
            'sampleRate', 'resampleCurves', 'SaveAndReimport', 'A_',
            'UNITY_ANIMATION_CLIP_RANGE_VERIFY_FAILED', 'AssetDatabase.CreateAsset')) {
        if (-not $animationClipSource.Contains($value)) {
            throw "Missing animation clip import behavior: $value"
        }
    }
    foreach ($value in @('A_AnimatedProp_Attack.anim', 'A_AnimatedProp_BendLoop.anim',
            'OutputAssetReferences.SequenceEqual', 'importedAttack.firstFrame',
            'importedLoop.lastFrame')) {
        if (-not $testSource.Contains($value)) {
            throw "Missing real Unity animation clip assertion: $value"
        }
    }
    foreach ($value in @("'--', `$animatedFbxPath, 'animated'",
            'Unity skin and skeleton validation Editor tests: passed',
            'Unity rigged-no-animation prefab and skeleton metadata tests: passed',
            'Unity exact animation clip extraction Editor tests: passed')) {
        if (-not $integrationSource.Contains($value)) {
            throw "Missing Blender-to-Unity rig/animation integration behavior: $value"
        }
    }
}

Invoke-Check 'Unity animation validation proves imported metadata, bindings, and sampled motion' {
    foreach ($value in @('DurationSeconds', 'FramesPerSecond', 'AnimationUtility.GetCurveBindings',
            'm_LocalRotation', 'AnimationMode.SampleAnimationClip', 'renderer.BakeMesh',
            'BoneMotionVerified', 'RendererMotionVerified', 'NonLoopingCompletionVerified')) {
        if (-not $animationValidatorSource.Contains($value)) {
            throw "Missing animation validation behavior: $value"
        }
    }
    foreach ($value in @('RenderableVolumeVerified',
            'UNITY_ANIMATION_RENDERER_VOLUME_DEGENERATE', 'renderer.localBounds.size')) {
        if (-not $animationValidatorSource.Contains($value)) {
            throw "Missing renderable-volume validation behavior: $value"
        }
    }
    foreach ($value in @('UnityAnimationMotionValidator.Validate',
            'GetAssetDependencyHash', 'transport.Scrub', 'transport.SetLoop',
            'transport.Playback', 'A_AnimatedProp_Attack', 'A_AnimatedProp_BendLoop')) {
        if (-not $testSource.Contains($value)) {
            throw "Missing animation validation integration assertion: $value"
        }
    }
}

Invoke-Check 'Unity clip loop, compression, and root-motion policy is explicit' {
    foreach ($value in @('UnityAnimationCompressionPolicy', 'UnityRootMotionPolicy',
            'ModelImporterAnimationCompression.Optimal', 'loopTime', 'loopPose',
            'lockRootRotation', 'lockRootHeightY', 'lockRootPositionXZ',
            'CompressionPolicy == UnityAnimationCompressionPolicy.Unspecified',
            'RootMotionPolicy == UnityRootMotionPolicy.Unspecified')) {
        if (-not $animationClipSource.Contains($value)) {
            throw "Missing explicit animation clip policy behavior: $value"
        }
    }
    foreach ($value in @('!importedAttack.loopTime', 'importedLoop.loopTime',
            'ModelImporterAnimationCompression.Optimal', 'UnityRootMotionPolicy.BakeIntoPose',
            'UnityRootMotionPolicy.Preserve')) {
        if (-not $testSource.Contains($value)) {
            throw "Missing real Unity clip-policy assertion: $value"
        }
    }
}

Invoke-Check 'Unity Animator Controller generation is deterministic and replayable' {
    foreach ($value in @('AC_', 'Replay_', 'AnimatorControllerParameterType.Trigger',
            'AddAnyStateTransition', 'defaultState', 'StringComparer.Ordinal',
            'UNITY_ANIMATOR_CONTROLLER_MOTION_MISSING', 'UNITY_ANIMATOR_CONTROLLER_INVALID')) {
        if (-not $animatorControllerSource.Contains($value)) {
            throw "Missing Animator Controller behavior: $value"
        }
    }
    foreach ($value in @('AC_AnimatedProp.controller', 'Replay_Attack', 'Replay_BendLoop',
            'states.Length == 2', 'defaultState.motion == attackClip')) {
        if (-not $testSource.Contains($value)) {
            throw "Missing real Animator Controller assertion: $value"
        }
    }
}

Invoke-Check 'Unity animated prefab preserves validated skin and assigns one controller' {
    foreach ($value in @('UnityPrefabHierarchyUtility.TryCreate',
            'UnitySkinSkeletonValidator.Validate', 'runtimeAnimatorController',
            'applyRootMotion', 'SkinnedMeshRenderer', 'UNITY_ANIMATED_PREFAB_INVALID',
            'GameObjectUtility.GetMonoBehavioursWithMissingScriptCount')) {
        if (-not $animatedPrefabSource.Contains($value)) {
            throw "Missing animated prefab behavior: $value"
        }
    }
    foreach ($value in @('P_AnimatedProp.prefab', 'savedAnimators.Length == 1',
            'savedAnimators[0].runtimeAnimatorController == controller',
            'GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 1')) {
        if (-not $testSource.Contains($value)) {
            throw "Missing real animated prefab assertion: $value"
        }
    }
}

Invoke-Check 'Unity rigged and animated prefabs share one hierarchy policy' {
    foreach ($source in @($riggedPrefabSource, $animatedPrefabSource)) {
        if (-not $source.Contains('UnityPrefabHierarchyUtility.TryCreate')) {
            throw 'A rigged prefab flow bypasses the shared hierarchy policy.'
        }
    }
    foreach ($value in @('P_Model', 'Reset', 'IsSafeReference', 'IsReset')) {
        if (-not $prefabHierarchySource.Contains($value)) {
            throw "Missing shared prefab hierarchy behavior: $value"
        }
    }
}

Invoke-Check 'Unity multi-clip fixture preserves mixed loops through clean reimport' {
    foreach ($value in @('A_MultiClipProp_Attack', 'A_MultiClipProp_BendLoop',
            'LoopTime = false', 'LoopTime = true', 'states.Length == 2',
            'UnityAnimationMotionValidator.Validate', 'UnityPackageExporter.TryExport')) {
        if (-not $multiClipSource.Contains($value)) {
            throw "Missing multi-clip integration behavior: $value"
        }
    }
    foreach ($value in @('multi-clip-animated', 'loopingClipCount',
            'nonLoopingClipCount', 'controllerStateCount', 'animationMotionVerified',
            'UNITY_REIMPORT_MULTI_CLIP_CONTROLLER_INVALID')) {
        if (-not $cleanReimportSource.Contains($value)) {
            throw "Missing multi-clip clean-reimport behavior: $value"
        }
    }
    foreach ($value in @('pb0701_generate_rig_fbx.py',
            'Invoke-CleanUnityPackageValidation', 'MultiClipProp.unitypackage',
            'PACKAGEBUILDER_UNITY_MULTI_CLIP_PASS')) {
        if (-not $multiClipHarnessSource.Contains($value)) {
            throw "Missing multi-clip harness behavior: $value"
        }
    }
}

Invoke-Check 'Unity Generic topology matrix is reusable and topology-neutral' {
    foreach ($value in @('mechanical-bow', 'mechanical-vehicle', 'quadruped-tail',
            'winged-creature', 'biped-tail', 'InvalidMultiRoot.fbx',
            'topology-matrix.txt')) {
        if (-not $topologyGeneratorSource.Contains($value)) {
            throw "Missing procedural topology fixture behavior: $value"
        }
    }
    foreach ($value in @('ModelImporterAnimationType.Generic', 'avatars.All(value => !value.isHuman)',
            'ValidateHierarchy', 'MaximumInfluences == 1', 'MovingBoneName',
            'UNITY_TOPOLOGY_ROOT_COUNT_INVALID', 'UNITY_ANIMATION_BINDINGS_MISMATCH:InvalidBinding')) {
        if (-not $topologyValidatorSource.Contains($value)) {
            throw "Missing topology-neutral Unity validation behavior: $value"
        }
    }
    foreach ($value in @('UnityRigModelImporterPolicy.TryApply',
            'UnityAnimationClipImporter.TryImportAndExtract',
            'UnityAnimatedPrefabGenerator.TryCreate',
            'UnityGenericTopologyMatrixValidator.Validate',
            'UnityPackageExporter.TryExport')) {
        if (-not $topologyIntegrationSource.Contains($value)) {
            throw "Missing topology-matrix integration reuse: $value"
        }
    }
    foreach ($value in @('generic-topology-matrix', 'topologyCaseCount',
            'topologyNegativeFindingsVerified', 'Invoke-CleanUnityPackageValidation',
            'PACKAGEBUILDER_UNITY_TOPOLOGY_MATRIX_PASS')) {
        if (-not $cleanReimportSource.Contains($value) -and
            -not $topologyHarnessSource.Contains($value)) {
            throw "Missing topology clean-reimport behavior: $value"
        }
    }
}

Invoke-Check 'Unity product policy sources are deterministic public-safe text' {
    $files = @(
        (Join-Path $editorRoot 'UnityProductFolderGenerator.cs'),
        (Join-Path $editorRoot 'UnityTextureImporterPolicy.cs'),
        (Join-Path $editorRoot 'UnityMetallicSmoothnessPacker.cs'),
        (Join-Path $editorRoot 'UnityUrpLitMaterialCompiler.cs'),
        (Join-Path $editorRoot 'UnityStaticModelImporterPolicy.cs'),
        (Join-Path $editorRoot 'UnityRigModelImporterPolicy.cs'),
        (Join-Path $editorRoot 'UnitySkinSkeletonValidator.cs'),
        (Join-Path $editorRoot 'UnityRiggedPrefabGenerator.cs'),
        (Join-Path $editorRoot 'UnityAnimationClipImporter.cs'),
        (Join-Path $editorRoot 'UnityAnimationMotionValidator.cs'),
        (Join-Path $editorRoot 'UnityGenericTopologyMatrixValidator.cs'),
        (Join-Path $editorRoot 'UnityPrefabHierarchyUtility.cs'),
        (Join-Path $editorRoot 'UnityAnimatorControllerGenerator.cs'),
        (Join-Path $editorRoot 'UnityAnimatedPrefabGenerator.cs'),
        (Join-Path $editorRoot 'UnityMeshAssetExtractor.cs'),
        (Join-Path $editorRoot 'UnityPrefabGenerator.cs'),
        (Join-Path $editorRoot 'UnityOverviewScenePipeline.cs'),
        (Join-Path $editorRoot 'UnityOverviewPlayModeSmokeTest.cs'),
        (Join-Path $editorRoot 'UnityPackageExporter.cs'),
        (Join-Path $editorRoot 'UnityPackageValidator.cs'),
        (Join-Path $editorRoot 'UnityCleanReimportIntegration.cs'),
        (Join-Path $editorRoot 'UnityTopologyMatrixIntegration.cs'),
        (Join-Path $repositoryRootPath `
            'tests\blender\engine\pb0714_generate_topology_matrix.py'),
        (Join-Path $repositoryRootPath `
            'scripts\Invoke-UnityTopologyMatrixIntegration.ps1'),
        (Join-Path $repositoryRootPath `
            'engine-templates\unity\6000.3\Assets\PackageBuilder\Preview\PackageBuilderPreviewController.cs'),
        (Join-Path $repositoryRootPath `
            'engine-templates\unity\6000.3\Assets\PackageBuilder\Preview\PackageBuilderAnimationTransport.cs'),
        (Join-Path $editorRoot 'UnityProductEditorIntegrationTests.cs'),
        (Join-Path $repositoryRootPath 'scripts\Invoke-UnityStaticVerticalSlice.ps1')
    )
    $utf8 = New-Object Text.UTF8Encoding($false, $true)
    foreach ($file in $files) {
        $bytes = [IO.File]::ReadAllBytes($file)
        $text = $utf8.GetString($bytes)
        if ($text.Contains("`r") -or $text -match '(?i)\b[A-Z]:[\\/]Users[\\/]') {
            throw "Unity policy source is not public-safe LF text: $file"
        }
    }
}

Write-Host ''
Write-Host "Unity product policy validation: $script:PassCount passed, $script:FailureCount failed."
if ($script:FailureCount -gt 0) {
    throw 'Unity product policy validation failed.'
}
