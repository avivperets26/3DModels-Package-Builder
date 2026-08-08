using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Represents one captured warning or error attributable to generated package content.</summary>
    public sealed class UnityPackageLogEntry
    {
        public UnityPackageLogEntry(LogType type, string message)
        {
            Type = type;
            Message = message ?? string.Empty;
        }

        public LogType Type { get; }

        public string Message { get; }
    }

    /// <summary>Describes the exact generated asset set and execution state to validate before release.</summary>
    public sealed class UnityPackageValidationRequest
    {
        public string ProductRootReference { get; set; }

        public IReadOnlyList<string> ExpectedAssetReferences { get; set; }

        public IReadOnlyList<UnityPackageLogEntry> PackageLogs { get; set; }

        public bool CompilationFailed { get; set; }
    }

    /// <summary>One stable release-blocking Unity validation finding.</summary>
    public sealed class UnityPackageValidationFinding
    {
        public UnityPackageValidationFinding(string code, string assetReference)
        {
            Code = code;
            AssetReference = assetReference ?? string.Empty;
        }

        public string Code { get; }

        public string AssetReference { get; }
    }

    /// <summary>Immutable validation result used by worker reporting and release gates.</summary>
    public sealed class UnityPackageValidationReport
    {
        public UnityPackageValidationReport(IReadOnlyList<UnityPackageValidationFinding> findings)
        {
            Findings = findings;
        }

        public IReadOnlyList<UnityPackageValidationFinding> Findings { get; }

        public bool IsSuccessful => Findings.Count == 0;
    }

    /// <summary>Validates generated Unity references, paths, logs, and GUID integrity before export.</summary>
    public static class UnityPackageValidator
    {
        private static readonly Regex GuidPattern = new Regex(
            @"\bguid:\s*(?<guid>[0-9a-fA-F]{32})\b", RegexOptions.CultureInvariant);

        /// <summary>Returns every deterministic blocking finding; it never repairs or mutates content.</summary>
        public static UnityPackageValidationReport Validate(UnityPackageValidationRequest request)
        {
            var findings = new List<UnityPackageValidationFinding>();
            if (request == null || string.IsNullOrEmpty(request.ProductRootReference) ||
                !AssetDatabase.IsValidFolder(request.ProductRootReference))
            {
                Add(findings, "UNITY_VALIDATION_PATH_INVALID", string.Empty);
                return Report(findings);
            }

            string root = request.ProductRootReference;
            string[] actual = AssetDatabase.GetAllAssetPaths()
                .Where(path => path == root || path.StartsWith(root + "/", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            string[] expected = (request.ExpectedAssetReferences ?? Array.Empty<string>())
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (expected.Distinct(StringComparer.OrdinalIgnoreCase).Count() != expected.Length)
            {
                Add(findings, "UNITY_VALIDATION_DUPLICATE_PATH", root);
            }

            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                Add(findings, "UNITY_VALIDATION_EXPORT_SET_MISMATCH", root);
            }

            if (actual.Distinct(StringComparer.OrdinalIgnoreCase).Count() != actual.Length)
            {
                Add(findings, "UNITY_VALIDATION_DUPLICATE_PATH", root);
            }

            ValidatePaths(root, actual, findings);
            ValidateMetadata(actual, findings);
            ValidateDependencies(root, actual, findings);
            ValidateLoadedContent(actual, findings);
            ValidateLogs(request, findings);
            return Report(findings);
        }

        private static void ValidatePaths(string root, IEnumerable<string> assets,
            ICollection<UnityPackageValidationFinding> findings)
        {
            foreach (string asset in assets)
            {
                if (asset.IndexOf('\\') >= 0 || asset.IndexOf("_Template", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    asset.IndexOf("/../", StringComparison.Ordinal) >= 0)
                {
                    Add(findings, "UNITY_VALIDATION_PATH_INVALID", asset);
                }

                if (asset == root || AssetDatabase.IsValidFolder(asset))
                {
                    continue;
                }

                string relative = asset.Substring(root.Length + 1);
                string extension = Path.GetExtension(asset).ToLowerInvariant();
                string expectedFolder = extension switch
                {
                    ".cs" or ".asmdef" => "Scripts/",
                    ".unity" => "Scenes/",
                    ".prefab" => "Prefabs/",
                    ".mat" => "Materials/",
                    ".asset" => "Meshes/",
                    ".fbx" or ".glb" => "Source/",
                    ".png" or ".jpg" or ".jpeg" or ".tga" or ".tif" or ".tiff" => "Textures/",
                    ".txt" or ".md" or ".pdf" => "Documentation/",
                    _ => string.Empty,
                };
                if (string.IsNullOrEmpty(expectedFolder) ||
                    !relative.StartsWith(expectedFolder, StringComparison.Ordinal))
                {
                    Add(findings, "UNITY_VALIDATION_PATH_INVALID", asset);
                }
            }
        }

        private static void ValidateMetadata(IEnumerable<string> assets,
            ICollection<UnityPackageValidationFinding> findings)
        {
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string asset in assets)
            {
                string meta = ToPhysicalPath(asset) + ".meta";
                if (!File.Exists(meta))
                {
                    Add(findings, "UNITY_VALIDATION_META_MISSING", asset);
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(asset);
                if (string.IsNullOrEmpty(guid) || !guids.Add(guid))
                {
                    Add(findings, "UNITY_VALIDATION_DUPLICATE_GUID", asset);
                }

                if (!AssetDatabase.IsValidFolder(asset))
                {
                    ValidateYamlGuids(asset, findings);
                }
            }
        }

        private static void ValidateYamlGuids(string asset,
            ICollection<UnityPackageValidationFinding> findings)
        {
            string extension = Path.GetExtension(asset).ToLowerInvariant();
            if (extension != ".unity" && extension != ".prefab" && extension != ".mat" &&
                extension != ".asset" && extension != ".controller" && extension != ".meta")
            {
                return;
            }

            string physical = ToPhysicalPath(asset);
            if (!File.Exists(physical))
            {
                Add(findings, "UNITY_VALIDATION_FILE_MISSING", asset);
                return;
            }

            foreach (Match match in GuidPattern.Matches(File.ReadAllText(physical)))
            {
                string guid = match.Groups["guid"].Value;
                if (guid == "0000000000000000e000000000000000" ||
                    guid == "0000000000000000f000000000000000")
                {
                    continue;
                }

                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    Add(findings, extension == ".mat" ? "UNITY_VALIDATION_MISSING_TEXTURE" :
                        "UNITY_VALIDATION_BROKEN_GUID", asset);
                }
            }
        }

        private static void ValidateDependencies(string root, IEnumerable<string> assets,
            ICollection<UnityPackageValidationFinding> findings)
        {
            string[] files = assets.Where(path => !AssetDatabase.IsValidFolder(path)).ToArray();
            foreach (string dependency in AssetDatabase.GetDependencies(files, true))
            {
                if (dependency.StartsWith("Assets/", StringComparison.Ordinal) && dependency != root &&
                    !dependency.StartsWith(root + "/", StringComparison.Ordinal))
                {
                    Add(findings, "UNITY_VALIDATION_DEPENDENCY_OUTSIDE_PRODUCT", dependency);
                }
            }
        }

        private static void ValidateLoadedContent(IEnumerable<string> assets,
            ICollection<UnityPackageValidationFinding> findings)
        {
            foreach (string prefabReference in assets.Where(path => path.EndsWith(
                ".prefab", StringComparison.OrdinalIgnoreCase)))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabReference);
                ValidateGameObject(prefab, prefabReference, findings);
            }

            foreach (string sceneReference in assets.Where(path => path.EndsWith(
                ".unity", StringComparison.OrdinalIgnoreCase)))
            {
                var scene = EditorSceneManager.OpenScene(sceneReference, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    ValidateGameObject(root, sceneReference, findings);
                }
            }
        }

        private static void ValidateGameObject(GameObject root, string assetReference,
            ICollection<UnityPackageValidationFinding> findings)
        {
            if (root == null)
            {
                Add(findings, "UNITY_VALIDATION_FILE_MISSING", assetReference);
                return;
            }

            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) > 0)
            {
                Add(findings, "UNITY_VALIDATION_MISSING_SCRIPT", assetReference);
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sharedMaterials.Any(material => material == null))
                {
                    Add(findings, "UNITY_VALIDATION_MISSING_MATERIAL", assetReference);
                }
            }
        }

        private static void ValidateLogs(UnityPackageValidationRequest request,
            ICollection<UnityPackageValidationFinding> findings)
        {
            if (request.CompilationFailed || EditorUtility.scriptCompilationFailed)
            {
                Add(findings, "UNITY_VALIDATION_COMPILATION_FAILED", request.ProductRootReference);
            }

            foreach (UnityPackageLogEntry entry in request.PackageLogs ?? Array.Empty<UnityPackageLogEntry>())
            {
                if (entry.Type == LogType.Warning)
                {
                    Add(findings, "UNITY_VALIDATION_PACKAGE_WARNING", entry.Message);
                }
                else if (entry.Type == LogType.Error || entry.Type == LogType.Exception ||
                    entry.Type == LogType.Assert)
                {
                    Add(findings, "UNITY_VALIDATION_PACKAGE_ERROR", entry.Message);
                }
            }
        }

        private static string ToPhysicalPath(string assetReference)
        {
            return Path.Combine(Application.dataPath,
                assetReference.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
        }

        private static UnityPackageValidationReport Report(IEnumerable<UnityPackageValidationFinding> findings)
        {
            return new UnityPackageValidationReport(Array.AsReadOnly(findings
                .OrderBy(value => value.Code, StringComparer.Ordinal)
                .ThenBy(value => value.AssetReference, StringComparer.Ordinal)
                .ToArray()));
        }

        private static void Add(ICollection<UnityPackageValidationFinding> findings, string code,
            string assetReference)
        {
            if (!findings.Any(value => value.Code == code && value.AssetReference == assetReference))
            {
                findings.Add(new UnityPackageValidationFinding(code, assetReference));
            }
        }
    }
}
