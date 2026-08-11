using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Describes one expected bone and its parent in the engine-neutral topology manifest.</summary>
    [Serializable]
    internal sealed class UnityGenericTopologyBoneExpectation
    {
        public string name;

        public string parent;
    }

    /// <summary>Describes one valid animated Generic-rig case shared with later engine adapters.</summary>
    [Serializable]
    internal sealed class UnityGenericTopologyCaseExpectation
    {
        public string id;

        public string category;

        public string assetId;

        public string modelFile;

        public string rendererName;

        public string rootBone;

        public string movingBone;

        public string clipId;

        public int firstFrame;

        public int lastFrame;

        public float frameRate;

        public UnityGenericTopologyBoneExpectation[] bones =
            Array.Empty<UnityGenericTopologyBoneExpectation>();
    }

    /// <summary>Describes one intentionally invalid source and its required stable finding.</summary>
    [Serializable]
    internal sealed class UnityGenericTopologyInvalidExpectation
    {
        public string id;

        public string modelFile;

        public string expectedFinding;
    }

    /// <summary>Owns the reusable versioned topology matrix produced by the Blender fixture generator.</summary>
    [Serializable]
    internal sealed class UnityGenericTopologyMatrixManifest
    {
        public int schemaVersion;

        public UnityGenericTopologyCaseExpectation[] cases =
            Array.Empty<UnityGenericTopologyCaseExpectation>();

        public UnityGenericTopologyInvalidExpectation[] invalidCases =
            Array.Empty<UnityGenericTopologyInvalidExpectation>();
    }

    /// <summary>Summarizes exact positive and negative conformance for one imported topology matrix.</summary>
    internal sealed class UnityGenericTopologyMatrixReport
    {
        internal int ValidCaseCount { get; set; }

        internal int ImportedClipCount { get; set; }

        internal int ControllerStateCount { get; set; }

        internal bool GenericImportVerified { get; set; }

        internal bool HierarchyVerified { get; set; }

        internal bool SkinWeightsVerified { get; set; }

        internal bool MotionVerified { get; set; }

        internal bool NegativeFindingsVerified { get; set; }

        internal string[] Categories { get; set; } = Array.Empty<string>();

        internal string[] Findings { get; set; } = Array.Empty<string>();

        internal bool IsValid => Findings.Length == 0;
    }

    /// <summary>
    /// Validates topology-neutral Generic import, hierarchy, skin, animation, and negative cases.
    /// The same manifest and validation entrypoint run before export and after clean reimport.
    /// </summary>
    internal static class UnityGenericTopologyMatrixValidator
    {
        internal const string ManifestReference =
            "Assets/PBTopologyMatrixTests/Documentation/topology-matrix.txt";

        /// <summary>Loads the exact versioned manifest without changing imported assets.</summary>
        internal static UnityGenericTopologyMatrixManifest LoadManifest(string productRootReference)
        {
            string reference = productRootReference + "/Documentation/topology-matrix.txt";
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(reference);
            UnityGenericTopologyMatrixManifest manifest = asset == null
                ? null
                : JsonUtility.FromJson<UnityGenericTopologyMatrixManifest>(asset.text);
            return manifest != null && manifest.schemaVersion == 1
                ? manifest
                : null;
        }

        /// <summary>Returns deterministic findings for the complete imported matrix and its outputs.</summary>
        internal static UnityGenericTopologyMatrixReport Validate(string productRootReference)
        {
            var report = new UnityGenericTopologyMatrixReport();
            var findings = new List<string>();
            UnityGenericTopologyMatrixManifest manifest = LoadManifest(productRootReference);
            if (manifest == null || manifest.cases == null || manifest.cases.Length != 5 ||
                manifest.invalidCases == null || manifest.invalidCases.Length != 1)
            {
                report.Findings = new[] { "UNITY_TOPOLOGY_MANIFEST_INVALID" };
                return report;
            }

            report.Categories = manifest.cases.Select(value => value.category)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (report.Categories.Any(string.IsNullOrWhiteSpace) ||
                report.Categories.Distinct(StringComparer.Ordinal).Count() != 5)
            {
                findings.Add("UNITY_TOPOLOGY_CATEGORY_MATRIX_INVALID");
            }

            bool generic = true;
            bool hierarchy = true;
            bool skin = true;
            bool motion = true;
            int importedClipCount = 0;
            int controllerStateCount = 0;
            foreach (UnityGenericTopologyCaseExpectation expectation in manifest.cases
                .OrderBy(value => value.id, StringComparer.Ordinal))
            {
                ValidateCase(productRootReference, expectation, findings,
                    ref generic, ref hierarchy, ref skin, ref motion,
                    ref importedClipCount, ref controllerStateCount);
            }

            report.ValidCaseCount = manifest.cases.Length;
            report.ImportedClipCount = importedClipCount;
            report.ControllerStateCount = controllerStateCount;
            report.GenericImportVerified = generic;
            report.HierarchyVerified = hierarchy;
            report.SkinWeightsVerified = skin;
            report.MotionVerified = motion;
            report.NegativeFindingsVerified = ValidateNegativeCases(
                productRootReference, manifest, findings);
            report.Findings = findings.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return report;
        }

        private static void ValidateCase(
            string productRootReference,
            UnityGenericTopologyCaseExpectation expectation,
            ICollection<string> findings,
            ref bool generic,
            ref bool hierarchy,
            ref bool skin,
            ref bool motion,
            ref int importedClipCount,
            ref int controllerStateCount)
        {
            if (!ValidExpectation(expectation))
            {
                findings.Add("UNITY_TOPOLOGY_CASE_EXPECTATION_INVALID:" + expectation?.id);
                generic = hierarchy = skin = motion = false;
                return;
            }

            string sourceReference = productRootReference + "/Source/" + expectation.modelFile;
            var importer = AssetImporter.GetAtPath(sourceReference) as ModelImporter;
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourceReference);
            Avatar[] avatars = AssetDatabase.LoadAllAssetsAtPath(sourceReference)
                .OfType<Avatar>().ToArray();
            bool caseGeneric = importer != null && source != null &&
                importer.animationType == ModelImporterAnimationType.Generic &&
                importer.importAnimation && !importer.optimizeGameObjects &&
                avatars.All(value => !value.isHuman);
            if (!caseGeneric)
            {
                findings.Add("UNITY_TOPOLOGY_GENERIC_IMPORT_INVALID:" + expectation.id);
                generic = false;
            }

            SkinnedMeshRenderer[] renderers = source == null
                ? Array.Empty<SkinnedMeshRenderer>()
                : source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer renderer = renderers.Length == 1 ? renderers[0] : null;
            bool caseHierarchy = ValidateHierarchy(source, renderer, expectation);
            if (!caseHierarchy)
            {
                findings.Add("UNITY_TOPOLOGY_HIERARCHY_INVALID:" + expectation.id);
                hierarchy = false;
            }

            UnitySkinSkeletonReport sourceSkin = UnitySkinSkeletonValidator.Validate(source, 4);
            bool caseSkin = sourceSkin.IsValid && sourceSkin.RendererCount == 1 &&
                sourceSkin.UniqueBoneCount == expectation.bones.Length &&
                sourceSkin.MaximumInfluences == 1 && sourceSkin.UnweightedVertexCount == 0;
            if (!caseSkin)
            {
                findings.Add("UNITY_TOPOLOGY_SKIN_INVALID:" + expectation.id);
                skin = false;
            }

            string clipReference = productRootReference + "/Animations/A_" +
                expectation.assetId + "_" + expectation.clipId + ".anim";
            string controllerReference = productRootReference + "/Controllers/AC_" +
                expectation.assetId + ".controller";
            string prefabReference = productRootReference + "/Prefabs/P_" +
                expectation.assetId + ".prefab";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipReference);
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerReference);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabReference);
            importedClipCount += clip == null ? 0 : 1;
            int stateCount = controller == null || controller.layers.Length != 1
                ? 0
                : controller.layers[0].stateMachine.states.Length;
            controllerStateCount += stateCount;
            Animator[] animators = prefab == null
                ? Array.Empty<Animator>()
                : prefab.GetComponentsInChildren<Animator>(true);
            bool caseOutputs = clip != null && !clip.isLooping &&
                Mathf.Abs(clip.frameRate - expectation.frameRate) <= 0.001f &&
                controller != null && stateCount == 1 &&
                controller.layers[0].stateMachine.defaultState.motion == clip &&
                prefab != null && animators.Length == 1 &&
                animators[0].runtimeAnimatorController == controller;
            var motionExpectation = new UnityAnimationClipExpectation
            {
                Name = clip == null ? string.Empty : clip.name,
                DurationSeconds = (expectation.lastFrame - expectation.firstFrame) /
                    expectation.frameRate,
                FramesPerSecond = expectation.frameRate,
                Looping = false,
                MovingBoneName = expectation.movingBone,
            };
            UnityAnimationMotionReport motionReport = UnityAnimationMotionValidator.Validate(
                productRootReference + "/Animations",
                prefabReference,
                new[] { motionExpectation },
                new[] { clipReference });
            bool caseMotion = caseOutputs && motionReport.IsValid &&
                motionReport.BindingsVerified && motionReport.BoneMotionVerified &&
                motionReport.RendererMotionVerified && motionReport.NonLoopingCompletionVerified;
            if (!caseMotion)
            {
                findings.Add("UNITY_TOPOLOGY_MOTION_INVALID:" + expectation.id);
                motion = false;
            }
        }

        private static bool ValidateHierarchy(
            GameObject source,
            SkinnedMeshRenderer renderer,
            UnityGenericTopologyCaseExpectation expectation)
        {
            if (source == null || renderer == null || renderer.name != expectation.rendererName ||
                renderer.rootBone == null || renderer.rootBone.name != expectation.rootBone ||
                renderer.bones.Length != expectation.bones.Length)
            {
                return false;
            }

            Transform[] transforms = source.GetComponentsInChildren<Transform>(true);
            var expectedNames = new HashSet<string>(
                expectation.bones.Select(value => value.name), StringComparer.Ordinal);
            foreach (UnityGenericTopologyBoneExpectation bone in expectation.bones)
            {
                Transform[] matches = transforms.Where(value => value.name == bone.name).ToArray();
                if (matches.Length != 1)
                {
                    return false;
                }
                string actualParent = matches[0].parent != null &&
                    expectedNames.Contains(matches[0].parent.name)
                    ? matches[0].parent.name
                    : string.Empty;
                if (!string.Equals(actualParent, bone.parent ?? string.Empty,
                    StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return renderer.bones.Select(value => value == null ? string.Empty : value.name)
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(expectedNames.OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal);
        }

        private static bool ValidateNegativeCases(
            string productRootReference,
            UnityGenericTopologyMatrixManifest manifest,
            ICollection<string> findings)
        {
            UnityGenericTopologyInvalidExpectation invalid = manifest.invalidCases[0];
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                productRootReference + "/Source/" + invalid.modelFile);
            string[] rootFindings = ValidateDeclaredRootCount(source);
            bool multiRoot = rootFindings.SequenceEqual(
                new[] { invalid.expectedFinding }, StringComparer.Ordinal);

            var invalidBinding = new AnimationClip { name = "InvalidBinding" };
            string[] bindingFindings;
            try
            {
                AnimationUtility.SetEditorCurve(
                    invalidBinding,
                    EditorCurveBinding.FloatCurve(
                        "MissingBindingTarget", typeof(Transform), "m_LocalRotation.x"),
                    AnimationCurve.Linear(0f, 0f, 1f, 1f));
                bindingFindings = UnityAnimationMotionValidator.ValidateRotationBinding(
                    invalidBinding, manifest.cases[0].movingBone);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidBinding);
            }
            bool invalidBindingVerified = bindingFindings.SequenceEqual(
                new[] { "UNITY_ANIMATION_BINDINGS_MISMATCH:InvalidBinding" },
                StringComparer.Ordinal);
            if (!multiRoot)
            {
                findings.Add("UNITY_TOPOLOGY_MULTI_ROOT_FINDING_MISSING");
            }
            if (!invalidBindingVerified)
            {
                findings.Add("UNITY_TOPOLOGY_BINDING_FINDING_MISSING");
            }
            return multiRoot && invalidBindingVerified;
        }

        /// <summary>Returns a stable finding for missing skin data or a non-single-root bone set.</summary>
        internal static string[] ValidateDeclaredRootCount(GameObject source)
        {
            if (source == null)
            {
                return new[] { "UNITY_TOPOLOGY_REQUEST_INVALID" };
            }
            SkinnedMeshRenderer[] renderers = source == null
                ? Array.Empty<SkinnedMeshRenderer>()
                : source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var bones = new HashSet<Transform>(
                renderers.SelectMany(value => value.bones ?? Array.Empty<Transform>())
                    .Where(value => value != null));
            if (renderers.Length == 0 || bones.Count == 0)
            {
                return new[] { "UNITY_TOPOLOGY_SKIN_MISSING" };
            }
            int rootCount = bones.Count(value => value.parent == null || !bones.Contains(value.parent));
            return rootCount == 1
                ? Array.Empty<string>()
                : new[] { "UNITY_TOPOLOGY_ROOT_COUNT_INVALID" };
        }

        private static bool ValidExpectation(UnityGenericTopologyCaseExpectation expectation) =>
            expectation != null && !string.IsNullOrWhiteSpace(expectation.id) &&
            !string.IsNullOrWhiteSpace(expectation.category) &&
            !string.IsNullOrWhiteSpace(expectation.assetId) &&
            !string.IsNullOrWhiteSpace(expectation.modelFile) &&
            !string.IsNullOrWhiteSpace(expectation.rendererName) &&
            !string.IsNullOrWhiteSpace(expectation.rootBone) &&
            !string.IsNullOrWhiteSpace(expectation.movingBone) &&
            !string.IsNullOrWhiteSpace(expectation.clipId) &&
            expectation.frameRate > 0f && expectation.lastFrame > expectation.firstFrame &&
            expectation.bones != null && expectation.bones.Length >= 2 &&
            expectation.bones.All(value => value != null &&
                !string.IsNullOrWhiteSpace(value.name));
    }
}
