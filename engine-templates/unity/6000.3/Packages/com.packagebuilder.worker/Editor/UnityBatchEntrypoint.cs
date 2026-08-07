using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Executes one protocol-v1 request in Unity batch mode and exits with a stable code.</summary>
    public static class UnityBatchEntrypoint
    {
        public const string EngineVersion = "6000.3.10f1";
        private const string RequestArgument = "-packageBuilderRequest";

        public static void Run()
        {
            UnityWorkerExitCode exitCode = Execute(Environment.GetCommandLineArgs());
            EditorApplication.Exit((int)exitCode);
        }

        internal static UnityWorkerExitCode Execute(string[] arguments)
        {
            string requestPath;
            if (!TryReadRequestArgument(arguments, out requestPath))
            {
                Console.Error.WriteLine("UNITY_INVOCATION_INVALID");
                return UnityWorkerExitCode.InvocationFailure;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string requestJson;
            try
            {
                requestJson = File.ReadAllText(requestPath, Encoding.UTF8);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Console.Error.WriteLine("UNITY_REQUEST_READ_FAILED");
                return UnityWorkerExitCode.InvalidRequest;
            }

            UnityWorkerRequest request;
            string diagnosticCode;
            if (!UnityWorkerRequestParser.TryParse(requestJson, out request, out diagnosticCode))
            {
                Console.Error.WriteLine(diagnosticCode);
                return UnityWorkerExitCode.InvalidRequest;
            }

            string resultPath;
            try
            {
                resultPath = UnityWorkerFileSystem.ResolveProjectReference(projectRoot, request.resultFileReference);
            }
            catch (InvalidDataException)
            {
                Console.Error.WriteLine("UNITY_RESULT_REFERENCE_INVALID");
                return UnityWorkerExitCode.InvalidRequest;
            }

            if (!string.Equals(request.operation, "probe-unity-worker", StringComparison.Ordinal))
            {
                return WriteFailure(
                    request,
                    resultPath,
                    UnityWorkerExitCode.UnsupportedOperation,
                    "UNITY_OPERATION_UNSUPPORTED",
                    "The requested Unity worker operation is not supported.");
            }

            string cancellationFile = Environment.GetEnvironmentVariable("PACKAGEBUILDER_CANCELLATION_FILE");
            if (!string.IsNullOrEmpty(cancellationFile) && File.Exists(cancellationFile))
            {
                try
                {
                    UnityWorkerFileSystem.WriteAllTextAtomically(resultPath, UnityWorkerJson.CancelledResult(request.jobId));
                    Emit(UnityWorkerJson.Progress(request.jobId, "cancelled", "Cancellation was acknowledged.", 100));
                    return UnityWorkerExitCode.Cancelled;
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    Console.Error.WriteLine("UNITY_RESULT_WRITE_FAILED");
                    return UnityWorkerExitCode.ResultWriteFailure;
                }
            }

            try
            {
                Emit(UnityWorkerJson.Progress(request.jobId, "starting", "Unity worker request accepted.", 0));
                string manifestPath = UnityWorkerFileSystem.ResolveProjectReference(
                    projectRoot,
                    request.productManifestReference);
                string inputPath = UnityWorkerFileSystem.ResolveProjectReference(
                    projectRoot,
                    request.inputDirectoryReference);
                if (!File.Exists(manifestPath) || !Directory.Exists(inputPath))
                {
                    return WriteFailure(
                        request,
                        resultPath,
                        UnityWorkerExitCode.ExecutionFailure,
                        "UNITY_INPUT_MISSING",
                        "The requested manifest or input directory is unavailable.");
                }

                string outputPath = UnityWorkerFileSystem.ResolveProjectReference(
                    projectRoot,
                    request.outputDirectoryReference);
                string assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar);
                if (!outputPath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return WriteFailure(
                        request,
                        resultPath,
                        UnityWorkerExitCode.ExecutionFailure,
                        "UNITY_OUTPUT_NOT_ASSET",
                        "The Unity worker output must be located beneath Assets.");
                }

                Directory.CreateDirectory(outputPath);
                string artifactPath = Path.Combine(outputPath, "worker-probe.txt");
                string artifactText = "Package Builder Unity worker 1.0.0\n" + request.jobId + "\n";
                UnityWorkerFileSystem.WriteAllTextAtomically(artifactPath, artifactText.TrimEnd('\n'));
                string artifactReference = MakeProjectRelative(projectRoot, artifactPath);
                AssetDatabase.ImportAsset(artifactReference, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.SaveAssets();
                Emit(UnityWorkerJson.Progress(request.jobId, "saving-assets", "Unity assets were saved.", 90));
                Emit(UnityWorkerJson.Metric(request.jobId, "assets-saved", 1, "count"));

                long byteCount = new FileInfo(artifactPath).Length;
                UnityWorkerFileSystem.WriteAllTextAtomically(
                    resultPath,
                    UnityWorkerJson.SuccessResult(request.jobId, artifactReference, byteCount));
                Emit(UnityWorkerJson.Progress(request.jobId, "completed", "Unity worker request completed.", 100));
                return UnityWorkerExitCode.Success;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException)
            {
                return WriteFailure(
                    request,
                    resultPath,
                    UnityWorkerExitCode.ExecutionFailure,
                    "UNITY_WORKER_EXECUTION_FAILED",
                    "The Unity worker could not complete the requested operation.");
            }
        }

        private static UnityWorkerExitCode WriteFailure(
            UnityWorkerRequest request,
            string resultPath,
            UnityWorkerExitCode exitCode,
            string findingCode,
            string explanation)
        {
            try
            {
                UnityWorkerFileSystem.WriteAllTextAtomically(
                    resultPath,
                    UnityWorkerJson.FailureResult(request.jobId, findingCode, explanation));
                return exitCode;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Console.Error.WriteLine("UNITY_RESULT_WRITE_FAILED");
                return UnityWorkerExitCode.ResultWriteFailure;
            }
        }

        private static bool TryReadRequestArgument(string[] arguments, out string requestPath)
        {
            requestPath = string.Empty;
            for (int index = 0; index < arguments.Length; index++)
            {
                if (!string.Equals(arguments[index], RequestArgument, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(requestPath) || index + 1 >= arguments.Length ||
                    string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    return false;
                }

                requestPath = Path.GetFullPath(arguments[++index]);
            }

            return !string.IsNullOrEmpty(requestPath) && File.Exists(requestPath);
        }

        private static string MakeProjectRelative(string projectRoot, string path)
        {
            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).Substring(root.Length).Replace('\\', '/');
        }

        private static void Emit(string json)
        {
            Console.Out.WriteLine(json);
            Console.Out.Flush();
        }
    }
}
