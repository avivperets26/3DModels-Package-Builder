using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PackageBuilder.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Builds the same original equipment fixture as the portable adapter and verifies its closed package.</summary>
    internal static class UnityEquipmentSetIntegration
    {
        internal const string Root = "Assets/PBEquipmentTests";
        private const string ScenePath = Root + "/Scenes/S_EquipmentSet_Overview.unity";
        private const string AssemblyPath = Root + "/Prefabs/P_EquipmentSet_Assembled.prefab";
        private const string DocumentPath = Root + "/Documentation/SET_EquipmentSet.json";
        private static readonly string[] ItemIds = { "Helmet", "Armour" };

        /// <summary>Uses production import, mesh extraction, item/assembly composition, selector and exact export boundaries.</summary>
        internal static void Run()
        {
            string fixture = Environment.GetEnvironmentVariable("PACKAGEBUILDER_EQUIPMENT_SOURCE");
            string plans = Environment.GetEnvironmentVariable("PACKAGEBUILDER_EQUIPMENT_OUTPUT");
            foreach (string folder in new[] { "Source", "Meshes", "Materials", "Textures", "Prefabs", "Scenes", "Documentation" })
            { Directory.CreateDirectory(Root + "/" + folder); }
            File.Copy(Path.Combine(fixture, "T_SharedSteel_Albedo.png"), Root + "/Textures/T_SharedSteel_Albedo.png");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_SharedSteel_Albedo.png"));
            AssetDatabase.CreateAsset(material, Root + "/Materials/M_SharedSteel.mat");
            var requests = ItemIds.Select(id => {
                var model = UnityItemPrefabIntegration.Prepare(Path.Combine(fixture, id + ".fbx"), id,
                    "source/" + id + ".fbx", Root + "/Materials/M_SharedSteel.mat", Root);
                var request = UnityItemPrefabIntegration.Item(id, model);
                request.OutputAssetReference = Root + "/Prefabs/P_" + id + ".prefab";
                return request;
            }).ToArray();
            string diagnostic;
            GameObject[] prefabs;
            Require(UnityItemPrefabGenerator.TryCreate(File.ReadAllText(Path.Combine(plans, "ownership.json")), requests, out prefabs, out diagnostic), diagnostic);

            // Character attachment targets are inspected during validation, and are not exported as customer assets.
            Directory.CreateDirectory("Assets/PBEquipmentTargets");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var character = new GameObject("Character");
            new GameObject("head").transform.SetParent(character.transform, false);
            new GameObject("body").transform.SetParent(character.transform, false);
            const string Target = "Assets/PBEquipmentTargets/Character.prefab";
            PrefabUtility.SaveAsPrefabAsset(character, Target);
            UnityEngine.Object.DestroyImmediate(character);
            GameObject assembled;
            Require(UnityAssembledSetGenerator.TryCreate(File.ReadAllText(Path.Combine(plans, "set.json")), ItemReferences(), AssemblyPath, DocumentPath,
                out assembled, out diagnostic, File.ReadAllText(Path.Combine(plans, "attachments.json")),
                new Dictionary<string, string> { { "Character", Target } }), diagnostic);

            Require(string.IsNullOrEmpty(AssetDatabase.MoveAsset("Assets/PBModelTests/Scripts", Root + "/Scripts")), "Equipment runtime relocation failed.");
            try
            {
                Scene scene;
                Require(UnityOverviewSceneComposer.TryCompose(Request(), out scene, out diagnostic), diagnostic);
                VerifySaved();
                UnityPackageExportPlan exported;
                Require(UnityPackageExporter.TryExport(new UnityPackageExportRequest { ProductRootReference = Root,
                    OutputPackagePath = Path.Combine(Directory.GetCurrentDirectory(), "PackageBuilderExports/EquipmentSet.unitypackage") }, out exported, out diagnostic), diagnostic);
                Require(exported.AssetReferences.All(path => path == Root || path.StartsWith(Root + "/", StringComparison.Ordinal)), "Equipment exported a foreign dependency.");
                Debug.Log("PACKAGEBUILDER_UNITY_EQUIPMENT_SET_PASS");
            }
            finally
            {
                Require(string.IsNullOrEmpty(AssetDatabase.MoveAsset(Root + "/Scripts", "Assets/PBModelTests/Scripts")), "Equipment runtime restore failed.");
            }
        }

        /// <summary>Reopens the exported scene and verifies exact item/assembly identities, shared references and selector behavior.</summary>
        internal static void VerifySaved()
        {
            string diagnostic;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(UnityOverviewSceneComposer.VerifyComposition(scene, Request(), out diagnostic), diagnostic);
            var controller = scene.GetRootGameObjects().Single(root => root.name == "PackageBuilderOverview").GetComponent<PackageBuilderPreviewController>();
            UnityMultiItemIntegration.VerifySelector(controller);
            var plan = JsonUtility.FromJson<UnitySetPlan>(File.ReadAllText(DocumentPath));
            Require(plan.members.Select(member => member.itemId).SequenceEqual(ItemIds) && plan.attachmentValidation == "validated",
                "Equipment assembly order or attachment evidence changed.");
            Require(UnityAssembledSetGenerator.VerifySaved(AssetDatabase.LoadAssetAtPath<GameObject>(AssemblyPath), plan, ItemReferences()), "Equipment assembly references failed.");
            Require(AssetDatabase.FindAssets("t:Prefab", new[] { Root }).Length == 3, "Equipment prefab inventory is not exact.");
            Require(AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }).Length == 2, "Equipment shared/background texture inventory changed.");
            foreach (string reference in ItemReferences())
            {
                var item = AssetDatabase.LoadAssetAtPath<GameObject>(reference);
                Require(item.GetComponentsInChildren<MeshFilter>(true).All(filter => filter.sharedMesh != null && filter.sharedMesh.vertexCount >= 8), "Equipment geometry missing.");
                Require(item.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.sharedMaterials.All(material =>
                    AssetDatabase.GetAssetPath(material) == Root + "/Materials/M_SharedSteel.mat" && material.GetTexture("_BaseMap") != null)), "Equipment shared material or texture missing.");
            }
        }

        private static string[] ItemReferences() => ItemIds.Select(id => Root + "/Prefabs/P_" + id + ".prefab").ToArray();
        private static UnityOverviewSceneCompositionRequest Request() => new UnityOverviewSceneCompositionRequest {
            AssetId = "EquipmentSet", TemplateSceneReference = "Assets/PBOverviewTemplate/OverviewTemplate.unity",
            ProductPrefabReference = ItemReferences()[0], ItemPrefabReferences = ItemReferences(),
            PreviewControllerScriptReference = Root + "/Scripts/PackageBuilderPreviewController.cs",
            OutputBackgroundMaterialReference = Root + "/Materials/M_EquipmentSet_OverviewBackground.mat",
            OutputBackgroundTextureReference = Root + "/Textures/T_EquipmentSet_OverviewBackground.png",
            OutputSceneReference = ScenePath };
        private static void Require(bool value, string message) { if (!value) { throw new InvalidOperationException(message); } }
    }
}
