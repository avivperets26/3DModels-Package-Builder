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
        private const string ModelTestRoot = "Assets/PBModelTests";
        private const string OverviewTemplateRoot = "Assets/PBOverviewTemplate";
        private const string ModelSourceReference = "Assets/PBModelTests/Source/StoneArch.fbx";

        public static void Run()
        {
            try
            {
                TestFolderPlansAndCreation();
                TestTextureImporterPolicies();
                TestMetallicSmoothnessPacking();
                TestUrpLitMaterialCompilation();
                TestStaticModelImportMeshExtractionAndPrefab();
                TestOverviewTemplateControllerAndComposition();
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
                    AssetDatabase.DeleteAsset(ModelTestRoot);
                    AssetDatabase.DeleteAsset(OverviewTemplateRoot);
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

        private static void TestStaticModelImportMeshExtractionAndPrefab()
        {
            var originalModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelSourceReference);
            Require(originalModel != null, "The real static FBX fixture was not imported.");
            string[] sourceMaterialNames = AssetDatabase.LoadAllAssetsAtPath(ModelSourceReference)
                .OfType<Material>()
                .Select(material => material.name)
                .Concat(originalModel.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .Select(material => material.name))
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(name => name, StringComparer.Ordinal)
                .ToArray();
            Require(sourceMaterialNames.Length > 0, "The static FBX fixture has no source materials.");

            string compiledMaterialReference = ModelTestRoot + "/Materials/M_StoneArch_URP.mat";
            Require(AssetDatabase.CopyAsset(
                MaterialTestRoot + "/Opaque.mat",
                compiledMaterialReference), "The compiled fixture material could not be copied.");
            AssetDatabase.ImportAsset(compiledMaterialReference, ImportAssetOptions.ForceSynchronousImport);

            UnityMaterialRemap[] remaps = sourceMaterialNames
                .Select(name => new UnityMaterialRemap(name, compiledMaterialReference))
                .ToArray();
            string requiredMaterialName = originalModel.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Select(material => material.name)
                .First();
            string diagnostic;
            Require(!UnityStaticModelImporterPolicy.TryApply(
                ModelSourceReference, 1f, true,
                remaps.Where(remap => remap.SourceMaterialName != requiredMaterialName),
                out diagnostic) &&
                diagnostic == "UNITY_STATIC_MODEL_MATERIAL_PLAN_INCOMPLETE",
                "An incomplete static material remap plan must fail closed.");
            Require(UnityStaticModelImporterPolicy.TryApply(
                ModelSourceReference, 1f, true, remaps, out diagnostic), diagnostic);

            var importer = AssetImporter.GetAtPath(ModelSourceReference) as ModelImporter;
            Require(importer != null, "The static ModelImporter disappeared after reimport.");
            Require(importer.animationType == ModelImporterAnimationType.None && !importer.importAnimation,
                "Static rig and animation import must be disabled.");
            Require(!importer.importCameras && !importer.importLights && !importer.importVisibility &&
                !importer.importBlendShapes,
                "Camera, light, visibility, and blend-shape import must be disabled.");
            Require(importer.importNormals == ModelImporterNormals.Import &&
                importer.importTangents == ModelImporterTangents.CalculateMikk,
                "Static normal and tangent policy is incorrect.");
            Require(Approximately(importer.globalScale, 1f) && importer.preserveHierarchy,
                "Static scale or hierarchy policy is incorrect.");
            Require(importer.materialLocation == ModelImporterMaterialLocation.InPrefab &&
                importer.materialImportMode == ModelImporterMaterialImportMode.ImportViaMaterialDescription,
                "Static material import policy is incorrect.");

            var materialMap = importer.GetExternalObjectMap()
                .Where(pair => pair.Key.type == typeof(Material))
                .ToDictionary(pair => pair.Key.name, pair => AssetDatabase.GetAssetPath(pair.Value),
                    StringComparer.Ordinal);
            Require(materialMap.Count == sourceMaterialNames.Length &&
                sourceMaterialNames.All(name => materialMap[name] == compiledMaterialReference),
                "Material remapping is incomplete or nondeterministic.");
            string firstRemapSignature = string.Join("|", materialMap.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value));
            Require(UnityStaticModelImporterPolicy.TryApply(
                ModelSourceReference, 1f, true, remaps.Reverse(), out diagnostic), diagnostic);
            importer = AssetImporter.GetAtPath(ModelSourceReference) as ModelImporter;
            string secondRemapSignature = string.Join("|", importer.GetExternalObjectMap()
                .Where(pair => pair.Key.type == typeof(Material))
                .OrderBy(pair => pair.Key.name, StringComparer.Ordinal)
                .Select(pair => pair.Key.name + "=" + AssetDatabase.GetAssetPath(pair.Value)));
            Require(firstRemapSignature == secondRemapSignature,
                "Material remapping changed when request order changed.");

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelSourceReference);
            Require(modelAsset != null && modelAsset.GetComponentsInChildren<MeshFilter>(true).Length > 0,
                "The static FBX contains no imported MeshFilter.");
            Require(modelAsset.GetComponentsInChildren<Camera>(true).Length == 0 &&
                modelAsset.GetComponentsInChildren<Light>(true).Length == 0 &&
                modelAsset.GetComponentsInChildren<Animator>(true).Length == 0 &&
                modelAsset.GetComponentsInChildren<Animation>(true).Length == 0,
                "Static import retained a camera, light, or animation component.");
            Require(modelAsset.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .All(material => material != null &&
                    AssetDatabase.GetAssetPath(material) == compiledMaterialReference),
                "The imported static renderers do not use the deterministic compiled material.");

            Mesh[] referencedMeshes = modelAsset.GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.sharedMesh)
                .Concat(modelAsset.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Select(renderer => renderer.sharedMesh))
                .Where(mesh => mesh != null)
                .ToArray();
            int uniqueReferencedMeshCount = referencedMeshes.Distinct().Count();
            Require(uniqueReferencedMeshCount > 0,
                "The real fixture contains no referenced meshes to extract.");

            UnityExtractedMeshSet extractedMeshes;
            Require(UnityMeshAssetExtractor.TryExtract(
                ModelSourceReference,
                ModelTestRoot + "/Meshes",
                "StoneArch",
                out extractedMeshes,
                out diagnostic), diagnostic);
            Require(extractedMeshes.Bindings.Count == uniqueReferencedMeshCount,
                "Standalone mesh extraction did not preserve the unique referenced-mesh set.");
            string[] extractedReferences = extractedMeshes.Bindings
                .Select(binding => binding.OutputAssetReference)
                .ToArray();
            string[] expectedExtractedReferences = uniqueReferencedMeshCount == 1
                ? new[] { ModelTestRoot + "/Meshes/MS_StoneArch.asset" }
                : Enumerable.Range(1, uniqueReferencedMeshCount)
                    .Select(index => ModelTestRoot + "/Meshes/MS_StoneArch_" +
                        index.ToString("D2") + ".asset")
                    .ToArray();
            Require(extractedReferences.SequenceEqual(expectedExtractedReferences),
                "Standalone mesh names are not stable.");
            Require(extractedMeshes.Bindings.All(binding => binding.SourceMesh != binding.ExtractedMesh &&
                binding.SourceMesh.vertexCount == binding.ExtractedMesh.vertexCount &&
                AssetDatabase.GetAssetPath(binding.ExtractedMesh).StartsWith(
                    ModelTestRoot + "/Meshes/MS_",
                    StringComparison.Ordinal)), "A standalone mesh asset is missing or invalid.");
            Require(Directory.GetFiles(ToPhysicalPath(ModelTestRoot + "/Source"), "*.asset").Length == 0,
                "Standalone mesh assets must never be duplicated in Source.");
            UnityExtractedMeshSet rejectedSet;
            Require(!UnityMeshAssetExtractor.TryExtract(
                ModelSourceReference,
                ModelTestRoot + "/Meshes",
                "StoneArch",
                out rejectedSet,
                out diagnostic) && diagnostic == "UNITY_MESH_EXTRACTION_OUTPUT_COLLISION",
                "Existing standalone mesh outputs must fail closed.");

            string prefabReference = ModelTestRoot + "/Prefabs/P_StoneArch.prefab";
            var prefabRequest = new UnityPrefabRequest
            {
                AssetId = "StoneArch",
                SourceModelReference = ModelSourceReference,
                OutputAssetReference = prefabReference,
                ExtractedMeshes = extractedMeshes,
                ExpectedMaterialReferences = new[] { compiledMaterialReference },
            };
            GameObject prefab;
            Require(UnityPrefabGenerator.TryCreate(prefabRequest, out prefab, out diagnostic), diagnostic);
            Require(prefab != null && prefab.name == "P_StoneArch" && prefab.transform.childCount == 1,
                "The saved product prefab hierarchy is incorrect.");
            Transform modelChild = prefab.transform.GetChild(0);
            Require(modelChild.name == "P_Model" && IsReset(prefab.transform) && IsReset(modelChild),
                "The product root or P_Model transform is not reset.");
            Require(modelChild.GetComponentsInChildren<MeshFilter>(true)
                .All(filter => filter.sharedMesh != null && AssetDatabase.GetAssetPath(filter.sharedMesh)
                    .StartsWith(ModelTestRoot + "/Meshes/MS_", StringComparison.Ordinal)),
                "The prefab does not reference only standalone mesh assets.");
            Require(modelChild.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .All(material => material != null &&
                    AssetDatabase.GetAssetPath(material) == compiledMaterialReference),
                "The prefab material references are incorrect.");
            GameObject rejectedPrefab;
            Require(!UnityPrefabGenerator.TryCreate(
                prefabRequest, out rejectedPrefab, out diagnostic) && diagnostic == "UNITY_PREFAB_INVALID",
                "Existing prefab outputs must fail closed.");
        }

        private static void TestOverviewTemplateControllerAndComposition()
        {
            AssetDatabase.DeleteAsset(OverviewTemplateRoot);
            string templateFolderGuid = AssetDatabase.CreateFolder("Assets", "PBOverviewTemplate");
            Require(!string.IsNullOrEmpty(templateFolderGuid),
                "Overview template test folder could not be created.");

            string templateSceneReference = OverviewTemplateRoot + "/OverviewTemplate.unity";
            var templateRequest = new UnityOverviewSceneTemplateRequest
            {
                OutputSceneReference = templateSceneReference,
            };
            UnityEngine.SceneManagement.Scene templateScene;
            string diagnostic;
            Require(UnityOverviewSceneTemplateBuilder.TryCreate(
                templateRequest,
                out templateScene,
                out diagnostic), diagnostic);
            Require(UnityOverviewSceneTemplateBuilder.VerifyTemplate(templateScene, out diagnostic), diagnostic);

            string outputSceneReference = ModelTestRoot + "/Scenes/S_StoneArch_Overview.unity";
            string controllerScriptReference =
                ModelTestRoot + "/Scripts/PackageBuilderPreviewController.cs";
            var compositionRequest = new UnityOverviewSceneCompositionRequest
            {
                AssetId = "StoneArch",
                TemplateSceneReference = templateSceneReference,
                ProductPrefabReference = ModelTestRoot + "/Prefabs/P_StoneArch.prefab",
                PreviewControllerScriptReference = controllerScriptReference,
                OutputSceneReference = outputSceneReference,
            };
            UnityEngine.SceneManagement.Scene composedScene;
            Require(UnityOverviewSceneComposer.TryCompose(
                compositionRequest,
                out composedScene,
                out diagnostic), diagnostic);
            Require(UnityOverviewSceneComposer.VerifyComposition(
                composedScene,
                compositionRequest,
                out diagnostic), diagnostic);

            GameObject root = UnityOverviewSceneTemplateBuilder.FindUniqueRoot(
                composedScene,
                UnityOverviewSceneTemplateBuilder.OverviewRootName);
            var controller = root.GetComponent<PackageBuilder.Preview.PackageBuilderPreviewController>();
            Transform product = controller.PreviewTarget.GetChild(0);
            Transform[] productTransforms = product.GetComponentsInChildren<Transform>(true);
            Vector3[] positions = productTransforms.Select(value => value.localPosition).ToArray();
            Quaternion[] rotations = productTransforms.Select(value => value.localRotation).ToArray();
            Vector3[] scales = productTransforms.Select(value => value.localScale).ToArray();
            Vector3 cameraBefore = controller.PreviewCamera.transform.position;
            Require(controller.AutoFrame(), "Overview auto-frame failed.");
            Require(controller.Orbit(22f, 7f), "Overview orbit failed.");
            Vector3 cameraAfterOrbit = controller.PreviewCamera.transform.position;
            Require(cameraAfterOrbit != cameraBefore, "Overview orbit did not move the camera.");
            Require(controller.Zoom(0.2f), "Overview zoom failed.");
            Require(controller.PreviewCamera.transform.position != cameraAfterOrbit,
                "Overview zoom did not move the camera.");
            for (int index = 0; index < productTransforms.Length; index++)
            {
                Require(productTransforms[index].localPosition == positions[index] &&
                    productTransforms[index].localRotation == rotations[index] &&
                    productTransforms[index].localScale == scales[index],
                    "Overview camera navigation changed a product transform.");
            }

            Require(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(controller)) ==
                controllerScriptReference, "Overview scene references a non-product controller script.");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(outputSceneReference) != null,
                "The product overview scene was not saved beneath the product root.");

            UnityEngine.SceneManagement.Scene reopenedTemplate =
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(templateSceneReference);
            Require(UnityOverviewSceneTemplateBuilder.VerifyTemplate(reopenedTemplate, out diagnostic),
                "The reusable overview template retained a previous product: " + diagnostic);
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

        private static bool IsReset(Transform value)
        {
            return value.localPosition == Vector3.zero && value.localRotation == Quaternion.identity &&
                value.localScale == Vector3.one;
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
