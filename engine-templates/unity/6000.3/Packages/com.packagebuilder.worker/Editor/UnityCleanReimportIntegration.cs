using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PackageBuilder.Preview;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>One stable, structured finding produced by the clean Unity reimport check.</summary>
    [Serializable]
    public sealed class UnityCleanReimportFinding
    {
        public string code = string.Empty;
        public string assetReference = string.Empty;
    }

    /// <summary>Machine-readable result retained outside the disposable clean Unity project.</summary>
    [Serializable]
    public sealed class UnityCleanReimportResult
    {
        public int schemaVersion = 1;
        public bool passed;
        public string productRootReference = string.Empty;
        public string sceneReference = string.Empty;
        public string prefabReference = string.Empty;
        public string validationMode = string.Empty;
        public int rendererCount;
        public int skinnedRendererCount;
        public int boneCount;
        public int animationClipCount;
        public int animatorCount;
        public int materialCount;
        public int textureCount;
        public int controllerStateCount;
        public int loopingClipCount;
        public int nonLoopingClipCount;
        public bool animationMotionVerified;
        public int topologyCaseCount;
        public bool genericTopologyVerified;
        public bool topologyHierarchyVerified;
        public bool topologySkinWeightsVerified;
        public bool topologyNegativeFindingsVerified;
        public string[] topologyCategories = Array.Empty<string>();
        public string[] clipNames = Array.Empty<string>();
        public bool synchronizedRendererMotionVerified;
        public UnityCleanReimportFinding[] findings = Array.Empty<UnityCleanReimportFinding>();
    }

    /// <summary>
    /// Validates a package after Unity imports it into a newly cloned project with no prior
    /// product assets. The command-line harness owns import timing and process isolation.
    /// </summary>
    public static class UnityCleanReimportIntegration
    {
        private const string DefaultProductRoot = "Assets/PBModelTests";
        private const string DefaultScene = "Assets/PBModelTests/Scenes/S_StoneArch_Overview.unity";
        private const string DefaultPrefab = "Assets/PBModelTests/Prefabs/P_StoneArch.prefab";

        /// <summary>Runs validation, writes its structured result atomically, and exits Unity.</summary>
        public static void Run()
        {
            string resultPath = Environment.GetEnvironmentVariable(
                "PACKAGEBUILDER_UNITY_REIMPORT_RESULT");
            var result = new UnityCleanReimportResult
            {
                validationMode = EnvironmentValue(
                    "PACKAGEBUILDER_UNITY_REIMPORT_MODE", "overview"),
                productRootReference = EnvironmentValue(
                    "PACKAGEBUILDER_UNITY_PRODUCT_ROOT", DefaultProductRoot),
                sceneReference = EnvironmentValue(
                    "PACKAGEBUILDER_UNITY_OVERVIEW_SCENE", DefaultScene),
                prefabReference = EnvironmentValue(
                    "PACKAGEBUILDER_UNITY_PRODUCT_PREFAB", DefaultPrefab),
            };
            var findings = new List<UnityCleanReimportFinding>();
            if (string.Equals(result.validationMode, "rigged-no-animation", StringComparison.Ordinal) ||
                string.Equals(result.validationMode, "multi-clip-animated", StringComparison.Ordinal) ||
                string.Equals(result.validationMode, "generic-topology-matrix", StringComparison.Ordinal))
            {
                result.sceneReference = string.Empty;
            }
            try
            {
                Validate(result, findings);
            }
            catch (Exception exception)
            {
                findings.Add(Finding("UNITY_REIMPORT_VALIDATION_EXCEPTION", exception.GetType().Name));
            }

            result.findings = findings
                .OrderBy(value => value.code, StringComparer.Ordinal)
                .ThenBy(value => value.assetReference, StringComparer.Ordinal)
                .ToArray();
            result.passed = result.findings.Length == 0;

            try
            {
                WriteResult(resultPath, result);
            }
            catch (Exception exception)
            {
                Debug.LogError("PACKAGEBUILDER_UNITY_REIMPORT_RESULT_WRITE_FAILED:" +
                    exception.GetType().Name);
                EditorApplication.Exit(1);
                return;
            }

            if (result.passed)
            {
                Debug.Log("PACKAGEBUILDER_UNITY_CLEAN_REIMPORT_PASS");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("PACKAGEBUILDER_UNITY_CLEAN_REIMPORT_FAIL:" +
                    string.Join(",", result.findings.Select(value => value.code)));
                EditorApplication.Exit(1);
            }
        }

        private static void Validate(
            UnityCleanReimportResult result,
            List<UnityCleanReimportFinding> findings)
        {
            if (string.Equals(result.validationMode, "item-prefabs", StringComparison.Ordinal) ||
                string.Equals(result.validationMode, "item-and-set-prefabs", StringComparison.Ordinal) ||
                string.Equals(result.validationMode, "collection-overview", StringComparison.Ordinal))
            {
                result.sceneReference = string.Empty;
                try
                {
                    UnityItemPrefabIntegration.VerifySaved();
                    if (result.validationMode == "item-and-set-prefabs") { UnityAssembledSetIntegration.VerifySaved(); }
                    if (result.validationMode == "collection-overview") { UnityMultiItemIntegration.VerifySaved(); }
                }
                catch (InvalidOperationException) { findings.Add(Finding("UNITY_REIMPORT_ITEM_PREFAB_INVALID", result.productRootReference)); }
                return;
            }
            if (string.Equals(result.validationMode, "rigged-no-animation", StringComparison.Ordinal))
            {
                ValidateRiggedNoAnimation(result, findings);
                return;
            }
            if (string.Equals(result.validationMode, "multi-clip-animated", StringComparison.Ordinal))
            {
                ValidateMultiClipAnimated(result, findings);
                return;
            }
            if (string.Equals(result.validationMode, "generic-topology-matrix", StringComparison.Ordinal))
            {
                ValidateGenericTopologyMatrix(result, findings);
                return;
            }

            bool silverwing = string.Equals(
                result.validationMode, "silverwing-animated", StringComparison.Ordinal);
            if (!string.Equals(result.validationMode, "overview", StringComparison.Ordinal) &&
                !silverwing)
            {
                findings.Add(Finding("UNITY_REIMPORT_MODE_INVALID", result.validationMode));
                return;
            }

            if (!AssetDatabase.IsValidFolder(result.productRootReference))
            {
                findings.Add(Finding("UNITY_REIMPORT_PRODUCT_ROOT_MISSING", result.productRootReference));
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.prefabReference);
            if (prefab == null)
            {
                findings.Add(Finding("UNITY_REIMPORT_PREFAB_MISSING", result.prefabReference));
                return;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(result.sceneReference);
            if (sceneAsset == null)
            {
                findings.Add(Finding("UNITY_REIMPORT_SCENE_MISSING", result.sceneReference));
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(result.sceneReference, OpenSceneMode.Single);
            GameObject root = UnityOverviewSceneTemplateBuilder.FindUniqueRoot(
                scene,
                UnityOverviewSceneTemplateBuilder.OverviewRootName);
            PackageBuilderPreviewController controller = root == null
                ? null
                : root.GetComponent<PackageBuilderPreviewController>();
            if (root == null || controller == null || controller.PreviewTarget == null ||
                controller.PreviewCamera == null || controller.KeyLight == null ||
                controller.StudioBackground == null || controller.PreviewTarget.childCount != 1)
            {
                findings.Add(Finding("UNITY_REIMPORT_SCENE_REFERENCES_INVALID", result.sceneReference));
                return;
            }

            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) != 0)
            {
                findings.Add(Finding("UNITY_REIMPORT_MISSING_SCRIPT", result.sceneReference));
            }

            Renderer[] renderers = controller.PreviewTarget.GetComponentsInChildren<Renderer>(true);
            result.rendererCount = renderers.Length;
            if (renderers.Length == 0)
            {
                findings.Add(Finding("UNITY_REIMPORT_RENDERER_MISSING", result.prefabReference));
                return;
            }

            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            foreach (Renderer rendererValue in renderers)
            {
                if (rendererValue == null || !rendererValue.enabled)
                {
                    continue;
                }

                foreach (Material material in rendererValue.sharedMaterials)
                {
                    if (material == null)
                    {
                        findings.Add(Finding("UNITY_REIMPORT_MATERIAL_MISSING", result.prefabReference));
                        continue;
                    }

                    _ = materials.Add(material);
                    string materialPath = AssetDatabase.GetAssetPath(material);
                    if (!WithinRoot(materialPath, result.productRootReference))
                    {
                        findings.Add(Finding("UNITY_REIMPORT_MATERIAL_OUTSIDE_PRODUCT", materialPath));
                    }

                    foreach (string propertyName in material.GetTexturePropertyNames())
                    {
                        Texture texture = material.GetTexture(propertyName);
                        if (texture == null)
                        {
                            continue;
                        }

                        _ = textures.Add(texture);
                        string texturePath = AssetDatabase.GetAssetPath(texture);
                        if (!WithinRoot(texturePath, result.productRootReference))
                        {
                            findings.Add(Finding("UNITY_REIMPORT_TEXTURE_OUTSIDE_PRODUCT", texturePath));
                        }
                    }
                }
            }

            result.materialCount = materials.Count;
            result.textureCount = textures.Count;
            if (materials.Count == 0 || textures.Count == 0)
            {
                findings.Add(Finding("UNITY_REIMPORT_MATERIAL_REFERENCES_EMPTY", result.prefabReference));
            }

            if (!controller.AutoFrame() || !controller.TryGetProductBounds(out Bounds bounds))
            {
                findings.Add(Finding("UNITY_REIMPORT_CAMERA_FRAME_FAILED", result.sceneReference));
                return;
            }

            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(controller.PreviewCamera);
            if (!GeometryUtility.TestPlanesAABB(frustum, bounds))
            {
                findings.Add(Finding("UNITY_REIMPORT_PREFAB_NOT_RENDERABLE", result.prefabReference));
                return;
            }

            var renderTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = controller.PreviewCamera.targetTexture;
            try
            {
                if (!renderTexture.Create())
                {
                    findings.Add(Finding("UNITY_REIMPORT_RENDER_TARGET_FAILED", result.sceneReference));
                    return;
                }
                controller.PreviewCamera.targetTexture = renderTexture;
                controller.PreviewCamera.Render();
            }
            finally
            {
                controller.PreviewCamera.targetTexture = previous;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            if (silverwing)
            {
                ValidateSilverwingAnimated(result, findings, controller);
            }
        }

        private static void ValidateSilverwingAnimated(
            UnityCleanReimportResult result,
            List<UnityCleanReimportFinding> findings,
            PackageBuilderPreviewController preview)
        {
            string[] modelReferences = AssetDatabase.FindAssets(
                    "t:Model", new[] { result.productRootReference + "/Source" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(value => value.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var importer = modelReferences.Length == 1
                ? AssetImporter.GetAtPath(modelReferences[0]) as ModelImporter
                : null;
            if (importer == null || importer.animationType != ModelImporterAnimationType.Generic ||
                !importer.importAnimation)
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_SILVERWING_IMPORT_POLICY_INVALID", result.productRootReference));
            }

            AnimationClip[] clips = AssetDatabase.FindAssets(
                    "t:AnimationClip", new[] { result.productRootReference + "/Animations" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
                .Where(value => value != null)
                .OrderBy(value => value.name, StringComparer.Ordinal)
                .ToArray();
            result.animationClipCount = clips.Length;
            result.clipNames = clips.Select(value => value.name).ToArray();
            AnimationClip shot = clips.Length == 1 ? clips[0] : null;
            if (shot == null || shot.name != "A_SilverwingTalonbow_Bow_Shot" ||
                shot.isLooping || Mathf.Abs(shot.frameRate - 30f) > 0.001f)
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_SILVERWING_CLIP_INVALID", result.productRootReference));
            }

            AnimatorController[] controllers = AssetDatabase.FindAssets(
                    "t:AnimatorController", new[] { result.productRootReference + "/Controllers" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AnimatorController>)
                .Where(value => value != null)
                .ToArray();
            if (controllers.Length != 1 || controllers[0].layers.Length != 1 ||
                controllers[0].layers[0].stateMachine.states.Length != 1 ||
                controllers[0].layers[0].stateMachine.defaultState == null ||
                controllers[0].layers[0].stateMachine.defaultState.motion != shot)
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_SILVERWING_CONTROLLER_INVALID", result.productRootReference));
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.prefabReference);
            if (prefab == null)
            {
                findings.Add(Finding("UNITY_REIMPORT_PREFAB_MISSING", result.prefabReference));
                return;
            }
            UnitySkinSkeletonReport skin = UnitySkinSkeletonValidator.Validate(prefab, 4);
            SkinnedMeshRenderer[] skinnedRenderers = prefab
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .OrderBy(value => value.name, StringComparer.Ordinal)
                .ToArray();
            result.rendererCount = skin.RendererCount;
            result.skinnedRendererCount = skinnedRenderers.Length;
            result.boneCount = skin.UniqueBoneCount;
            result.animatorCount = prefab.GetComponentsInChildren<Animator>(true).Length;
            string[] expectedRenderers =
            {
                "P_SilverwingTalonbow_Body",
                "P_SilverwingTalonbow_String",
            };
            if (!skin.IsValid || result.rendererCount != 2 || result.skinnedRendererCount != 2 ||
                result.boneCount != 38 || result.animatorCount != 1 ||
                !skinnedRenderers.Select(value => value.name)
                    .SequenceEqual(expectedRenderers, StringComparer.Ordinal))
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_SILVERWING_PREFAB_INVALID", result.prefabReference));
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                result.productRootReference + "/Materials/M_SilverwingTalonbow_URP.mat");
            if (material == null || material.GetTexture("_BaseMap") == null ||
                material.GetTexture("_BumpMap") == null ||
                material.GetTexture("_MetallicGlossMap") == null ||
                material.GetTexture("_EmissionMap") == null ||
                skinnedRenderers.Any(value => value.sharedMaterials.Length == 0 ||
                    value.sharedMaterials.Any(rendererMaterial => rendererMaterial != material)))
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_SILVERWING_TEXTURED_MATERIAL_INVALID", result.prefabReference));
            }

            if (shot != null)
            {
                UnitySynchronizedRendererMotionReport motion =
                    UnityAnimationMotionValidator.ValidateSynchronizedRenderers(
                        AssetDatabase.GetAssetPath(shot), result.prefabReference, expectedRenderers);
                result.synchronizedRendererMotionVerified = motion.IsValid;
                if (!motion.IsValid)
                {
                    findings.Add(Finding(
                        "UNITY_REIMPORT_SILVERWING_SYNCHRONIZED_MOTION_INVALID",
                        string.Join(",", motion.Findings)));
                }
            }

            PackageBuilderAnimationTransport transport = preview.AnimationTransport;
            if (transport == null || !transport.Available || transport.LoopEnabled ||
                !transport.ClipNames.SequenceEqual(
                    new[] { "A_SilverwingTalonbow_Bow_Shot" }, StringComparer.Ordinal))
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_SILVERWING_TRANSPORT_INVALID", result.sceneReference));
            }
        }

        private static void ValidateRiggedNoAnimation(
            UnityCleanReimportResult result,
            List<UnityCleanReimportFinding> findings)
        {
            if (!AssetDatabase.IsValidFolder(result.productRootReference))
            {
                findings.Add(Finding("UNITY_REIMPORT_PRODUCT_ROOT_MISSING", result.productRootReference));
                return;
            }

            if (AssetDatabase.IsValidFolder(result.productRootReference + "/Animations") ||
                AssetDatabase.IsValidFolder(result.productRootReference + "/Controllers"))
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_RIG_ANIMATION_FOLDER_PRESENT", result.productRootReference));
            }

            string[] clipGuids = AssetDatabase.FindAssets(
                "t:AnimationClip", new[] { result.productRootReference });
            string[] controllerGuids = AssetDatabase.FindAssets(
                "t:AnimatorController", new[] { result.productRootReference });
            result.animationClipCount = clipGuids.Length;
            if (clipGuids.Length != 0 || controllerGuids.Length != 0)
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_RIG_ANIMATION_ASSET_PRESENT", result.productRootReference));
            }

            string[] modelReferences = AssetDatabase.FindAssets(
                    "t:Model", new[] { result.productRootReference + "/Source" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(value => value.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (modelReferences.Length != 1)
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_RIG_SOURCE_COUNT_INVALID", result.productRootReference));
                return;
            }

            var importer = AssetImporter.GetAtPath(modelReferences[0]) as ModelImporter;
            if (importer == null || importer.animationType != ModelImporterAnimationType.Generic ||
                importer.importAnimation)
            {
                findings.Add(Finding("UNITY_REIMPORT_RIG_IMPORT_POLICY_INVALID", modelReferences[0]));
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.prefabReference);
            if (prefab == null)
            {
                findings.Add(Finding("UNITY_REIMPORT_PREFAB_MISSING", result.prefabReference));
                return;
            }

            result.animatorCount = prefab.GetComponentsInChildren<Animator>(true).Length;
            result.skinnedRendererCount =
                prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            UnitySkinSkeletonReport report = UnitySkinSkeletonValidator.Validate(prefab, 4);
            result.rendererCount = report.RendererCount;
            result.boneCount = report.UniqueBoneCount;
            if (!report.IsValid || result.skinnedRendererCount != 1 || report.RendererCount != 1 ||
                report.UniqueBoneCount != 2 || result.animatorCount != 0 ||
                prefab.GetComponentsInChildren<Animation>(true).Length != 0 ||
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) != 0)
            {
                findings.Add(Finding("UNITY_REIMPORT_RIG_PREFAB_INVALID", result.prefabReference));
            }

            string[] metadataReferences = AssetDatabase.FindAssets(
                    "t:TextAsset", new[] { result.productRootReference + "/Documentation" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(value => value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            TextAsset metadata = metadataReferences.Length == 1
                ? AssetDatabase.LoadAssetAtPath<TextAsset>(metadataReferences[0])
                : null;
            if (metadata == null || !metadata.text.Contains("\"hasAnimationClips\": false"))
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_RIG_METADATA_INVALID", result.productRootReference));
            }
        }

        private static void ValidateMultiClipAnimated(
            UnityCleanReimportResult result,
            List<UnityCleanReimportFinding> findings)
        {
            if (!AssetDatabase.IsValidFolder(result.productRootReference))
            {
                findings.Add(Finding("UNITY_REIMPORT_PRODUCT_ROOT_MISSING", result.productRootReference));
                return;
            }

            string[] modelReferences = AssetDatabase.FindAssets(
                    "t:Model", new[] { result.productRootReference + "/Source" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(value => value.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var importer = modelReferences.Length == 1
                ? AssetImporter.GetAtPath(modelReferences[0]) as ModelImporter
                : null;
            if (importer == null || importer.animationType != ModelImporterAnimationType.Generic ||
                !importer.importAnimation || importer.clipAnimations.Length != 2 ||
                importer.clipAnimations.Count(value => value.loopTime) != 1 ||
                importer.clipAnimations.Count(value => !value.loopTime) != 1)
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_MULTI_CLIP_IMPORT_POLICY_INVALID", result.productRootReference));
            }

            AnimationClip[] clips = AssetDatabase.FindAssets(
                    "t:AnimationClip", new[] { result.productRootReference + "/Animations" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
                .Where(value => value != null)
                .OrderBy(value => value.name, StringComparer.Ordinal)
                .ToArray();
            result.animationClipCount = clips.Length;
            result.clipNames = clips.Select(value => value.name).ToArray();
            result.loopingClipCount = clips.Count(value => value.isLooping);
            result.nonLoopingClipCount = clips.Count(value => !value.isLooping);
            string[] expectedNames =
            {
                "A_MultiClipProp_Attack",
                "A_MultiClipProp_BendLoop",
            };
            if (!result.clipNames.SequenceEqual(expectedNames, StringComparer.Ordinal) ||
                result.loopingClipCount != 1 || result.nonLoopingClipCount != 1 ||
                clips.Any(value => Mathf.Abs(value.frameRate - 30f) > 0.001f) ||
                clips.FirstOrDefault(value => value.name == expectedNames[0])?.isLooping != false ||
                clips.FirstOrDefault(value => value.name == expectedNames[1])?.isLooping != true)
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_MULTI_CLIP_INVENTORY_INVALID", result.productRootReference));
            }

            AnimatorController[] controllers = AssetDatabase.FindAssets(
                    "t:AnimatorController", new[] { result.productRootReference + "/Controllers" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AnimatorController>)
                .Where(value => value != null)
                .ToArray();
            AnimatorController controller = controllers.Length == 1 ? controllers[0] : null;
            result.controllerStateCount = controller == null || controller.layers.Length != 1
                ? 0
                : controller.layers[0].stateMachine.states.Length;
            AnimationClip attack = clips.FirstOrDefault(value => value.name == expectedNames[0]);
            if (controller == null || result.controllerStateCount != 2 ||
                controller.layers[0].stateMachine.defaultState == null ||
                controller.layers[0].stateMachine.defaultState.motion != attack ||
                controller.layers[0].stateMachine.states.Any(value => value.state.motion == null) ||
                !controller.layers[0].stateMachine.states.Select(value => value.state.motion.name)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(expectedNames, StringComparer.Ordinal))
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_MULTI_CLIP_CONTROLLER_INVALID", result.productRootReference));
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.prefabReference);
            if (prefab == null)
            {
                findings.Add(Finding("UNITY_REIMPORT_PREFAB_MISSING", result.prefabReference));
                return;
            }
            UnitySkinSkeletonReport skin = UnitySkinSkeletonValidator.Validate(prefab, 4);
            Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
            result.rendererCount = skin.RendererCount;
            result.skinnedRendererCount =
                prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            result.boneCount = skin.UniqueBoneCount;
            result.animatorCount = animators.Length;
            if (!skin.IsValid || result.rendererCount != 1 || result.skinnedRendererCount != 1 ||
                result.boneCount != 2 || result.animatorCount != 1 ||
                animators[0].runtimeAnimatorController != controller ||
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) != 0)
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_MULTI_CLIP_PREFAB_INVALID", result.prefabReference));
            }

            UnityAnimationMotionReport motion = UnityAnimationMotionValidator.Validate(
                result.productRootReference + "/Animations",
                result.prefabReference,
                new[]
                {
                    new UnityAnimationClipExpectation
                    {
                        Name = expectedNames[0],
                        DurationSeconds = 20f / 30f,
                        FramesPerSecond = 30f,
                        Looping = false,
                    },
                    new UnityAnimationClipExpectation
                    {
                        Name = expectedNames[1],
                        DurationSeconds = 20f / 30f,
                        FramesPerSecond = 30f,
                        Looping = true,
                    },
                });
            result.animationMotionVerified = motion.IsValid && motion.BoneMotionVerified &&
                motion.RendererMotionVerified && motion.NonLoopingCompletionVerified;
            if (!result.animationMotionVerified)
            {
                findings.Add(Finding(
                    "UNITY_REIMPORT_MULTI_CLIP_MOTION_INVALID", string.Join(",", motion.Findings)));
            }
        }

        private static void ValidateGenericTopologyMatrix(
            UnityCleanReimportResult result,
            List<UnityCleanReimportFinding> findings)
        {
            UnityGenericTopologyMatrixReport matrix =
                UnityGenericTopologyMatrixValidator.Validate(result.productRootReference);
            result.topologyCaseCount = matrix.ValidCaseCount;
            result.animationClipCount = matrix.ImportedClipCount;
            result.controllerStateCount = matrix.ControllerStateCount;
            result.genericTopologyVerified = matrix.GenericImportVerified;
            result.topologyHierarchyVerified = matrix.HierarchyVerified;
            result.topologySkinWeightsVerified = matrix.SkinWeightsVerified;
            result.animationMotionVerified = matrix.MotionVerified;
            result.topologyNegativeFindingsVerified = matrix.NegativeFindingsVerified;
            result.topologyCategories = matrix.Categories;
            foreach (string finding in matrix.Findings)
            {
                findings.Add(Finding("UNITY_REIMPORT_TOPOLOGY_MATRIX_INVALID", finding));
            }
        }

        private static bool WithinRoot(string assetReference, string root) =>
            string.Equals(assetReference, root, StringComparison.Ordinal) ||
            assetReference.StartsWith(root + "/", StringComparison.Ordinal);

        private static UnityCleanReimportFinding Finding(string code, string reference) => new()
        {
            code = code,
            assetReference = reference ?? string.Empty,
        };

        private static string EnvironmentValue(string name, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static void WriteResult(string path, UnityCleanReimportResult result)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            {
                throw new InvalidOperationException("The clean-reimport result path is invalid.");
            }

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("The clean-reimport result directory is missing.");
            }

            _ = Directory.CreateDirectory(directory);
            string temporary = path + ".partial";
            File.WriteAllText(temporary, JsonUtility.ToJson(result, true));
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temporary, path);
        }
    }
}
