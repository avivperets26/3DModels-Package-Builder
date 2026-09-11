using System;
using System.IO;
using System.Linq;
using PackageBuilder.Preview;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Real GPU integration in a disposable clone; retains only a contact sheet and receipts outside it.</summary>
    public static class UnityStillImageCaptureIntegration
    {
        private static double started;
        public static void Run()
        {
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Execute;
        }

        private static void Execute()
        {
            if (EditorApplication.timeSinceStartup - started < 5 || ShaderUtil.anythingCompiling) return;
            EditorApplication.update -= Execute;
            try
            {
                if (!UnityOverviewSceneTemplateBuilder.TryCreate(new UnityOverviewSceneTemplateRequest
                    { OutputSceneReference = "Assets/CaptureTests/Overview.unity" }, out _, out string code))
                    throw new InvalidOperationException(code);
                PackageBuilderPreviewController preview = UnityEngine.Object.FindFirstObjectByType<PackageBuilderPreviewController>();
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CaptureTests/StoneArch.fbx");
                if (model == null) throw new InvalidOperationException("Real FBX fixture missing.");
                GameObject product = (GameObject)PrefabUtility.InstantiatePrefab(model);
                product.transform.SetParent(preview.PreviewTarget, false);
                foreach (Camera embedded in product.GetComponentsInChildren<Camera>()) UnityEngine.Object.DestroyImmediate(embedded);
                foreach (Light embedded in product.GetComponentsInChildren<Light>()) UnityEngine.Object.DestroyImmediate(embedded);
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(.18f, .58f, .32f));
                material.SetFloat("_Smoothness", .25f);
                AssetDatabase.CreateAsset(material, "Assets/CaptureTests/Final.mat");
                foreach (Renderer renderer in product.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                preview.AutoFrame();
                // A very large magenta helper must not appear or affect product framing.
                GameObject helper = GameObject.CreatePrimitive(PrimitiveType.Cube);
                helper.transform.localScale = Vector3.one * 100;
                var helperMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                helperMaterial.SetColor("_BaseColor", Color.magenta);
                helper.GetComponent<Renderer>().sharedMaterial = helperMaterial;
                Vector3 position = product.transform.position;
                Quaternion rotation = product.transform.rotation;
                Vector3 cameraPosition = preview.PreviewCamera.transform.position;
                Vector3 backdropPosition = preview.StudioBackground.localPosition;
                var first = Capture(preview, "first");
                var second = Capture(preview, "second");
                Require(first.Length == 5 && first.Select(x => x.sha256).SequenceEqual(second.Select(x => x.sha256)), "Repeat hashes differ.");
                Require(first.Select(x => x.coverageSha256).SequenceEqual(second.Select(x => x.coverageSha256)), "Repeat coverage hashes differ.");
                Require(first.Select(x => x.sha256).Distinct().Count() >= 3, "View angles did not change.");
                Require(position == product.transform.position && rotation == product.transform.rotation &&
                    cameraPosition == preview.PreviewCamera.transform.position && backdropPosition == preview.StudioBackground.localPosition &&
                    preview.ControlsVisible && helper.GetComponent<Renderer>().enabled, "Capture changed scene state.");
                Require(!UnityStillImageCapture.TryCapture(preview, "first", out _, out _), "Output collision accepted.");
                Require(!UnityStillImageCapture.TryCapture(preview, "../outside", out _, out _), "Traversal accepted.");
                Require(!UnityStillImageCapture.TryCapture(null, "null", out _, out _), "Null scene accepted.");
                // Force a failure after resource allocation and prove full restoration and partial-output cleanup.
                preview.StudioBackground.parent.localScale = Vector3.one * 2;
                Require(!UnityStillImageCapture.TryCapture(preview, "failed", out _, out _), "Scaled backdrop accepted.");
                preview.StudioBackground.parent.localScale = Vector3.one;
                Require(preview.ControlsVisible && helper.GetComponent<Renderer>().enabled &&
                    !Directory.Exists("PackageBuilderCaptures/capture-failed"), "Failure cleanup incomplete.");
                Renderer firstRenderer = product.GetComponentInChildren<Renderer>();
                Material[] originals = firstRenderer.sharedMaterials;
                firstRenderer.sharedMaterials = new Material[originals.Length];
                Require(!UnityStillImageCapture.TryCapture(preview, "missing", out _, out _), "Missing material accepted.");
                firstRenderer.sharedMaterials = originals;
                CreateEvidence(first);
                UnityEngine.Object.DestroyImmediate(helper);
                UnityEngine.Object.DestroyImmediate(helperMaterial);
                Debug.Log("PB-0907: five 1920x1080 views, repeat hashes, final material pixels, hidden helper, state restoration, path/collision and failure cleanup passed.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static UnityStillImageReceipt[] Capture(PackageBuilderPreviewController preview, string name)
        {
            if (!UnityStillImageCapture.TryCapture(preview, name, out UnityStillImageReceipt[] receipts, out string code))
                throw new InvalidOperationException(code);
            return receipts;
        }

        private static void CreateEvidence(UnityStillImageReceipt[] receipts)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string run = Path.GetDirectoryName(project);
            var sheet = new Texture2D(480 * 5, 270, TextureFormat.RGB24, false);
            try
            {
                for (int index = 0; index < receipts.Length; index++)
                {
                    var texture = new Texture2D(2, 2);
                    try
                    {
                        Require(texture.LoadImage(File.ReadAllBytes(Path.Combine(project, receipts[index].file))), "PNG decode failed.");
                        Require(texture.width == 1920 && texture.height == 1080, "Wrong resolution.");
                        Color32[] pixels = texture.GetPixels32();
                        int green = pixels.Count(c => c.g > c.r * 1.2f && c.g > c.b * 1.1f && c.g > 35);
                        int pink = pixels.Count(c => c.r > 220 && c.b > 220 && c.g < 50);
                        Require(green > 5000 && pink == 0, "Final material missing or helper visible.");
                        for (int y = 0; y < 270; y++)
                            for (int x = 0; x < 480; x++) sheet.SetPixel(index * 480 + x, y, texture.GetPixel(x * 4, y * 4));
                        Require(texture.LoadImage(File.ReadAllBytes(Path.Combine(project, receipts[index].coverageFile))), "Coverage decode failed.");
                        Require(texture.width == 1920 && texture.height == 1080, "Coverage dimensions differ.");
                        Color32[] coverage = texture.GetPixels32();
                        Require(coverage.Count(c => c.a >= 16) > 5000 && coverage.Count(c => c.a == 0) > 5000, "Coverage is empty or includes the studio.");
                        Require(receipts[index].left > 0 && receipts[index].right < 1 && receipts[index].bottom > 0 && receipts[index].top < 1 &&
                            !receipts[index].depthClipped && receipts[index].visibleHelpers == 0 && receipts[index].missingMaterials == 0, "Render facts differ from scene.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(texture); }
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(run, "capture-contact-sheet.png"), sheet.EncodeToPNG());
                File.WriteAllText(Path.Combine(run, "capture-receipts.json"), JsonUtility.ToJson(new ReceiptEnvelope
                    { images = receipts, checksPassed = true, unityVersion = Application.unityVersion }, true));
            }
            finally { UnityEngine.Object.DestroyImmediate(sheet); }
        }

        [Serializable]
        private sealed class ReceiptEnvelope
        {
            public UnityStillImageReceipt[] images;
            public bool checksPassed;
            public string unityVersion;
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
