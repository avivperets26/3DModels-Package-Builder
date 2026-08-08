using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Describes one exact, dependency-closed customer package export.</summary>
    public sealed class UnityPackageExportRequest
    {
        public string ProductRootReference { get; set; }

        public string OutputPackagePath { get; set; }
    }

    /// <summary>Records the deterministic asset set supplied to Unity's package exporter.</summary>
    public sealed class UnityPackageExportPlan
    {
        public UnityPackageExportPlan(string productRootReference, string outputPackagePath,
            IReadOnlyList<string> assetReferences)
        {
            ProductRootReference = productRootReference;
            OutputPackagePath = outputPackagePath;
            AssetReferences = assetReferences;
        }

        public string ProductRootReference { get; }

        public string OutputPackagePath { get; }

        public IReadOnlyList<string> AssetReferences { get; }
    }

    /// <summary>Builds exact Unity packages without recursive or implicit external inclusion.</summary>
    public static class UnityPackageExporter
    {
        private static readonly HashSet<string> AllowedProductFolders = new HashSet<string>(
            new[]
            {
                "Animations", "Controllers", "Documentation", "Materials", "Meshes", "Prefabs",
                "Scenes", "Scripts", "Source", "Textures",
            },
            StringComparer.Ordinal);

        /// <summary>Plans, validates, exports, and verifies one collision-safe package file.</summary>
        public static bool TryExport(UnityPackageExportRequest request, out UnityPackageExportPlan plan,
            out string diagnosticCode)
        {
            plan = null;
            diagnosticCode = "UNITY_PACKAGE_EXPORT_INVALID";
            if (!TryCreatePlan(request, out plan, out diagnosticCode))
            {
                return false;
            }

            if (File.Exists(plan.OutputPackagePath))
            {
                diagnosticCode = "UNITY_PACKAGE_EXPORT_OUTPUT_COLLISION";
                return false;
            }

            try
            {
                string outputFolder = Path.GetDirectoryName(plan.OutputPackagePath);
                if (string.IsNullOrEmpty(outputFolder))
                {
                    return false;
                }

                Directory.CreateDirectory(outputFolder);
                AssetDatabase.ExportPackage(plan.AssetReferences.ToArray(), plan.OutputPackagePath,
                    ExportPackageOptions.Default);
                if (!File.Exists(plan.OutputPackagePath) || new FileInfo(plan.OutputPackagePath).Length == 0)
                {
                    diagnosticCode = "UNITY_PACKAGE_EXPORT_FILE_MISSING";
                    return false;
                }

                diagnosticCode = string.Empty;
                return true;
            }
            catch
            {
                if (File.Exists(plan.OutputPackagePath))
                {
                    File.Delete(plan.OutputPackagePath);
                }

                diagnosticCode = "UNITY_PACKAGE_EXPORT_FAILED";
                return false;
            }
        }

        /// <summary>Builds the exact package inventory and proves that all Assets dependencies are local.</summary>
        public static bool TryCreatePlan(UnityPackageExportRequest request, out UnityPackageExportPlan plan,
            out string diagnosticCode)
        {
            plan = null;
            diagnosticCode = "UNITY_PACKAGE_EXPORT_INVALID";
            if (request == null || !IsSafeProductRoot(request.ProductRootReference) ||
                !TryResolveOutput(request.OutputPackagePath, out string outputPath))
            {
                return false;
            }

            string root = request.ProductRootReference;
            if (!AssetDatabase.IsValidFolder(root))
            {
                diagnosticCode = "UNITY_PACKAGE_PRODUCT_ROOT_MISSING";
                return false;
            }

            string[] assets = AssetDatabase.GetAllAssetPaths()
                .Where(path => path == root || path.StartsWith(root + "/", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (assets.Length == 0 || assets.Distinct(StringComparer.OrdinalIgnoreCase).Count() != assets.Length)
            {
                diagnosticCode = "UNITY_PACKAGE_DUPLICATE_ASSET_PATH";
                return false;
            }

            foreach (string asset in assets)
            {
                if (!HasApprovedProductPath(root, asset))
                {
                    diagnosticCode = "UNITY_PACKAGE_PATH_INVALID";
                    return false;
                }
            }

            string[] fileAssets = assets.Where(path => !AssetDatabase.IsValidFolder(path)).ToArray();
            string[] dependencies = AssetDatabase.GetDependencies(fileAssets, true)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (dependencies.Any(path => path.StartsWith("Assets/", StringComparison.Ordinal) &&
                path != root && !path.StartsWith(root + "/", StringComparison.Ordinal)))
            {
                diagnosticCode = "UNITY_PACKAGE_DEPENDENCY_OUTSIDE_PRODUCT";
                return false;
            }

            plan = new UnityPackageExportPlan(root, outputPath, Array.AsReadOnly(assets));
            diagnosticCode = string.Empty;
            return true;
        }

        private static bool IsSafeProductRoot(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith("Assets/", StringComparison.Ordinal) &&
                value.IndexOf('\\') < 0 && value.IndexOf(':') < 0 &&
                value.IndexOf("/../", StringComparison.Ordinal) < 0 &&
                value.IndexOf("/./", StringComparison.Ordinal) < 0 &&
                !value.EndsWith("/", StringComparison.Ordinal) &&
                value.IndexOf("_Template", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool HasApprovedProductPath(string root, string asset)
        {
            if (asset == root)
            {
                return true;
            }

            string relative = asset.Substring(root.Length + 1);
            int separator = relative.IndexOf('/');
            string first = separator < 0 ? relative : relative.Substring(0, separator);
            return AllowedProductFolders.Contains(first);
        }

        private static bool TryResolveOutput(string value, out string outputPath)
        {
            outputPath = string.Empty;
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value) ||
                !value.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(value);
            string prefix = projectRoot + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string relative = candidate.Substring(prefix.Length).Replace('\\', '/');
            if (relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("ProjectSettings/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("Library/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            outputPath = candidate;
            return true;
        }
    }
}
