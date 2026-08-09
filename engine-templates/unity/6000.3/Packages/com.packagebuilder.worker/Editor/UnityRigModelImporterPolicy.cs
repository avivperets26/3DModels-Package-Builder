using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Identifies the rig type explicitly requested by the product manifest.</summary>
    internal enum UnityRigImportMode
    {
        Generic,
        Humanoid,
    }

    /// <summary>Maps one Unity human-bone identity to one uniquely named source bone.</summary>
    internal sealed class UnityHumanoidBoneMapping
    {
        internal UnityHumanoidBoneMapping(string humanName, string boneName)
        {
            HumanName = humanName;
            BoneName = boneName;
        }

        internal string HumanName { get; private set; }

        internal string BoneName { get; private set; }
    }

    /// <summary>Captures the complete manifest-owned rig import decision.</summary>
    internal sealed class UnityRigImportRequest
    {
        internal string ModelAssetReference { get; set; }

        internal UnityRigImportMode Mode { get; set; }

        internal string RootNodePath { get; set; }

        internal bool PreserveHierarchy { get; set; }

        internal bool OptimizeGameObjects { get; set; }

        internal string[] ExposedTransformPaths { get; set; } = Array.Empty<string>();

        internal UnityHumanoidBoneMapping[] HumanoidBoneMappings { get; set; } =
            Array.Empty<UnityHumanoidBoneMapping>();

        internal bool HumanoidRequestedByManifest { get; set; }

        internal bool ApproveGenericFallback { get; set; }
    }

    /// <summary>Reports the applied rig type and whether explicit fallback approval was consumed.</summary>
    internal sealed class UnityRigImportResult
    {
        internal UnityRigImportMode RequestedMode { get; set; }

        internal UnityRigImportMode AppliedMode { get; set; }

        internal bool UsedApprovedFallback { get; set; }

        internal bool AvatarIsValid { get; set; }

        internal bool AvatarIsHuman { get; set; }
    }

    /// <summary>
    /// Applies deterministic Generic rig settings and an opt-in, validated Humanoid policy without
    /// ever silently changing the manifest's requested rig type.
    /// </summary>
    internal static class UnityRigModelImporterPolicy
    {
        /// <summary>
        /// Applies and verifies one rig policy transaction. A failed Humanoid import is restored,
        /// unless the manifest separately grants permission to apply the Generic fallback.
        /// </summary>
        internal static bool TryApply(
            UnityRigImportRequest request,
            out UnityRigImportResult result,
            out string diagnosticCode)
        {
            result = null;
            diagnosticCode = "UNITY_RIG_IMPORT_INVALID";
            ModelImporter importer;
            string[] exposedPaths;
            UnityHumanoidBoneMapping[] mappings;
            if (!TryValidateRequest(request, out importer, out exposedPaths, out mappings,
                out diagnosticCode))
            {
                return false;
            }

            var snapshot = ImporterSnapshot.Capture(importer);
            try
            {
                if (request.Mode == UnityRigImportMode.Generic)
                {
                    ApplyGeneric(importer, request, exposedPaths);
                    importer.SaveAndReimport();
                    return TryVerifyGeneric(request, exposedPaths, false, out result,
                        out diagnosticCode);
                }

                ApplyHumanoid(importer, request, exposedPaths, mappings);
                importer.SaveAndReimport();
                importer = AssetImporter.GetAtPath(request.ModelAssetReference) as ModelImporter;
                Avatar avatar = importer == null ? null : LoadGeneratedAvatar(request.ModelAssetReference);
                if (importer != null && avatar != null && avatar.isValid && avatar.isHuman &&
                    importer.animationType == ModelImporterAnimationType.Human)
                {
                    result = new UnityRigImportResult
                    {
                        RequestedMode = UnityRigImportMode.Humanoid,
                        AppliedMode = UnityRigImportMode.Humanoid,
                        AvatarIsValid = true,
                        AvatarIsHuman = true,
                    };
                    diagnosticCode = string.Empty;
                    return true;
                }

                Restore(request.ModelAssetReference, snapshot);
                if (!request.ApproveGenericFallback)
                {
                    diagnosticCode = "UNITY_HUMANOID_AVATAR_INVALID";
                    return false;
                }

                importer = AssetImporter.GetAtPath(request.ModelAssetReference) as ModelImporter;
                if (importer == null)
                {
                    diagnosticCode = "UNITY_RIG_MODEL_IMPORTER_MISSING";
                    return false;
                }

                ApplyGeneric(importer, request, exposedPaths);
                importer.SaveAndReimport();
                return TryVerifyGeneric(request, exposedPaths, true, out result,
                    out diagnosticCode);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is UnityException)
            {
                Restore(request.ModelAssetReference, snapshot);
                if (request.Mode == UnityRigImportMode.Humanoid && request.ApproveGenericFallback)
                {
                    try
                    {
                        importer = AssetImporter.GetAtPath(request.ModelAssetReference) as ModelImporter;
                        if (importer != null)
                        {
                            ApplyGeneric(importer, request, exposedPaths);
                            importer.SaveAndReimport();
                            return TryVerifyGeneric(request, exposedPaths, true, out result,
                                out diagnosticCode);
                        }
                    }
                    catch (Exception fallbackException) when (
                        fallbackException is ArgumentException ||
                        fallbackException is InvalidOperationException ||
                        fallbackException is UnityException)
                    {
                        Restore(request.ModelAssetReference, snapshot);
                    }
                }

                diagnosticCode = "UNITY_RIG_IMPORT_FAILED";
                return false;
            }
        }

        private static bool TryValidateRequest(
            UnityRigImportRequest request,
            out ModelImporter importer,
            out string[] exposedPaths,
            out UnityHumanoidBoneMapping[] mappings,
            out string diagnosticCode)
        {
            importer = null;
            exposedPaths = Array.Empty<string>();
            mappings = Array.Empty<UnityHumanoidBoneMapping>();
            diagnosticCode = "UNITY_RIG_IMPORT_INVALID";
            if (request == null || !IsSafeModelReference(request.ModelAssetReference) ||
                !IsSafeTransformPath(request.RootNodePath))
            {
                return false;
            }

            importer = AssetImporter.GetAtPath(request.ModelAssetReference) as ModelImporter;
            if (importer == null)
            {
                diagnosticCode = "UNITY_RIG_MODEL_IMPORTER_MISSING";
                return false;
            }

            var availablePaths = new HashSet<string>(importer.transformPaths ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            if (!availablePaths.Contains(request.RootNodePath) ||
                !TryNormalizePaths(request.ExposedTransformPaths, availablePaths, out exposedPaths))
            {
                diagnosticCode = "UNITY_RIG_TRANSFORM_PLAN_INVALID";
                return false;
            }

            if (request.Mode == UnityRigImportMode.Humanoid)
            {
                if (!request.HumanoidRequestedByManifest)
                {
                    diagnosticCode = "UNITY_HUMANOID_MANIFEST_OPT_IN_REQUIRED";
                    return false;
                }

                if (!TryNormalizeMappings(request.HumanoidBoneMappings, availablePaths, out mappings))
                {
                    diagnosticCode = "UNITY_HUMANOID_MAPPING_INVALID";
                    return false;
                }
            }
            else if (request.HumanoidRequestedByManifest || request.ApproveGenericFallback ||
                (request.HumanoidBoneMappings != null && request.HumanoidBoneMappings.Length != 0))
            {
                diagnosticCode = "UNITY_GENERIC_RIG_HUMANOID_OPTIONS_FORBIDDEN";
                return false;
            }

            return true;
        }

        private static void ApplyGeneric(
            ModelImporter importer,
            UnityRigImportRequest request,
            string[] exposedPaths)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = false;
            importer.preserveHierarchy = request.PreserveHierarchy;
            importer.optimizeGameObjects = request.OptimizeGameObjects;
            importer.motionNodeName = request.RootNodePath;
            importer.extraExposedTransformPaths = exposedPaths;
        }

        private static void ApplyHumanoid(
            ModelImporter importer,
            UnityRigImportRequest request,
            string[] exposedPaths,
            UnityHumanoidBoneMapping[] mappings)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = false;
            importer.preserveHierarchy = request.PreserveHierarchy;
            importer.optimizeGameObjects = request.OptimizeGameObjects;
            importer.motionNodeName = request.RootNodePath;
            importer.extraExposedTransformPaths = exposedPaths;

            HumanDescription description = importer.humanDescription;
            description.human = mappings.Select(mapping => new HumanBone
            {
                humanName = mapping.HumanName,
                boneName = mapping.BoneName,
                limit = new HumanLimit { useDefaultValues = true },
            }).ToArray();
            importer.humanDescription = description;
        }

        private static bool TryVerifyGeneric(
            UnityRigImportRequest request,
            string[] exposedPaths,
            bool usedFallback,
            out UnityRigImportResult result,
            out string diagnosticCode)
        {
            result = null;
            diagnosticCode = "UNITY_GENERIC_RIG_VERIFY_FAILED";
            var importer = AssetImporter.GetAtPath(request.ModelAssetReference) as ModelImporter;
            if (importer == null || importer.animationType != ModelImporterAnimationType.Generic ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                importer.importAnimation || importer.preserveHierarchy != request.PreserveHierarchy ||
                importer.optimizeGameObjects != request.OptimizeGameObjects ||
                !string.Equals(importer.motionNodeName, request.RootNodePath, StringComparison.Ordinal) ||
                !exposedPaths.SequenceEqual(importer.extraExposedTransformPaths ?? Array.Empty<string>(),
                    StringComparer.Ordinal))
            {
                return false;
            }

            Avatar avatar = LoadGeneratedAvatar(request.ModelAssetReference);
            result = new UnityRigImportResult
            {
                RequestedMode = usedFallback ? UnityRigImportMode.Humanoid : UnityRigImportMode.Generic,
                AppliedMode = UnityRigImportMode.Generic,
                UsedApprovedFallback = usedFallback,
                AvatarIsValid = avatar != null && avatar.isValid,
                AvatarIsHuman = avatar != null && avatar.isHuman,
            };
            diagnosticCode = string.Empty;
            return true;
        }

        /// <summary>
        /// Loads the single Avatar generated as an FBX sub-asset. Unity 6 exposes generated
        /// avatars through the AssetDatabase rather than through ModelImporter.
        /// </summary>
        private static Avatar LoadGeneratedAvatar(string modelAssetReference)
        {
            Avatar[] avatars = AssetDatabase.LoadAllAssetsAtPath(modelAssetReference)
                .OfType<Avatar>()
                .ToArray();
            return avatars.Length == 1 ? avatars[0] : null;
        }

        private static bool TryNormalizePaths(
            IEnumerable<string> values,
            ISet<string> availablePaths,
            out string[] normalized)
        {
            normalized = Array.Empty<string>();
            if (values == null)
            {
                return false;
            }

            string[] candidates = values.ToArray();
            if (candidates.Any(value => !IsSafeTransformPath(value) || !availablePaths.Contains(value)) ||
                candidates.Distinct(StringComparer.Ordinal).Count() != candidates.Length)
            {
                return false;
            }

            normalized = candidates.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return true;
        }

        private static bool TryNormalizeMappings(
            IEnumerable<UnityHumanoidBoneMapping> values,
            IEnumerable<string> availablePaths,
            out UnityHumanoidBoneMapping[] normalized)
        {
            normalized = Array.Empty<UnityHumanoidBoneMapping>();
            if (values == null)
            {
                return false;
            }

            UnityHumanoidBoneMapping[] candidates = values.ToArray();
            var knownHumanNames = new HashSet<string>(HumanTrait.BoneName, StringComparer.Ordinal);
            var sourceBoneCounts = availablePaths
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => path.Split('/').Last())
                .GroupBy(name => name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            if (candidates.Length == 0 || candidates.Any(mapping => mapping == null ||
                !knownHumanNames.Contains(mapping.HumanName) ||
                !IsSafeBoneName(mapping.BoneName) ||
                !sourceBoneCounts.ContainsKey(mapping.BoneName) ||
                sourceBoneCounts[mapping.BoneName] != 1) ||
                candidates.Select(mapping => mapping.HumanName).Distinct(StringComparer.Ordinal).Count() !=
                    candidates.Length ||
                candidates.Select(mapping => mapping.BoneName).Distinct(StringComparer.Ordinal).Count() !=
                    candidates.Length)
            {
                return false;
            }

            normalized = candidates.OrderBy(mapping => mapping.HumanName, StringComparer.Ordinal).ToArray();
            return true;
        }

        private static bool IsSafeModelReference(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith("Assets/", StringComparison.Ordinal) &&
                value.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                value.IndexOf("/Source/", StringComparison.Ordinal) >= 0 &&
                value.IndexOf('\\') < 0 && value.IndexOf(':') < 0 &&
                value.IndexOf("/../", StringComparison.Ordinal) < 0;
        }

        private static bool IsSafeTransformPath(string value)
        {
            return !string.IsNullOrEmpty(value) && value[0] != '/' && value[value.Length - 1] != '/' &&
                value.IndexOf('\\') < 0 && value.IndexOf(':') < 0 &&
                value.IndexOf("//", StringComparison.Ordinal) < 0 &&
                value.IndexOf("..", StringComparison.Ordinal) < 0 &&
                string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
                value.All(character => !char.IsControl(character));
        }

        private static bool IsSafeBoneName(string value)
        {
            return IsSafeTransformPath(value) && value.IndexOf('/') < 0;
        }

        private static void Restore(string modelAssetReference, ImporterSnapshot snapshot)
        {
            try
            {
                var importer = AssetImporter.GetAtPath(modelAssetReference) as ModelImporter;
                if (importer != null)
                {
                    snapshot.Apply(importer);
                    importer.SaveAndReimport();
                }
            }
            catch (Exception)
            {
                // The original stable diagnostic remains authoritative. The isolated job clone is
                // retained by policy when Unity itself cannot restore an importer transaction.
            }
        }

        private sealed class ImporterSnapshot
        {
            private readonly ModelImporterAnimationType animationType;
            private readonly ModelImporterAvatarSetup avatarSetup;
            private readonly Avatar sourceAvatar;
            private readonly HumanDescription humanDescription;
            private readonly bool importAnimation;
            private readonly bool preserveHierarchy;
            private readonly bool optimizeGameObjects;
            private readonly string motionNodeName;
            private readonly string[] extraExposedTransformPaths;

            private ImporterSnapshot(ModelImporter importer)
            {
                animationType = importer.animationType;
                avatarSetup = importer.avatarSetup;
                sourceAvatar = importer.sourceAvatar;
                humanDescription = importer.humanDescription;
                importAnimation = importer.importAnimation;
                preserveHierarchy = importer.preserveHierarchy;
                optimizeGameObjects = importer.optimizeGameObjects;
                motionNodeName = importer.motionNodeName;
                extraExposedTransformPaths = importer.extraExposedTransformPaths == null
                    ? Array.Empty<string>()
                    : importer.extraExposedTransformPaths.ToArray();
            }

            internal static ImporterSnapshot Capture(ModelImporter importer)
            {
                return new ImporterSnapshot(importer);
            }

            internal void Apply(ModelImporter importer)
            {
                importer.animationType = animationType;
                importer.avatarSetup = avatarSetup;
                importer.sourceAvatar = sourceAvatar;
                importer.humanDescription = humanDescription;
                importer.importAnimation = importAnimation;
                importer.preserveHierarchy = preserveHierarchy;
                importer.optimizeGameObjects = optimizeGameObjects;
                importer.motionNodeName = motionNodeName;
                importer.extraExposedTransformPaths = extraExposedTransformPaths;
            }
        }
    }
}
