using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Versioned application ownership envelope; engine code verifies supplied bindings against it.</summary>
    [Serializable]
    internal sealed class UnityItemPrefabOwnership
    {
        public int schemaVersion = 0;
        public UnityItemPrefabOwner[] items = Array.Empty<UnityItemPrefabOwner>();
    }

    /// <summary>One application-reviewed item and its ordered original logical model sources.</summary>
    [Serializable]
    internal sealed class UnityItemPrefabOwner
    {
        public string itemId = string.Empty;
        public string prefabFileName = string.Empty;
        public string[] modelSources = Array.Empty<string>();
    }

    /// <summary>Creates separate static item prefabs transactionally using the canonical single-item generator.</summary>
    internal static class UnityItemPrefabGenerator
    {
        /// <summary>
        /// Requires the application ownership envelope and exact normalized/imported bindings for every item.
        /// Import adapters must resolve PB-0802 aliases before supplying material references. Existing assets are never overwritten.
        /// </summary>
        internal static bool TryCreate(string ownershipJson, UnityPrefabRequest[] requests,
            out GameObject[] prefabs, out string diagnosticCode)
        {
            prefabs = Array.Empty<GameObject>();
            diagnosticCode = "UNITY_ITEM_PREFAB_PLAN_INVALID";
            UnityItemPrefabOwnership ownership;
            try { ownership = JsonUtility.FromJson<UnityItemPrefabOwnership>(ownershipJson); }
            catch (ArgumentException) { return false; }
            if (ownership == null || ownership.schemaVersion != 1 || ownership.items == null ||
                ownership.items.Length == 0 || requests == null || requests.Length != ownership.items.Length)
            {
                return false;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var logicalSources = new HashSet<string>(StringComparer.Ordinal);
            var importedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<UnityPrefabRequest>();
            foreach (UnityItemPrefabOwner item in ownership.items)
            {
                if (item == null || string.IsNullOrEmpty(item.itemId) || !ids.Add(item.itemId) ||
                    item.prefabFileName != "P_" + item.itemId + ".prefab" || item.modelSources == null || item.modelSources.Length == 0)
                {
                    return false;
                }

                UnityPrefabRequest[] matches = requests.Where(request => request != null && request.AssetId == item.itemId).ToArray();
                if (matches.Length != 1) { return false; }
                UnityPrefabRequest request = matches[0];
                if (!UnityPrefabGenerator.IsSafeAssetReference(request.OutputAssetReference)) { return false; }
                if (string.IsNullOrEmpty(request.OutputAssetReference) ||
                    !request.OutputAssetReference.EndsWith("/Prefabs/" + item.prefabFileName, StringComparison.Ordinal) ||
                    !outputs.Add(request.OutputAssetReference) ||
                    File.Exists(request.OutputAssetReference) || File.Exists(request.OutputAssetReference + ".meta") ||
                    AssetDatabase.LoadMainAssetAtPath(request.OutputAssetReference) != null)
                {
                    diagnosticCode = "UNITY_ITEM_PREFAB_OUTPUT_COLLISION";
                    return false;
                }

                UnityPrefabModelRequest[] models = UnityPrefabGenerator.GetModels(request);
                if (models.Length != item.modelSources.Length) { return false; }
                for (int index = 0; index < models.Length; index++)
                {
                    UnityPrefabModelRequest model = models[index];
                    if (model == null || string.IsNullOrEmpty(item.modelSources[index]) ||
                        model.LogicalSourceReference != item.modelSources[index] ||
                        !logicalSources.Add(model.LogicalSourceReference) ||
                        !UnityPrefabGenerator.IsSafeAssetReference(model.SourceModelReference) || !importedSources.Add(model.SourceModelReference))
                    {
                        diagnosticCode = "UNITY_ITEM_PREFAB_OWNERSHIP_MISMATCH";
                        return false;
                    }
                }

                ordered.Add(request);
            }

            var created = new List<string>();
            var results = new List<GameObject>();
            try
            {
                foreach (UnityPrefabRequest request in ordered.OrderBy(item => item.AssetId, StringComparer.Ordinal))
                {
                    GameObject prefab;
                    if (!UnityPrefabGenerator.TryCreate(request, out prefab, out diagnosticCode)) { return false; }
                    created.Add(request.OutputAssetReference);
                    results.Add(prefab);
                }

                prefabs = results.ToArray();
                diagnosticCode = string.Empty;
                return true;
            }
            finally
            {
                if (prefabs.Length == 0)
                {
                    foreach (string path in created) { AssetDatabase.DeleteAsset(path); }
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
