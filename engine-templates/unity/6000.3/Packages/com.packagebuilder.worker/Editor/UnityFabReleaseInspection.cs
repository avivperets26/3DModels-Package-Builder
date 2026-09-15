using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PackageBuilder.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Test-only bridge from a completed clean import to measured release inputs.
    /// Preparation exports a disposable fixture; inspection reuses validation and capture without repairing it.</summary>
    public static class UnityFabReleaseInspection
    {
        private static double started;
        private static bool opened;

        /// <summary>Creates a distinct static release fixture from the imported regression product.
        /// Texture semantics and offline instructions replace intentionally repeated test patterns.
        /// The result must be exported and clean-imported again before release inspection.</summary>
        public static void Prepare()
        {
            try
            {
                const string root = "Assets/PBModelTests";
                string[] names = { "albedo", "normal", "emission", "ambient-occlusion" };
                Color[] colors = { new Color(.45f, .35f, .2f), new Color(.5f, .5f, 1f), Color.black, Color.white };
                for (int i = 0; i < names.Length; i++)
                {
                    var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    try
                    {
                        texture.SetPixels(Enumerable.Repeat(colors[i], 16).ToArray()); texture.Apply();
                        string path = root + "/Textures/" + names[i] + ".png";
                        File.WriteAllBytes(path, texture.EncodeToPNG());
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(texture); }
                }
                string readme = root + "/Documentation/README.txt";
                File.WriteAllText(readme, "StoneArch static validation cube\nUnity " + Application.unityVersion + " with URP. See the release README for exact dependencies.\n"
                    + "Create a URP project, install its dependencies, then import this package.\n"
                    + "Open Scenes/S_StoneArch_Overview.unity and enter Play mode. Use the visible controls to orbit, zoom and reset.\n"
                    + "Drag Prefabs/P_StoneArch.prefab into your own scene. Source FBX, mesh, material and textures are included.\n"
                    + "This disposable fixture uses repository preview code; the final release README lists measured metrics and dependencies.\n");
                AssetDatabase.ImportAsset(readme, ImportAssetOptions.ForceSynchronousImport);
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                if (!UnityPackageExporter.TryExport(new UnityPackageExportRequest { ProductRootReference = root,
                    OutputPackagePath = Path.Combine(project, "PackageBuilderExports", "StoneArchFab.unitypackage") },
                    out UnityPackageExportPlan plan, out string code)) throw new InvalidOperationException(code);
                File.WriteAllLines(Path.Combine(Path.GetDirectoryName(project), "fab-package-assets.txt"), plan.AssetReferences);
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Execute;
        }

        private static void Execute()
        {
            if (EditorApplication.timeSinceStartup - started < 5 || ShaderUtil.anythingCompiling) return;
            try
            {
                const string root = "Assets/PBModelTests";
                if (!opened)
                {
                    EditorSceneManager.OpenScene(root + "/Scenes/S_StoneArch_Overview.unity");
                    opened = true;
                    started = EditorApplication.timeSinceStartup;
                    return;
                }
                EditorApplication.update -= Execute;
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string run = Path.GetDirectoryName(project);
                string[] expected = File.ReadAllLines(Path.Combine(run, "fab-package-assets.txt"));
                var report = UnityPackageValidator.Validate(new UnityPackageValidationRequest
                {
                    ProductRootReference = root, ExpectedAssetReferences = expected,
                    CompilationFailed = EditorUtility.scriptCompilationFailed,
                    PackageLogs = Array.Empty<UnityPackageLogEntry>()
                });
                if (!report.IsSuccessful) throw new InvalidOperationException(string.Join(",", report.Findings.Select(f => f.Code)));
                var preview = UnityEngine.Object.FindFirstObjectByType<PackageBuilderPreviewController>();
                if (!UnityStillImageCapture.TryCapture(preview, "fab-release-" + Guid.NewGuid().ToString("N").Substring(0, 8), out UnityStillImageReceipt[] images, out string code))
                    throw new InvalidOperationException(code);
                string[] files = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(root + "/", StringComparison.Ordinal)
                    && !AssetDatabase.IsValidFolder(p)).OrderBy(p => p, StringComparer.Ordinal).ToArray();
                string[] entryPoints = files.Where(p => p.EndsWith(".unity", StringComparison.Ordinal)
                    || p.EndsWith(".prefab", StringComparison.Ordinal)).ToArray();
                // Traverse direct edges to retain distinct, referenced files even when Unity internally
                // deduplicates their imported content. Include compilation units of referenced scripts.
                var used = new HashSet<string>(StringComparer.Ordinal);
                var pending = new Queue<string>(entryPoints);
                while (pending.Count != 0)
                {
                    string path = pending.Dequeue();
                    if (!used.Add(path)) continue;
                    foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                        if (dependency.StartsWith(root + "/", StringComparison.Ordinal) && !used.Contains(dependency)) pending.Enqueue(dependency);
                }
                foreach (var assembly in UnityEditor.Compilation.CompilationPipeline.GetAssemblies())
                {
                    if (!assembly.sourceFiles.Any(used.Contains)) continue;
                    foreach (string source in assembly.sourceFiles) used.Add(source);
                    string definition = UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
                    if (!string.IsNullOrEmpty(definition)) used.Add(definition);
                }
                var result = new Snapshot
                {
                    unityVersion = Application.unityVersion, productRoot = root, images = images,
                    assets = files.Select(p => new Asset
                    {
                        path = p, sha256 = Hash(p), bytes = new FileInfo(p).Length,
                        dependencies = AssetDatabase.GetDependencies(p, false).Where(d => d != p
                            && d != "Resources/unity_builtin_extra" && d != "Library/unity default resources").ToArray(),
                        isUsed = used.Contains(p) || p.StartsWith(root + "/Documentation/", StringComparison.Ordinal)
                            || p.StartsWith(root + "/Source/", StringComparison.Ordinal)
                    }).ToArray()
                };
                if (!preview.TryGetProductBounds(out Bounds bounds)) throw new InvalidOperationException("Missing product bounds.");
                result.width = bounds.size.x; result.height = bounds.size.y; result.depth = bounds.size.z;
                result.triangles = preview.PreviewTarget.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f => f.sharedMesh != null).Sum(f => f.sharedMesh.triangles.Length / 3);
                result.materials = preview.PreviewTarget.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials).Distinct().Count();
                result.textures = result.assets.Count(a => AssetDatabase.LoadAssetAtPath<Texture>(a.path) != null);
                File.WriteAllText(Path.Combine(run, "fab-inspection.json"), JsonUtility.ToJson(result, true));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                EditorApplication.update -= Execute;
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        [Serializable] private sealed class Snapshot
        {
            public string unityVersion;
            public string productRoot;
            public Asset[] assets;
            public UnityStillImageReceipt[] images;
            public float width, height, depth;
            public int triangles, materials, textures;
        }
        [Serializable] private sealed class Asset
        {
            public string path;
            public string sha256;
            public long bytes;
            public string[] dependencies;
            public bool isUsed;
        }
    }
}
