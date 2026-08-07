using System;
using UnityEditor;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Represents the complete reviewed TextureImporter policy for one canonical texture role.</summary>
    internal sealed class UnityTextureImportPolicy
    {
        internal UnityTextureImportPolicy(
            bool usesSrgb,
            TextureImporterType textureType,
            TextureImporterAlphaSource alphaSource,
            bool alphaIsTransparency)
        {
            UsesSrgb = usesSrgb;
            TextureType = textureType;
            AlphaSource = alphaSource;
            AlphaIsTransparency = alphaIsTransparency;
        }

        internal bool UsesSrgb { get; private set; }

        internal TextureImporterType TextureType { get; private set; }

        internal TextureImporterAlphaSource AlphaSource { get; private set; }

        internal bool AlphaIsTransparency { get; private set; }
    }

    /// <summary>Applies deterministic role-aware colour-space, normal-map, and alpha settings.</summary>
    internal static class UnityTextureImporterPolicy
    {
        internal static bool TryGet(string role, out UnityTextureImportPolicy policy)
        {
            policy = null;
            switch (role)
            {
                case "albedo":
                    policy = new UnityTextureImportPolicy(
                        true,
                        TextureImporterType.Default,
                        TextureImporterAlphaSource.FromInput,
                        true);
                    return true;
                case "emission":
                    policy = new UnityTextureImportPolicy(
                        true,
                        TextureImporterType.Default,
                        TextureImporterAlphaSource.None,
                        false);
                    return true;
                case "normal":
                    policy = new UnityTextureImportPolicy(
                        false,
                        TextureImporterType.NormalMap,
                        TextureImporterAlphaSource.None,
                        false);
                    return true;
                case "metallic":
                case "roughness":
                case "ambient-occlusion":
                case "height":
                    policy = Data(false);
                    return true;
                case "opacity":
                    policy = new UnityTextureImportPolicy(
                        false,
                        TextureImporterType.Default,
                        TextureImporterAlphaSource.FromInput,
                        false);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Updates one imported texture and performs the synchronous reimport required by Unity.</summary>
        internal static bool TryApply(string assetReference, string role, out string diagnosticCode)
        {
            diagnosticCode = "UNITY_TEXTURE_IMPORTER_INVALID";
            if (!IsSafeAssetReference(assetReference))
            {
                return false;
            }

            UnityTextureImportPolicy policy;
            if (!TryGet(role, out policy))
            {
                diagnosticCode = "UNITY_TEXTURE_ROLE_UNSUPPORTED";
                return false;
            }

            var importer = AssetImporter.GetAtPath(assetReference) as TextureImporter;
            if (importer == null)
            {
                diagnosticCode = "UNITY_TEXTURE_IMPORTER_MISSING";
                return false;
            }

            importer.sRGBTexture = policy.UsesSrgb;
            importer.textureType = policy.TextureType;
            importer.alphaSource = policy.AlphaSource;
            importer.alphaIsTransparency = policy.AlphaIsTransparency;
            importer.SaveAndReimport();
            diagnosticCode = string.Empty;
            return true;
        }

        private static UnityTextureImportPolicy Data(bool usesSrgb)
        {
            return new UnityTextureImportPolicy(
                usesSrgb,
                TextureImporterType.Default,
                TextureImporterAlphaSource.None,
                false);
        }

        private static bool IsSafeAssetReference(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith("Assets/", StringComparison.Ordinal) ||
                value.IndexOf('\\') >= 0 || value.IndexOf(':') >= 0 || value.IndexOf("//", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            string[] segments = value.Split('/');
            foreach (string segment in segments)
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    return false;
                }
            }

            return true;
        }
    }
}
