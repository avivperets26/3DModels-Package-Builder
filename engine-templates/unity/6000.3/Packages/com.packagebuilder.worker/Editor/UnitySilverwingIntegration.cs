using System;
using System.IO;
using System.Linq;
using PackageBuilder.Preview;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>
    /// Builds the private-at-runtime Silverwing fixture through the production Unity adapters and
    /// exports only the generated customer product. Private source paths never enter package data.
    /// </summary>
    public static class UnitySilverwingIntegration
    {
        private const string ProductRoot = "Assets/PBSilverwingTests";
        private const string AssetId = "SilverwingTalonbow";
        private const string SourceReference = ProductRoot + "/Source/SilverwingTalonbow.fbx";
        private const string ClipReference =
            ProductRoot + "/Animations/A_SilverwingTalonbow_Bow_Shot.anim";
        private const string ControllerReference =
            ProductRoot + "/Controllers/AC_SilverwingTalonbow.controller";
        private const string PrefabReference =
            ProductRoot + "/Prefabs/P_SilverwingTalonbow.prefab";
        private const string SceneReference =
            ProductRoot + "/Scenes/S_SilverwingTalonbow_Overview.unity";
        private const string MaterialReference =
            ProductRoot + "/Materials/M_SilverwingTalonbow_URP.mat";

        /// <summary>Runs the isolated real-engine fixture build and exits Unity with a stable code.</summary>
        public static void Run()
        {
            try
            {
                BuildAndValidate();
                Debug.Log("PACKAGEBUILDER_UNITY_SILVERWING_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("PACKAGEBUILDER_UNITY_SILVERWING_FAIL:" + exception.Message);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildAndValidate()
        {
            Require(AssetDatabase.IsValidFolder(ProductRoot),
                "The isolated Silverwing product root is missing.");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(SourceReference) != null,
                "The normalized Silverwing FBX is missing.");
            string diagnostic;

            string albedo = ProductRoot + "/Textures/T_SilverwingTalonbow_Albedo.png";
            string emission = ProductRoot + "/Textures/T_SilverwingTalonbow_Emission.png";
            string metallic = ProductRoot + "/Textures/T_SilverwingTalonbow_Metallic.png";
            string normal = ProductRoot + "/Textures/T_SilverwingTalonbow_Normal.png";
            string roughness = ProductRoot + "/Textures/T_SilverwingTalonbow_Roughness.png";
            ApplyTexturePolicy(albedo, "albedo");
            ApplyTexturePolicy(emission, "emission");
            ApplyTexturePolicy(metallic, "metallic");
            ApplyTexturePolicy(normal, "normal");
            ApplyTexturePolicy(roughness, "roughness");
            string packed = ProductRoot +
                "/Textures/T_SilverwingTalonbow_MetallicSmoothness.png";
            Require(UnityMetallicSmoothnessPacker.TryPack(
                metallic, roughness, packed, out diagnostic), diagnostic);

            Material material;
            Require(UnityUrpLitMaterialCompiler.TryCompile(
                new UnityUrpLitMaterialRequest
                {
                    OutputAssetReference = MaterialReference,
                    BaseMapAssetReference = albedo,
                    NormalMapAssetReference = normal,
                    MetallicSmoothnessAssetReference = packed,
                    EmissionMapAssetReference = emission,
                    BaseColour = Color.white,
                    EmissionColour = Color.white,
                    MetallicFactor = 1f,
                    RoughnessFactor = 0.5f,
                    NormalScale = 1f,
                    Opacity = 1f,
                    SurfaceMode = "opaque",
                    DoubleSided = false,
                },
                out material,
                out diagnostic), diagnostic);

            var importer = AssetImporter.GetAtPath(SourceReference) as ModelImporter;
            Require(importer != null, "The Silverwing ModelImporter is missing.");
            string rootPath = (importer.transformPaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrEmpty(path))
                .OrderBy(path => path.Count(character => character == '/'))
                .ThenBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            Require(!string.IsNullOrEmpty(rootPath), "The Silverwing rig has no declared root.");
            UnityRigImportResult rigResult;
            Require(UnityRigModelImporterPolicy.TryApply(
                new UnityRigImportRequest
                {
                    ModelAssetReference = SourceReference,
                    Mode = UnityRigImportMode.Generic,
                    RootNodePath = rootPath,
                    PreserveHierarchy = true,
                    OptimizeGameObjects = false,
                    ExposedTransformPaths = Array.Empty<string>(),
                },
                out rigResult,
                out diagnostic), diagnostic);
            Require(rigResult.AppliedMode == UnityRigImportMode.Generic && !rigResult.AvatarIsHuman,
                "Silverwing must remain a Generic non-Humanoid rig.");

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceReference);
            UnitySkinSkeletonReport sourceSkin = UnitySkinSkeletonValidator.Validate(source, 4);
            Require(sourceSkin.IsValid && sourceSkin.RendererCount == 2 &&
                sourceSkin.UniqueBoneCount == 38,
                "The normalized Silverwing skin or skeleton inventory is incorrect.");

            UnityAnimationClipPlan[] discovered;
            Require(UnityAnimationClipImporter.TryDiscoverSourceActions(
                SourceReference, out discovered, out diagnostic), diagnostic);
            Require(discovered.Length == 1 && Approximately(discovered[0].SampleRate, 30f),
                "Silverwing must expose one 30 FPS source take.");
            var shotPlan = new UnityAnimationClipPlan
            {
                ClipId = "Bow_Shot",
                SourceTakeName = discovered[0].SourceTakeName,
                FirstFrame = discovered[0].FirstFrame,
                LastFrame = discovered[0].LastFrame,
                SampleRate = discovered[0].SampleRate,
                LoopTime = false,
                RootMotionPolicy = UnityRootMotionPolicy.BakeIntoPose,
            };
            UnityAnimationClipImportResult clipResult;
            Require(UnityAnimationClipImporter.TryImportAndExtract(
                new UnityAnimationClipImportRequest
                {
                    AssetId = AssetId,
                    SourceModelReference = SourceReference,
                    OutputAnimationFolderReference = ProductRoot + "/Animations",
                    CompressionPolicy = UnityAnimationCompressionPolicy.Optimal,
                    Clips = new[] { shotPlan },
                },
                out clipResult,
                out diagnostic), diagnostic);
            Require(clipResult.OutputAssetReferences.SequenceEqual(
                    new[] { ClipReference }, StringComparer.Ordinal),
                "Silverwing extracted an unexpected clip inventory.");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipReference);
            Require(clip != null && !clip.isLooping && Approximately(clip.frameRate, 30f),
                "Bow_Shot must be present, sampled at 30 FPS, and non-looping.");

            AnimatorController controller;
            Require(UnityAnimatorControllerGenerator.TryCreate(
                new UnityAnimatorControllerRequest
                {
                    AssetId = AssetId,
                    OutputControllerReference = ControllerReference,
                    DefaultClipReference = ClipReference,
                    ClipReferences = new[] { ClipReference },
                },
                out controller,
                out diagnostic), diagnostic);
            Require(controller.layers.Length == 1 &&
                controller.layers[0].stateMachine.states.Length == 1,
                "Silverwing must generate exactly one controller state.");

            GameObject prefab;
            UnitySkinSkeletonReport prefabSkin;
            Require(UnityAnimatedPrefabGenerator.TryCreate(
                new UnityAnimatedPrefabRequest
                {
                    AssetId = AssetId,
                    SourceModelReference = SourceReference,
                    AnimatorControllerReference = ControllerReference,
                    OutputPrefabReference = PrefabReference,
                    AllowedMaximumInfluences = 4,
                    ApplyRootMotion = false,
                },
                out prefab,
                out prefabSkin,
                out diagnostic), diagnostic);
            AssignMaterial(PrefabReference, material);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabReference);
            Require(prefabSkin.IsValid &&
                prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 2 &&
                prefab.GetComponentsInChildren<Animator>(true).Length == 1,
                "Silverwing prefab did not preserve both skinned renderers and one Animator.");

            UnitySynchronizedRendererMotionReport synchronizedMotion =
                UnityAnimationMotionValidator.ValidateSynchronizedRenderers(
                    ClipReference,
                    PrefabReference,
                    new[]
                    {
                        "P_SilverwingTalonbow_Body",
                        "P_SilverwingTalonbow_String",
                    });
            Require(synchronizedMotion.IsValid,
                "Bow body and string did not deform together: " +
                string.Join(",", synchronizedMotion.Findings) + "; moving=" +
                string.Join(",", synchronizedMotion.MovingRendererNames));

            string templateRoot = "Assets/PBSilverwingOverviewTemplate";
            Require(!AssetDatabase.IsValidFolder(templateRoot) &&
                !string.IsNullOrEmpty(AssetDatabase.CreateFolder(
                    "Assets", "PBSilverwingOverviewTemplate")),
                "The Silverwing overview-template workspace could not be created.");
            string templateScene = templateRoot + "/OverviewTemplate.unity";
            Scene unusedTemplate;
            Require(UnityOverviewSceneTemplateBuilder.TryCreate(
                new UnityOverviewSceneTemplateRequest { OutputSceneReference = templateScene },
                out unusedTemplate,
                out diagnostic), diagnostic);
            Scene scene;
            var composition = new UnityOverviewSceneCompositionRequest
            {
                AssetId = AssetId,
                TemplateSceneReference = templateScene,
                ProductPrefabReference = PrefabReference,
                PreviewControllerScriptReference =
                    ProductRoot + "/Scripts/PackageBuilderPreviewController.cs",
                OutputBackgroundMaterialReference =
                    ProductRoot + "/Materials/M_SilverwingTalonbow_OverviewBackground.mat",
                OutputBackgroundTextureReference =
                    ProductRoot + "/Textures/T_SilverwingTalonbow_OverviewBackground.png",
                OutputSceneReference = SceneReference,
            };
            Require(UnityOverviewSceneComposer.TryCompose(
                composition, out scene, out diagnostic), diagnostic);
            Require(UnityOverviewSceneComposer.VerifyComposition(
                scene, composition, out diagnostic), diagnostic);
            GameObject overviewRoot = UnityOverviewSceneTemplateBuilder.FindUniqueRoot(
                scene, UnityOverviewSceneTemplateBuilder.OverviewRootName);
            var preview = overviewRoot == null
                ? null
                : overviewRoot.GetComponent<PackageBuilderPreviewController>();
            Require(preview != null && preview.AutoFrame() &&
                preview.AnimationTransport != null && preview.AnimationTransport.Available &&
                preview.AnimationTransport.ClipNames.SequenceEqual(
                    new[] { "A_SilverwingTalonbow_Bow_Shot" }, StringComparer.Ordinal) &&
                !preview.AnimationTransport.LoopEnabled,
                "The Silverwing overview transport or automatic framing is incorrect.");

            string documentationReference = ProductRoot + "/Documentation/README.txt";
            File.WriteAllText(ToPhysicalPath(documentationReference),
                "Silverwing Talonbow\nGeneric rig with one non-looping Bow_Shot clip.\n");
            AssetDatabase.ImportAsset(
                documentationReference, ImportAssetOptions.ForceSynchronousImport);

            string packagePath = Environment.GetEnvironmentVariable(
                "PACKAGEBUILDER_UNITY_SILVERWING_PACKAGE_OUTPUT");
            string manifestPath = Environment.GetEnvironmentVariable(
                "PACKAGEBUILDER_UNITY_SILVERWING_PACKAGE_MANIFEST");
            Require(!string.IsNullOrWhiteSpace(packagePath) &&
                !string.IsNullOrWhiteSpace(manifestPath),
                "The contained Silverwing package evidence paths are missing.");
            var exportRequest = new UnityPackageExportRequest
            {
                ProductRootReference = ProductRoot,
                OutputPackagePath = packagePath,
            };
            UnityPackageExportPlan exportPlan;
            Require(UnityPackageExporter.TryCreatePlan(
                exportRequest, out exportPlan, out diagnostic), diagnostic);
            UnityPackageValidationReport validation = UnityPackageValidator.Validate(
                new UnityPackageValidationRequest
                {
                    ProductRootReference = ProductRoot,
                    ExpectedAssetReferences = exportPlan.AssetReferences,
                    PackageLogs = Array.Empty<UnityPackageLogEntry>(),
                });
            Require(validation.IsSuccessful,
                "Silverwing package validation failed: " + string.Join(",",
                    validation.Findings.Select(value => value.Code)));
            File.WriteAllLines(manifestPath, exportPlan.AssetReferences);
            Require(UnityPackageExporter.TryExport(
                exportRequest, out exportPlan, out diagnostic), diagnostic);
        }

        private static void ApplyTexturePolicy(string assetReference, string role)
        {
            string diagnostic;
            Require(UnityTextureImporterPolicy.TryApply(
                assetReference, role, out diagnostic), diagnostic);
        }

        private static void AssignMaterial(string prefabReference, Material material)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabReference);
            try
            {
                Renderer[] renderers = contents.GetComponentsInChildren<Renderer>(true);
                Require(renderers.Length == 2, "Silverwing prefab renderer inventory changed.");
                foreach (Renderer renderer in renderers)
                {
                    int count = Math.Max(1, renderer.sharedMaterials.Length);
                    renderer.sharedMaterials = Enumerable.Repeat(material, count).ToArray();
                }
                PrefabUtility.SaveAsPrefabAsset(contents, prefabReference);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(prefabReference, ImportAssetOptions.ForceSynchronousImport);
        }

        private static string ToPhysicalPath(string assetReference) => Path.Combine(
            Application.dataPath,
            assetReference.Substring("Assets/".Length)
                .Replace('/', Path.DirectorySeparatorChar));

        private static bool Approximately(float first, float second) =>
            Mathf.Abs(first - second) <= 0.001f;

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
