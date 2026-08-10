using System;
using System.Linq;
using PackageBuilder.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Describes the output location for a product-free overview scene template.</summary>
    public sealed class UnityOverviewSceneTemplateRequest
    {
        public string OutputSceneReference { get; set; }
    }

    /// <summary>Describes one deterministic product composition into an overview scene.</summary>
    public sealed class UnityOverviewSceneCompositionRequest
    {
        public string AssetId { get; set; }

        public string TemplateSceneReference { get; set; }

        public string ProductPrefabReference { get; set; }

        public string PreviewControllerScriptReference { get; set; }

        public string OutputBackgroundMaterialReference { get; set; }

        public string OutputBackgroundTextureReference { get; set; }

        public string OutputSceneReference { get; set; }
    }

    /// <summary>Creates the reusable, product-free URP overview scene definition.</summary>
    public static class UnityOverviewSceneTemplateBuilder
    {
        public const string OverviewRootName = "PackageBuilderOverview";
        public const string PreviewTargetName = "PreviewTarget";
        public const string CameraName = "Main Camera";
        public const string BackgroundName = "Background";
        public const string KeyLightName = "Key Light";
        public const string FillLightName = "Fill Light";

        /// <summary>Creates and verifies a clean scene template at the requested asset reference.</summary>
        public static bool TryCreate(
            UnityOverviewSceneTemplateRequest request,
            out Scene scene,
            out string diagnosticCode)
        {
            scene = default;
            diagnosticCode = "UNITY_OVERVIEW_TEMPLATE_INVALID";
            if (request == null || !IsSceneReference(request.OutputSceneReference) ||
                AssetDatabase.LoadMainAssetAtPath(request.OutputSceneReference) != null)
            {
                return false;
            }

            string folder = FolderOf(request.OutputSceneReference);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                diagnosticCode = "UNITY_OVERVIEW_TEMPLATE_FOLDER_MISSING";
                return false;
            }

            string materialReference = folder + "/M_OverviewBackground.mat";
            string textureReference = folder + "/T_OverviewBackground.png";
            if (AssetDatabase.LoadMainAssetAtPath(materialReference) != null)
            {
                diagnosticCode = "UNITY_OVERVIEW_TEMPLATE_OUTPUT_COLLISION";
                return false;
            }

            Material backgroundMaterial = null;
            try
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    diagnosticCode = "UNITY_OVERVIEW_TEMPLATE_URP_SHADER_MISSING";
                    return false;
                }

                CreateRadialBackgroundTexture(textureReference);
                Texture2D backgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureReference);
                if (backgroundTexture == null)
                {
                    diagnosticCode = "UNITY_OVERVIEW_TEMPLATE_BACKGROUND_TEXTURE_MISSING";
                    return false;
                }

                backgroundMaterial = new Material(shader)
                {
                    name = "M_OverviewBackground",
                };
                backgroundMaterial.SetTexture("_BaseMap", backgroundTexture);
                backgroundMaterial.SetColor("_BaseColor", Color.white);
                backgroundMaterial.SetFloat("_Cull", 0f);
                AssetDatabase.CreateAsset(backgroundMaterial, materialReference);

                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject(OverviewRootName);
                Reset(root.transform);

                var previewTarget = new GameObject(PreviewTargetName);
                previewTarget.transform.SetParent(root.transform, false);
                Reset(previewTarget.transform);

                var cameraObject = new GameObject(CameraName);
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.position = new Vector3(4f, 2.75f, -4f);
                var previewCamera = cameraObject.AddComponent<Camera>();
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
                previewCamera.fieldOfView = 35f;

                Light keyLight = CreateDirectionalLight(
                    root.transform, KeyLightName, new Vector3(42f, -32f, 0f),
                    1.15f, new Color(1f, 0.94f, 0.84f, 1f));
                CreateDirectionalLight(root.transform, FillLightName, new Vector3(25f, 145f, 0f),
                    0.55f, new Color(0.62f, 0.75f, 1f, 1f));

                GameObject background = GameObject.CreatePrimitive(PrimitiveType.Quad);
                background.name = BackgroundName;
                background.transform.SetParent(root.transform, false);
                UnityEngine.Object.DestroyImmediate(background.GetComponent<Collider>());
                background.GetComponent<Renderer>().sharedMaterial = backgroundMaterial;

                var controller = root.AddComponent<PackageBuilderPreviewController>();
                root.AddComponent<PackageBuilderAnimationTransport>();
                controller.Configure(previewTarget.transform, previewCamera, keyLight, background.transform);
                controller.ConfigureAnimation(null);

                if (!EditorSceneManager.SaveScene(scene, request.OutputSceneReference) ||
                    !VerifyTemplate(scene, out diagnosticCode))
                {
                    return false;
                }

                AssetDatabase.SaveAssets();
                diagnosticCode = string.Empty;
                return true;
            }
            catch
            {
                diagnosticCode = "UNITY_OVERVIEW_TEMPLATE_CREATE_FAILED";
                return false;
            }
            finally
            {
                if (!string.IsNullOrEmpty(diagnosticCode))
                {
                    AssetDatabase.DeleteAsset(request.OutputSceneReference);
                    AssetDatabase.DeleteAsset(materialReference);
                    AssetDatabase.DeleteAsset(textureReference);
                }
            }
        }

        /// <summary>Checks the exact generic hierarchy and guarantees that no product is retained.</summary>
        public static bool VerifyTemplate(Scene scene, out string diagnosticCode)
        {
            GameObject root = FindUniqueRoot(scene, OverviewRootName);
            if (root == null || !IsReset(root.transform))
            {
                diagnosticCode = "UNITY_OVERVIEW_TEMPLATE_ROOT_INVALID";
                return false;
            }

            Transform previewTarget = FindUniqueChild(root.transform, PreviewTargetName);
            Camera previewCamera = FindUniqueChild(root.transform, CameraName)?.GetComponent<Camera>();
            PackageBuilderPreviewController controller = root.GetComponent<PackageBuilderPreviewController>();
            PackageBuilderAnimationTransport transport =
                root.GetComponent<PackageBuilderAnimationTransport>();
            bool valid = previewTarget != null && previewTarget.childCount == 0 &&
                previewCamera != null && controller != null && transport != null &&
                controller.PreviewTarget == previewTarget && controller.PreviewCamera == previewCamera &&
                controller.AnimationTransport == transport && !transport.Available &&
                controller.StudioBackground == FindUniqueChild(root.transform, BackgroundName) &&
                controller.KeyLight == FindUniqueChild(root.transform, KeyLightName)?.GetComponent<Light>() &&
                FindUniqueChild(root.transform, FillLightName)?.GetComponent<Light>() != null &&
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) == 0;
            diagnosticCode = valid ? string.Empty : "UNITY_OVERVIEW_TEMPLATE_CONTENT_INVALID";
            return valid;
        }

        internal static GameObject FindUniqueRoot(Scene scene, string name)
        {
            GameObject[] matches = scene.GetRootGameObjects()
                .Where(value => value.name == name)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        internal static Transform FindUniqueChild(Transform root, string name)
        {
            Transform[] matches = root.GetComponentsInChildren<Transform>(true)
                .Where(value => value != root && value.name == name)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        internal static bool IsSceneReference(string value)
        {
            return IsSafeAssetReference(value) && value.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsSafeAssetReference(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith("Assets/", StringComparison.Ordinal) &&
                value.IndexOf('\\') < 0 && value.IndexOf(':') < 0 &&
                value.IndexOf("/../", StringComparison.Ordinal) < 0 &&
                value.IndexOf("/./", StringComparison.Ordinal) < 0 &&
                !value.EndsWith("/", StringComparison.Ordinal);
        }

        internal static string FolderOf(string assetReference)
        {
            return assetReference.Substring(0, assetReference.LastIndexOf('/'));
        }

        internal static void Reset(Transform value)
        {
            value.localPosition = Vector3.zero;
            value.localRotation = Quaternion.identity;
            value.localScale = Vector3.one;
        }

        internal static bool IsReset(Transform value)
        {
            return value.localPosition == Vector3.zero && value.localRotation == Quaternion.identity &&
                value.localScale == Vector3.one;
        }

        private static Light CreateDirectionalLight(
            Transform parent,
            string name,
            Vector3 eulerAngles,
            float intensity,
            Color colour)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localRotation = Quaternion.Euler(eulerAngles);
            var lightValue = lightObject.AddComponent<Light>();
            lightValue.type = LightType.Directional;
            lightValue.intensity = intensity;
            lightValue.color = colour;
            lightValue.shadows = LightShadows.Soft;
            return lightValue;
        }

        private static void CreateRadialBackgroundTexture(string assetReference)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            try
            {
                var pixels = new Color32[size * size];
                Vector2 centre = new(0.5f, 0.58f);
                Color outer = new(0.012f, 0.014f, 0.018f, 1f);
                Color inner = new(0.14f, 0.16f, 0.20f, 1f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 uv = new((x + 0.5f) / size, (y + 0.5f) / size);
                        Vector2 delta = uv - centre;
                        delta.x *= 0.82f;
                        float radial = Mathf.Clamp01(delta.magnitude / 0.7f);
                        float blend = radial * radial * (3f - 2f * radial);
                        pixels[y * size + x] = Color.Lerp(inner, outer, blend);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                string physicalPath = System.IO.Path.Combine(
                    Application.dataPath,
                    assetReference.Substring("Assets/".Length).Replace('/', System.IO.Path.DirectorySeparatorChar));
                System.IO.File.WriteAllBytes(physicalPath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(assetReference, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(assetReference) as TextureImporter;
                if (importer != null)
                {
                    importer.sRGBTexture = true;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }

    /// <summary>Composes exactly one requested product prefab into a clean overview scene.</summary>
    public static class UnityOverviewSceneComposer
    {
        /// <summary>Creates, frames, saves, reopens, and verifies the product overview scene.</summary>
        public static bool TryCompose(
            UnityOverviewSceneCompositionRequest request,
            out Scene scene,
            out string diagnosticCode)
        {
            scene = default;
            diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_INVALID";
            if (!Validate(request))
            {
                return false;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(request.ProductPrefabReference);
            MonoScript controllerScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
                request.PreviewControllerScriptReference);
            string templateFolder = UnityOverviewSceneTemplateBuilder.FolderOf(
                request.TemplateSceneReference);
            string templateMaterialReference = templateFolder + "/M_OverviewBackground.mat";
            string templateTextureReference = templateFolder + "/T_OverviewBackground.png";
            Material templateMaterial = AssetDatabase.LoadAssetAtPath<Material>(templateMaterialReference);
            Texture2D templateTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(templateTextureReference);
            if (prefab == null || controllerScript == null || templateMaterial == null ||
                templateTexture == null)
            {
                diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_REFERENCE_MISSING";
                return false;
            }

            try
            {
                scene = EditorSceneManager.OpenScene(request.TemplateSceneReference, OpenSceneMode.Single);
                GameObject root = UnityOverviewSceneTemplateBuilder.FindUniqueRoot(
                    scene,
                    UnityOverviewSceneTemplateBuilder.OverviewRootName);
                if (root == null)
                {
                    diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_TEMPLATE_INVALID";
                    return false;
                }

                Transform previewTarget = UnityOverviewSceneTemplateBuilder.FindUniqueChild(
                    root.transform,
                    UnityOverviewSceneTemplateBuilder.PreviewTargetName);
                var controller = root.GetComponent<PackageBuilderPreviewController>();
                if (previewTarget == null || previewTarget.childCount != 0 || controller == null)
                {
                    diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_TEMPLATE_NOT_EMPTY";
                    return false;
                }

                if (!AssetDatabase.CopyAsset(templateMaterialReference,
                    request.OutputBackgroundMaterialReference))
                {
                    diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_BACKGROUND_COPY_FAILED";
                    return false;
                }

                if (!AssetDatabase.CopyAsset(templateTextureReference,
                    request.OutputBackgroundTextureReference))
                {
                    diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_BACKGROUND_TEXTURE_COPY_FAILED";
                    return false;
                }

                Material productBackground = AssetDatabase.LoadAssetAtPath<Material>(
                    request.OutputBackgroundMaterialReference);
                Transform background = UnityOverviewSceneTemplateBuilder.FindUniqueChild(
                    root.transform,
                    UnityOverviewSceneTemplateBuilder.BackgroundName);
                Renderer backgroundRenderer = background == null ? null : background.GetComponent<Renderer>();
                if (productBackground == null || backgroundRenderer == null)
                {
                    diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_BACKGROUND_MISSING";
                    return false;
                }

                Texture2D productBackgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    request.OutputBackgroundTextureReference);
                if (productBackgroundTexture == null)
                {
                    diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_BACKGROUND_TEXTURE_MISSING";
                    return false;
                }
                productBackground.SetTexture("_BaseMap", productBackgroundTexture);
                backgroundRenderer.sharedMaterial = productBackground;
                controller.Configure(
                    previewTarget,
                    controller.PreviewCamera,
                    UnityOverviewSceneTemplateBuilder.FindUniqueChild(
                        root.transform,
                        UnityOverviewSceneTemplateBuilder.KeyLightName)?.GetComponent<Light>(),
                    background);

                var product = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (product == null)
                {
                    diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_PREFAB_FAILED";
                    return false;
                }

                product.transform.SetParent(previewTarget, false);
                UnityOverviewSceneTemplateBuilder.Reset(product.transform);
                Animator[] animators = product.GetComponentsInChildren<Animator>(true);
                if (animators.Length > 1)
                {
                    diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_ANIMATOR_COUNT_INVALID";
                    return false;
                }
                controller.ConfigureAnimation(animators.Length == 1 ? animators[0] : null);
                if (!controller.AutoFrame())
                {
                    diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_BOUNDS_MISSING";
                    return false;
                }

                if (!EditorSceneManager.SaveScene(scene, request.OutputSceneReference, true))
                {
                    diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_SAVE_FAILED";
                    return false;
                }

                scene = EditorSceneManager.OpenScene(request.OutputSceneReference, OpenSceneMode.Single);
                if (!VerifyComposition(scene, request, out diagnosticCode))
                {
                    AssetDatabase.DeleteAsset(request.OutputSceneReference);
                    return false;
                }

                AssetDatabase.SaveAssets();
                diagnosticCode = string.Empty;
                return true;
            }
            catch
            {
                diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_CREATE_FAILED";
                return false;
            }
            finally
            {
                if (!string.IsNullOrEmpty(diagnosticCode))
                {
                    AssetDatabase.DeleteAsset(request.OutputSceneReference);
                    AssetDatabase.DeleteAsset(request.OutputBackgroundMaterialReference);
                    AssetDatabase.DeleteAsset(request.OutputBackgroundTextureReference);
                }
            }
        }

        /// <summary>Verifies exact product identity, prefab linkage, controller linkage, and reset transforms.</summary>
        public static bool VerifyComposition(
            Scene scene,
            UnityOverviewSceneCompositionRequest request,
            out string diagnosticCode)
        {
            GameObject root = UnityOverviewSceneTemplateBuilder.FindUniqueRoot(
                scene,
                UnityOverviewSceneTemplateBuilder.OverviewRootName);
            Transform previewTarget = root == null ? null : UnityOverviewSceneTemplateBuilder.FindUniqueChild(
                root.transform,
                UnityOverviewSceneTemplateBuilder.PreviewTargetName);
            if (root == null || previewTarget == null || previewTarget.childCount != 1)
            {
                diagnosticCode = "UNITY_OVERVIEW_COMPOSITION_PRODUCT_COUNT_INVALID";
                return false;
            }

            Transform product = previewTarget.GetChild(0);
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(product.gameObject);
            var controller = root.GetComponent<PackageBuilderPreviewController>();
            var transport = root.GetComponent<PackageBuilderAnimationTransport>();
            MonoScript controllerScript = controller == null ? null : MonoScript.FromMonoBehaviour(controller);
            Transform background = UnityOverviewSceneTemplateBuilder.FindUniqueChild(
                root.transform,
                UnityOverviewSceneTemplateBuilder.BackgroundName);
            Renderer backgroundRenderer = background == null ? null : background.GetComponent<Renderer>();
            bool valid = product.name == "P_" + request.AssetId &&
                UnityOverviewSceneTemplateBuilder.IsReset(product) && source != null &&
                AssetDatabase.GetAssetPath(source) == request.ProductPrefabReference &&
                controller != null && controller.PreviewTarget == previewTarget &&
                transport != null && controller.AnimationTransport == transport &&
                controller.PreviewCamera != null && controller.KeyLight != null &&
                controller.StudioBackground == background &&
                AssetDatabase.GetAssetPath(controllerScript) == request.PreviewControllerScriptReference &&
                backgroundRenderer != null && backgroundRenderer.sharedMaterial != null &&
                AssetDatabase.GetAssetPath(backgroundRenderer.sharedMaterial) ==
                    request.OutputBackgroundMaterialReference &&
                AssetDatabase.GetAssetPath(backgroundRenderer.sharedMaterial.GetTexture("_BaseMap")) ==
                    request.OutputBackgroundTextureReference &&
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) == 0;
            Animator[] animators = product.GetComponentsInChildren<Animator>(true);
            valid = valid && animators.Length <= 1 &&
                (animators.Length == 0
                    ? !transport.Available
                    : transport.Available && transport.Animator == animators[0]);
            diagnosticCode = valid ? string.Empty : "UNITY_OVERVIEW_COMPOSITION_VERIFY_FAILED";
            return valid;
        }

        private static bool Validate(UnityOverviewSceneCompositionRequest request)
        {
            if (request == null || !IsAssetId(request.AssetId) ||
                !UnityOverviewSceneTemplateBuilder.IsSceneReference(request.TemplateSceneReference) ||
                !UnityOverviewSceneTemplateBuilder.IsSceneReference(request.OutputSceneReference) ||
                !UnityOverviewSceneTemplateBuilder.IsSafeAssetReference(request.ProductPrefabReference) ||
                !UnityOverviewSceneTemplateBuilder.IsSafeAssetReference(
                    request.PreviewControllerScriptReference) ||
                !UnityOverviewSceneTemplateBuilder.IsSafeAssetReference(
                    request.OutputBackgroundMaterialReference) ||
                !UnityOverviewSceneTemplateBuilder.IsSafeAssetReference(
                    request.OutputBackgroundTextureReference) ||
                !request.ProductPrefabReference.EndsWith(
                    "/Prefabs/P_" + request.AssetId + ".prefab",
                    StringComparison.Ordinal) ||
                !request.OutputSceneReference.EndsWith(
                    "/Scenes/S_" + request.AssetId + "_Overview.unity",
                    StringComparison.Ordinal) ||
                !request.OutputBackgroundMaterialReference.EndsWith(
                    "/Materials/M_" + request.AssetId + "_OverviewBackground.mat",
                    StringComparison.Ordinal) ||
                !request.OutputBackgroundTextureReference.EndsWith(
                    "/Textures/T_" + request.AssetId + "_OverviewBackground.png",
                    StringComparison.Ordinal) ||
                request.PreviewControllerScriptReference.IndexOf(
                    "/Scripts/",
                    StringComparison.Ordinal) < 0 ||
                !request.PreviewControllerScriptReference.EndsWith(
                    "PackageBuilderPreviewController.cs",
                    StringComparison.Ordinal) ||
                AssetDatabase.LoadMainAssetAtPath(request.OutputSceneReference) != null ||
                AssetDatabase.LoadMainAssetAtPath(request.OutputBackgroundMaterialReference) != null ||
                AssetDatabase.LoadMainAssetAtPath(request.OutputBackgroundTextureReference) != null)
            {
                return false;
            }

            return AssetDatabase.IsValidFolder(
                UnityOverviewSceneTemplateBuilder.FolderOf(request.OutputSceneReference));
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
