using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Versioned collection inventory intent; order is supplied by the application.</summary>
    [Serializable]
    internal sealed class UnityCollectionPlan
    {
        public int schemaVersion = 0;
        public string productId = string.Empty;
        public UnityItemPrefabOwner[] items = Array.Empty<UnityItemPrefabOwner>();
    }

    /// <summary>Exports one dependency-closed collection while enforcing separate named item outputs.</summary>
    internal static class UnityCollectionPackageFlow
    {
        /// <summary>Requires the declared independent prefab inventory and current product validation before export.</summary>
        internal static bool TryExport(string json, UnityPackageExportRequest request, out UnityPackageExportPlan exported, out string diagnostic)
        {
            exported = null;
            diagnostic = "UNITY_COLLECTION_PLAN_INVALID";
            UnityCollectionPlan collection;
            try { collection = JsonUtility.FromJson<UnityCollectionPlan>(json); }
            catch (ArgumentException) { return false; }
            if (collection == null || collection.schemaVersion != 1 || !UnityAssetNameValidator.IsProductFolder(collection.productId) || collection.items == null || collection.items.Length == 0 || collection.items.Length > 10000 || request == null ||
                collection.items.Any(item => item == null || !UnityAssetNameValidator.IsProductFolder(item.itemId) || item.prefabFileName != "P_" + item.itemId + ".prefab") ||
                collection.items.Select(item => item.itemId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != collection.items.Length) { return false; }
            UnityPackageExportPlan inventory;
            if (!UnityPackageExporter.TryCreatePlan(request, out inventory, out diagnostic)) { return false; }
            string[] expected = collection.items.Select(item => request.ProductRootReference + "/Prefabs/" + item.prefabFileName).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] actual = inventory.AssetReferences.Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected)) { diagnostic = "UNITY_COLLECTION_PREFAB_INVENTORY_INVALID"; return false; }
            foreach (string path in expected)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.name + ".prefab" != System.IO.Path.GetFileName(path)) { diagnostic = "UNITY_COLLECTION_ITEM_INVALID"; return false; }
            }
            UnityPackageValidationReport report = UnityPackageValidator.Validate(new UnityPackageValidationRequest {
                ProductRootReference = request.ProductRootReference, ExpectedAssetReferences = inventory.AssetReferences });
            if (!report.IsSuccessful) { diagnostic = report.Findings[0].Code; return false; }
            return UnityPackageExporter.TryExport(request, out exported, out diagnostic);
        }
    }
}
