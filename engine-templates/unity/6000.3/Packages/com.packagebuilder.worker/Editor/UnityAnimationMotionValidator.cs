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

    /// <summary>
    /// Verifies imported clip inventory and evaluates animation on a disposable prefab instance.
    /// Sampling never saves or changes source clips, importer settings, controllers, or prefabs.
    /// </summary>
    internal static class UnityAnimationMotionValidator
    {
        /// <summary>Validates exact metadata, curve bindings, deformation, bone motion, and one-shot completion.</summary>
        internal static UnityAnimationMotionReport Validate(
            string animationFolderReference,
            string animatedPrefabReference,
            IReadOnlyList<UnityAnimationClipExpectation> expected)
        {
            var report = new UnityAnimationMotionReport();
            var findings = new List<string>();
            if (!AssetDatabase.IsValidFolder(animationFolderReference) || expected == null ||
                expected.Count == 0 || expected.Any(value => value == null))
            {
                report.Findings = new[] { "UNITY_ANIMATION_VALIDATION_REQUEST_INVALID" };
                return report;
            }

            AnimationClip[] clips = AssetDatabase.FindAssets("t:AnimationClip",
                    new[] { animationFolderReference })
                .Select(AssetDatabase.GUIDToAssetPath)
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

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                bool hasBoneRotation = bindings.Any(value =>
                    !string.IsNullOrEmpty(value.path) && value.path.EndsWith("Tip", StringComparison.Ordinal) &&
                    value.propertyName.StartsWith("m_LocalRotation", StringComparison.Ordinal));
                if (bindings.Length == 0 || !hasBoneRotation)
                {
                    findings.Add("UNITY_ANIMATION_BINDINGS_MISMATCH:" + expectation.Name);
                }
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
                Transform movingBone = renderer == null
                    ? null
                    : renderer.bones.FirstOrDefault(value => value != null && value.name == "Tip");
                AnimationClip motionClip = clips.FirstOrDefault();
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

            for (int index = 0; index < first.Length; index++)
            {
                if ((first[index] - second[index]).sqrMagnitude > 0.000001f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
