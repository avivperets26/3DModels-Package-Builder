using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Runs assembly acceptance against PB-0803 item prefabs and the application golden contract.</summary>
    internal static class UnityAssembledSetIntegration
    {
        internal const string Root = "Assets/PBSetTests";
        private const string Output = Root + "/Prefabs/P_ExampleSet_Assembled.prefab";
        private const string Document = Root + "/Documentation/SET_ExampleSet.json";
        private static readonly string[] Items = { "Assets/PBItemTests/Prefabs/P_Zed.prefab", "Assets/PBItemTests/Prefabs/P_Alpha.prefab" };

        /// <summary>Checks invalid bindings, overwrite protection, exact order/slots and source immutability.</summary>
        internal static void Run()
        {
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Documentation");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string json = File.ReadAllText(Environment.GetEnvironmentVariable("PACKAGEBUILDER_SET_PLAN")).Trim();
            string[] original = Items.Select(File.ReadAllText).ToArray();
            string[] guids = Items.Select(AssetDatabase.AssetPathToGUID).ToArray();
            GameObject prefab;
            string diagnostic;
            Require(!UnityAssembledSetGenerator.TryCreate(json, new[] { Items[0], Items[0] }, Output, Document, out prefab, out diagnostic), "Duplicate/missing item binding accepted.");
            Require(!UnityAssembledSetGenerator.TryCreate(json.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"), Items,
                Output, Document, out prefab, out diagnostic), "Unknown version accepted.");
            Require(!UnityAssembledSetGenerator.TryCreate(json, Items, "Assets/../Prefabs/P_ExampleSet_Assembled.prefab", Document,
                out prefab, out diagnostic), "Traversal output accepted.");

            // A document already on disk must survive even before Unity has imported it.
            File.WriteAllText(Document, "preserve");
            Require(!UnityAssembledSetGenerator.TryCreate(json, Items, Output, Document, out prefab, out diagnostic) &&
                diagnostic == "UNITY_SET_OUTPUT_COLLISION" && File.ReadAllText(Document) == "preserve" && !File.Exists(Output),
                "Document collision modified existing data or left a prefab.");
            File.Delete(Document);

            // A damaged nested mesh in an otherwise correctly named input must block assembly.
            string badFolder = Root + "/Bad/Prefabs";
            Directory.CreateDirectory(badFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            GameObject damaged = PrefabUtility.LoadPrefabContents(Items[1]);
            string bad = badFolder + "/P_Alpha.prefab";
            try
            {
                damaged.GetComponentInChildren<MeshFilter>(true).sharedMesh = null;
                PrefabUtility.SaveAsPrefabAsset(damaged, bad);
            }
            finally { PrefabUtility.UnloadPrefabContents(damaged); }
            Require(!UnityAssembledSetGenerator.TryCreate(json, new[] { Items[0], bad }, Output, Document, out prefab, out diagnostic) &&
                diagnostic == "UNITY_SET_ITEM_REFERENCE_INVALID", "Broken input mesh accepted.");
            AssetDatabase.DeleteAsset(Root + "/Bad");
            Require(UnityAssembledSetGenerator.TryCreate(json, Items.Reverse().ToArray(), Output, Document, out prefab, out diagnostic), diagnostic);
            VerifySaved();
            string setGuid = AssetDatabase.AssetPathToGUID(Output);
            Require(!UnityAssembledSetGenerator.TryCreate(json, Items, Output, Document, out prefab, out diagnostic) &&
                diagnostic == "UNITY_SET_OUTPUT_COLLISION" && setGuid == AssetDatabase.AssetPathToGUID(Output), "Existing set overwritten.");
            Require(original.SequenceEqual(Items.Select(File.ReadAllText)) && guids.SequenceEqual(Items.Select(AssetDatabase.AssetPathToGUID)),
                "Assembly modified the individual source prefabs.");
            UnityItemPrefabIntegration.VerifySaved();
            Debug.Log("PACKAGEBUILDER_UNITY_ASSEMBLED_SET_PASS");
        }

        /// <summary>Rechecks nested references, order, slots and compatibility after isolated package import.</summary>
        internal static void VerifySaved()
        {
            var document = AssetDatabase.LoadAssetAtPath<TextAsset>(Document);
            Require(document != null, "Missing set compatibility document.");
            UnitySetPlan plan = JsonUtility.FromJson<UnitySetPlan>(document.text);
            Require(plan.schemaVersion == 1 && plan.members.Length == 2 && plan.members[0].itemId == "Zed" &&
                plan.members[0].slot == "head" && plan.members[1].itemId == "Alpha" && plan.members[1].slot == "body" &&
                plan.compatibility.Length == 1 && plan.compatibility[0].key == "Generation" && plan.compatibility[0].value == "One" &&
                plan.attachmentValidation == "not-performed", "Set declaration/compatibility changed.");
            Require(UnityAssembledSetGenerator.VerifySaved(AssetDatabase.LoadAssetAtPath<GameObject>(Output), plan, Items),
                "Saved set order, slots, transforms or references are invalid.");
            Require(AssetDatabase.FindAssets("t:Prefab", new[] { Root }).Length == 1, "Extra assembly output remains.");
        }

        private static void Require(bool value, string message) { if (!value) { throw new InvalidOperationException(message); } }
    }
}
