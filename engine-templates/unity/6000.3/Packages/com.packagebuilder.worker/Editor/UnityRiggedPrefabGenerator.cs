using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Defines the case-2 rigged prefab and skeleton-metadata output plan.</summary>
    internal sealed class UnityRiggedPrefabRequest
    {
        internal string AssetId { get; set; }

        internal string SourceModelReference { get; set; }

        internal string OutputPrefabReference { get; set; }

        internal string OutputSkeletonMetadataReference { get; set; }

        internal int AllowedMaximumInfluences { get; set; } = 4;
    }

    /// <summary>Creates a skinned case-2 prefab while deliberately producing no clips or controller.</summary>
    internal static class UnityRiggedPrefabGenerator
    {
        /// <summary>Creates and verifies one reset rigged prefab plus deterministic JSON metadata.</summary>
        internal static bool TryCreate(
            UnityRiggedPrefabRequest request,
            out GameObject prefabAsset,
            out UnitySkinSkeletonReport report,
            out string diagnosticCode)
        {
            prefabAsset = null;
            report = null;
            diagnosticCode = "UNITY_RIGGED_PREFAB_INVALID";
            if (!TryValidate(request))
            {
                return false;
            }

            GameObject root = null;
            GameObject model = null;
            try
            {
                if (!UnityPrefabHierarchyUtility.TryCreate(
                    request.AssetId, request.SourceModelReference, out root, out model))
                {
                    diagnosticCode = "UNITY_RIGGED_PREFAB_INSTANTIATION_FAILED";
                    return false;
                }

                report = UnitySkinSkeletonValidator.Validate(model, request.AllowedMaximumInfluences);
                if (!report.IsValid)
                {
                    diagnosticCode = report.Findings[0].Code;
                    return false;
                }

                RemoveEmptyAnimationComponents(model);
                bool saved;
                PrefabUtility.SaveAsPrefabAsset(root, request.OutputPrefabReference, out saved);
                if (!saved)
                {
                    diagnosticCode = "UNITY_RIGGED_PREFAB_CREATE_FAILED";
                    return false;
                }

                if (!TryWriteMetadata(request, report))
                {
                    diagnosticCode = "UNITY_RIGGED_PREFAB_METADATA_FAILED";
                    return false;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(request.OutputPrefabReference,
                    ImportAssetOptions.ForceSynchronousImport);
                prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(request.OutputPrefabReference);
                if (!Verify(request, prefabAsset, out report, out diagnosticCode))
                {
                    prefabAsset = null;
                    return false;
                }

                diagnosticCode = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException ||
                exception is InvalidOperationException || exception is IOException ||
                exception is UnityException)
            {
                diagnosticCode = "UNITY_RIGGED_PREFAB_CREATE_FAILED";
                return false;
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
                if (prefabAsset == null)
                {
                    AssetDatabase.DeleteAsset(request.OutputPrefabReference);
                    AssetDatabase.DeleteAsset(request.OutputSkeletonMetadataReference);
                }
            }
        }

        private static bool TryValidate(UnityRiggedPrefabRequest request)
        {
            return request != null && UnityAssetNameValidator.IsProductFolder(request.AssetId) &&
                UnityPrefabHierarchyUtility.IsSafeReference(
                    request.SourceModelReference, "/Source/", ".fbx") &&
                UnityPrefabHierarchyUtility.IsSafeReference(
                    request.OutputPrefabReference, "/Prefabs/P_", ".prefab") &&
                request.OutputPrefabReference.EndsWith("/P_" + request.AssetId + ".prefab",
                    StringComparison.Ordinal) &&
                UnityPrefabHierarchyUtility.IsSafeReference(
                    request.OutputSkeletonMetadataReference, "/Documentation/SKEL_", ".json") &&
                request.OutputSkeletonMetadataReference.EndsWith(
                    "/SKEL_" + request.AssetId + ".json", StringComparison.Ordinal) &&
                request.AllowedMaximumInfluences > 0 && request.AllowedMaximumInfluences <= 255 &&
                AssetDatabase.LoadMainAssetAtPath(request.OutputPrefabReference) == null &&
                AssetDatabase.LoadMainAssetAtPath(request.OutputSkeletonMetadataReference) == null;
        }

        private static bool TryWriteMetadata(
            UnityRiggedPrefabRequest request,
            UnitySkinSkeletonReport report)
        {
            var metadata = new SkeletonMetadata
            {
                schemaVersion = 1,
                assetId = request.AssetId,
                rigType = "generic",
                skinnedRendererCount = report.RendererCount,
                boneCount = report.UniqueBoneCount,
                maximumInfluences = report.MaximumInfluences,
                hasAnimationClips = false,
            };
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string physicalPath = Path.Combine(projectPath,
                request.OutputSkeletonMetadataReference.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath));
            File.WriteAllText(physicalPath, JsonUtility.ToJson(metadata, true) + "\n",
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(request.OutputSkeletonMetadataReference,
                ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(request.OutputSkeletonMetadataReference) != null;
        }

        private static bool Verify(
            UnityRiggedPrefabRequest request,
            GameObject prefab,
            out UnitySkinSkeletonReport report,
            out string diagnosticCode)
        {
            report = null;
            diagnosticCode = "UNITY_RIGGED_PREFAB_VERIFY_FAILED";
            if (prefab == null || prefab.name != "P_" + request.AssetId ||
                prefab.transform.childCount != 1 ||
                !UnityPrefabHierarchyUtility.IsReset(prefab.transform))
            {
                return false;
            }

            Transform model = prefab.transform.GetChild(0);
            if (model.name != "P_Model" || !UnityPrefabHierarchyUtility.IsReset(model) ||
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) != 0)
            {
                return false;
            }

            report = UnitySkinSkeletonValidator.Validate(model.gameObject,
                request.AllowedMaximumInfluences);
            if (!report.IsValid || model.GetComponentsInChildren<Animation>(true).Length != 0 ||
                model.GetComponentsInChildren<Animator>(true).Any(animator =>
                    animator.runtimeAnimatorController != null))
            {
                return false;
            }

            string productRoot = request.OutputPrefabReference.Substring(0,
                request.OutputPrefabReference.IndexOf("/Prefabs/", StringComparison.Ordinal));
            if (AssetDatabase.FindAssets("t:AnimationClip", new[] { productRoot }).Length != 0 ||
                AssetDatabase.FindAssets("t:AnimatorController", new[] { productRoot }).Length != 0)
            {
                diagnosticCode = "UNITY_RIGGED_PREFAB_EMPTY_ANIMATION_OUTPUT";
                return false;
            }

            diagnosticCode = string.Empty;
            return true;
        }

        private static void RemoveEmptyAnimationComponents(GameObject model)
        {
            foreach (Animation animation in model.GetComponentsInChildren<Animation>(true))
            {
                UnityEngine.Object.DestroyImmediate(animation);
            }
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController == null)
                {
                    UnityEngine.Object.DestroyImmediate(animator);
                }
            }
        }

        [Serializable]
        private sealed class SkeletonMetadata
        {
            public int schemaVersion;
            public string assetId;
            public string rigType;
            public int skinnedRendererCount;
            public int boneCount;
            public int maximumInfluences;
            public bool hasAnimationClips;
        }
    }
}
