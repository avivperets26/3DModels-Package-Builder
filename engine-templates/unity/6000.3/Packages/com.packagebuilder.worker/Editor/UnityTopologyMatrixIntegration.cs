using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>
    /// Builds the PB-0714 procedural Generic-rig matrix through maintained production adapters and
    /// exports one exact package for topology-neutral clean-reimport validation.
    /// </summary>
    public static class UnityTopologyMatrixIntegration
    {
        private const string ProductRoot = "Assets/PBTopologyMatrixTests";

        /// <summary>Runs the matrix build and exits batch-mode Unity with a stable process result.</summary>
        public static void Run()
        {
            try
            {
                BuildAndValidate();
                Debug.Log("PACKAGEBUILDER_UNITY_TOPOLOGY_MATRIX_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("PACKAGEBUILDER_UNITY_TOPOLOGY_MATRIX_FAIL:" + exception.Message);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildAndValidate()
        {
            Require(AssetDatabase.IsValidFolder(ProductRoot),
                "The topology-matrix product root is missing.");
            UnityGenericTopologyMatrixManifest manifest =
                UnityGenericTopologyMatrixValidator.LoadManifest(ProductRoot);
            Require(manifest != null && manifest.cases.Length == 5 &&
                manifest.invalidCases.Length == 1,
                "The topology-matrix manifest is missing or invalid.");

            foreach (UnityGenericTopologyCaseExpectation expectation in manifest.cases
                .OrderBy(value => value.id, StringComparer.Ordinal))
            {
                string sourceReference = ProductRoot + "/Source/" + expectation.modelFile;
                ApplyGeneric(sourceReference);

                UnityAnimationClipPlan[] discovered;
                string diagnostic;
                Require(UnityAnimationClipImporter.TryDiscoverSourceActions(
                    sourceReference, out discovered, out diagnostic), diagnostic);
                Require(discovered.Length == 1 &&
                    Approximately(discovered[0].FirstFrame, expectation.firstFrame) &&
                    Approximately(discovered[0].LastFrame, expectation.lastFrame) &&
                    Approximately(discovered[0].SampleRate, expectation.frameRate),
                    "The discovered topology clip metadata is incorrect: " + expectation.id);

                string animationFolder = ProductRoot + "/Animations";
                UnityAnimationClipImportResult clipResult;
                Require(UnityAnimationClipImporter.TryImportAndExtract(
                    new UnityAnimationClipImportRequest
                    {
                        AssetId = expectation.assetId,
                        SourceModelReference = sourceReference,
                        OutputAnimationFolderReference = animationFolder,
                        CompressionPolicy = UnityAnimationCompressionPolicy.Optimal,
                        Clips = new[]
                        {
                            new UnityAnimationClipPlan
                            {
                                ClipId = expectation.clipId,
                                SourceTakeName = discovered[0].SourceTakeName,
                                FirstFrame = expectation.firstFrame,
                                LastFrame = expectation.lastFrame,
                                SampleRate = expectation.frameRate,
                                LoopTime = false,
                                RootMotionPolicy = UnityRootMotionPolicy.BakeIntoPose,
                            },
                        },
                    },
                    out clipResult,
                    out diagnostic), diagnostic);
                string clipReference = animationFolder + "/A_" + expectation.assetId +
                    "_" + expectation.clipId + ".anim";
                Require(clipResult.OutputAssetReferences.SequenceEqual(
                        new[] { clipReference }, StringComparer.Ordinal),
                    "The topology clip output is incorrect: " + expectation.id);

                string controllerReference = ProductRoot + "/Controllers/AC_" +
                    expectation.assetId + ".controller";
                AnimatorController controller;
                Require(UnityAnimatorControllerGenerator.TryCreate(
                    new UnityAnimatorControllerRequest
                    {
                        AssetId = expectation.assetId,
                        OutputControllerReference = controllerReference,
                        DefaultClipReference = clipReference,
                        ClipReferences = new[] { clipReference },
                    },
                    out controller,
                    out diagnostic), diagnostic);

                GameObject prefab;
                UnitySkinSkeletonReport prefabSkin;
                Require(UnityAnimatedPrefabGenerator.TryCreate(
                    new UnityAnimatedPrefabRequest
                    {
                        AssetId = expectation.assetId,
                        SourceModelReference = sourceReference,
                        AnimatorControllerReference = controllerReference,
                        OutputPrefabReference = ProductRoot + "/Prefabs/P_" +
                            expectation.assetId + ".prefab",
                        AllowedMaximumInfluences = 4,
                        ApplyRootMotion = false,
                    },
                    out prefab,
                    out prefabSkin,
                    out diagnostic), diagnostic);
                Require(controller != null && prefab != null && prefabSkin.IsValid,
                    "The topology controller or prefab is invalid: " + expectation.id);
            }

            ApplyGeneric(ProductRoot + "/Source/" + manifest.invalidCases[0].modelFile);
            UnityGenericTopologyMatrixReport matrix =
                UnityGenericTopologyMatrixValidator.Validate(ProductRoot);
            Require(matrix.IsValid && matrix.ValidCaseCount == 5 &&
                matrix.ImportedClipCount == 5 && matrix.ControllerStateCount == 5 &&
                matrix.GenericImportVerified && matrix.HierarchyVerified &&
                matrix.SkinWeightsVerified && matrix.MotionVerified &&
                matrix.NegativeFindingsVerified,
                "Topology-matrix validation failed: " + string.Join(",", matrix.Findings));

            string readmeReference = ProductRoot + "/Documentation/README.txt";
            File.WriteAllText(ToPhysicalPath(readmeReference),
                "PB-0714 Generic topology matrix\n" +
                "Five redistribution-safe animated rigs plus stable negative findings.\n");
            AssetDatabase.ImportAsset(readmeReference, ImportAssetOptions.ForceSynchronousImport);

            string packagePath = Environment.GetEnvironmentVariable(
                "PACKAGEBUILDER_UNITY_TOPOLOGY_MATRIX_PACKAGE_OUTPUT");
            string packageManifestPath = Environment.GetEnvironmentVariable(
                "PACKAGEBUILDER_UNITY_TOPOLOGY_MATRIX_PACKAGE_MANIFEST");
            Require(!string.IsNullOrWhiteSpace(packagePath) &&
                !string.IsNullOrWhiteSpace(packageManifestPath),
                "The contained topology package evidence paths are missing.");
            var exportRequest = new UnityPackageExportRequest
            {
                ProductRootReference = ProductRoot,
                OutputPackagePath = packagePath,
            };
            UnityPackageExportPlan exportPlan;
            string exportDiagnostic;
            Require(UnityPackageExporter.TryCreatePlan(
                exportRequest, out exportPlan, out exportDiagnostic), exportDiagnostic);
            UnityPackageValidationReport validation = UnityPackageValidator.Validate(
                new UnityPackageValidationRequest
                {
                    ProductRootReference = ProductRoot,
                    ExpectedAssetReferences = exportPlan.AssetReferences,
                    PackageLogs = Array.Empty<UnityPackageLogEntry>(),
                });
            Require(validation.IsSuccessful,
                "Topology package validation failed: " + string.Join(",",
                    validation.Findings.Select(value => value.Code)));
            File.WriteAllLines(packageManifestPath, exportPlan.AssetReferences);
            Require(UnityPackageExporter.TryExport(
                exportRequest, out exportPlan, out exportDiagnostic), exportDiagnostic);
        }

        private static void ApplyGeneric(string sourceReference)
        {
            var importer = AssetImporter.GetAtPath(sourceReference) as ModelImporter;
            Require(importer != null, "The topology FBX importer is missing: " + sourceReference);
            string rootPath = (importer.transformPaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrEmpty(path))
                .OrderBy(path => path.Count(character => character == '/'))
                .ThenBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            Require(!string.IsNullOrEmpty(rootPath),
                "The topology FBX has no declared root: " + sourceReference);
            UnityRigImportResult result;
            string diagnostic;
            Require(UnityRigModelImporterPolicy.TryApply(
                new UnityRigImportRequest
                {
                    ModelAssetReference = sourceReference,
                    Mode = UnityRigImportMode.Generic,
                    RootNodePath = rootPath,
                    PreserveHierarchy = true,
                    OptimizeGameObjects = false,
                    ExposedTransformPaths = Array.Empty<string>(),
                },
                out result,
                out diagnostic), diagnostic);
            Require(result.AppliedMode == UnityRigImportMode.Generic && !result.AvatarIsHuman,
                "The topology fixture must remain Generic and non-Humanoid: " + sourceReference);
        }

        private static string ToPhysicalPath(string assetReference) => Path.Combine(
            Application.dataPath,
            assetReference.Substring("Assets/".Length)
                .Replace('/', Path.DirectorySeparatorChar));

        private static bool Approximately(float first, float second) =>
            Mathf.Abs(first - second) <= 0.001f;

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
