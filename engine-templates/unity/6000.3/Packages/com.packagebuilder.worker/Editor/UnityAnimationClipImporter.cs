using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Defines how Unity imports and stores animation curves for a product.</summary>
    internal enum UnityAnimationCompressionPolicy
    {
        Unspecified = 0,
        Off = 1,
        KeyframeReduction = 2,
        KeyframeReductionAndCompression = 3,
        Optimal = 4,
    }

    /// <summary>Defines whether a clip preserves root motion or bakes it into the pose.</summary>
    internal enum UnityRootMotionPolicy
    {
        Unspecified = 0,
        Preserve = 1,
        BakeIntoPose = 2,
    }

    /// <summary>Defines one manifest-owned source take and exact output clip boundary.</summary>
    internal sealed class UnityAnimationClipPlan
    {
        internal string ClipId { get; set; }

        internal string SourceTakeName { get; set; }

        internal float FirstFrame { get; set; }

        internal float LastFrame { get; set; }

        internal float SampleRate { get; set; }

        internal bool LoopTime { get; set; }

        internal UnityRootMotionPolicy RootMotionPolicy { get; set; }
    }

    /// <summary>Defines deterministic animation import and extracted-clip output paths.</summary>
    internal sealed class UnityAnimationClipImportRequest
    {
        internal string AssetId { get; set; }

        internal string SourceModelReference { get; set; }

        internal string OutputAnimationFolderReference { get; set; }

        internal UnityAnimationCompressionPolicy CompressionPolicy { get; set; }

        internal UnityAnimationClipPlan[] Clips { get; set; } = Array.Empty<UnityAnimationClipPlan>();
    }

    /// <summary>Reports the exact independently saved AnimationClip assets.</summary>
    internal sealed class UnityAnimationClipImportResult
    {
        internal UnityAnimationClipImportResult(string[] outputAssetReferences)
        {
            OutputAssetReferences = outputAssetReferences;
        }

        internal string[] OutputAssetReferences { get; private set; }
    }

    /// <summary>Configures ModelImporter clip ranges and extracts stable A_ assets transactionally.</summary>
    internal static class UnityAnimationClipImporter
    {
        /// <summary>Discovers source actions as deterministic full-range plans for manifest review.</summary>
        internal static bool TryDiscoverSourceActions(
            string sourceModelReference,
            out UnityAnimationClipPlan[] plans,
            out string diagnosticCode)
        {
            plans = Array.Empty<UnityAnimationClipPlan>();
            diagnosticCode = "UNITY_ANIMATION_SOURCE_INVALID";
            TakeInfo[] takes;
            if (!TryReadSourceTakes(sourceModelReference, out takes))
            {
                diagnosticCode = "UNITY_ANIMATION_SOURCE_TAKE_MISSING";
                return false;
            }

            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            var discovered = new List<UnityAnimationClipPlan>();
            foreach (TakeInfo take in takes.OrderBy(value => value.name, StringComparer.Ordinal))
            {
                string clipId = ToClipId(string.IsNullOrEmpty(take.defaultClipName)
                    ? take.name
                    : take.defaultClipName);
                if (string.IsNullOrEmpty(clipId) || !usedIds.Add(clipId) ||
                    !IsFinitePositive(take.sampleRate))
                {
                    diagnosticCode = "UNITY_ANIMATION_SOURCE_TAKE_INVALID";
                    return false;
                }

                discovered.Add(new UnityAnimationClipPlan
                {
                    ClipId = clipId,
                    SourceTakeName = take.name,
                    FirstFrame = take.bakeStartTime * take.sampleRate,
                    LastFrame = take.bakeStopTime * take.sampleRate,
                    SampleRate = take.sampleRate,
                    LoopTime = false,
                    RootMotionPolicy = UnityRootMotionPolicy.BakeIntoPose,
                });
            }

            plans = discovered.ToArray();
            diagnosticCode = string.Empty;
            return true;
        }

        /// <summary>Imports exact ranges, verifies them, and saves independent A_ clip assets.</summary>
        internal static bool TryImportAndExtract(
            UnityAnimationClipImportRequest request,
            out UnityAnimationClipImportResult result,
            out string diagnosticCode)
        {
            result = null;
            diagnosticCode = "UNITY_ANIMATION_CLIP_PLAN_INVALID";
            ModelImporter importer;
            UnityAnimationClipPlan[] plans;
            if (!TryValidate(request, out importer, out plans, out diagnosticCode))
            {
                return false;
            }

            bool originalImportAnimation = importer.importAnimation;
            bool originalResampleCurves = importer.resampleCurves;
            ModelImporterAnimationCompression originalCompression = importer.animationCompression;
            ModelImporterClipAnimation[] originalClips = importer.clipAnimations;
            var created = new List<string>();
            try
            {
                importer.importAnimation = true;
                importer.resampleCurves = true;
                importer.animationCompression = ToImporterCompression(request.CompressionPolicy);
                importer.clipAnimations = plans.Select(plan => CreateImporterClip(
                    request.AssetId, plan)).ToArray();
                importer.SaveAndReimport();

                AnimationClip[] imported = AssetDatabase.LoadAllAssetsAtPath(request.SourceModelReference)
                    .OfType<AnimationClip>()
                    .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    .ToArray();
                foreach (UnityAnimationClipPlan plan in plans)
                {
                    string name = OutputName(request.AssetId, plan.ClipId);
                    AnimationClip source = imported.SingleOrDefault(clip => clip.name == name);
                    if (source == null || Mathf.Abs(source.frameRate - plan.SampleRate) > 0.001f)
                    {
                        diagnosticCode = "UNITY_ANIMATION_CLIP_IMPORT_VERIFY_FAILED";
                        return false;
                    }

                    float expectedLength = (plan.LastFrame - plan.FirstFrame) / plan.SampleRate;
                    if (Mathf.Abs(source.length - expectedLength) > 1f / plan.SampleRate + 0.001f)
                    {
                        diagnosticCode = "UNITY_ANIMATION_CLIP_RANGE_VERIFY_FAILED";
                        return false;
                    }

                    string output = request.OutputAnimationFolderReference + "/" + name + ".anim";
                    var copy = UnityEngine.Object.Instantiate(source);
                    copy.name = name;
                    AssetDatabase.CreateAsset(copy, output);
                    created.Add(output);
                }

                AssetDatabase.SaveAssets();
                if (created.Any(path => AssetDatabase.LoadAssetAtPath<AnimationClip>(path) == null))
                {
                    diagnosticCode = "UNITY_ANIMATION_CLIP_EXTRACT_VERIFY_FAILED";
                    return false;
                }

                result = new UnityAnimationClipImportResult(created.ToArray());
                diagnosticCode = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException ||
                exception is InvalidOperationException || exception is UnityException)
            {
                diagnosticCode = "UNITY_ANIMATION_CLIP_IMPORT_FAILED";
                return false;
            }
            finally
            {
                if (result == null)
                {
                    foreach (string path in created)
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                    var current = AssetImporter.GetAtPath(request.SourceModelReference) as ModelImporter;
                    if (current != null)
                    {
                        current.importAnimation = originalImportAnimation;
                        current.resampleCurves = originalResampleCurves;
                        current.animationCompression = originalCompression;
                        current.clipAnimations = originalClips;
                        current.SaveAndReimport();
                    }
                }
            }
        }

        private static bool TryValidate(
            UnityAnimationClipImportRequest request,
            out ModelImporter importer,
            out UnityAnimationClipPlan[] plans,
            out string diagnosticCode)
        {
            importer = null;
            plans = Array.Empty<UnityAnimationClipPlan>();
            diagnosticCode = "UNITY_ANIMATION_CLIP_PLAN_INVALID";
            if (request == null || !UnityAssetNameValidator.IsProductFolder(request.AssetId) ||
                !IsSafeModelReference(request.SourceModelReference) ||
                !IsSafeAnimationFolder(request.OutputAnimationFolderReference) ||
                !AssetDatabase.IsValidFolder(request.OutputAnimationFolderReference) ||
                request.Clips == null || request.Clips.Length == 0 ||
                request.CompressionPolicy == UnityAnimationCompressionPolicy.Unspecified ||
                !Enum.IsDefined(typeof(UnityAnimationCompressionPolicy), request.CompressionPolicy))
            {
                return false;
            }

            TakeInfo[] takes;
            if (!TryReadSourceTakes(request.SourceModelReference, out takes))
            {
                diagnosticCode = "UNITY_ANIMATION_SOURCE_TAKE_MISSING";
                return false;
            }
            importer = AssetImporter.GetAtPath(request.SourceModelReference) as ModelImporter;
            if (importer == null)
            {
                diagnosticCode = "UNITY_ANIMATION_SOURCE_TAKE_MISSING";
                return false;
            }

            if (takes.Any(take => string.IsNullOrEmpty(take.name)) ||
                takes.Select(take => take.name).Distinct(StringComparer.Ordinal).Count() != takes.Length)
            {
                diagnosticCode = "UNITY_ANIMATION_SOURCE_TAKE_INVALID";
                return false;
            }

            var takesByName = takes.ToDictionary(take => take.name, StringComparer.Ordinal);
            plans = request.Clips.OrderBy(clip => clip.ClipId, StringComparer.Ordinal).ToArray();
            if (plans.Any(plan => plan == null || !IsClipId(plan.ClipId) ||
                string.IsNullOrEmpty(plan.SourceTakeName) || !takesByName.ContainsKey(plan.SourceTakeName) ||
                !IsFinite(plan.FirstFrame) || !IsFinite(plan.LastFrame) ||
                plan.LastFrame <= plan.FirstFrame || !IsFinitePositive(plan.SampleRate) ||
                plan.RootMotionPolicy == UnityRootMotionPolicy.Unspecified ||
                !Enum.IsDefined(typeof(UnityRootMotionPolicy), plan.RootMotionPolicy)) ||
                plans.Select(plan => plan.ClipId).Distinct(StringComparer.Ordinal).Count() != plans.Length)
            {
                return false;
            }

            foreach (UnityAnimationClipPlan plan in plans)
            {
                TakeInfo take = takesByName[plan.SourceTakeName];
                float minimum = take.bakeStartTime * take.sampleRate;
                float maximum = take.bakeStopTime * take.sampleRate;
                string output = request.OutputAnimationFolderReference + "/" +
                    OutputName(request.AssetId, plan.ClipId) + ".anim";
                if (plan.FirstFrame < minimum - 0.001f || plan.LastFrame > maximum + 0.001f ||
                    Mathf.Abs(plan.SampleRate - take.sampleRate) > 0.001f ||
                    AssetDatabase.LoadMainAssetAtPath(output) != null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reads Unity's source-take metadata even when the preceding rig policy disabled animation.
        /// The temporary import is fully restored so discovery remains non-mutating.
        /// </summary>
        private static bool TryReadSourceTakes(string sourceModelReference, out TakeInfo[] takes)
        {
            takes = Array.Empty<TakeInfo>();
            var importer = AssetImporter.GetAtPath(sourceModelReference) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            TakeInfo[] existing = importer.importedTakeInfos;
            if (existing != null && existing.Length != 0)
            {
                takes = existing.ToArray();
                return true;
            }

            bool originalImportAnimation = importer.importAnimation;
            bool originalResampleCurves = importer.resampleCurves;
            ModelImporterClipAnimation[] originalClips = importer.clipAnimations;
            bool restoreRequired = !originalImportAnimation;
            try
            {
                importer.importAnimation = true;
                importer.SaveAndReimport();
                importer = AssetImporter.GetAtPath(sourceModelReference) as ModelImporter;
                TakeInfo[] imported = importer == null ? Array.Empty<TakeInfo>() :
                    importer.importedTakeInfos;
                if (imported == null || imported.Length == 0)
                {
                    return false;
                }

                takes = imported.ToArray();
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException ||
                exception is InvalidOperationException || exception is UnityException)
            {
                takes = Array.Empty<TakeInfo>();
                return false;
            }
            finally
            {
                if (restoreRequired)
                {
                    var current = AssetImporter.GetAtPath(sourceModelReference) as ModelImporter;
                    if (current != null)
                    {
                        current.importAnimation = originalImportAnimation;
                        current.resampleCurves = originalResampleCurves;
                        current.clipAnimations = originalClips;
                        current.SaveAndReimport();
                    }
                }
            }
        }

        private static string OutputName(string assetId, string clipId)
        {
            return "A_" + assetId + "_" + clipId;
        }

        private static ModelImporterClipAnimation CreateImporterClip(
            string assetId,
            UnityAnimationClipPlan plan)
        {
            bool bakeRootMotion = plan.RootMotionPolicy == UnityRootMotionPolicy.BakeIntoPose;
            return new ModelImporterClipAnimation
            {
                name = OutputName(assetId, plan.ClipId),
                takeName = plan.SourceTakeName,
                firstFrame = plan.FirstFrame,
                lastFrame = plan.LastFrame,
                loopTime = plan.LoopTime,
                loopPose = plan.LoopTime,
                lockRootRotation = bakeRootMotion,
                lockRootHeightY = bakeRootMotion,
                lockRootPositionXZ = bakeRootMotion,
                keepOriginalOrientation = true,
                keepOriginalPositionY = true,
                keepOriginalPositionXZ = true,
            };
        }

        private static ModelImporterAnimationCompression ToImporterCompression(
            UnityAnimationCompressionPolicy policy)
        {
            switch (policy)
            {
                case UnityAnimationCompressionPolicy.Off:
                    return ModelImporterAnimationCompression.Off;
                case UnityAnimationCompressionPolicy.KeyframeReduction:
                    return ModelImporterAnimationCompression.KeyframeReduction;
                case UnityAnimationCompressionPolicy.KeyframeReductionAndCompression:
                    return ModelImporterAnimationCompression.KeyframeReductionAndCompression;
                case UnityAnimationCompressionPolicy.Optimal:
                    return ModelImporterAnimationCompression.Optimal;
                default:
                    throw new InvalidOperationException("Animation compression must be explicit.");
            }
        }

        private static string ToClipId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] characters = value.Trim().Select(character =>
                IsAsciiLetterOrDigit(character) ? character : '_').ToArray();
            string result = new string(characters).Trim('_');
            return IsClipId(result) ? result : string.Empty;
        }

        private static bool IsClipId(string value)
        {
            return !string.IsNullOrEmpty(value) && IsAsciiLetter(value[0]) &&
                value.All(character => IsAsciiLetterOrDigit(character) || character == '_');
        }

        private static bool IsSafeModelReference(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith("Assets/", StringComparison.Ordinal) &&
                value.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                value.IndexOf("/Source/", StringComparison.Ordinal) >= 0 &&
                value.IndexOf('\\') < 0 && value.IndexOf(':') < 0 &&
                value.IndexOf("/../", StringComparison.Ordinal) < 0;
        }

        private static bool IsSafeAnimationFolder(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith("Assets/", StringComparison.Ordinal) &&
                value.EndsWith("/Animations", StringComparison.Ordinal) &&
                value.IndexOf('\\') < 0 && value.IndexOf(':') < 0 &&
                value.IndexOf("/../", StringComparison.Ordinal) < 0;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsAsciiLetterOrDigit(char value)
        {
            return IsAsciiLetter(value) || value >= '0' && value <= '9';
        }

        private static bool IsAsciiLetter(char value)
        {
            return value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z';
        }
    }
}
