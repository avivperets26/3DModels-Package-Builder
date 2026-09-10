using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Real imported-model acceptance checks, also reused after isolated package reimport.</summary>
    internal static class UnityItemPrefabIntegration
    {
        internal const string Root = "Assets/PBItemTests";

        /// <summary>Exercises ownership, multi-file items, material separation, collisions and transactional cleanup.</summary>
        internal static void Run(string existingSource)
        {
            foreach (string folder in new[] { "Source", "Meshes", "Materials", "Prefabs" })
            {
                Directory.CreateDirectory(Root + "/" + folder);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var shared = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            shared.SetColor("_BaseColor", Color.blue);
            var distinct = new Material(shared);
            distinct.SetColor("_BaseColor", Color.red);
            string sharedPath = Root + "/Materials/M_Shared.mat";
            string distinctPath = Root + "/Materials/M_Distinct.mat";
            AssetDatabase.CreateAsset(shared, sharedPath);
            AssetDatabase.CreateAsset(distinct, distinctPath);

            UnityPrefabModelRequest first = Prepare(existingSource, "AlphaA", "models/alpha-a.fbx", sharedPath);
            UnityPrefabModelRequest second = Prepare(existingSource, "AlphaB", "models/alpha-b.fbx", sharedPath);
            UnityPrefabModelRequest third = Prepare(existingSource, "Zed", "models/zed.fbx", distinctPath);
            UnityPrefabRequest alpha = Item("Alpha", first, second);
            UnityPrefabRequest zed = Item("Zed", third);
            string json = File.ReadAllText(Environment.GetEnvironmentVariable("PACKAGEBUILDER_ITEM_OWNERSHIP_PLAN"));
            GameObject[] output;
            string diagnostic;

            string originalLogical = zed.LogicalSourceReference;
            zed.LogicalSourceReference = alpha.LogicalSourceReference;
            Require(!UnityItemPrefabGenerator.TryCreate(json, new[] { zed, alpha }, out output, out diagnostic) &&
                diagnostic == "UNITY_ITEM_PREFAB_OWNERSHIP_MISMATCH", "Ownership mismatch was accepted.");
            zed.LogicalSourceReference = originalLogical;

            // The later item fails after Alpha has been saved: the batch must roll Alpha back too.
            zed.ExpectedMaterialReferences = new[] { sharedPath };
            Require(!UnityItemPrefabGenerator.TryCreate(json, new[] { alpha, zed }, out output, out diagnostic),
                "Wrong per-item materials were accepted.");
            Require(!File.Exists(alpha.OutputAssetReference) && !File.Exists(zed.OutputAssetReference),
                "Failed batch retained partial prefab output.");
            zed.ExpectedMaterialReferences = new[] { distinctPath };

            UnityExtractedMeshSet originalMeshes = zed.ExtractedMeshes;
            zed.ExtractedMeshes = first.ExtractedMeshes;
            Require(!UnityItemPrefabGenerator.TryCreate(json, new[] { alpha, zed }, out output, out diagnostic) &&
                diagnostic == "UNITY_PREFAB_MESH_PLAN_INVALID", "Cross-item source mesh bindings were accepted.");
            zed.ExtractedMeshes = originalMeshes;
            Require(!File.Exists(alpha.OutputAssetReference), "Mesh failure retained a partial batch.");

            Require(!UnityItemPrefabGenerator.TryCreate(json, new[] { alpha, alpha }, out output, out diagnostic),
                "Duplicate item requests were accepted.");
            Require(!UnityItemPrefabGenerator.TryCreate(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"),
                new[] { alpha, zed }, out output, out diagnostic), "Unknown ownership version was accepted.");
            Require(UnityItemPrefabGenerator.TryCreate(json, new[] { zed, alpha }, out output, out diagnostic), diagnostic);
            Require(output.Length == 2 && output[0].name == "P_Alpha" && output[1].name == "P_Zed",
                "Item output order or names changed.");
            VerifySaved();
            string firstGuid = AssetDatabase.AssetPathToGUID(alpha.OutputAssetReference);
            Require(!UnityItemPrefabGenerator.TryCreate(json, new[] { alpha, zed }, out output, out diagnostic) &&
                diagnostic == "UNITY_ITEM_PREFAB_OUTPUT_COLLISION", "Existing outputs were overwritten.");
            Require(firstGuid == AssetDatabase.AssetPathToGUID(alpha.OutputAssetReference), "Collision changed an existing asset.");
            VerifySaved();

            UnityAssembledSetIntegration.Run();
            string package = Environment.GetEnvironmentVariable("PACKAGEBUILDER_ITEM_PACKAGE_OUTPUT");
            Require(!string.IsNullOrEmpty(package), "Missing item package output.");
            string[] assets = AssetDatabase.FindAssets(string.Empty, new[] { Root, UnityAssembledSetIntegration.Root }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !AssetDatabase.IsValidFolder(path)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            AssetDatabase.ExportPackage(assets, package, ExportPackageOptions.Default);
            Require(File.Exists(package), "Item package was not exported.");
            Debug.Log("PACKAGEBUILDER_UNITY_ITEM_PREFABS_PASS");
        }

        /// <summary>Verifies saved item assets by exact names, per-part mesh ownership, materials and reset transforms.</summary>
        internal static void VerifySaved()
        {
            string[] items = { "Alpha", "Zed" };
            string[][] parts = { new[] { "AlphaA", "AlphaB" }, new[] { "Zed" } };
            for (int index = 0; index < items.Length; index++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/P_" + items[index] + ".prefab");
                Require(prefab != null && prefab.name == "P_" + items[index] && prefab.transform.childCount == 1 &&
                    UnityPrefabHierarchyUtility.IsReset(prefab.transform), "Saved item root is invalid.");
                Transform model = prefab.transform.GetChild(0);
                Require(model.name == "P_Model" && UnityPrefabHierarchyUtility.IsReset(model), "Saved model root is invalid.");
                if (index == 0) { Require(model.childCount == 2, "Multi-source item lost a part."); }
                for (int partIndex = 0; partIndex < parts[index].Length; partIndex++)
                {
                    Transform part = index == 0 ? model.GetChild(partIndex) : model;
                    Require(UnityPrefabHierarchyUtility.IsReset(part), "Part transform is not reset.");
                    MeshFilter[] filters = part.GetComponentsInChildren<MeshFilter>(true);
                    Require(filters.Length > 0 && filters.All(filter => filter.sharedMesh != null &&
                        (AssetDatabase.GetAssetPath(filter.sharedMesh) == Root + "/Meshes/MS_" + parts[index][partIndex] + ".asset" ||
                        AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(Root + "/Meshes/MS_" + parts[index][partIndex] + "_", StringComparison.Ordinal))),
                        "Saved item has a missing or cross-item mesh.");
                    string expectedMaterial = Root + "/Materials/" + (index == 0 ? "M_Shared.mat" : "M_Distinct.mat");
                    Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
                    Require(renderers.Length > 0 && renderers.All(renderer => renderer.sharedMaterials.Length > 0 &&
                        renderer.sharedMaterials.All(material => material != null && AssetDatabase.GetAssetPath(material) == expectedMaterial)),
                        "Saved item has a missing or cross-item material.");
                }

                Require(prefab.GetComponentsInChildren<Transform>(true).All(child =>
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0), "Missing item script.");
            }

            Require(AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/Prefabs" }).Length == 2,
                "Unexpected combined or partial prefab exists.");
        }

        internal static UnityPrefabModelRequest Prepare(string original, string name, string logical, string material, string productRoot = Root)
        {
            string source = productRoot + "/Source/" + name + ".fbx";
            // Copy only FBX bytes: inherited importer remaps would hide the original material identities.
            File.Copy(original, source);
            AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceSynchronousImport);
            string[] names = AssetDatabase.LoadAllAssetsAtPath(source).OfType<Material>().Select(value => value.name)
                .Concat(AssetDatabase.LoadAssetAtPath<GameObject>(source).GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials).Select(value => value.name)).Distinct(StringComparer.Ordinal).ToArray();
            string diagnostic;
            Require(UnityStaticModelImporterPolicy.TryApply(source, 1f, true,
                names.Select(value => new UnityMaterialRemap(value, material)), out diagnostic), diagnostic);
            UnityExtractedMeshSet meshes;
            Require(UnityMeshAssetExtractor.TryExtract(source, productRoot + "/Meshes", name, out meshes, out diagnostic), diagnostic);
            return new UnityPrefabModelRequest { LogicalSourceReference = logical, SourceModelReference = source,
                ExtractedMeshes = meshes, ExpectedMaterialReferences = new[] { material } };
        }

        internal static UnityPrefabRequest Item(string id, UnityPrefabModelRequest first, params UnityPrefabModelRequest[] additional)
        {
            return new UnityPrefabRequest { AssetId = id, LogicalSourceReference = first.LogicalSourceReference,
                SourceModelReference = first.SourceModelReference, ExtractedMeshes = first.ExtractedMeshes,
                ExpectedMaterialReferences = first.ExpectedMaterialReferences, AdditionalModels = additional,
                OutputAssetReference = Root + "/Prefabs/P_" + id + ".prefab" };
        }

        private static void Require(bool value, string message)
        {
            if (!value) { throw new InvalidOperationException(message); }
        }
    }
}
