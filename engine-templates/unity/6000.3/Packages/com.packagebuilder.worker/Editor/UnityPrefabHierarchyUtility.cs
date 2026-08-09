using System;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Shares the canonical reset product-root and model-child prefab hierarchy.</summary>
    internal static class UnityPrefabHierarchyUtility
    {
        /// <summary>Instantiates a source model beneath reset P_AssetId and P_Model transforms.</summary>
        internal static bool TryCreate(
            string assetId,
            string sourceModelReference,
            out GameObject root,
            out GameObject model)
        {
            root = null;
            model = null;
            if (!UnityAssetNameValidator.IsProductFolder(assetId) ||
                !IsSafeReference(sourceModelReference, "/Source/", ".fbx"))
            {
                return false;
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourceModelReference);
            if (source == null)
            {
                return false;
            }

            root = new GameObject("P_" + assetId);
            model = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (model == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                return false;
            }

            model.name = "P_Model";
            model.transform.SetParent(root.transform, false);
            ResetTransform(root.transform);
            ResetTransform(model.transform);
            return true;
        }

        /// <summary>Reports whether a project-relative asset reference is contained and well formed.</summary>
        internal static bool IsSafeReference(
            string value,
            string requiredFolder,
            string extension)
        {
            return !string.IsNullOrEmpty(value) &&
                value.StartsWith("Assets/", StringComparison.Ordinal) &&
                value.IndexOf(requiredFolder, StringComparison.Ordinal) >= 0 &&
                value.EndsWith(extension, StringComparison.OrdinalIgnoreCase) &&
                value.IndexOf('\\') < 0 && value.IndexOf(':') < 0 &&
                value.IndexOf("/../", StringComparison.Ordinal) < 0;
        }

        /// <summary>Resets a hierarchy transform without modifying any descendant deformation.</summary>
        internal static void ResetTransform(Transform value)
        {
            value.localPosition = Vector3.zero;
            value.localRotation = Quaternion.identity;
            value.localScale = Vector3.one;
        }

        /// <summary>Reports whether a transform has the canonical identity local transform.</summary>
        internal static bool IsReset(Transform value)
        {
            return value != null && value.localPosition == Vector3.zero &&
                value.localRotation == Quaternion.identity && value.localScale == Vector3.one;
        }
    }
}
