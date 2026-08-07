using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Maps one source material identity to an already compiled Unity material asset.</summary>
    internal sealed class UnityMaterialRemap
    {
        internal UnityMaterialRemap(string sourceMaterialName, string targetMaterialReference)
        {
            SourceMaterialName = sourceMaterialName;
            TargetMaterialReference = targetMaterialReference;
        }

        internal string SourceMaterialName { get; private set; }

        internal string TargetMaterialReference { get; private set; }
    }

    /// <summary>Applies the complete deterministic ModelImporter policy for a static FBX source.</summary>
    internal static class UnityStaticModelImporterPolicy
    {
        /// <summary>
        /// Disables rig, animation, camera, light, and visibility import; applies exact geometry
        /// settings; and replaces the material-remap table with the caller's ordinal plan.
        /// </summary>
        internal static bool TryApply(
            string modelAssetReference,
            float globalScale,
            bool preserveHierarchy,
            IEnumerable<UnityMaterialRemap> materialRemaps,
            out string diagnosticCode)
        {
            diagnosticCode = "UNITY_STATIC_MODEL_IMPORT_INVALID";
            List<ResolvedRemap> resolvedRemaps;
            if (!IsSafeModelReference(modelAssetReference) ||
                !IsFinitePositive(globalScale) ||
                !TryResolveRemaps(materialRemaps, out resolvedRemaps, out diagnosticCode))
            {
                return false;
            }

            var importer = AssetImporter.GetAtPath(modelAssetReference) as ModelImporter;
            if (importer == null)
            {
                diagnosticCode = "UNITY_STATIC_MODEL_IMPORTER_MISSING";
                return false;
            }

            if (!HasCompleteMaterialPlan(modelAssetReference, resolvedRemaps))
            {
                diagnosticCode = "UNITY_STATIC_MODEL_MATERIAL_PLAN_INCOMPLETE";
                return false;
            }

            var original = ImporterSnapshot.Capture(importer);
            try
            {
                ApplyGeometryPolicy(importer, globalScale, preserveHierarchy);
                ReplaceMaterialRemaps(importer, resolvedRemaps);
                importer.SaveAndReimport();

                importer = AssetImporter.GetAtPath(modelAssetReference) as ModelImporter;
                if (importer == null ||
                    !HasExpectedPolicy(importer, globalScale, preserveHierarchy, resolvedRemaps))
                {
                    Restore(modelAssetReference, original);
                    diagnosticCode = "UNITY_STATIC_MODEL_IMPORT_VERIFY_FAILED";
                    return false;
                }

                diagnosticCode = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is UnityException)
            {
                Restore(modelAssetReference, original);
                diagnosticCode = "UNITY_STATIC_MODEL_IMPORT_FAILED";
                return false;
            }
        }

        private static void ApplyGeometryPolicy(
            ModelImporter importer,
            float globalScale,
            bool preserveHierarchy)
        {
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.globalScale = globalScale;
            importer.preserveHierarchy = preserveHierarchy;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
        }

        private static void ReplaceMaterialRemaps(
            ModelImporter importer,
            IReadOnlyList<ResolvedRemap> remaps)
        {
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> pair in
                importer.GetExternalObjectMap())
            {
                if (pair.Key.type == typeof(Material))
                {
                    importer.RemoveRemap(pair.Key);
                }
            }

            foreach (ResolvedRemap remap in remaps)
            {
                importer.AddRemap(
                    new AssetImporter.SourceAssetIdentifier(typeof(Material), remap.SourceMaterialName),
                    remap.TargetMaterial);
            }
        }

        private static bool HasExpectedPolicy(
            ModelImporter importer,
            float globalScale,
            bool preserveHierarchy,
            IReadOnlyList<ResolvedRemap> remaps)
        {
            if (importer.animationType != ModelImporterAnimationType.None || importer.importAnimation ||
                importer.importCameras || importer.importLights || importer.importVisibility ||
                importer.importBlendShapes || importer.importNormals != ModelImporterNormals.Import ||
                importer.importTangents != ModelImporterTangents.CalculateMikk ||
                Math.Abs(importer.globalScale - globalScale) > 0.0001f ||
                importer.preserveHierarchy != preserveHierarchy ||
                importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription ||
                importer.materialLocation != ModelImporterMaterialLocation.InPrefab ||
                importer.materialName != ModelImporterMaterialName.BasedOnMaterialName ||
                importer.materialSearch != ModelImporterMaterialSearch.Local)
            {
                return false;
            }

            var actual = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> pair in
                importer.GetExternalObjectMap())
            {
                if (pair.Key.type == typeof(Material))
                {
                    var material = pair.Value as Material;
                    if (material == null || actual.ContainsKey(pair.Key.name))
                    {
                        return false;
                    }

                    actual.Add(pair.Key.name, material);
                }
            }

            if (actual.Count != remaps.Count)
            {
                return false;
            }

            foreach (ResolvedRemap remap in remaps)
            {
                Material target;
                if (!actual.TryGetValue(remap.SourceMaterialName, out target) || target != remap.TargetMaterial)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryResolveRemaps(
            IEnumerable<UnityMaterialRemap> remaps,
            out List<ResolvedRemap> resolved,
            out string diagnosticCode)
        {
            resolved = new List<ResolvedRemap>();
            diagnosticCode = "UNITY_STATIC_MODEL_MATERIAL_REMAP_INVALID";
            if (remaps == null)
            {
                return false;
            }

            var sourceNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (UnityMaterialRemap remap in remaps)
            {
                if (remap == null || !IsSafeMaterialName(remap.SourceMaterialName) ||
                    !IsSafeMaterialReference(remap.TargetMaterialReference) ||
                    !sourceNames.Add(remap.SourceMaterialName))
                {
                    return false;
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(remap.TargetMaterialReference);
                if (material == null)
                {
                    diagnosticCode = "UNITY_STATIC_MODEL_MATERIAL_MISSING";
                    return false;
                }

                resolved.Add(new ResolvedRemap(remap.SourceMaterialName, material));
            }

            resolved.Sort((first, second) =>
                string.Compare(first.SourceMaterialName, second.SourceMaterialName, StringComparison.Ordinal));
            return true;
        }

        private static bool HasCompleteMaterialPlan(
            string modelAssetReference,
            IReadOnlyList<ResolvedRemap> remaps)
        {
            var plannedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (ResolvedRemap remap in remaps)
            {
                plannedNames.Add(remap.SourceMaterialName);
            }

            var sourceNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(modelAssetReference))
            {
                var material = asset as Material;
                if (material != null)
                {
                    sourceNames.Add(material.name);
                }
            }

            var importer = AssetImporter.GetAtPath(modelAssetReference) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> pair in
                importer.GetExternalObjectMap())
            {
                if (pair.Key.type == typeof(Material))
                {
                    sourceNames.Add(pair.Key.name);
                }
            }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetReference);
            if (modelAsset == null)
            {
                return false;
            }

            // Before the first remap, some Unity importer versions expose only renderer material
            // identities. After a remap those renderers reference the target material and must not
            // be compared with FBX source identifiers, so this is deliberately a last-resort path.
            if (sourceNames.Count == 0)
            {
                foreach (Renderer renderer in modelAsset.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null)
                        {
                            sourceNames.Add(material.name);
                        }
                    }
                }
            }

            return sourceNames.Count > 0 && plannedNames.SetEquals(sourceNames);
        }

        private static bool IsSafeModelReference(string value)
        {
            return IsSafeAssetReference(value) &&
                value.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                value.IndexOf("/Source/", StringComparison.Ordinal) >= 0;
        }

        private static bool IsSafeMaterialReference(string value)
        {
            return IsSafeAssetReference(value) &&
                value.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) &&
                value.IndexOf("/Materials/", StringComparison.Ordinal) >= 0;
        }

        private static bool IsSafeAssetReference(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith("Assets/", StringComparison.Ordinal) &&
                value.IndexOf('\\') < 0 && value.IndexOf(':') < 0 &&
                value.IndexOf("/../", StringComparison.Ordinal) < 0 &&
                value.IndexOf("/./", StringComparison.Ordinal) < 0 &&
                !value.EndsWith("/", StringComparison.Ordinal);
        }

        private static bool IsSafeMaterialName(string value)
        {
            if (string.IsNullOrEmpty(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            foreach (char character in value)
            {
                if (char.IsControl(character) || character == '/' || character == '\\')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static void Restore(string modelAssetReference, ImporterSnapshot snapshot)
        {
            try
            {
                var importer = AssetImporter.GetAtPath(modelAssetReference) as ModelImporter;
                if (importer == null)
                {
                    return;
                }

                snapshot.Apply(importer);
                importer.SaveAndReimport();
            }
            catch (Exception)
            {
                // The original diagnostic remains authoritative; the retained clone preserves
                // the failed importer state for inspection when Unity itself refuses restoration.
            }
        }

        private sealed class ResolvedRemap
        {
            internal ResolvedRemap(string sourceMaterialName, Material targetMaterial)
            {
                SourceMaterialName = sourceMaterialName;
                TargetMaterial = targetMaterial;
            }

            internal string SourceMaterialName { get; private set; }

            internal Material TargetMaterial { get; private set; }
        }

        private sealed class ImporterSnapshot
        {
            private readonly ModelImporterAnimationType animationType;
            private readonly bool importAnimation;
            private readonly bool importCameras;
            private readonly bool importLights;
            private readonly bool importVisibility;
            private readonly bool importBlendShapes;
            private readonly ModelImporterNormals importNormals;
            private readonly ModelImporterTangents importTangents;
            private readonly float globalScale;
            private readonly bool preserveHierarchy;
            private readonly ModelImporterMaterialImportMode materialImportMode;
            private readonly ModelImporterMaterialLocation materialLocation;
            private readonly ModelImporterMaterialName materialName;
            private readonly ModelImporterMaterialSearch materialSearch;
            private readonly Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> remaps;

            private ImporterSnapshot(ModelImporter importer)
            {
                animationType = importer.animationType;
                importAnimation = importer.importAnimation;
                importCameras = importer.importCameras;
                importLights = importer.importLights;
                importVisibility = importer.importVisibility;
                importBlendShapes = importer.importBlendShapes;
                importNormals = importer.importNormals;
                importTangents = importer.importTangents;
                globalScale = importer.globalScale;
                preserveHierarchy = importer.preserveHierarchy;
                materialImportMode = importer.materialImportMode;
                materialLocation = importer.materialLocation;
                materialName = importer.materialName;
                materialSearch = importer.materialSearch;
                remaps = new Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object>(
                    importer.GetExternalObjectMap());
            }

            internal static ImporterSnapshot Capture(ModelImporter importer)
            {
                return new ImporterSnapshot(importer);
            }

            internal void Apply(ModelImporter importer)
            {
                importer.animationType = animationType;
                importer.importAnimation = importAnimation;
                importer.importCameras = importCameras;
                importer.importLights = importLights;
                importer.importVisibility = importVisibility;
                importer.importBlendShapes = importBlendShapes;
                importer.importNormals = importNormals;
                importer.importTangents = importTangents;
                importer.globalScale = globalScale;
                importer.preserveHierarchy = preserveHierarchy;
                importer.materialImportMode = materialImportMode;
                importer.materialLocation = materialLocation;
                importer.materialName = materialName;
                importer.materialSearch = materialSearch;
                foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> pair in
                    importer.GetExternalObjectMap())
                {
                    importer.RemoveRemap(pair.Key);
                }

                foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> pair in remaps)
                {
                    importer.AddRemap(pair.Key, pair.Value);
                }
            }
        }
    }
}
