using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Links one imported model mesh to its standalone customer mesh asset.</summary>
    internal sealed class UnityMeshAssetBinding
    {
        internal UnityMeshAssetBinding(Mesh sourceMesh, Mesh extractedMesh, string outputAssetReference)
        {
            SourceMesh = sourceMesh;
            ExtractedMesh = extractedMesh;
            OutputAssetReference = outputAssetReference;
        }

        internal Mesh SourceMesh { get; private set; }

        internal Mesh ExtractedMesh { get; private set; }

        internal string OutputAssetReference { get; private set; }
    }

    /// <summary>Contains the complete immutable source-to-standalone mesh replacement plan.</summary>
    internal sealed class UnityExtractedMeshSet
    {
        private readonly UnityMeshAssetBinding[] bindings;

        internal UnityExtractedMeshSet(UnityMeshAssetBinding[] bindings)
        {
            this.bindings = bindings;
        }

        internal IReadOnlyList<UnityMeshAssetBinding> Bindings
        {
            get { return bindings; }
        }

        internal bool TryGetExtractedMesh(Mesh sourceMesh, out Mesh extractedMesh)
        {
            foreach (UnityMeshAssetBinding binding in bindings)
            {
                if (binding.SourceMesh == sourceMesh)
                {
                    extractedMesh = binding.ExtractedMesh;
                    return true;
                }
            }

            extractedMesh = null;
            return false;
        }
    }

    /// <summary>Creates deduplicated standalone mesh assets without mutating the imported source model.</summary>
    internal static class UnityMeshAssetExtractor
    {
        /// <summary>
        /// Extracts every uniquely referenced mesh transactionally into the product Meshes folder.
        /// One imported mesh referenced by multiple renderers produces one standalone asset.
        /// </summary>
        internal static bool TryExtract(
            string sourceModelReference,
            string outputFolderReference,
            string assetId,
            out UnityExtractedMeshSet extractedMeshSet,
            out string diagnosticCode)
        {
            extractedMeshSet = null;
            diagnosticCode = "UNITY_MESH_EXTRACTION_INVALID";
            if (!IsSafeSourceReference(sourceModelReference) ||
                !IsSafeOutputFolder(outputFolderReference) ||
                !IsAssetId(assetId) ||
                !AssetDatabase.IsValidFolder(outputFolderReference))
            {
                return false;
            }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourceModelReference);
            if (modelAsset == null)
            {
                diagnosticCode = "UNITY_MESH_EXTRACTION_SOURCE_MISSING";
                return false;
            }

            List<SourceMesh> sourceMeshes;
            if (!TryCollectSourceMeshes(modelAsset, sourceModelReference, out sourceMeshes, out diagnosticCode))
            {
                return false;
            }

            var outputReferences = new List<string>(sourceMeshes.Count);
            var outputSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < sourceMeshes.Count; index++)
            {
                string suffix = sourceMeshes.Count == 1 ? string.Empty : "_" + (index + 1).ToString("D2");
                string outputReference = outputFolderReference + "/MS_" + assetId + suffix + ".asset";
                if (!outputSet.Add(outputReference) || AssetDatabase.LoadMainAssetAtPath(outputReference) != null)
                {
                    diagnosticCode = "UNITY_MESH_EXTRACTION_OUTPUT_COLLISION";
                    return false;
                }

                outputReferences.Add(outputReference);
            }

            var created = new List<string>();
            var bindings = new List<UnityMeshAssetBinding>();
            try
            {
                for (int index = 0; index < sourceMeshes.Count; index++)
                {
                    Mesh source = sourceMeshes[index].Mesh;
                    Mesh copy = UnityEngine.Object.Instantiate(source);
                    copy.name = Path.GetFileNameWithoutExtension(outputReferences[index]);
                    AssetDatabase.CreateAsset(copy, outputReferences[index]);
                    created.Add(outputReferences[index]);
                    var extracted = AssetDatabase.LoadAssetAtPath<Mesh>(outputReferences[index]);
                    if (extracted == null || extracted == source ||
                        extracted.vertexCount != source.vertexCount ||
                        extracted.subMeshCount != source.subMeshCount)
                    {
                        diagnosticCode = "UNITY_MESH_EXTRACTION_VERIFY_FAILED";
                        return false;
                    }

                    bindings.Add(new UnityMeshAssetBinding(source, extracted, outputReferences[index]));
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                extractedMeshSet = new UnityExtractedMeshSet(bindings.ToArray());
                diagnosticCode = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is UnityException)
            {
                diagnosticCode = "UNITY_MESH_EXTRACTION_FAILED";
                return false;
            }
            finally
            {
                if (extractedMeshSet == null)
                {
                    foreach (string outputReference in created)
                    {
                        AssetDatabase.DeleteAsset(outputReference);
                    }

                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
            }
        }

        private static bool TryCollectSourceMeshes(
            GameObject modelAsset,
            string sourceModelReference,
            out List<SourceMesh> sourceMeshes,
            out string diagnosticCode)
        {
            diagnosticCode = "UNITY_MESH_EXTRACTION_REFERENCE_INVALID";
            var unique = new Dictionary<long, SourceMesh>();
            foreach (MeshFilter filter in modelAsset.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!TryAdd(filter.sharedMesh, sourceModelReference, unique))
                {
                    sourceMeshes = null;
                    return false;
                }
            }

            foreach (SkinnedMeshRenderer renderer in modelAsset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!TryAdd(renderer.sharedMesh, sourceModelReference, unique))
                {
                    sourceMeshes = null;
                    return false;
                }
            }

            sourceMeshes = new List<SourceMesh>(unique.Values);
            sourceMeshes.Sort((first, second) =>
            {
                int byName = string.Compare(first.Mesh.name, second.Mesh.name, StringComparison.Ordinal);
                return byName != 0 ? byName : first.LocalFileId.CompareTo(second.LocalFileId);
            });
            if (sourceMeshes.Count == 0)
            {
                diagnosticCode = "UNITY_MESH_EXTRACTION_EMPTY";
                return false;
            }

            diagnosticCode = string.Empty;
            return true;
        }

        private static bool TryAdd(
            Mesh mesh,
            string sourceModelReference,
            IDictionary<long, SourceMesh> unique)
        {
            string guid;
            long localFileId;
            if (mesh == null || AssetDatabase.GetAssetPath(mesh) != sourceModelReference ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out guid, out localFileId) ||
                localFileId == 0)
            {
                return false;
            }

            SourceMesh existing;
            if (unique.TryGetValue(localFileId, out existing))
            {
                return existing.Mesh == mesh;
            }

            unique.Add(localFileId, new SourceMesh(mesh, localFileId));
            return true;
        }

        private static bool IsSafeSourceReference(string value)
        {
            return IsSafeAssetReference(value) &&
                value.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                value.IndexOf("/Source/", StringComparison.Ordinal) >= 0;
        }

        private static bool IsSafeOutputFolder(string value)
        {
            return IsSafeAssetReference(value) && value.EndsWith("/Meshes", StringComparison.Ordinal);
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

        private sealed class SourceMesh
        {
            internal SourceMesh(Mesh mesh, long localFileId)
            {
                Mesh = mesh;
                LocalFileId = localFileId;
            }

            internal Mesh Mesh { get; private set; }

            internal long LocalFileId { get; private set; }
        }
    }
}
