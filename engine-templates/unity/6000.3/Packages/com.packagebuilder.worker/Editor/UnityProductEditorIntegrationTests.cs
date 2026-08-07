using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Runs dependency-free Editor integration tests against real AssetDatabase import behavior.</summary>
    public static class UnityProductEditorIntegrationTests
    {
        private const string FolderTestRoot = "Assets/PBFolderTests";
        private const string TextureTestRoot = "Assets/PBTextureTests";

        public static void Run()
        {
            try
            {
                TestFolderPlansAndCreation();
                TestTextureImporterPolicies();
                Debug.Log("PACKAGEBUILDER_UNITY_PRODUCT_TESTS_PASS");
                EditorApplication.Exit(0);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError("PACKAGEBUILDER_UNITY_PRODUCT_TESTS_FAIL:" + exception.Message);
                EditorApplication.Exit(1);
            }
            catch (Exception exception)
            {
                Debug.LogError("PACKAGEBUILDER_UNITY_PRODUCT_TESTS_FAIL:" + exception.GetType().Name);
                EditorApplication.Exit(1);
            }
            finally
            {
                // The contained integration harness may retain the generated assets for a manual
                // Project-window/Inspector review. Normal invocations always clean their test data.
                if (!string.Equals(
                    Environment.GetEnvironmentVariable("PACKAGEBUILDER_RETAIN_UNITY_TEST_ASSETS"),
                    "1",
                    StringComparison.Ordinal))
                {
                    AssetDatabase.DeleteAsset(FolderTestRoot);
                    AssetDatabase.DeleteAsset(TextureTestRoot);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
            }
        }

        private static void TestFolderPlansAndCreation()
        {
            AssetDatabase.DeleteAsset(FolderTestRoot);
            string[] cases = { "static", "rigged", "rigged-animated", "item-set", "item-collection" };
            foreach (string productCase in cases)
            {
                string folderName = "Product_" + productCase.Replace("-", string.Empty);
                string manifest = Manifest("PBFolderTests", folderName, productCase);
                UnityProductFolderPlan plan;
                string diagnostic;
                Require(UnityProductFolderGenerator.TryCreatePlan(manifest, out plan, out diagnostic), diagnostic);
                int expectedCount = productCase == "rigged-animated" ? 10 : 8;
                Require(plan.AssetFolders.Length == expectedCount, "Unexpected case-specific folder count.");
                Require(!plan.AssetFolders.Any(path => path.IndexOf("_Template", StringComparison.Ordinal) >= 0),
                    "Template output is prohibited.");
                Require(UnityProductFolderGenerator.TryCreateFolders(plan, out diagnostic), diagnostic);
                foreach (string folder in plan.AssetFolders)
                {
                    Require(AssetDatabase.IsValidFolder(folder), "Generated folder is missing.");
                }

                Require(!UnityProductFolderGenerator.TryCreateFolders(plan, out diagnostic) &&
                    diagnostic == "UNITY_PRODUCT_FOLDER_COLLISION", "Existing product roots must fail closed.");
            }

            UnityProductFolderPlan rejected;
            string rejectedCode;
            Require(!UnityProductFolderGenerator.TryCreatePlan(
                Manifest("../Publisher", "Unsafe", "static"), out rejected, out rejectedCode),
                "Unsafe publisher roots must be rejected.");
        }

        private static void TestTextureImporterPolicies()
        {
            AssetDatabase.DeleteAsset(TextureTestRoot);
            string guid = AssetDatabase.CreateFolder("Assets", "PBTextureTests");
            Require(!string.IsNullOrEmpty(guid), "Texture test folder could not be created.");

            string[] roles =
            {
                "albedo",
                "emission",
                "normal",
                "metallic",
                "roughness",
                "ambient-occlusion",
                "opacity",
                "height",
            };

            foreach (string role in roles)
            {
                string assetPath = TextureTestRoot + "/" + role + ".png";
                WriteTexture(assetPath);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                string diagnostic;
                Require(UnityTextureImporterPolicy.TryApply(assetPath, role, out diagnostic), diagnostic);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                Require(importer != null, "TextureImporter is missing after reimport.");

                bool colourTexture = role == "albedo" || role == "emission";
                Require(importer.sRGBTexture == colourTexture, "Texture colour-space policy is incorrect.");
                Require((importer.textureType == TextureImporterType.NormalMap) == (role == "normal"),
                    "Normal-map typing policy is incorrect.");
                TextureImporterAlphaSource expectedAlpha = role == "albedo" || role == "opacity"
                    ? TextureImporterAlphaSource.FromInput
                    : TextureImporterAlphaSource.None;
                Require(importer.alphaSource == expectedAlpha, "Texture alpha-source policy is incorrect.");
                Require(importer.alphaIsTransparency == (role == "albedo"),
                    "Texture alpha-transparency policy is incorrect.");
            }

            string ignored;
            Require(!UnityTextureImporterPolicy.TryApply(TextureTestRoot + "/albedo.png", "orm", out ignored),
                "Ambiguous packed texture roles must not be accepted.");
            Require(!UnityTextureImporterPolicy.TryApply("../outside.png", "albedo", out ignored),
                "Outside texture references must be rejected.");
        }

        private static string Manifest(string publisher, string folder, string productCase)
        {
            return "{\"schemaVersion\":1,\"publisherProfileReference\":\"" + publisher +
                "\",\"product\":{\"folderName\":\"" + folder + "\",\"case\":\"" + productCase + "\"}}";
        }

        private static void WriteTexture(string assetPath)
        {
            // Four-by-four is the smallest fixture accepted by Unity's block-compression preview
            // without producing a misleading Inspector warning during manual verification.
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color[16];
                for (int index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = (index % 4) switch
                    {
                        0 => Color.red,
                        1 => Color.green,
                        2 => Color.blue,
                        _ => Color.clear,
                    };
                }

                texture.SetPixels(pixels);
                texture.Apply();
                string physicalPath = Path.Combine(
                    Application.dataPath,
                    assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
                File.WriteAllBytes(physicalPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
