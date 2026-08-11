using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Defines exact imported metadata expected for one extracted animation clip.</summary>
    internal sealed class UnityAnimationClipExpectation
    {
        internal string Name { get; set; }

        internal float DurationSeconds { get; set; }

        internal float FramesPerSecond { get; set; }

        internal bool Looping { get; set; }

        /// <summary>
        /// Names the topology-specific bone whose rotation binding and sampled motion are required.
        /// Existing two-bone fixtures default to Tip when the expectation omits this value.
        /// </summary>
        internal string MovingBoneName { get; set; } = "Tip";
    }

    /// <summary>Captures deterministic imported animation metadata and sampled-motion evidence.</summary>
    internal sealed class UnityAnimationMotionReport
    {
        internal string[] ClipNames { get; set; } = Array.Empty<string>();

        internal bool BindingsVerified { get; set; }

        internal bool BoneMotionVerified { get; set; }

        internal bool RendererMotionVerified { get; set; }

        internal bool RenderableVolumeVerified { get; set; }

        internal bool NonLoopingCompletionVerified { get; set; }

        internal string[] Findings { get; set; } = Array.Empty<string>();

        internal bool IsValid => Findings.Length == 0;
    }

    /// <summary>Records whether one clip deforms every explicitly named skinned renderer together.</summary>
    internal sealed class UnitySynchronizedRendererMotionReport
    {
        internal string[] RendererNames { get; set; } = Array.Empty<string>();

        internal string[] MovingRendererNames { get; set; } = Array.Empty<string>();

        internal float VerifiedSampleTimeSeconds { get; set; }

        internal string[] Findings { get; set; } = Array.Empty<string>();

        internal bool IsValid => Findings.Length == 0;
    }

    /// <summary>
    /// Verifies imported clip inventory and evaluates animation on a disposable prefab instance.
    /// Sampling never saves or changes source clips, importer settings, controllers, or prefabs.
    /// </summary>
    internal static class UnityAnimationMotionValidator
    {
        /// <summary>
        /// Samples one extracted clip without saving it and requires every named skinned renderer
        /// to deform during the same sampled pose. Renderer names are compared ordinally.
        /// </summary>
        internal static UnitySynchronizedRendererMotionReport ValidateSynchronizedRenderers(
            string animationClipReference,
            string animatedPrefabReference,
            IReadOnlyList<string> expectedRendererNames)
        {
            var report = new UnitySynchronizedRendererMotionReport();
            var findings = new List<string>();
            string[] expected = expectedRendererNames == null
                ? Array.Empty<string>()
                : expectedRendererNames.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (expected.Length == 0 || expected.Any(string.IsNullOrWhiteSpace) ||
                expected.Distinct(StringComparer.Ordinal).Count() != expected.Length)
            {
                report.Findings = new[] { "UNITY_ANIMATION_RENDERER_EXPECTATION_INVALID" };
                return report;
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animationClipReference);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(animatedPrefabReference);
            GameObject instance = prefab == null ? null : UnityEngine.Object.Instantiate(prefab);
            try
            {
                SkinnedMeshRenderer[] renderers = instance == null
                    ? Array.Empty<SkinnedMeshRenderer>()
                    : instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .OrderBy(value => value.name, StringComparer.Ordinal).ToArray();
                Animator animator = instance == null
                    ? null
                    : instance.GetComponentInChildren<Animator>(true);
                report.RendererNames = renderers.Select(value => value.name).ToArray();
                if (clip == null || instance == null || animator == null ||
                    !report.RendererNames.SequenceEqual(expected, StringComparer.Ordinal))
                {
                    findings.Add("UNITY_ANIMATION_RENDERER_INVENTORY_MISMATCH");
                }
                else
                {
                    Dictionary<string, Vector3[]> start = SampleRenderers(
                        animator.gameObject, clip, 0f, renderers);
                    float[] candidates = Enumerable.Range(1, 9)
                        .Select(index => clip.length * index / 10f).ToArray();
                    var movingAtAnySample = new HashSet<string>(StringComparer.Ordinal);
                    foreach (float candidate in candidates)
                    {
                        Dictionary<string, Vector3[]> sampled = SampleRenderers(
                            animator.gameObject, clip, candidate, renderers);
                        string[] moving = renderers
                            .Where(value => VerticesMoved(start[value.name], sampled[value.name]))
                            .Select(value => value.name)
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToArray();
                        foreach (string rendererName in moving)
                        {
                            movingAtAnySample.Add(rendererName);
                        }
                        if (moving.SequenceEqual(expected, StringComparer.Ordinal))
                        {
                            report.MovingRendererNames = moving;
                            report.VerifiedSampleTimeSeconds = candidate;
                            break;
                        }
                    }
                    if (report.MovingRendererNames.Length == 0)
                    {
                        report.MovingRendererNames = movingAtAnySample
                            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                    }
                    if (!report.MovingRendererNames.SequenceEqual(expected, StringComparer.Ordinal))
                    {
                        findings.Add("UNITY_ANIMATION_SYNCHRONIZED_RENDERER_MOTION_MISSING");
                    }
                }
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            report.Findings = findings.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return report;
        }

        /// <summary>Validates exact metadata, curve bindings, deformation, bone motion, and one-shot completion.</summary>
        internal static UnityAnimationMotionReport Validate(
            string animationFolderReference,
            string animatedPrefabReference,
            IReadOnlyList<UnityAnimationClipExpectation> expected,
            IReadOnlyList<string> clipAssetReferences = null)
        {
            var report = new UnityAnimationMotionReport();
            var findings = new List<string>();
            if (!AssetDatabase.IsValidFolder(animationFolderReference) || expected == null ||
                expected.Count == 0 || expected.Any(value => value == null) ||
                clipAssetReferences != null &&
                (clipAssetReferences.Count == 0 ||
                    clipAssetReferences.Any(value =>
                        !IsSafeClipReference(animationFolderReference, value)) ||
                    clipAssetReferences.Distinct(StringComparer.Ordinal).Count() !=
                        clipAssetReferences.Count))
            {
                report.Findings = new[] { "UNITY_ANIMATION_VALIDATION_REQUEST_INVALID" };
                return report;
            }

            IEnumerable<string> clipReferences = clipAssetReferences ??
                AssetDatabase.FindAssets("t:AnimationClip", new[] { animationFolderReference })
                    .Select(AssetDatabase.GUIDToAssetPath);
            AnimationClip[] clips = clipReferences
                .Distinct(StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
                .Where(value => value != null)
                .OrderBy(value => value.name, StringComparer.Ordinal)
                .ToArray();
            report.ClipNames = clips.Select(value => value.name).ToArray();
            string[] expectedNames = expected.Select(value => value.Name)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!report.ClipNames.SequenceEqual(expectedNames, StringComparer.Ordinal))
            {
                findings.Add("UNITY_ANIMATION_CLIP_INVENTORY_MISMATCH");
            }

            foreach (UnityAnimationClipExpectation expectation in expected)
            {
                AnimationClip clip = clips.SingleOrDefault(value => value.name == expectation.Name);
                if (clip == null)
                {
                    continue;
                }

                if (Mathf.Abs(clip.length - expectation.DurationSeconds) > 0.001f)
                {
                    findings.Add("UNITY_ANIMATION_DURATION_MISMATCH:" + expectation.Name);
                }
                if (Mathf.Abs(clip.frameRate - expectation.FramesPerSecond) > 0.001f)
                {
                    findings.Add("UNITY_ANIMATION_FPS_MISMATCH:" + expectation.Name);
                }
                if (clip.isLooping != expectation.Looping)
                {
                    findings.Add("UNITY_ANIMATION_LOOP_MISMATCH:" + expectation.Name);
                }

                findings.AddRange(ValidateRotationBinding(clip, expectation.MovingBoneName));
            }
            report.BindingsVerified = !findings.Any(value =>
                value.StartsWith("UNITY_ANIMATION_BINDINGS_", StringComparison.Ordinal));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(animatedPrefabReference);
            GameObject instance = prefab == null ? null : UnityEngine.Object.Instantiate(prefab);
            try
            {
                Animator animator = instance == null ? null : instance.GetComponentInChildren<Animator>(true);
                SkinnedMeshRenderer renderer = instance == null
                    ? null
                    : instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                AnimationClip motionClip = clips.FirstOrDefault();
                UnityAnimationClipExpectation motionExpectation = motionClip == null
                    ? null
                    : expected.FirstOrDefault(value => value.Name == motionClip.name);
                string movingBoneName = motionExpectation == null ||
                    string.IsNullOrWhiteSpace(motionExpectation.MovingBoneName)
                    ? "Tip"
                    : motionExpectation.MovingBoneName;
                Transform movingBone = renderer == null
                    ? null
                    : renderer.bones.FirstOrDefault(value =>
                        value != null && value.name == movingBoneName);
                if (animator == null || renderer == null || movingBone == null || motionClip == null)
                {
                    findings.Add("UNITY_ANIMATION_MOTION_TARGET_MISSING");
                }
                else
                {
                    Vector3 localSize = renderer.localBounds.size;
                    report.RenderableVolumeVerified = localSize.x > 0.00001f &&
                        localSize.y > 0.00001f && localSize.z > 0.00001f;
                    if (!report.RenderableVolumeVerified)
                    {
                        findings.Add("UNITY_ANIMATION_RENDERER_VOLUME_DEGENERATE");
                    }

                    Quaternion startRotation;
                    Vector3[] startVertices;
                    Sample(animator.gameObject, renderer, movingBone, motionClip, 0f,
                        out startRotation, out startVertices);
                    Quaternion movedRotation;
                    Vector3[] movedVertices;
                    Sample(animator.gameObject, renderer, movingBone, motionClip,
                        motionClip.length * 0.5f, out movedRotation, out movedVertices);
                    report.BoneMotionVerified = Quaternion.Angle(startRotation, movedRotation) > 0.1f;
                    report.RendererMotionVerified = VerticesMoved(startVertices, movedVertices);
                    if (!report.BoneMotionVerified)
                    {
                        findings.Add("UNITY_ANIMATION_BONE_MOTION_MISSING");
                    }
                    if (!report.RendererMotionVerified)
                    {
                        findings.Add("UNITY_ANIMATION_RENDERER_MOTION_MISSING");
                    }

                    AnimationClip oneShot = clips.SingleOrDefault(value => !value.isLooping);
                    if (oneShot == null)
                    {
                        findings.Add("UNITY_ANIMATION_NON_LOOPING_CLIP_MISSING");
                    }
                    else
                    {
                        var transportHost = new GameObject("PBAnimationTransportValidation");
                        try
                        {
                            var transport = transportHost.AddComponent<
                                PackageBuilder.Preview.PackageBuilderAnimationTransport>();
                            transport.Configure(animator);
                            int oneShotIndex = Array.IndexOf(transport.ClipNames, oneShot.name);
                            report.NonLoopingCompletionVerified = oneShotIndex >= 0 &&
                                transport.Select(oneShotIndex) && !transport.LoopEnabled;
                            transport.Play();
                            transport.Tick(oneShot.length + 1f);
                            report.NonLoopingCompletionVerified =
                                report.NonLoopingCompletionVerified &&
                                transport.Playback ==
                                    PackageBuilder.Preview.PackageBuilderPlaybackState.Completed &&
                                Mathf.Abs(transport.CurrentTimeSeconds - oneShot.length) <= 0.001f;
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(transportHost);
                        }
                        if (!report.NonLoopingCompletionVerified)
                        {
                            findings.Add("UNITY_ANIMATION_NON_LOOPING_COMPLETION_MISMATCH");
                        }
                    }
                }
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            report.Findings = findings.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return report;
        }

        /// <summary>
        /// Returns one stable finding unless the clip contains a quaternion rotation binding for
        /// the exact declared moving-bone path segment. It never changes the clip.
        /// </summary>
        internal static string[] ValidateRotationBinding(AnimationClip clip, string movingBoneName)
        {
            if (clip == null || string.IsNullOrWhiteSpace(movingBoneName))
            {
                return new[] { "UNITY_ANIMATION_BINDING_REQUEST_INVALID" };
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            bool hasBoneRotation = bindings.Any(value =>
                IsExactPathSegment(value.path, movingBoneName) &&
                value.propertyName.StartsWith("m_LocalRotation", StringComparison.Ordinal));
            return bindings.Length > 0 && hasBoneRotation
                ? Array.Empty<string>()
                : new[] { "UNITY_ANIMATION_BINDINGS_MISMATCH:" + clip.name };
        }

        private static bool IsExactPathSegment(string path, string segment) =>
            string.Equals(path, segment, StringComparison.Ordinal) ||
            (!string.IsNullOrEmpty(path) &&
                path.EndsWith("/" + segment, StringComparison.Ordinal));

        private static bool IsSafeClipReference(string animationFolderReference, string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.StartsWith(animationFolderReference + "/", StringComparison.Ordinal) &&
            value.EndsWith(".anim", StringComparison.Ordinal) &&
            value.IndexOf('\\') < 0 && value.IndexOf(':') < 0 &&
            value.IndexOf("/../", StringComparison.Ordinal) < 0;

        private static void Sample(
            GameObject root,
            SkinnedMeshRenderer renderer,
            Transform movingBone,
            AnimationClip clip,
            float time,
            out Quaternion rotation,
            out Vector3[] vertices)
        {
            AnimationMode.StartAnimationMode();
            var mesh = new Mesh();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(root, clip, time);
                AnimationMode.EndSampling();
                rotation = movingBone.localRotation;
                renderer.BakeMesh(mesh);
                vertices = mesh.vertices;
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static bool VerticesMoved(Vector3[] first, Vector3[] second)
        {
            if (first == null || second == null || first.Length == 0 || first.Length != second.Length)
            {
                return false;
            }

            Vector3 minimum = first[0];
            Vector3 maximum = first[0];
            for (int index = 1; index < first.Length; index++)
            {
                minimum = Vector3.Min(minimum, first[index]);
                maximum = Vector3.Max(maximum, first[index]);
            }
            float toleranceSquared = Mathf.Max(
                (maximum - minimum).sqrMagnitude * 0.00000001f,
                0.00000000000001f);
            for (int index = 0; index < first.Length; index++)
            {
                if ((first[index] - second[index]).sqrMagnitude > toleranceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, Vector3[]> SampleRenderers(
            GameObject root,
            AnimationClip clip,
            float time,
            IReadOnlyList<SkinnedMeshRenderer> renderers)
        {
            var sampled = new Dictionary<string, Vector3[]>(StringComparer.Ordinal);
            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(root, clip, time);
                AnimationMode.EndSampling();
                foreach (SkinnedMeshRenderer renderer in renderers)
                {
                    var mesh = new Mesh();
                    try
                    {
                        renderer.BakeMesh(mesh);
                        sampled.Add(renderer.name, mesh.vertices);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(mesh);
                    }
                }
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }
            }
            return sampled;
        }
    }
}
