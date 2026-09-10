using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Defines the validated inputs required to create one static customer prefab.</summary>
    internal class UnityPrefabModelRequest
    {
        internal string LogicalSourceReference { get; set; }

        internal string SourceModelReference { get; set; }

        internal UnityExtractedMeshSet ExtractedMeshes { get; set; }

        internal string[] ExpectedMaterialReferences { get; set; }
    }

    /// <summary>One reviewed item, with optional additional normalized model files.</summary>
    internal sealed class UnityPrefabRequest : UnityPrefabModelRequest
    {
        internal string AssetId { get; set; }
        internal string OutputAssetReference { get; set; }
        internal UnityPrefabModelRequest[] AdditionalModels { get; set; }
    }

    /// <summary>Creates the reset product-root/P_Model hierarchy and verifies all saved references.</summary>
    internal static class UnityPrefabGenerator
    {
        /// <summary>Creates a new static prefab only after every mesh and material reference is resolvable.</summary>
        internal static bool TryCreate(
            UnityPrefabRequest request,
            out GameObject prefabAsset,
            out string diagnosticCode)
        {
            prefabAsset = null;
            diagnosticCode = "UNITY_PREFAB_INVALID";
            HashSet<string> expectedMaterials;
            if (!TryValidate(request, out expectedMaterials, out diagnosticCode))
            {
                return false;
            }

            UnityPrefabModelRequest[] models = GetModels(request);
            foreach (UnityPrefabModelRequest modelRequest in models)
            {
                HashSet<string> materials;
                if (modelRequest == null || !TryValidate(ForModel(request, modelRequest), out materials, out diagnosticCode) ||
                    AssetDatabase.LoadAssetAtPath<GameObject>(modelRequest.SourceModelReference) == null)
                {
                    return false;
                }
            }

            GameObject productRoot = null;
            try
            {
                productRoot = new GameObject("P_" + request.AssetId);
                GameObject modelContainer = null;
                if (models.Length > 1)
                {
                    modelContainer = new GameObject("P_Model");
                    modelContainer.transform.SetParent(productRoot.transform, false);
                    ResetTransform(modelContainer.transform);
                }

                for (int index = 0; index < models.Length; index++)
                {
                    UnityPrefabModelRequest part = models[index];
                    var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(part.SourceModelReference);
                    GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                    if (modelInstance == null)
                    {
                        diagnosticCode = "UNITY_PREFAB_MODEL_INSTANTIATION_FAILED";
                        return false;
                    }

                    modelInstance.name = models.Length == 1 ? "P_Model" : "P_Part" + (index + 1).ToString("D3", CultureInfo.InvariantCulture);
                    modelInstance.transform.SetParent((modelContainer ?? productRoot).transform, false);
                    ResetTransform(modelInstance.transform);
                    if (!ReplaceMeshes(modelInstance, part.ExtractedMeshes) ||
                        !HasCompleteMaterials(modelInstance, new HashSet<string>(part.ExpectedMaterialReferences, StringComparer.Ordinal)))
                    {
                        diagnosticCode = "UNITY_PREFAB_REFERENCE_INVALID";
                        return false;
                    }
                }

                ResetTransform(productRoot.transform);

                bool success;
                PrefabUtility.SaveAsPrefabAsset(productRoot, request.OutputAssetReference, out success);
                if (!success)
                {
                    diagnosticCode = "UNITY_PREFAB_CREATE_FAILED";
                    return false;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(request.OutputAssetReference, ImportAssetOptions.ForceSynchronousImport);
                prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(request.OutputAssetReference);
                if (!VerifySavedPrefab(prefabAsset, request, expectedMaterials, out diagnosticCode))
                {
                    prefabAsset = null;
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
                diagnosticCode = "UNITY_PREFAB_CREATE_FAILED";
                return false;
            }
            finally
            {
                if (productRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(productRoot);
                }

                if (prefabAsset == null && request != null &&
                    !string.IsNullOrEmpty(request.OutputAssetReference) &&
                    AssetDatabase.LoadMainAssetAtPath(request.OutputAssetReference) != null)
                {
                    AssetDatabase.DeleteAsset(request.OutputAssetReference);
                }
            }
        }

        private static bool TryValidate(
            UnityPrefabRequest request,
            out HashSet<string> expectedMaterials,
            out string diagnosticCode)
        {
            expectedMaterials = null;
            diagnosticCode = "UNITY_PREFAB_INVALID";
            if (request == null || !IsAssetId(request.AssetId) ||
                !IsSafeAssetReference(request.SourceModelReference) ||
                request.SourceModelReference.IndexOf("/Source/", StringComparison.Ordinal) < 0 ||
                !request.SourceModelReference.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                !IsSafeAssetReference(request.OutputAssetReference) ||
                !request.OutputAssetReference.EndsWith(
                    "/Prefabs/P_" + request.AssetId + ".prefab",
                    StringComparison.Ordinal) ||
                request.ExtractedMeshes == null || request.ExtractedMeshes.Bindings.Count == 0 ||
                request.ExpectedMaterialReferences == null || request.ExpectedMaterialReferences.Length == 0 ||
                AssetDatabase.LoadMainAssetAtPath(request.OutputAssetReference) != null)
            {
                return false;
            }

            string outputFolder = request.OutputAssetReference.Substring(
                0,
                request.OutputAssetReference.LastIndexOf('/'));
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                diagnosticCode = "UNITY_PREFAB_OUTPUT_FOLDER_MISSING";
                return false;
            }

            expectedMaterials = new HashSet<string>(StringComparer.Ordinal);
            foreach (string materialReference in request.ExpectedMaterialReferences)
            {
                if (!IsSafeAssetReference(materialReference) ||
                    materialReference.IndexOf("/Materials/", StringComparison.Ordinal) < 0 ||
                    !materialReference.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ||
                    AssetDatabase.LoadAssetAtPath<Material>(materialReference) == null ||
                    !expectedMaterials.Add(materialReference))
                {
                    diagnosticCode = "UNITY_PREFAB_MATERIAL_PLAN_INVALID";
                    return false;
                }
            }

            foreach (UnityMeshAssetBinding binding in request.ExtractedMeshes.Bindings)
            {
                if (binding == null || binding.SourceMesh == null || binding.ExtractedMesh == null ||
                    AssetDatabase.GetAssetPath(binding.SourceMesh) != request.SourceModelReference ||
                    !IsSafeAssetReference(binding.OutputAssetReference) ||
                    binding.OutputAssetReference.IndexOf("/Meshes/MS_", StringComparison.Ordinal) < 0 ||
                    !binding.OutputAssetReference.EndsWith(".asset", StringComparison.Ordinal) ||
                    AssetDatabase.GetAssetPath(binding.ExtractedMesh) != binding.OutputAssetReference)
                {
                    diagnosticCode = "UNITY_PREFAB_MESH_PLAN_INVALID";
                    return false;
                }
            }

            return true;
        }

        private static bool ReplaceMeshes(GameObject modelInstance, UnityExtractedMeshSet extractedMeshes)
        {
            var usedMeshes = new HashSet<Mesh>();
            foreach (MeshFilter filter in modelInstance.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh extracted;
                if (!extractedMeshes.TryGetExtractedMesh(filter.sharedMesh, out extracted))
                {
                    return false;
                }

                filter.sharedMesh = extracted;
                usedMeshes.Add(extracted);
            }

            foreach (SkinnedMeshRenderer renderer in
                modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh extracted;
                if (!extractedMeshes.TryGetExtractedMesh(renderer.sharedMesh, out extracted))
                {
                    return false;
                }

                renderer.sharedMesh = extracted;
                usedMeshes.Add(extracted);
            }

            if (usedMeshes.Count != extractedMeshes.Bindings.Count)
            {
                return false;
            }

            foreach (UnityMeshAssetBinding binding in extractedMeshes.Bindings)
            {
                if (!usedMeshes.Contains(binding.ExtractedMesh))
                {
                    return false;
                }
            }

            return usedMeshes.Count > 0;
        }

        private static bool HasCompleteMaterials(GameObject modelInstance, ISet<string> expectedMaterials)
        {
            Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return false;
            }

            var usedMaterials = new HashSet<string>(StringComparer.Ordinal);
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials.Length == 0)
                {
                    return false;
                }

                foreach (Material material in materials)
                {
                    if (material == null || !expectedMaterials.Contains(AssetDatabase.GetAssetPath(material)))
                    {
                        return false;
                    }

                    usedMaterials.Add(AssetDatabase.GetAssetPath(material));
                }
            }

            return usedMaterials.SetEquals(expectedMaterials);
        }

        private static bool VerifySavedPrefab(
            GameObject prefabAsset,
            UnityPrefabRequest request,
            ISet<string> expectedMaterials,
            out string diagnosticCode)
        {
            if (prefabAsset == null)
            {
                diagnosticCode = "UNITY_PREFAB_ASSET_VERIFY_FAILED";
                return false;
            }

            if (prefabAsset.name != "P_" + request.AssetId)
            {
                diagnosticCode = "UNITY_PREFAB_ROOT_NAME_VERIFY_FAILED";
                return false;
            }

            if (!IsReset(prefabAsset.transform))
            {
                diagnosticCode = "UNITY_PREFAB_ROOT_TRANSFORM_VERIFY_FAILED";
                return false;
            }

            if (prefabAsset.transform.childCount != 1)
            {
                diagnosticCode = "UNITY_PREFAB_CHILD_COUNT_VERIFY_FAILED";
                return false;
            }

            Transform model = prefabAsset.transform.GetChild(0);
            if (model.name != "P_Model")
            {
                diagnosticCode = "UNITY_PREFAB_MODEL_NAME_VERIFY_FAILED";
                return false;
            }

            if (!IsReset(model))
            {
                diagnosticCode = "UNITY_PREFAB_MODEL_TRANSFORM_VERIFY_FAILED";
                return false;
            }

            UnityPrefabModelRequest[] models = GetModels(request);
            if (models.Length > 1 && model.childCount != models.Length)
            {
                diagnosticCode = "UNITY_PREFAB_CHILD_COUNT_VERIFY_FAILED";
                return false;
            }

            for (int index = 0; index < models.Length; index++)
            {
                Transform part = models.Length == 1 ? model : model.GetChild(index);
                if (models.Length > 1 && (part.name != "P_Part" + (index + 1).ToString("D3", CultureInfo.InvariantCulture) || !IsReset(part)))
                {
                    diagnosticCode = "UNITY_PREFAB_MODEL_TRANSFORM_VERIFY_FAILED";
                    return false;
                }

                if (!VerifyModelReferences(part.gameObject, models[index], out diagnosticCode))
                {
                    return false;
                }
            }

            diagnosticCode = string.Empty;
            return true;
        }

        private static bool VerifyModelReferences(GameObject model, UnityPrefabModelRequest request, out string diagnosticCode)
        {
            if (!HasCompleteMaterials(model, new HashSet<string>(request.ExpectedMaterialReferences, StringComparer.Ordinal)))
            {
                diagnosticCode = "UNITY_PREFAB_MATERIAL_VERIFY_FAILED";
                return false;
            }

            if (model.GetComponentsInChildren<Transform>(true).Any(child =>
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0))
            {
                diagnosticCode = "UNITY_PREFAB_MISSING_SCRIPT_VERIFY_FAILED";
                return false;
            }

            Mesh[] meshes = model.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh)
                .Concat(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(renderer => renderer.sharedMesh)).ToArray();
            if (meshes.Length == 0 || meshes.Any(mesh => !IsExpectedMesh(mesh, request.ExtractedMeshes)))
            {
                diagnosticCode = "UNITY_PREFAB_MESH_VERIFY_FAILED";
                return false;
            }

            diagnosticCode = string.Empty;
            return true;
        }

        /// <summary>Returns the primary source followed by the caller's reviewed additional source order.</summary>
        internal static UnityPrefabModelRequest[] GetModels(UnityPrefabRequest request)
        {
            return new UnityPrefabModelRequest[] { request }.Concat(request.AdditionalModels ?? Array.Empty<UnityPrefabModelRequest>()).ToArray();
        }

        private static UnityPrefabRequest ForModel(UnityPrefabRequest item, UnityPrefabModelRequest model)
        {
            return new UnityPrefabRequest
            {
                AssetId = item.AssetId, OutputAssetReference = item.OutputAssetReference,
                SourceModelReference = model.SourceModelReference, ExtractedMeshes = model.ExtractedMeshes,
                ExpectedMaterialReferences = model.ExpectedMaterialReferences,
            };
        }

        private static bool IsExpectedMesh(Mesh mesh, UnityExtractedMeshSet extractedMeshes)
        {
            if (mesh == null)
            {
                return false;
            }

            foreach (UnityMeshAssetBinding binding in extractedMeshes.Bindings)
            {
                if (binding.ExtractedMesh == mesh &&
                    AssetDatabase.GetAssetPath(mesh) == binding.OutputAssetReference)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ResetTransform(Transform value) => UnityPrefabHierarchyUtility.ResetTransform(value);

        private static bool IsReset(Transform value) => UnityPrefabHierarchyUtility.IsReset(value);

        /// <summary>Checks canonical project-relative references before filesystem or AssetDatabase access.</summary>
        internal static bool IsSafeAssetReference(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith("Assets/", StringComparison.Ordinal) &&
                value.IndexOf('\\') < 0 && value.IndexOf(':') < 0 &&
                value.IndexOf("/../", StringComparison.Ordinal) < 0 &&
                value.IndexOf("/./", StringComparison.Ordinal) < 0 &&
                value.IndexOf("//", StringComparison.Ordinal) < 0 &&
                !value.EndsWith("/", StringComparison.Ordinal);
        }

        private static bool IsAssetId(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsAsciiLetter(value[0]))
            {
                return false;
            }

            for (int index = 1; index < value.Length; index++)
            {
                char character = value[index];
                if (!IsAsciiLetter(character) && (character < '0' || character > '9'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAsciiLetter(char value)
        {
            return value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z';
        }
    }
}
