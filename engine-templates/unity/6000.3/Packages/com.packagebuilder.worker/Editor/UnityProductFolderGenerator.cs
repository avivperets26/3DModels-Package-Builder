using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Describes the deterministic Unity asset folders owned by one product manifest.</summary>
    internal sealed class UnityProductFolderPlan
    {
        internal UnityProductFolderPlan(
            string publisherRoot,
            string productFolder,
            string productCase,
            string[] assetFolders)
        {
            PublisherRoot = publisherRoot;
            ProductFolder = productFolder;
            ProductCase = productCase;
            AssetFolders = assetFolders;
        }

        internal string PublisherRoot { get; private set; }

        internal string ProductFolder { get; private set; }

        internal string ProductCase { get; private set; }

        internal string ProductRoot
        {
            get { return "Assets/" + PublisherRoot + "/" + ProductFolder; }
        }

        internal string[] AssetFolders { get; private set; }
    }

    /// <summary>
    /// Reads the manifest-owned Unity identity and creates a clean case-specific product layout.
    /// The generator operates only through Unity's AssetDatabase so every folder receives a meta file.
    /// </summary>
    internal static class UnityProductFolderGenerator
    {
        private const int MaximumManifestCharacters = 1_048_576;

        private static readonly string[] BaseFolderNames =
        {
            "Source",
            "Meshes",
            "Materials",
            "Textures",
            "Prefabs",
            "Documentation",
            "Scenes",
            "Scripts",
        };

        private static readonly HashSet<string> SupportedCases = new HashSet<string>(StringComparer.Ordinal)
        {
            "static",
            "rigged",
            "rigged-animated",
            "item-set",
            "item-collection",
        };

        /// <summary>Builds an immutable folder plan from the validated product manifest JSON.</summary>
        internal static bool TryCreatePlan(
            string manifestJson,
            out UnityProductFolderPlan plan,
            out string diagnosticCode)
        {
            plan = null;
            diagnosticCode = "UNITY_PRODUCT_MANIFEST_INVALID";
            if (string.IsNullOrWhiteSpace(manifestJson) || manifestJson.Length > MaximumManifestCharacters)
            {
                return false;
            }

            ProductManifestIdentity manifest;
            try
            {
                manifest = JsonUtility.FromJson<ProductManifestIdentity>(manifestJson);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (manifest == null || manifest.schemaVersion != 1 || manifest.product == null ||
                !UnityAssetNameValidator.IsPublisherRoot(manifest.publisherProfileReference) ||
                !UnityAssetNameValidator.IsProductFolder(manifest.product.folderName) ||
                !SupportedCases.Contains(manifest.product.@case))
            {
                return false;
            }

            var names = new List<string>(BaseFolderNames);
            if (string.Equals(manifest.product.@case, "rigged-animated", StringComparison.Ordinal))
            {
                names.Add("Animations");
                names.Add("Controllers");
            }

            var assetFolders = new string[names.Count];
            string productRoot = "Assets/" + manifest.publisherProfileReference + "/" + manifest.product.folderName;
            for (int index = 0; index < names.Count; index++)
            {
                assetFolders[index] = productRoot + "/" + names[index];
            }

            plan = new UnityProductFolderPlan(
                manifest.publisherProfileReference,
                manifest.product.folderName,
                manifest.product.@case,
                assetFolders);
            diagnosticCode = string.Empty;
            return true;
        }

        /// <summary>Creates the complete product layout or removes every folder created by the failed attempt.</summary>
        internal static bool TryCreateFolders(
            UnityProductFolderPlan plan,
            out string diagnosticCode)
        {
            diagnosticCode = "UNITY_PRODUCT_FOLDER_CREATE_FAILED";
            if (plan == null || !AssetDatabase.IsValidFolder("Assets"))
            {
                return false;
            }

            string publisherPath = "Assets/" + plan.PublisherRoot;
            if (AssetDatabase.IsValidFolder(plan.ProductRoot))
            {
                diagnosticCode = "UNITY_PRODUCT_FOLDER_COLLISION";
                return false;
            }

            bool publisherCreated = false;
            bool productCreated = false;
            try
            {
                if (!AssetDatabase.IsValidFolder(publisherPath))
                {
                    publisherCreated = CreateFolder("Assets", plan.PublisherRoot);
                    if (!publisherCreated)
                    {
                        return false;
                    }
                }

                productCreated = CreateFolder(publisherPath, plan.ProductFolder);
                if (!productCreated)
                {
                    return false;
                }

                foreach (string assetFolder in plan.AssetFolders)
                {
                    int separator = assetFolder.LastIndexOf('/');
                    string parent = assetFolder.Substring(0, separator);
                    string name = assetFolder.Substring(separator + 1);
                    if (!CreateFolder(parent, name))
                    {
                        return false;
                    }
                }

                AssetDatabase.SaveAssets();
                diagnosticCode = string.Empty;
                return true;
            }
            finally
            {
                if (!string.IsNullOrEmpty(diagnosticCode))
                {
                    if (productCreated && AssetDatabase.IsValidFolder(plan.ProductRoot))
                    {
                        AssetDatabase.DeleteAsset(plan.ProductRoot);
                    }

                    if (publisherCreated && AssetDatabase.IsValidFolder(publisherPath))
                    {
                        AssetDatabase.DeleteAsset(publisherPath);
                    }

                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
            }
        }

        private static bool CreateFolder(string parent, string name)
        {
            string guid = AssetDatabase.CreateFolder(parent, name);
            return !string.IsNullOrEmpty(guid) && AssetDatabase.IsValidFolder(parent + "/" + name);
        }

        [Serializable]
        private sealed class ProductManifestIdentity
        {
            public int schemaVersion;
            public string publisherProfileReference = string.Empty;
            public ProductIdentity product;
        }

        [Serializable]
        private sealed class ProductIdentity
        {
            public string folderName = string.Empty;
            public string @case = string.Empty;
        }
    }

    /// <summary>Mirrors the approved domain grammars at the dependency-free Unity boundary.</summary>
    internal static class UnityAssetNameValidator
    {
        internal static bool IsPublisherRoot(string value)
        {
            if (!IsSafeSegment(value) || !IsAsciiLetter(value[0]))
            {
                return false;
            }

            for (int index = 1; index < value.Length; index++)
            {
                char character = value[index];
                if (!IsAsciiLetterOrDigit(character) && character != '_')
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsProductFolder(string value)
        {
            if (!IsSafeSegment(value) || !IsAsciiLetterOrDigit(value[0]))
            {
                return false;
            }

            for (int index = 1; index < value.Length; index++)
            {
                char character = value[index];
                if (!IsAsciiLetterOrDigit(character) && character != '_' && character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSafeSegment(string value)
        {
            if (string.IsNullOrEmpty(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                value.IndexOf('/') >= 0 || value.IndexOf('\\') >= 0 || value.IndexOf(':') >= 0 ||
                value[value.Length - 1] == '.' || value[value.Length - 1] == ' ' ||
                IsReserved(value))
            {
                return false;
            }

            foreach (char character in value)
            {
                if (char.IsControl(character))
                {
                    return false;
                }
            }

            return value != "." && value != "..";
        }

        private static bool IsReserved(string value)
        {
            string upper = value.ToUpperInvariant();
            if (upper == "CON" || upper == "PRN" || upper == "AUX" || upper == "NUL")
            {
                return true;
            }

            if (upper.Length == 4 && (upper.StartsWith("COM", StringComparison.Ordinal) ||
                upper.StartsWith("LPT", StringComparison.Ordinal)))
            {
                return upper[3] >= '1' && upper[3] <= '9';
            }

            return false;
        }

        private static bool IsAsciiLetterOrDigit(char value)
        {
            return IsAsciiLetter(value) || value >= '0' && value <= '9';
        }

        private static bool IsAsciiLetter(char value)
        {
            return value >= 'A' && value <= 'Z' || value >= 'a' && value <= 'z';
        }
    }
}
