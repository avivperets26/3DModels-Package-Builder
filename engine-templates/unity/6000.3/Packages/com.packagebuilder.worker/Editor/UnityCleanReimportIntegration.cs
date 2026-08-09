using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PackageBuilder.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>One stable, structured finding produced by the clean Unity reimport check.</summary>
    [Serializable]
    public sealed class UnityCleanReimportFinding
    {
        public string code = string.Empty;
        public string assetReference = string.Empty;
    }

    /// <summary>Machine-readable result retained outside the disposable clean Unity project.</summary>
    [Serializable]
    public sealed class UnityCleanReimportResult
    {
        public int schemaVersion = 1;
        public bool passed;
        public string productRootReference = string.Empty;
        public string sceneReference = string.Empty;
        public string prefabReference = string.Empty;
        public int rendererCount;
        public int materialCount;
        public int textureCount;
        public UnityCleanReimportFinding[] findings = Array.Empty<UnityCleanReimportFinding>();
    }

    /// <summary>
    /// Validates a package after Unity imports it into a newly cloned project with no prior
    /// product assets. The command-line harness owns import timing and process isolation.
    /// </summary>
    public static class UnityCleanReimportIntegration
    {
        private const string DefaultProductRoot = "Assets/PBModelTests";
        private const string DefaultScene = "Assets/PBModelTests/Scenes/S_StoneArch_Overview.unity";
        private const string DefaultPrefab = "Assets/PBModelTests/Prefabs/P_StoneArch.prefab";

        /// <summary>Runs validation, writes its structured result atomically, and exits Unity.</summary>
        public static void Run()
        {
            string resultPath = Environment.GetEnvironmentVariable(
                "PACKAGEBUILDER_UNITY_REIMPORT_RESULT");
            var result = new UnityCleanReimportResult
            {
                productRootReference = EnvironmentValue(
                    "PACKAGEBUILDER_UNITY_PRODUCT_ROOT", DefaultProductRoot),
                sceneReference = EnvironmentValue(
                    "PACKAGEBUILDER_UNITY_OVERVIEW_SCENE", DefaultScene),
                prefabReference = EnvironmentValue(
                    "PACKAGEBUILDER_UNITY_PRODUCT_PREFAB", DefaultPrefab),
            };
            var findings = new List<UnityCleanReimportFinding>();

            try
            {
                Validate(result, findings);
            }
            catch (Exception exception)
            {
                findings.Add(Finding("UNITY_REIMPORT_VALIDATION_EXCEPTION", exception.GetType().Name));
            }

            result.findings = findings
                .OrderBy(value => value.code, StringComparer.Ordinal)
                .ThenBy(value => value.assetReference, StringComparer.Ordinal)
                .ToArray();
            result.passed = result.findings.Length == 0;

            try
            {
                WriteResult(resultPath, result);
            }
            catch (Exception exception)
            {
                Debug.LogError("PACKAGEBUILDER_UNITY_REIMPORT_RESULT_WRITE_FAILED:" +
                    exception.GetType().Name);
                EditorApplication.Exit(1);
                return;
            }

            if (result.passed)
            {
                Debug.Log("PACKAGEBUILDER_UNITY_CLEAN_REIMPORT_PASS");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("PACKAGEBUILDER_UNITY_CLEAN_REIMPORT_FAIL:" +
                    string.Join(",", result.findings.Select(value => value.code)));
                EditorApplication.Exit(1);
            }
        }

        private static void Validate(
            UnityCleanReimportResult result,
            List<UnityCleanReimportFinding> findings)
        {
            if (!AssetDatabase.IsValidFolder(result.productRootReference))
            {
                findings.Add(Finding("UNITY_REIMPORT_PRODUCT_ROOT_MISSING", result.productRootReference));
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.prefabReference);
            if (prefab == null)
            {
                findings.Add(Finding("UNITY_REIMPORT_PREFAB_MISSING", result.prefabReference));
                return;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(result.sceneReference);
            if (sceneAsset == null)
            {
                findings.Add(Finding("UNITY_REIMPORT_SCENE_MISSING", result.sceneReference));
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(result.sceneReference, OpenSceneMode.Single);
            GameObject root = UnityOverviewSceneTemplateBuilder.FindUniqueRoot(
                scene,
                UnityOverviewSceneTemplateBuilder.OverviewRootName);
            PackageBuilderPreviewController controller = root == null
                ? null
                : root.GetComponent<PackageBuilderPreviewController>();
            if (root == null || controller == null || controller.PreviewTarget == null ||
                controller.PreviewCamera == null || controller.KeyLight == null ||
                controller.StudioBackground == null || controller.PreviewTarget.childCount != 1)
            {
                findings.Add(Finding("UNITY_REIMPORT_SCENE_REFERENCES_INVALID", result.sceneReference));
                return;
            }

            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) != 0)
            {
                findings.Add(Finding("UNITY_REIMPORT_MISSING_SCRIPT", result.sceneReference));
            }

            Renderer[] renderers = controller.PreviewTarget.GetComponentsInChildren<Renderer>(true);
            result.rendererCount = renderers.Length;
            if (renderers.Length == 0)
            {
                findings.Add(Finding("UNITY_REIMPORT_RENDERER_MISSING", result.prefabReference));
                return;
            }

            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            foreach (Renderer rendererValue in renderers)
            {
                if (rendererValue == null || !rendererValue.enabled)
                {
                    continue;
                }

                foreach (Material material in rendererValue.sharedMaterials)
                {
                    if (material == null)
                    {
                        findings.Add(Finding("UNITY_REIMPORT_MATERIAL_MISSING", result.prefabReference));
                        continue;
                    }

                    _ = materials.Add(material);
                    string materialPath = AssetDatabase.GetAssetPath(material);
                    if (!WithinRoot(materialPath, result.productRootReference))
                    {
                        findings.Add(Finding("UNITY_REIMPORT_MATERIAL_OUTSIDE_PRODUCT", materialPath));
                    }

                    foreach (string propertyName in material.GetTexturePropertyNames())
                    {
                        Texture texture = material.GetTexture(propertyName);
                        if (texture == null)
                        {
                            continue;
                        }

                        _ = textures.Add(texture);
                        string texturePath = AssetDatabase.GetAssetPath(texture);
                        if (!WithinRoot(texturePath, result.productRootReference))
                        {
                            findings.Add(Finding("UNITY_REIMPORT_TEXTURE_OUTSIDE_PRODUCT", texturePath));
                        }
                    }
                }
            }

            result.materialCount = materials.Count;
            result.textureCount = textures.Count;
            if (materials.Count == 0 || textures.Count == 0)
            {
                findings.Add(Finding("UNITY_REIMPORT_MATERIAL_REFERENCES_EMPTY", result.prefabReference));
            }

            if (!controller.AutoFrame() || !controller.TryGetProductBounds(out Bounds bounds))
            {
                findings.Add(Finding("UNITY_REIMPORT_CAMERA_FRAME_FAILED", result.sceneReference));
                return;
            }

            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(controller.PreviewCamera);
            if (!GeometryUtility.TestPlanesAABB(frustum, bounds))
            {
                findings.Add(Finding("UNITY_REIMPORT_PREFAB_NOT_RENDERABLE", result.prefabReference));
                return;
            }

            var renderTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = controller.PreviewCamera.targetTexture;
            try
            {
                if (!renderTexture.Create())
                {
                    findings.Add(Finding("UNITY_REIMPORT_RENDER_TARGET_FAILED", result.sceneReference));
                    return;
                }
                controller.PreviewCamera.targetTexture = renderTexture;
                controller.PreviewCamera.Render();
            }
            finally
            {
                controller.PreviewCamera.targetTexture = previous;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static bool WithinRoot(string assetReference, string root) =>
            string.Equals(assetReference, root, StringComparison.Ordinal) ||
            assetReference.StartsWith(root + "/", StringComparison.Ordinal);

        private static UnityCleanReimportFinding Finding(string code, string reference) => new()
        {
            code = code,
            assetReference = reference ?? string.Empty,
        };

        private static string EnvironmentValue(string name, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static void WriteResult(string path, UnityCleanReimportResult result)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            {
                throw new InvalidOperationException("The clean-reimport result path is invalid.");
            }

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("The clean-reimport result directory is missing.");
            }

            _ = Directory.CreateDirectory(directory);
            string temporary = path + ".partial";
            File.WriteAllText(temporary, JsonUtility.ToJson(result, true));
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temporary, path);
        }
    }
}
