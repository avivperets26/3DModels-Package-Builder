using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Mirrors the application v1 assembly/document contract; the shared vector verifies its wire shape.</summary>
    [Serializable]
    internal sealed class UnitySetPlan
    {
        public int schemaVersion;
        public string setId;
        public string prefabFileName;
        public string documentationFileName;
        public string placement;
        public string attachmentValidation;
        public bool requireUniqueAttachmentSlots;
        public UnitySetMember[] members;
        public UnitySetCompatibility[] compatibility;
        public PackageBuilder.MultiItem.AttachmentBinding[] attachments;
    }

    /// <summary>An ordered item binding with an application-selected logical slot container.</summary>
    [Serializable]
    internal sealed class UnitySetMember
    {
        public string itemId;
        public string prefabFileName;
        public string slot;
        public string containerName;
    }

    /// <summary>Preserved compatibility claim; engine attachment compatibility is not inferred.</summary>
    [Serializable]
    internal sealed class UnitySetCompatibility
    {
        public string key;
        public string value;
    }

    /// <summary>Composes existing item prefabs as nested instances and emits the reviewed compatibility document.</summary>
    internal static class UnityAssembledSetGenerator
    {
        /// <summary>Creates only new outputs; failures clean owned outputs while preserving input prefabs and dependencies.</summary>
        internal static bool TryCreate(string json, string[] itemReferences, string output, string documentation,
            out GameObject prefab, out string diagnostic, string attachments = null, IDictionary<string, string> targets = null)
        {
            prefab = null;
            diagnostic = "UNITY_SET_PLAN_INVALID";
            UnitySetPlan plan;
            try { plan = JsonUtility.FromJson<UnitySetPlan>(json); }
            catch (ArgumentException) { return false; }
            if (plan == null || plan.schemaVersion != 1 || !UnityAssetNameValidator.IsProductFolder(plan.setId) ||
                plan.members == null || plan.members.Length == 0 || plan.compatibility == null ||
                plan.placement != "logical-slots-at-origin" || plan.attachmentValidation != "not-performed" ||
                plan.prefabFileName != "P_" + plan.setId + "_Assembled.prefab" ||
                plan.documentationFileName != "SET_" + plan.setId + ".json" ||
                !SafeOutput(output, "/Prefabs/" + plan.prefabFileName) ||
                !SafeOutput(documentation, "/Documentation/" + plan.documentationFileName) ||
                itemReferences == null || itemReferences.Length != plan.members.Length)
            {
                return false;
            }
            if (Exists(output) || Exists(documentation)) { diagnostic = "UNITY_SET_OUTPUT_COLLISION"; return false; }
            if (plan.members.Any(member => member != null && !string.IsNullOrEmpty(member.slot)) || attachments != null)
            {
                if (!UnitySetAttachmentValidator.Validate(plan, attachments, targets, out diagnostic)) { return false; }
                plan.attachmentValidation = "validated";
                plan.attachments = JsonUtility.FromJson<UnityAttachmentRequest>(attachments).bindings;
            }
            string expectedDocument = JsonUtility.ToJson(plan);
            diagnostic = "UNITY_SET_PLAN_INVALID";

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var containers = new HashSet<string>(StringComparer.Ordinal);
            var sources = new List<string>();
            foreach (UnitySetMember member in plan.members)
            {
                if (member == null || !UnityAssetNameValidator.IsProductFolder(member.itemId) || !ids.Add(member.itemId) ||
                    member.prefabFileName != "P_" + member.itemId + ".prefab" || string.IsNullOrEmpty(member.containerName) ||
                    member.containerName.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || !containers.Add(member.containerName)) { return false; }
                string[] matches = itemReferences.Where(path => UnityPrefabGenerator.IsSafeAssetReference(path) &&
                    path.EndsWith("/Prefabs/" + member.prefabFileName, StringComparison.Ordinal)).ToArray();
                if (matches.Length != 1) { diagnostic = "UNITY_SET_ITEM_BINDING_INVALID"; return false; }
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(matches[0]);
                if (source == null || source.name != "P_" + member.itemId || !PrefabUtility.IsPartOfPrefabAsset(source) ||
                    !HasReferences(source, matches[0])) { diagnostic = "UNITY_SET_ITEM_REFERENCE_INVALID"; return false; }
                sources.Add(matches[0]);
            }

            GameObject root = null;
            bool saved = false;
            bool wroteDocument = false;
            try
            {
                root = new GameObject(Path.GetFileNameWithoutExtension(plan.prefabFileName));
                UnityPrefabHierarchyUtility.ResetTransform(root.transform);
                for (int index = 0; index < plan.members.Length; index++)
                {
                    var container = new GameObject(plan.members[index].containerName);
                    container.transform.SetParent(root.transform, false);
                    UnityPrefabHierarchyUtility.ResetTransform(container.transform);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sources[index]));
                    instance.transform.SetParent(container.transform, false);
                    UnityPrefabHierarchyUtility.ResetTransform(instance.transform);
                }
                PrefabUtility.SaveAsPrefabAsset(root, output, out saved);
                if (!saved) { diagnostic = "UNITY_SET_SAVE_FAILED"; return false; }
                AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
                GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(output);
                if (!VerifySaved(candidate, plan, sources.ToArray())) { diagnostic = "UNITY_SET_VERIFY_FAILED"; return false; }
                // CreateNew prevents replacement if a document appears between preflight and emission.
                using (var stream = new FileStream(documentation, FileMode.CreateNew, FileAccess.Write))
                {
                    wroteDocument = true;
                    byte[] bytes = new UTF8Encoding(false).GetBytes(expectedDocument);
                    stream.Write(bytes, 0, bytes.Length);
                }
                AssetDatabase.ImportAsset(documentation, ImportAssetOptions.ForceSynchronousImport);
                var document = AssetDatabase.LoadAssetAtPath<TextAsset>(documentation);
                if (document == null || document.text != expectedDocument) { diagnostic = "UNITY_SET_DOCUMENT_VERIFY_FAILED"; return false; }
                AssetDatabase.SaveAssets();
                prefab = candidate;
                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is InvalidOperationException || exception is UnityException)
            {
                diagnostic = "UNITY_SET_CREATE_FAILED";
                return false;
            }
            finally
            {
                if (root != null) { UnityEngine.Object.DestroyImmediate(root); }
                if (prefab == null)
                {
                    if (saved) { AssetDatabase.DeleteAsset(output); }
                    if (wroteDocument)
                    {
                        AssetDatabase.DeleteAsset(documentation);
                        if (File.Exists(documentation)) { File.Delete(documentation); }
                    }
                }
            }
        }

        /// <summary>Checks reset order/slot containers and exact nested prefab source identities after save/reimport.</summary>
        internal static bool VerifySaved(GameObject prefab, UnitySetPlan plan, string[] sources)
        {
            if (prefab == null || prefab.name != Path.GetFileNameWithoutExtension(plan.prefabFileName) ||
                !UnityPrefabHierarchyUtility.IsReset(prefab.transform) || prefab.transform.childCount != plan.members.Length ||
                sources.Length != plan.members.Length || !HasReferences(prefab, AssetDatabase.GetAssetPath(prefab))) { return false; }
            for (int index = 0; index < plan.members.Length; index++)
            {
                Transform container = prefab.transform.GetChild(index);
                if (container.name != plan.members[index].containerName || container.childCount != 1 ||
                    !UnityPrefabHierarchyUtility.IsReset(container)) { return false; }
                Transform item = container.GetChild(0);
                if (item.name != "P_" + plan.members[index].itemId || !UnityPrefabHierarchyUtility.IsReset(item) ||
                    AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject)) != sources[index]) { return false; }
            }
            return true;
        }

        private static bool HasReferences(GameObject root, string path)
        {
            var findings = new List<UnityPackageValidationFinding>();
            UnityPackageValidator.ValidateGameObject(root, path, findings);
            return findings.Count == 0 && (root.GetComponentsInChildren<MeshFilter>(true).Length > 0 ||
                root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0);
        }

        private static bool SafeOutput(string path, string suffix) => UnityPrefabGenerator.IsSafeAssetReference(path) &&
            path.EndsWith(suffix, StringComparison.Ordinal) && AssetDatabase.IsValidFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

        private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") ||
            AssetDatabase.LoadMainAssetAtPath(path) != null;
    }
}
