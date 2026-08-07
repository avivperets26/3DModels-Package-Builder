using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Runs dependency-free Editor integration tests against real AssetDatabase import behavior.</summary>
    public static class UnityProductEditorIntegrationTests
    {
        private const string FolderTestRoot = "Assets/PBFolderTests";
        private const string TextureTestRoot = "Assets/PBTextureTests";
        private const string MaterialTestRoot = "Assets/PBMaterialTests";

        public static void Run()
        {
            try
            {
                TestFolderPlansAndCreation();
                TestTextureImporterPolicies();
                TestMetallicSmoothnessPacking();
                TestUrpLitMaterialCompilation();
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
                    AssetDatabase.DeleteAsset(MaterialTestRoot);
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

        private static void TestMetallicSmoothnessPacking()
        {
            string metallicPath = TextureTestRoot + "/metallic-source.png";
            string roughnessPath = TextureTestRoot + "/roughness-source.png";
            string packedPath = TextureTestRoot + "/metallic-smoothness.png";
            WriteDataTexture(metallicPath, 4, 4, index => new Color32((byte)(index * 11), 23, 47, 255));
            WriteDataTexture(roughnessPath, 4, 4, index => new Color32((byte)(index * 7), 61, 89, 255));
            AssetDatabase.ImportAsset(metallicPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(roughnessPath, ImportAssetOptions.ForceSynchronousImport);

            string diagnostic;
            Require(UnityTextureImporterPolicy.TryApply(metallicPath, "metallic", out diagnostic), diagnostic);
            Require(UnityTextureImporterPolicy.TryApply(roughnessPath, "roughness", out diagnostic), diagnostic);
            Require(UnityMetallicSmoothnessPacker.TryPack(
                metallicPath, roughnessPath, packedPath, out diagnostic), diagnostic);

            Color32[] packedPixels = ReadPngPixels(packedPath, out int width, out int height);
            Require(width == 4 && height == 4, "Packed texture dimensions changed.");
            for (int index = 0; index < packedPixels.Length; index++)
            {
                Require(packedPixels[index].r == (byte)(index * 11), "Metallic red channel is incorrect.");
                Require(packedPixels[index].g == 0 && packedPixels[index].b == 0,
                    "Unused packed channels must be deterministic zeroes.");
                Require(packedPixels[index].a == (byte)(byte.MaxValue - (index * 7)),
                    "Smoothness alpha must equal one minus roughness.");
            }

            var outputImporter = AssetImporter.GetAtPath(packedPath) as TextureImporter;
            Require(outputImporter != null && !outputImporter.sRGBTexture,
                "Packed texture must import as linear data.");
            Require(outputImporter.alphaSource == TextureImporterAlphaSource.FromInput &&
                !outputImporter.alphaIsTransparency, "Packed smoothness alpha policy is incorrect.");
            Require(!outputImporter.isReadable, "Packed customer texture must not remain CPU-readable.");

            var metallicImporter = AssetImporter.GetAtPath(metallicPath) as TextureImporter;
            var roughnessImporter = AssetImporter.GetAtPath(roughnessPath) as TextureImporter;
            Require(metallicImporter != null && roughnessImporter != null &&
                !metallicImporter.isReadable && !roughnessImporter.isReadable,
                "Source readability must be restored after packing.");

            Require(!UnityMetallicSmoothnessPacker.TryPack(
                metallicPath, roughnessPath, packedPath, out diagnostic) &&
                diagnostic == "UNITY_METALLIC_SMOOTHNESS_OUTPUT_EXISTS",
                "Existing packed outputs must fail closed.");

            string mismatchPath = TextureTestRoot + "/roughness-mismatch.png";
            WriteDataTexture(mismatchPath, 8, 4, index => new Color32((byte)index, 0, 0, 255));
            AssetDatabase.ImportAsset(mismatchPath, ImportAssetOptions.ForceSynchronousImport);
            Require(UnityTextureImporterPolicy.TryApply(mismatchPath, "roughness", out diagnostic), diagnostic);
            Require(!UnityMetallicSmoothnessPacker.TryPack(
                metallicPath,
                mismatchPath,
                TextureTestRoot + "/dimension-mismatch-output.png",
                out diagnostic) && diagnostic == "UNITY_METALLIC_SMOOTHNESS_DIMENSION_MISMATCH",
                "Mismatched source dimensions must be rejected.");
        }

        private static void TestUrpLitMaterialCompilation()
        {
            string guid = AssetDatabase.CreateFolder("Assets", "PBMaterialTests");
            Require(!string.IsNullOrEmpty(guid), "Material test folder could not be created.");

            string packedPath = TextureTestRoot + "/metallic-smoothness.png";
            var opaqueRequest = MaterialRequest("opaque", "Opaque.mat", 1f, null, false);
            Material opaque;
            string diagnostic;
            Require(UnityUrpLitMaterialCompiler.TryCompile(
                opaqueRequest, out opaque, out diagnostic), diagnostic);
            VerifyCommonMaterial(opaque, packedPath);
            Require(opaque.renderQueue == (int)RenderQueue.Geometry &&
                opaque.GetFloat("_Surface") == 0f && opaque.GetFloat("_AlphaClip") == 0f &&
                opaque.GetFloat("_ZWrite") == 1f && opaque.GetFloat("_Cull") == 2f,
                "Opaque URP state is incorrect.");
            Require(opaque.GetTag("RenderType", false) == "Opaque", "Opaque RenderType is incorrect.");
            VerifyUrpStateIsStable(opaque);

            var cutoutRequest = MaterialRequest("cutout", "Cutout.mat", 0.75f, 0.42f, true);
            Material cutout;
            Require(UnityUrpLitMaterialCompiler.TryCompile(
                cutoutRequest, out cutout, out diagnostic), diagnostic);
            VerifyCommonMaterial(cutout, packedPath);
            Require(cutout.renderQueue == (int)RenderQueue.AlphaTest &&
                cutout.GetFloat("_AlphaClip") == 1f &&
                Approximately(cutout.GetFloat("_Cutoff"), 0.42f) && cutout.GetFloat("_Cull") == 0f,
                "Cutout or double-sided URP state is incorrect.");
            Require(cutout.IsKeywordEnabled("_ALPHATEST_ON") &&
                cutout.GetTag("RenderType", false) == "TransparentCutout",
                "Cutout keyword or RenderType is incorrect.");
            VerifyUrpStateIsStable(cutout);

            var transparentRequest = MaterialRequest("transparent", "Transparent.mat", 0.4f, null, false);
            Material transparent;
            Require(UnityUrpLitMaterialCompiler.TryCompile(
                transparentRequest, out transparent, out diagnostic), diagnostic);
            VerifyCommonMaterial(transparent, packedPath);
            Require(transparent.renderQueue == (int)RenderQueue.Transparent &&
                transparent.GetFloat("_Surface") == 1f && transparent.GetFloat("_ZWrite") == 0f &&
                transparent.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),
                "Transparent URP surface state is incorrect.");
            Require(transparent.GetTag("RenderType", false) == "Transparent",
                "Transparent RenderType is incorrect.");
            VerifyUrpStateIsStable(transparent);

            Material rejected;
            Require(!UnityUrpLitMaterialCompiler.TryCompile(
                opaqueRequest, out rejected, out diagnostic) && diagnostic == "UNITY_URP_MATERIAL_INVALID",
                "Existing material outputs must fail closed.");
        }

        private static UnityUrpLitMaterialRequest MaterialRequest(
            string surfaceMode,
            string fileName,
            float opacity,
            float? alphaCutoff,
            bool doubleSided)
        {
            return new UnityUrpLitMaterialRequest
            {
                OutputAssetReference = MaterialTestRoot + "/" + fileName,
                BaseMapAssetReference = TextureTestRoot + "/albedo.png",
                NormalMapAssetReference = TextureTestRoot + "/normal.png",
                MetallicSmoothnessAssetReference = TextureTestRoot + "/metallic-smoothness.png",
                EmissionMapAssetReference = TextureTestRoot + "/emission.png",
                AmbientOcclusionMapAssetReference = TextureTestRoot + "/ambient-occlusion.png",
                BaseColour = new Color(0.8f, 0.7f, 0.6f, 1f),
                EmissionColour = new Color(0.2f, 0.1f, 0.05f, 1f),
                MetallicFactor = 0.65f,
                RoughnessFactor = 0.25f,
                NormalScale = 0.75f,
                AmbientOcclusionStrength = 0.8f,
                Opacity = opacity,
                SurfaceMode = surfaceMode,
                AlphaCutoff = alphaCutoff,
                DoubleSided = doubleSided,
            };
        }

        private static void VerifyCommonMaterial(Material material, string packedPath)
        {
            Require(material != null && material.shader.name == "Universal Render Pipeline/Lit",
                "URP/Lit shader assignment is incorrect.");
            Require(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")) ==
                TextureTestRoot + "/albedo.png", "Base map assignment is incorrect.");
            Require(AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")) ==
                TextureTestRoot + "/normal.png", "Normal map assignment is incorrect.");
            Require(AssetDatabase.GetAssetPath(material.GetTexture("_MetallicGlossMap")) == packedPath,
                "Metallic-smoothness assignment is incorrect.");
            Require(AssetDatabase.GetAssetPath(material.GetTexture("_EmissionMap")) ==
                TextureTestRoot + "/emission.png", "Emission map assignment is incorrect.");
            Require(AssetDatabase.GetAssetPath(material.GetTexture("_OcclusionMap")) ==
                TextureTestRoot + "/ambient-occlusion.png", "AO map assignment is incorrect.");
            Require(Approximately(material.GetFloat("_Metallic"), 0.65f) &&
                Approximately(material.GetFloat("_Smoothness"), 0.75f) &&
                Approximately(material.GetFloat("_BumpScale"), 0.75f) &&
                Approximately(material.GetFloat("_OcclusionStrength"), 0.8f),
                "URP material factors are incorrect.");
            foreach (string keyword in new[]
            {
                "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_EMISSION", "_OCCLUSIONMAP",
            })
            {
                Require(material.IsKeywordEnabled(keyword), "Missing URP keyword: " + keyword);
            }
        }

        private static void VerifyUrpStateIsStable(Material material)
        {
            string before = MaterialState(material);
            UnityUrpLitMaterialCompiler.SynchronizeUrpState(material);
            string after = MaterialState(material);
            Require(before == after, "URP canonicalization changed the compiled material state.");
        }

        private static string MaterialState(Material material)
        {
            return string.Join("|", new[]
            {
                material.renderQueue.ToString(),
                material.GetTag("RenderType", false),
                material.GetFloat("_Surface").ToString("R"),
                material.GetFloat("_AlphaClip").ToString("R"),
                material.GetFloat("_SrcBlend").ToString("R"),
                material.GetFloat("_DstBlend").ToString("R"),
                material.GetFloat("_ZWrite").ToString("R"),
                ((int)material.globalIlluminationFlags).ToString(),
                string.Join(",", material.shaderKeywords.OrderBy(value => value, StringComparer.Ordinal)),
            });
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

        private static void WriteDataTexture(
            string assetPath,
            int width,
            int height,
            Func<int, Color32> pixelFactory)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                var pixels = new Color32[width * height];
                for (int index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = pixelFactory(index);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(ToPhysicalPath(assetPath), texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Color32[] ReadPngPixels(string assetPath, out int width, out int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Require(ImageConversion.LoadImage(texture, File.ReadAllBytes(ToPhysicalPath(assetPath)), false),
                    "Packed PNG could not be decoded.");
                width = texture.width;
                height = texture.height;
                return texture.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string ToPhysicalPath(string assetPath)
        {
            return Path.Combine(
                Application.dataPath,
                assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
        }

        private static bool Approximately(float first, float second)
        {
            return Mathf.Abs(first - second) <= 0.0001f;
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
