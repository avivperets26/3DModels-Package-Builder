using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PackageBuilder.Preview;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PackageBuilder.UnityWorker.Editor
{
    [Serializable]
    public sealed class UnityStillImageReceipt
    {
        public string role;
        public string file;
        public int width;
        public int height;
        public string sha256;
    }

    /// <summary>Captures the current, final-material preview pose on the Editor main thread.
    /// Call outside a render callback, in an isolated worker project with a settled imported scene.
    /// No desktop screenshots, scene saves, product transforms or material replacements are used.</summary>
    public static class UnityStillImageCapture
    {
        public const int Width = 1920;
        public const int Height = 1080;
        private const float Padding = 1.25f;
        private static readonly string[] Roles = { "hero", "front", "back", "left", "right" };
        private static readonly Vector3[] Directions = {
            new Vector3(1f, .65f, -1f), Vector3.back, Vector3.forward, Vector3.left, Vector3.right
        };

        /// <summary>Creates a new output directory below Project/PackageBuilderCaptures.
        /// A collision is rejected; an unsuccessful run removes only its own partial files.</summary>
        public static bool TryCapture(PackageBuilderPreviewController preview, string outputName,
            out UnityStillImageReceipt[] receipts, out string diagnosticCode)
        {
            receipts = Array.Empty<UnityStillImageReceipt>();
            diagnosticCode = "UNITY_CAPTURE_INPUT_INVALID";
            if (preview == null || preview.PreviewCamera == null || preview.StudioBackground == null ||
                !preview.TryGetProductBounds(out Bounds bounds) || !Finite(bounds.center) || !Finite(bounds.size) ||
                bounds.size.magnitude <= 0f || bounds.size.magnitude > 100000f ||
                string.IsNullOrEmpty(outputName) || outputName.Length > 64 ||
                outputName.Any(c => !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-'))
            {
                return false;
            }

            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string output = Path.Combine(project, "PackageBuilderCaptures", "capture-" + outputName);
            if (!SafeAncestors(output) || Directory.Exists(output) || File.Exists(output))
            {
                diagnosticCode = "UNITY_CAPTURE_OUTPUT_INVALID";
                return false;
            }
            Renderer[] products = preview.PreviewTarget.GetComponentsInChildren<Renderer>(false)
                .Where(renderer => renderer.enabled).ToArray();
            if (products.Length == 0 || products.Any(renderer => renderer.sharedMaterials.Length == 0 ||
                renderer.sharedMaterials.Any(material => material == null || material.shader == null ||
                    !material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader")))
            {
                diagnosticCode = "UNITY_CAPTURE_MATERIAL_INVALID";
                return false;
            }

            var hidden = new List<Renderer>();
            var canvases = new List<Canvas>();
            var createdFiles = new List<string>();
            var results = new List<UnityStillImageReceipt>();
            Transform background = preview.StudioBackground;
            Vector3 backgroundPosition = background.localPosition;
            Quaternion backgroundRotation = background.localRotation;
            Vector3 backgroundScale = background.localScale;
            bool controlsVisible = preview.ControlsVisible;
            RenderTexture previousActive = RenderTexture.active;
            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            bool directoryCreated = false;
            bool succeeded = false;
            try
            {
                Directory.CreateDirectory(output);
                directoryCreated = true;
                preview.SetControlsVisible(false);
                // Suppress other scenes, helper geometry and world-space UI for this render only.
                foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (renderer.enabled && !products.Contains(renderer) && renderer.transform != background &&
                        !renderer.transform.IsChildOf(background))
                    { hidden.Add(renderer); renderer.enabled = false; }
                }
                foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (canvas.enabled) { canvases.Add(canvas); canvas.enabled = false; }
                }
                cameraObject = new GameObject("PackageBuilderStillCamera") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraObject, preview.gameObject.scene);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.CopyFrom(preview.PreviewCamera);
                camera.enabled = false;
                camera.targetTexture = null;
                camera.aspect = (float)Width / Height;
                camera.fieldOfView = 35f;
                camera.usePhysicalProperties = false;
                camera.allowDynamicResolution = false;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.cullingMask = -1;
                camera.rect = new Rect(0, 0, 1, 1);
                UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                cameraData.renderPostProcessing = false;
                cameraData.antialiasing = AntialiasingMode.None;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                    { antiAliasing = 1, useMipMap = false, autoGenerateMips = false };
                target.Create();
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                { diagnosticCode = "UNITY_CAPTURE_RENDER_UNSUPPORTED"; return false; }
                for (int index = 0; index < Roles.Length; index++)
                {
                    Frame(camera, bounds, Directions[index], index != 0);
                    float distance = camera.farClipPlane * .7f;
                    float height = camera.orthographic ? 2f * camera.orthographicSize :
                        2f * distance * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
                    background.position = camera.transform.position + camera.transform.forward * distance;
                    background.rotation = camera.transform.rotation;
                    // The standard studio root is identity; reject a scaled root rather than distort the backdrop.
                    if (background.parent != null && background.parent.lossyScale != Vector3.one)
                    { diagnosticCode = "UNITY_CAPTURE_BACKGROUND_SCALE_INVALID"; return false; }
                    background.localScale = new Vector3(height * camera.aspect * 1.1f, height * 1.1f, 1f);
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    pixels.Apply(false, false);
                    byte[] png = pixels.EncodeToPNG();
                    string file = Roles[index] + ".png";
                    string path = Path.Combine(output, file);
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        createdFiles.Add(path);
                        stream.Write(png, 0, png.Length);
                    }
                    using (SHA256 sha = SHA256.Create())
                    {
                        results.Add(new UnityStillImageReceipt { role = Roles[index], file =
                            "PackageBuilderCaptures/capture-" + outputName + "/" + file,
                            width = Width, height = Height,
                            sha256 = BitConverter.ToString(sha.ComputeHash(png)).Replace("-", "").ToLowerInvariant() });
                    }
                }
                receipts = results.ToArray();
                diagnosticCode = string.Empty;
                succeeded = true;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is UnityException || exception is ArgumentException || exception is InvalidOperationException)
            {
                diagnosticCode = "UNITY_CAPTURE_FAILED";
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                background.localPosition = backgroundPosition;
                background.localRotation = backgroundRotation;
                background.localScale = backgroundScale;
                preview.SetControlsVisible(controlsVisible);
                foreach (Renderer renderer in hidden) if (renderer != null) renderer.enabled = true;
                foreach (Canvas canvas in canvases) if (canvas != null) canvas.enabled = true;
                if (!succeeded && directoryCreated)
                {
                    foreach (string file in createdFiles) File.Delete(file);
                    Directory.Delete(output, false);
                }
            }
        }

        private static void Frame(Camera camera, Bounds bounds, Vector3 direction, bool orthographic)
        {
            camera.orthographic = orthographic;
            camera.transform.rotation = Quaternion.LookRotation(-direction.normalized, Vector3.up);
            Quaternion inverse = Quaternion.Inverse(camera.transform.rotation);
            float distance = bounds.size.magnitude;
            float size = .005f;
            float tangent = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = inverse * Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        size = Mathf.Max(size, Mathf.Abs(corner.y), Mathf.Abs(corner.x) / camera.aspect);
                        distance = Mathf.Max(distance, Padding * Mathf.Abs(corner.y) / tangent - corner.z,
                            Padding * Mathf.Abs(corner.x) / (tangent * camera.aspect) - corner.z);
                    }
            camera.orthographicSize = size * Padding;
            camera.transform.position = bounds.center + direction.normalized * distance;
            camera.nearClipPlane = Mathf.Max(.0001f, distance * .001f);
            camera.farClipPlane = Mathf.Max(10f, distance + bounds.size.magnitude * 4f);
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool SafeAncestors(string path)
        {
            for (string current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((Directory.Exists(current) || File.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
            return true;
        }
    }
}
