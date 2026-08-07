using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Defines the validated inputs required to create one static customer prefab.</summary>
    internal sealed class UnityPrefabRequest
    {
        internal string AssetId { get; set; }

        internal string SourceModelReference { get; set; }

        internal string OutputAssetReference { get; set; }

        internal UnityExtractedMeshSet ExtractedMeshes { get; set; }

        internal string[] ExpectedMaterialReferences { get; set; }
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

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(request.SourceModelReference);
            if (modelAsset == null)
            {
                diagnosticCode = "UNITY_PREFAB_MODEL_MISSING";
                return false;
            }

            GameObject productRoot = null;
            try
            {
                // Unity identifies a prefab's main GameObject by the asset filename. Use that
                // canonical identity before saving so memory and clean reimport agree.
                productRoot = new GameObject("P_" + request.AssetId);
                GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                if (modelInstance == null)
                {
                    diagnosticCode = "UNITY_PREFAB_MODEL_INSTANTIATION_FAILED";
                    return false;
                }

                modelInstance.name = "P_Model";
                modelInstance.transform.SetParent(productRoot.transform, false);
                ResetTransform(productRoot.transform);
                ResetTransform(modelInstance.transform);

                if (!ReplaceMeshes(modelInstance, request.ExtractedMeshes) ||
                    !HasCompleteMaterials(modelInstance, expectedMaterials))
                {
                    diagnosticCode = "UNITY_PREFAB_REFERENCE_INVALID";
                    return false;
                }

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

            if (!HasCompleteMaterials(model.gameObject, expectedMaterials))
            {
                diagnosticCode = "UNITY_PREFAB_MATERIAL_VERIFY_FAILED";
                return false;
            }

            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefabAsset) != 0)
            {
                diagnosticCode = "UNITY_PREFAB_MISSING_SCRIPT_VERIFY_FAILED";
                return false;
            }

            int meshReferenceCount = 0;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!IsExpectedMesh(filter.sharedMesh, request.ExtractedMeshes))
                {
                    diagnosticCode = "UNITY_PREFAB_MESH_VERIFY_FAILED";
                    return false;
                }

                meshReferenceCount++;
            }

            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!IsExpectedMesh(renderer.sharedMesh, request.ExtractedMeshes))
                {
                    diagnosticCode = "UNITY_PREFAB_MESH_VERIFY_FAILED";
                    return false;
                }

                meshReferenceCount++;
            }

            diagnosticCode = meshReferenceCount > 0
                ? string.Empty
                : "UNITY_PREFAB_MESH_VERIFY_FAILED";
            return meshReferenceCount > 0;
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

        private static void ResetTransform(Transform value)
        {
            value.localPosition = Vector3.zero;
            value.localRotation = Quaternion.identity;
            value.localScale = Vector3.one;
        }

        private static bool IsReset(Transform value)
        {
            return value.localPosition == Vector3.zero && value.localRotation == Quaternion.identity &&
                value.localScale == Vector3.one;
        }

        private static bool IsSafeAssetReference(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith("Assets/", StringComparison.Ordinal) &&
                value.IndexOf('\\') < 0 && value.IndexOf(':') < 0 &&
                value.IndexOf("/../", StringComparison.Ordinal) < 0 &&
                value.IndexOf("/./", StringComparison.Ordinal) < 0 &&
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
