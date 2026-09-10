using System;
using System.IO;
using System.Linq;
using PackageBuilder.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Measured evidence retained outside disposable collection projects.</summary>
    [Serializable]
    public sealed class UnityCollectionItemMetric
    {
        public string itemId;
        public string prefabGuid;
        public int triangles;
        public Vector3 dimensions;
        public Vector3 overviewPosition;
        public int materials;
        public int textures;
    }

    /// <summary>Verifies the twelve-item golden collection using existing production generation and export boundaries.</summary>
    internal static class UnityTwelveItemCollectionIntegration
    {
        internal const string Root = "Assets/PBTwelveTests";
        internal const string ScenePath = Root + "/Scenes/S_TwelveColumns_Overview.unity";
        private const string Document = Root + "/Documentation/COLLECTION_TwelveColumns.json";
        private const string MaterialPath = Root + "/Materials/M_SharedStone.mat";
        private static readonly string[] Ids = { "Column12", "Column01", "Column10", "Column02", "Column11", "Column03",
            "Column09", "Column04", "Column08", "Column05", "Column07", "Column06" };

        /// <summary>Imports distinct FBXs, creates independent prefabs and exports an exact dependency-closed collection.</summary>
        internal static void Run()
        {
            string fixture = Environment.GetEnvironmentVariable("PACKAGEBUILDER_TWELVE_SOURCE");
            string plans = Environment.GetEnvironmentVariable("PACKAGEBUILDER_TWELVE_OUTPUT");
            foreach (string folder in new[] { "Source", "Meshes", "Materials", "Textures", "Prefabs", "Scenes", "Documentation" })
            { Directory.CreateDirectory(Root + "/" + folder); }
            File.Copy(Path.Combine(fixture, "T_SharedStone_Albedo.png"), Root + "/Textures/T_SharedStone_Albedo.png");
            File.Copy(Path.Combine(plans, "collection.json"), Document);
            File.Copy(Path.Combine(plans, "INVENTORY.md"), Root + "/Documentation/INVENTORY.md");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_SharedStone_Albedo.png"));
            AssetDatabase.CreateAsset(material, MaterialPath);
            var requests = Ids.Select(id => {
                var model = UnityItemPrefabIntegration.Prepare(Path.Combine(fixture, id + ".fbx"), id, "source/" + id + ".fbx", MaterialPath, Root);
                var request = UnityItemPrefabIntegration.Item(id, model);
                request.OutputAssetReference = Root + "/Prefabs/P_" + id + ".prefab";
                return request;
            }).ToArray();
            string diagnostic;
            GameObject[] prefabs;
            Require(UnityItemPrefabGenerator.TryCreate(File.ReadAllText(Path.Combine(plans, "ownership.json")), requests, out prefabs, out diagnostic), diagnostic);
            Require(string.IsNullOrEmpty(AssetDatabase.MoveAsset("Assets/PBModelTests/Scripts", Root + "/Scripts")), "Collection runtime relocation failed.");
            try
            {
                Scene scene;
                Require(UnityOverviewSceneComposer.TryCompose(Request(), out scene, out diagnostic), diagnostic);
                VerifySaved();
                UnityPackageExportPlan exported;
                Require(UnityCollectionPackageFlow.TryExport(File.ReadAllText(Document), new UnityPackageExportRequest {
                    ProductRootReference = Root, OutputPackagePath = Path.Combine(Directory.GetCurrentDirectory(), "PackageBuilderExports/TwelveColumns.unitypackage")
                }, out exported, out diagnostic), diagnostic);
                Require(exported.AssetReferences.All(path => path == Root || path.StartsWith(Root + "/", StringComparison.Ordinal)), "Foreign collection dependency exported.");
                File.WriteAllLines(Environment.GetEnvironmentVariable("PACKAGEBUILDER_TWELVE_ASSETS"), exported.AssetReferences);
                Debug.Log("PACKAGEBUILDER_UNITY_TWELVE_COLLECTION_PASS");
            }
            finally
            {
                Require(string.IsNullOrEmpty(AssetDatabase.MoveAsset(Root + "/Scripts", "Assets/PBModelTests/Scripts")), "Collection runtime restore failed.");
            }
        }

        /// <summary>Checks all identities, geometry metrics, shared references, layout and selection from saved package assets.</summary>
        internal static UnityCollectionItemMetric[] VerifySaved()
        {
            var plan = JsonUtility.FromJson<UnityCollectionPlan>(File.ReadAllText(Document));
            Require(plan.items.Select(item => item.itemId).SequenceEqual(Ids), "Collection declaration order changed.");
            string[] references = ItemReferences();
            Require(AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/Prefabs" }).Length == 12 &&
                references.Select(AssetDatabase.AssetPathToGUID).Distinct().Count() == 12, "Missing, duplicate or combined prefab.");
            var meshPaths = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var metrics = new System.Collections.Generic.List<UnityCollectionItemMetric>();
            foreach (string id in Ids)
            {
                var item = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/P_" + id + ".prefab");
                Require(item != null && item.name == "P_" + id && UnityPrefabHierarchyUtility.IsReset(item.transform), "Item root identity changed.");
                var filters = item.GetComponentsInChildren<MeshFilter>(true);
                int triangles = 4 * (int.Parse(id.Substring(6), System.Globalization.CultureInfo.InvariantCulture) + 4) - 4;
                Require(filters.Length == 1 && filters[0].sharedMesh != null && filters[0].sharedMesh.triangles.Length / 3 == triangles,
                    "Missing or incorrect geometry for " + id);
                string meshPath = AssetDatabase.GetAssetPath(filters[0].sharedMesh);
                Require(meshPath.StartsWith(Root + "/Meshes/MS_" + id, StringComparison.Ordinal) && meshPaths.Add(meshPath), "Cross-item mesh alias.");
                var renderers = item.GetComponentsInChildren<Renderer>(true);
                Require(renderers.Length == 1 && renderers[0].sharedMaterials.Length == 1 &&
                    AssetDatabase.GetAssetPath(renderers[0].sharedMaterial) == MaterialPath &&
                    AssetDatabase.GetAssetPath(renderers[0].sharedMaterial.GetTexture("_BaseMap")) == Root + "/Textures/T_SharedStone_Albedo.png",
                    "Shared material or texture changed.");
                Require(item.GetComponentsInChildren<Transform>(true).All(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0), "Missing item script.");
                metrics.Add(new UnityCollectionItemMetric { itemId = id,
                    prefabGuid = AssetDatabase.AssetPathToGUID(Root + "/Prefabs/P_" + id + ".prefab"),
                    triangles = filters[0].sharedMesh.triangles.Length / 3, dimensions = renderers[0].bounds.size,
                    materials = renderers[0].sharedMaterials.Length, textures = 1 });
            }
            Require(AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials" }).Length == 2 &&
                AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }).Length == 2, "Shared/background inventory is not exact.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            string diagnostic;
            Require(UnityOverviewSceneComposer.VerifyComposition(scene, Request(), out diagnostic), diagnostic);
            var controller = scene.GetRootGameObjects().Single(root => root.name == "PackageBuilderOverview").GetComponent<PackageBuilderPreviewController>();
            UnityMultiItemIntegration.VerifySelector(controller);
            var selector = controller.ItemSelector;
            for (int index = 0; index < 12; index++)
            {
                Require(selector.Select(index) && selector.CurrentName == "P_" + Ids[index], "Direct item identity mismatch.");
                for (int other = 0; other < 12; other++)
                { Require(controller.PreviewTarget.GetChild(other).gameObject.activeSelf == (other == index), "Selection leaked another item."); }
            }
            selector.ShowAll();
            for (int index = 0; index < metrics.Count; index++)
            { metrics[index].overviewPosition = controller.PreviewTarget.GetChild(index).localPosition; }
            Bounds bounds;
            Require(controller.TryGetProductBounds(out bounds), "Collection has no measured bounds.");
            foreach (float x in new[] { bounds.min.x, bounds.max.x })
            foreach (float y in new[] { bounds.min.y, bounds.max.y })
            foreach (float z in new[] { bounds.min.z, bounds.max.z })
            {
                Vector3 point = controller.PreviewCamera.WorldToViewportPoint(new Vector3(x, y, z));
                Require(point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1, "Twelve-item overview is cropped.");
            }
            return metrics.ToArray();
        }

        private static string[] ItemReferences() => Ids.Select(id => Root + "/Prefabs/P_" + id + ".prefab").ToArray();
        private static UnityOverviewSceneCompositionRequest Request() => new UnityOverviewSceneCompositionRequest {
            AssetId = "TwelveColumns", TemplateSceneReference = "Assets/PBOverviewTemplate/OverviewTemplate.unity",
            ProductPrefabReference = ItemReferences()[0], ItemPrefabReferences = ItemReferences(),
            PreviewControllerScriptReference = Root + "/Scripts/PackageBuilderPreviewController.cs",
            OutputBackgroundMaterialReference = Root + "/Materials/M_TwelveColumns_OverviewBackground.mat",
            OutputBackgroundTextureReference = Root + "/Textures/T_TwelveColumns_OverviewBackground.png",
            OutputSceneReference = ScenePath };
        private static void Require(bool value, string message) { if (!value) { throw new InvalidOperationException(message); } }
    }
}
