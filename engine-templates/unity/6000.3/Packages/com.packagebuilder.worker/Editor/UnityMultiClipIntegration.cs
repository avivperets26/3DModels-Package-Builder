using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>
    /// Builds the redistribution-safe PB-0713 product through the maintained animation adapters
    /// and exports an exact two-clip package for isolated clean-reimport validation.
    /// </summary>
    public static class UnityMultiClipIntegration
    {
        private const string ProductRoot = "Assets/PBMultiClipTests";
        private const string AssetId = "MultiClipProp";
        private const string SourceReference = ProductRoot + "/Source/MultiClipProp.fbx";
        private const string AttackClipReference =
            ProductRoot + "/Animations/A_MultiClipProp_Attack.anim";
        private const string LoopClipReference =
            ProductRoot + "/Animations/A_MultiClipProp_BendLoop.anim";
        private const string ControllerReference =
            ProductRoot + "/Controllers/AC_MultiClipProp.controller";
        private const string PrefabReference = ProductRoot + "/Prefabs/P_MultiClipProp.prefab";

        /// <summary>Runs the isolated fixture build and exits Unity with a stable process result.</summary>
        public static void Run()
        {
            try
            {
                BuildAndValidate();
                Debug.Log("PACKAGEBUILDER_UNITY_MULTI_CLIP_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("PACKAGEBUILDER_UNITY_MULTI_CLIP_FAIL:" + exception.Message);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildAndValidate()
        {
            Require(AssetDatabase.IsValidFolder(ProductRoot),
                "The isolated multi-clip product root is missing.");
            var importer = AssetImporter.GetAtPath(SourceReference) as ModelImporter;
            Require(importer != null, "The procedural animated FBX importer is missing.");
            string rootPath = (importer.transformPaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrEmpty(path))
                .OrderBy(path => path.Count(character => character == '/'))
                .ThenBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            Require(!string.IsNullOrEmpty(rootPath),
                "The procedural animated FBX has no declared root.");

            string diagnostic;
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
                "The multi-clip fixture must remain Generic and non-Humanoid.");

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceReference);
            UnitySkinSkeletonReport sourceSkin = UnitySkinSkeletonValidator.Validate(source, 4);
            Require(sourceSkin.IsValid && sourceSkin.RendererCount == 1 &&
                sourceSkin.UniqueBoneCount == 2,
                "The procedural multi-clip skin or skeleton inventory is incorrect.");

            UnityAnimationClipPlan[] discovered;
            Require(UnityAnimationClipImporter.TryDiscoverSourceActions(
                SourceReference, out discovered, out diagnostic), diagnostic);
            Require(discovered.Length == 1 && Approximately(discovered[0].SampleRate, 30f),
                "The multi-clip fixture must expose one 30 FPS source take.");
            var attackPlan = new UnityAnimationClipPlan
            {
                ClipId = "Attack",
                SourceTakeName = discovered[0].SourceTakeName,
                FirstFrame = discovered[0].FirstFrame,
                LastFrame = discovered[0].LastFrame,
                SampleRate = discovered[0].SampleRate,
                LoopTime = false,
                RootMotionPolicy = UnityRootMotionPolicy.BakeIntoPose,
            };
            var loopPlan = new UnityAnimationClipPlan
            {
                ClipId = "BendLoop",
                SourceTakeName = discovered[0].SourceTakeName,
                FirstFrame = discovered[0].FirstFrame,
                LastFrame = discovered[0].LastFrame,
                SampleRate = discovered[0].SampleRate,
                LoopTime = true,
                RootMotionPolicy = UnityRootMotionPolicy.Preserve,
            };
            UnityAnimationClipImportResult clipResult;
            Require(UnityAnimationClipImporter.TryImportAndExtract(
                new UnityAnimationClipImportRequest
                {
                    AssetId = AssetId,
                    SourceModelReference = SourceReference,
                    OutputAnimationFolderReference = ProductRoot + "/Animations",
                    CompressionPolicy = UnityAnimationCompressionPolicy.Optimal,
                    Clips = new[] { loopPlan, attackPlan },
                },
                out clipResult,
                out diagnostic), diagnostic);
            Require(clipResult.OutputAssetReferences.SequenceEqual(
                    new[] { AttackClipReference, LoopClipReference }, StringComparer.Ordinal),
                "The extracted multi-clip inventory or ordering is incorrect.");

            AnimationClip attack = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClipReference);
            AnimationClip loop = AssetDatabase.LoadAssetAtPath<AnimationClip>(LoopClipReference);
            Require(attack != null && loop != null && !attack.isLooping && loop.isLooping &&
                Approximately(attack.frameRate, 30f) && Approximately(loop.frameRate, 30f),
                "The extracted clips do not preserve their mixed loop settings.");

            AnimatorController controller;
            Require(UnityAnimatorControllerGenerator.TryCreate(
                new UnityAnimatorControllerRequest
                {
                    AssetId = AssetId,
                    OutputControllerReference = ControllerReference,
                    DefaultClipReference = AttackClipReference,
                    ClipReferences = clipResult.OutputAssetReferences,
                },
                out controller,
                out diagnostic), diagnostic);
            Require(controller.layers.Length == 1 &&
                controller.layers[0].stateMachine.states.Length == 2 &&
                controller.layers[0].stateMachine.defaultState.motion == attack &&
                controller.layers[0].stateMachine.states
                    .Select(value => value.state.motion)
                    .OrderBy(value => value.name, StringComparer.Ordinal)
                    .SequenceEqual(new Motion[] { attack, loop }
                        .OrderBy(value => value.name, StringComparer.Ordinal)),
                "The multi-clip controller state or default-motion plan is incorrect.");

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
            Require(prefabSkin.IsValid &&
                prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 1 &&
                prefab.GetComponentsInChildren<Animator>(true).Length == 1,
                "The multi-clip prefab did not preserve one skin and one Animator.");

            float duration = (discovered[0].LastFrame - discovered[0].FirstFrame) /
                discovered[0].SampleRate;
            UnityAnimationMotionReport motion = UnityAnimationMotionValidator.Validate(
                ProductRoot + "/Animations",
                PrefabReference,
                new[]
                {
                    new UnityAnimationClipExpectation
                    {
                        Name = "A_MultiClipProp_Attack",
                        DurationSeconds = duration,
                        FramesPerSecond = 30f,
                        Looping = false,
                    },
                    new UnityAnimationClipExpectation
                    {
                        Name = "A_MultiClipProp_BendLoop",
                        DurationSeconds = duration,
                        FramesPerSecond = 30f,
                        Looping = true,
                    },
                });
            Require(motion.IsValid && motion.BoneMotionVerified && motion.RendererMotionVerified &&
                motion.NonLoopingCompletionVerified,
                "Multi-clip movement validation failed: " + string.Join(",", motion.Findings));

            string documentationReference = ProductRoot + "/Documentation/README.txt";
            File.WriteAllText(ToPhysicalPath(documentationReference),
                "MultiClipProp\nAttack is one-shot; BendLoop loops.\n");
            AssetDatabase.ImportAsset(
                documentationReference, ImportAssetOptions.ForceSynchronousImport);

            string packagePath = Environment.GetEnvironmentVariable(
                "PACKAGEBUILDER_UNITY_MULTI_CLIP_PACKAGE_OUTPUT");
            string manifestPath = Environment.GetEnvironmentVariable(
                "PACKAGEBUILDER_UNITY_MULTI_CLIP_PACKAGE_MANIFEST");
            Require(!string.IsNullOrWhiteSpace(packagePath) &&
                !string.IsNullOrWhiteSpace(manifestPath),
                "The contained multi-clip package evidence paths are missing.");
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
                "Multi-clip package validation failed: " + string.Join(",",
                    validation.Findings.Select(value => value.Code)));
            File.WriteAllLines(manifestPath, exportPlan.AssetReferences);
            Require(UnityPackageExporter.TryExport(
                exportRequest, out exportPlan, out diagnostic), diagnostic);
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
