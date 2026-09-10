using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Exercises the real collection export and bounds-aware overview with generated PB-0803 items.</summary>
    internal static class UnityMultiItemIntegration
    {
        private const string Root = UnityItemPrefabIntegration.Root;

        /// <summary>Uses one runtime script identity across isolated fixture products; exports while product-local.</summary>
        internal static void Run()
        {
            foreach (string folder in new[] { "Scenes", "Textures", "Documentation" }) { Directory.CreateDirectory(Root + "/" + folder); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string moved = AssetDatabase.MoveAsset("Assets/PBModelTests/Scripts", Root + "/Scripts");
            Require(string.IsNullOrEmpty(moved), moved);
            try
            {
                string json = File.ReadAllText(Environment.GetEnvironmentVariable("PACKAGEBUILDER_COLLECTION_PLAN")).Trim();
                var plan = JsonUtility.FromJson<UnityCollectionPlan>(json);
                var request = Request(plan.items.Select(item => Root + "/Prefabs/" + item.prefabFileName).ToArray());
                string[] original = request.ItemPrefabReferences.Select(File.ReadAllText).ToArray();
                string diagnostic;
                Scene scene;
                request.ItemGap = -1;
                Require(!UnityOverviewSceneComposer.TryCompose(request, out scene, out diagnostic) &&
                    !File.Exists(request.OutputSceneReference) && !File.Exists(request.OutputBackgroundMaterialReference), "Invalid layout retained outputs.");
                request.ItemGap = 0.25;
                Require(UnityOverviewSceneComposer.TryCompose(request, out scene, out diagnostic), diagnostic);
                VerifySaved();
                scene = SceneManager.GetActiveScene();
                var target = scene.GetRootGameObjects().Single(root => root.name == "PackageBuilderOverview").transform.Find("PreviewTarget");
                Vector3 firstPosition = target.GetChild(0).localPosition;
                target.GetChild(0).localPosition = target.GetChild(1).localPosition;
                Require(!UnityOverviewSceneComposer.VerifyComposition(scene, request, out diagnostic), "Overlapping layout was accepted.");
                target.GetChild(0).localPosition = firstPosition;
                Require(original.SequenceEqual(request.ItemPrefabReferences.Select(File.ReadAllText)), "Layout modified source prefabs.");
                File.WriteAllText(Root + "/Documentation/COLLECTION_ExampleCollection.json", json);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var export = new UnityPackageExportRequest { ProductRootReference = Root,
                    OutputPackagePath = Environment.GetEnvironmentVariable("PACKAGEBUILDER_COLLECTION_PACKAGE_OUTPUT") };
                UnityPackageExportPlan exported;
                Require(!UnityCollectionPackageFlow.TryExport(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"), export, out exported, out diagnostic), "Unknown collection version accepted.");
                var extra = new GameObject("P_Combined");
                PrefabUtility.SaveAsPrefabAsset(extra, Root + "/Prefabs/P_Combined.prefab");
                UnityEngine.Object.DestroyImmediate(extra);
                Require(!UnityCollectionPackageFlow.TryExport(json, export, out exported, out diagnostic) &&
                    diagnostic == "UNITY_COLLECTION_PREFAB_INVENTORY_INVALID" && !File.Exists(export.OutputPackagePath), "Combined-prefab rejection failed: " + diagnostic);
                AssetDatabase.DeleteAsset(Root + "/Prefabs/P_Combined.prefab");
                File.WriteAllText(Root + "/Source/misplaced.json", "{}");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Require(!UnityCollectionPackageFlow.TryExport(json, export, out exported, out diagnostic) &&
                    diagnostic == "UNITY_VALIDATION_PATH_INVALID", "Misplaced JSON metadata exported.");
                AssetDatabase.DeleteAsset(Root + "/Source/misplaced.json");
                Require(UnityCollectionPackageFlow.TryExport(json, export, out exported, out diagnostic), diagnostic);
                Require(!UnityCollectionPackageFlow.TryExport(json, export, out exported, out diagnostic) &&
                    diagnostic == "UNITY_PACKAGE_EXPORT_OUTPUT_COLLISION", "Collection output overwritten.");
                Debug.Log("PACKAGEBUILDER_UNITY_MULTI_ITEM_PASS");
            }
            finally
            {
                Require(string.IsNullOrEmpty(AssetDatabase.MoveAsset(Root + "/Scripts", "Assets/PBModelTests/Scripts")), "Fixture runtime scripts could not be restored.");
            }
        }

        /// <summary>Verifies independent prefab identity, declared order, spacing and framing after fresh package import.</summary>
        internal static void VerifySaved()
        {
            UnityItemPrefabIntegration.VerifySaved();
            var request = Request(new[] { Root + "/Prefabs/P_Zed.prefab", Root + "/Prefabs/P_Alpha.prefab" });
            Scene scene = EditorSceneManager.OpenScene(request.OutputSceneReference, OpenSceneMode.Single);
            string diagnostic;
            Require(UnityOverviewSceneComposer.VerifyComposition(scene, request, out diagnostic), diagnostic);
            var controller = scene.GetRootGameObjects().Single(root => root.name == "PackageBuilderOverview")
                .GetComponent<PackageBuilder.Preview.PackageBuilderPreviewController>();
            VerifySelector(controller);
            Bounds bounds;
            Require(controller.TryGetProductBounds(out bounds), "Collection bounds unavailable.");
            foreach (float x in new[] { bounds.min.x, bounds.max.x })
            foreach (float y in new[] { bounds.min.y, bounds.max.y })
            foreach (float z in new[] { bounds.min.z, bounds.max.z })
            {
                Vector3 point = controller.PreviewCamera.WorldToViewportPoint(new Vector3(x, y, z));
                Require(point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1, "Overview cropped collection bounds.");
            }
        }

        /// <summary>Exercises serialized selection after reopen/reimport, including empty/single boundaries and source immutability.</summary>
        internal static void VerifySelector(PackageBuilder.Preview.PackageBuilderPreviewController controller)
        {
            var selector = controller.ItemSelector;
            Require(selector != null && selector.Count == controller.PreviewTarget.childCount && selector.SelectedIndex == -1,
                "Selector references or overview default were not serialized.");
            GameObject[] items = Enumerable.Range(0, selector.Count).Select(i => controller.PreviewTarget.GetChild(i).gameObject).ToArray();
            string[] paths = items.Select(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot).ToArray();
            string[] bytes = paths.Select(File.ReadAllText).ToArray();
            Vector3[] positions = items.Select(item => item.transform.localPosition).ToArray();
            selector.Next();
            Require(selector.SelectedIndex == 0 && items[0].activeSelf && items.Skip(1).All(item => !item.activeSelf), "Next did not select only the first item.");
            selector.Previous();
            Require(selector.SelectedIndex == items.Length - 1, "Previous did not wrap.");
            Require(selector.Select(0) && !selector.Select(-1) && !selector.Select(items.Length) && selector.SelectedIndex == 0,
                "Direct selection or invalid-index rejection failed.");
            Require(selector.CurrentName == items[0].name, "Current item name lost declaration order.");
            selector.ShowAll();
            Require(items.All(item => item.activeSelf), "Overview failed to restore all items.");
            var input = new Event { type = EventType.KeyDown, keyCode = KeyCode.Space };
            Require(selector.HandleKeyboard(input, PackageBuilder.Preview.PackageBuilderItemSelector.NextControl) && selector.SelectedIndex == 0,
                "Focused Next keyboard activation failed.");
            input = new Event { type = EventType.KeyDown, keyCode = KeyCode.End };
            Require(selector.HandleKeyboard(input, PackageBuilder.Preview.PackageBuilderItemSelector.SelectControl) && selector.SelectedIndex == items.Length - 1,
                "Direct picker keyboard boundary failed.");
            input = new Event { type = EventType.KeyDown, keyCode = KeyCode.Return };
            Require(selector.HandleKeyboard(input, PackageBuilder.Preview.PackageBuilderItemSelector.AllControl) && selector.SelectedIndex == -1,
                "Keyboard overview restore failed.");
            Require(bytes.SequenceEqual(paths.Select(File.ReadAllText)) && positions.SequenceEqual(items.Select(item => item.transform.localPosition)),
                "Selection changed source prefabs or layout transforms.");
            var temporary = new GameObject("SelectorBoundaryTest");
            try
            {
                var empty = temporary.AddComponent<PackageBuilder.Preview.PackageBuilderItemSelector>();
                empty.Configure(Array.Empty<GameObject>());
                empty.Next(); empty.Previous(); empty.ShowAll();
                Require(empty.Count == 0 && empty.SelectedIndex == -1 && !empty.Select(0), "Empty selector is unsafe.");
                empty.Configure(new[] { items[0] });
                empty.Previous(); empty.Next();
                Require(empty.SelectedIndex == 0 && items[0].activeSelf, "Single-item selector failed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); selector.ShowAll(); }
        }

        private static UnityOverviewSceneCompositionRequest Request(string[] items) => new UnityOverviewSceneCompositionRequest {
            AssetId = "ExampleCollection", TemplateSceneReference = "Assets/PBOverviewTemplate/OverviewTemplate.unity",
            ProductPrefabReference = items[0], ItemPrefabReferences = items,
            PreviewControllerScriptReference = Root + "/Scripts/PackageBuilderPreviewController.cs",
            OutputBackgroundMaterialReference = Root + "/Materials/M_ExampleCollection_OverviewBackground.mat",
            OutputBackgroundTextureReference = Root + "/Textures/T_ExampleCollection_OverviewBackground.png",
            OutputSceneReference = Root + "/Scenes/S_ExampleCollection_Overview.unity" };

        private static void Require(bool value, string message) { if (!value) { throw new InvalidOperationException(message); } }
    }
}
