using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Packs canonical metallic and roughness maps into the URP/Lit channel layout.</summary>
    internal static class UnityMetallicSmoothnessPacker
    {
        /// <summary>Creates a PNG whose red channel is metallic and alpha is one minus roughness.</summary>
        internal static bool TryPack(
            string metallicAssetReference,
            string roughnessAssetReference,
            string outputAssetReference,
            out string diagnosticCode)
        {
            diagnosticCode = "UNITY_METALLIC_SMOOTHNESS_INVALID";
            if (!IsSafeAssetReference(metallicAssetReference) ||
                !IsSafeAssetReference(roughnessAssetReference) ||
                !IsSafeAssetReference(outputAssetReference) ||
                !outputAssetReference.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(metallicAssetReference, roughnessAssetReference, StringComparison.Ordinal) ||
                string.Equals(metallicAssetReference, outputAssetReference, StringComparison.Ordinal) ||
                string.Equals(roughnessAssetReference, outputAssetReference, StringComparison.Ordinal))
            {
                diagnosticCode = "UNITY_METALLIC_SMOOTHNESS_REFERENCE_COLLISION";
                return false;
            }

            if (AssetDatabase.LoadMainAssetAtPath(outputAssetReference) != null ||
                File.Exists(ToPhysicalPath(outputAssetReference)))
            {
                diagnosticCode = "UNITY_METALLIC_SMOOTHNESS_OUTPUT_EXISTS";
                return false;
            }

            string outputFolder = Path.GetDirectoryName(outputAssetReference.Replace('/', Path.DirectorySeparatorChar))
                ?.Replace(Path.DirectorySeparatorChar, '/');
            if (string.IsNullOrEmpty(outputFolder) || !AssetDatabase.IsValidFolder(outputFolder))
            {
                diagnosticCode = "UNITY_METALLIC_SMOOTHNESS_OUTPUT_FOLDER_MISSING";
                return false;
            }

            var metallicImporter = AssetImporter.GetAtPath(metallicAssetReference) as TextureImporter;
            var roughnessImporter = AssetImporter.GetAtPath(roughnessAssetReference) as TextureImporter;
            if (metallicImporter == null || roughnessImporter == null)
            {
                diagnosticCode = "UNITY_METALLIC_SMOOTHNESS_SOURCE_MISSING";
                return false;
            }

            if (metallicImporter.sRGBTexture || roughnessImporter.sRGBTexture ||
                metallicImporter.textureType != TextureImporterType.Default ||
                roughnessImporter.textureType != TextureImporterType.Default)
            {
                diagnosticCode = "UNITY_METALLIC_SMOOTHNESS_SOURCE_POLICY_INVALID";
                return false;
            }

            var metallicState = ReadableImportState.Capture(metallicImporter);
            var roughnessState = ReadableImportState.Capture(roughnessImporter);
            bool outputWritten = false;
            try
            {
                metallicState.ApplyLosslessReadable(metallicImporter);
                roughnessState.ApplyLosslessReadable(roughnessImporter);

                var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicAssetReference);
                var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessAssetReference);
                if (metallic == null || roughness == null)
                {
                    diagnosticCode = "UNITY_METALLIC_SMOOTHNESS_SOURCE_MISSING";
                    return false;
                }

                if (metallic.width != roughness.width || metallic.height != roughness.height)
                {
                    diagnosticCode = "UNITY_METALLIC_SMOOTHNESS_DIMENSION_MISMATCH";
                    return false;
                }

                Color32[] metallicPixels = metallic.GetPixels32();
                Color32[] roughnessPixels = roughness.GetPixels32();
                var packedPixels = new Color32[metallicPixels.Length];
                for (int index = 0; index < packedPixels.Length; index++)
                {
                    packedPixels[index] = new Color32(
                        metallicPixels[index].r,
                        0,
                        0,
                        (byte)(byte.MaxValue - roughnessPixels[index].r));
                }

                var packed = new Texture2D(
                    metallic.width,
                    metallic.height,
                    TextureFormat.RGBA32,
                    false,
                    true);
                try
                {
                    packed.SetPixels32(packedPixels);
                    packed.Apply(false, false);
                    File.WriteAllBytes(ToPhysicalPath(outputAssetReference), packed.EncodeToPNG());
                    outputWritten = true;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(packed);
                }

                AssetDatabase.ImportAsset(outputAssetReference, ImportAssetOptions.ForceSynchronousImport);
                var outputImporter = AssetImporter.GetAtPath(outputAssetReference) as TextureImporter;
                if (outputImporter == null)
                {
                    diagnosticCode = "UNITY_METALLIC_SMOOTHNESS_OUTPUT_IMPORT_FAILED";
                    return false;
                }

                outputImporter.sRGBTexture = false;
                outputImporter.textureType = TextureImporterType.Default;
                outputImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                outputImporter.alphaIsTransparency = false;
                outputImporter.isReadable = false;
                outputImporter.SaveAndReimport();
                diagnosticCode = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is UnityException)
            {
                diagnosticCode = "UNITY_METALLIC_SMOOTHNESS_PACK_FAILED";
                return false;
            }
            finally
            {
                metallicState.Restore(metallicImporter);
                roughnessState.Restore(roughnessImporter);
                if (!string.IsNullOrEmpty(diagnosticCode) && outputWritten)
                {
                    AssetDatabase.DeleteAsset(outputAssetReference);
                }
            }
        }

        private static string ToPhysicalPath(string assetReference)
        {
            return Path.Combine(
                Application.dataPath,
                assetReference.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
        }

        private static bool IsSafeAssetReference(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith("Assets/", StringComparison.Ordinal) ||
                value.IndexOf('\\') >= 0 || value.IndexOf(':') >= 0 ||
                value.IndexOf("//", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            foreach (string segment in value.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Preserves every source setting changed to obtain exact uncompressed pixels.</summary>
        private sealed class ReadableImportState
        {
            private readonly bool _isReadable;
            private readonly TextureImporterCompression _compression;
            private readonly bool _crunchedCompression;

            private ReadableImportState(TextureImporter importer)
            {
                _isReadable = importer.isReadable;
                _compression = importer.textureCompression;
                _crunchedCompression = importer.crunchedCompression;
            }

            internal static ReadableImportState Capture(TextureImporter importer)
            {
                return new ReadableImportState(importer);
            }

            internal void ApplyLosslessReadable(TextureImporter importer)
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.crunchedCompression = false;
                importer.SaveAndReimport();
            }

            internal void Restore(TextureImporter importer)
            {
                importer.isReadable = _isReadable;
                importer.textureCompression = _compression;
                importer.crunchedCompression = _crunchedCompression;
                importer.SaveAndReimport();
            }
        }
    }
}
