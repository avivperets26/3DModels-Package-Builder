using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using PackageBuilder.Contracts.Processes;

namespace PackageBuilder.Infrastructure.Processes;

/// <summary>Runs contained tools directly and captures their two output streams independently.</summary>
public sealed class StructuredExternalProcessRunner : IExternalProcessRunner
{
    internal const int StreamBufferSize = 4_096;
    internal const int HashBufferSize = 65_536;
    internal const int MaximumCaptureLimit = 16_777_216;
    internal static readonly TimeSpan MaximumTimeout = TimeSpan.FromDays(7);

    private static readonly TimeSpan _monitorInterval = TimeSpan.FromMilliseconds(20);

    private static readonly HashSet<string> _managedEnvironmentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "APPDATA",
        "DOTNET_CLI_HOME",
        "HOME",
        "LOCALAPPDATA",
        "NUGET_PACKAGES",
        "PACKAGEBUILDER_CACHE_ROOT",
        "PACKAGEBUILDER_CANCELLATION_FILE",
        "PACKAGEBUILDER_LOG_ROOT",
        "TEMP",
        "TMP",
        "TMPDIR",
        "USERPROFILE",
        "SystemRoot",
        "WINDIR",
        "XDG_CACHE_HOME",
    };

    public Task<ProcessExecutionOperationResult> RunAsync(ExternalProcessRequest request) =>
        RunAsync(request, CancellationToken.None);

    public async Task<ProcessExecutionOperationResult> RunAsync(
        ExternalProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            List<ProcessExecutionFailure> validationFailures = Validate(request);
            if (validationFailures.Count != 0)
            {
                return ProcessExecutionResult.Failed([.. validationFailures]);
            }

            cancellationToken.ThrowIfCancellationRequested();

            string executionId = Guid.NewGuid().ToString("N");
            string controlFile = Path.Combine(
                request.TemporaryDirectory,
                $".packagebuilder-cancel-{executionId}.request");
            using ExecutionLogFiles logs = CreateExecutionLogs(request, executionId);

            // Lock the selected executable against writes and replacement while identity is measured and launch begins.
            using FileStream executable = new(
                request.ExecutablePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                HashBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            ExecutableMetadata metadata = await InspectExecutableAsync(
                    request.ProjectRoot,
                    request.ExecutablePath,
                    executable,
                    cancellationToken)
                .ConfigureAwait(false);

            ProcessStartInfo startInfo = CreateStartInfo(request, controlFile);
            using var process = new Process { StartInfo = startInfo };
            _ = process.Start();

            var activity = new ProcessActivityTracker();
            Task<ProcessStreamCapture> outputTask = CaptureAsync(
                process.StandardOutput,
                logs.StandardOutput,
                request.MaximumCapturedCharactersPerStream,
                activity);
            Task<ProcessStreamCapture> errorTask = CaptureAsync(
                process.StandardError,
                logs.StandardError,
                request.MaximumCapturedCharactersPerStream,
                activity);

            Task exitTask = process.WaitForExitAsync(CancellationToken.None);
            ProcessCompletionKind completionKind = await WaitForCompletionAsync(
                    exitTask,
                    activity,
                    request.ExecutionPolicy,
                    cancellationToken)
                .ConfigureAwait(false);
            ProcessCompletionMetadata completion = completionKind == ProcessCompletionKind.Exited
                ? ProcessCompletionMetadata.Exited
                : await TerminateAsync(
                        process,
                        exitTask,
                        completionKind,
                        controlFile,
                        request.ExecutionPolicy.GracefulTerminationTimeout)
                    .ConfigureAwait(false);

            ProcessStreamCapture[] captures = await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
            logs.Flush();

            return ProcessExecutionResult.Success(
                new ExternalProcessReceipt(
                    request.JobId,
                    metadata,
                    process.ExitCode,
                    captures[0],
                    captures[1],
                    completion,
                    logs.Metadata));
        }
        catch (OperationCanceledException)
        {
            return Failed(
                "PROCESS_CANCELLED_BEFORE_START",
                "$",
                "The external process was cancelled before it started.");
        }
        catch (Exception exception) when (IsExpectedProcessFailure(exception))
        {
            return Failed(
                "PROCESS_EXECUTION_FAILED",
                "$",
                "The external process could not be executed or captured safely.");
        }
    }

    /// <summary>Builds a shell-free start description with literal arguments and an explicit child environment.</summary>
    private static ProcessStartInfo CreateStartInfo(ExternalProcessRequest request, string cancellationFile)
    {
        var startInfo = new ProcessStartInfo
        {
            CreateNoWindow = true,
            FileName = request.ExecutablePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            WorkingDirectory = request.WorkingDirectory,
        };

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment.Clear();
        CopySystemEnvironment(startInfo, "SystemRoot");
        CopySystemEnvironment(startInfo, "WINDIR");
        foreach (ProcessEnvironmentVariable variable in request.EnvironmentVariables)
        {
            startInfo.Environment.Add(variable.Name, variable.Value);
        }

        // These roots are controlled by the runner so child tools cannot redirect project-owned state elsewhere.
        SetManagedEnvironment(
            startInfo,
            request.TemporaryDirectory,
            request.CacheDirectory,
            request.LogDirectory,
            cancellationFile);
        return startInfo;
    }

    /// <summary>Copies one reviewed operating-system variable rather than inheriting the complete parent environment.</summary>
    private static void CopySystemEnvironment(ProcessStartInfo startInfo, string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrEmpty(value))
        {
            startInfo.Environment.Add(name, value);
        }
    }

    /// <summary>Forces all common profile, temporary, cache, and Package Builder log variables to contained roots.</summary>
    private static void SetManagedEnvironment(
        ProcessStartInfo startInfo,
        string temporaryDirectory,
        string cacheDirectory,
        string logDirectory,
        string cancellationFile)
    {
        foreach (string name in new[] { "APPDATA", "HOME", "LOCALAPPDATA", "TEMP", "TMP", "TMPDIR", "USERPROFILE" })
        {
            startInfo.Environment[name] = temporaryDirectory;
        }

        foreach (string name in new[] { "DOTNET_CLI_HOME", "NUGET_PACKAGES", "PACKAGEBUILDER_CACHE_ROOT", "XDG_CACHE_HOME" })
        {
            startInfo.Environment[name] = cacheDirectory;
        }

        startInfo.Environment["PACKAGEBUILDER_CANCELLATION_FILE"] = cancellationFile;
        startInfo.Environment["PACKAGEBUILDER_LOG_ROOT"] = logDirectory;
    }

    /// <summary>Drains one stream completely while retaining at most the caller-approved character limit.</summary>
    private static async Task<ProcessStreamCapture> CaptureAsync(
        StreamReader reader,
        StreamWriter log,
        int maximumCharacters,
        ProcessActivityTracker activity)
    {
        char[] buffer = ArrayPool<char>.Shared.Rent(StreamBufferSize);
        var captured = new StringBuilder(Math.Min(maximumCharacters, StreamBufferSize));
        bool truncated = false;
        try
        {
            while (true)
            {
                int read = await reader.ReadAsync(buffer.AsMemory(0, StreamBufferSize)).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                activity.Record();
                await log.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                await log.FlushAsync().ConfigureAwait(false);

                int remaining = maximumCharacters - captured.Length;
                int retained = Math.Min(Math.Max(remaining, 0), read);
                if (retained > 0)
                {
                    _ = captured.Append(buffer, 0, retained);
                }

                truncated |= retained != read;
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer, clearArray: true);
        }

        return new ProcessStreamCapture(captured.ToString(), truncated);
    }

    /// <summary>Streams the locked executable into immutable size, SHA-256, and version metadata.</summary>
    private static async Task<ExecutableMetadata> InspectExecutableAsync(
        string projectRoot,
        string executablePath,
        Stream executable,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(HashBufferSize);
        long bytes = 0;
        try
        {
            while (true)
            {
                int read = await executable.ReadAsync(
                        buffer.AsMemory(0, HashBufferSize),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                bytes = checked(bytes + read);
                hash.AppendData(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        var version = FileVersionInfo.GetVersionInfo(executablePath);
        return new ExecutableMetadata(
            Path.GetRelativePath(projectRoot, executablePath).Replace(Path.DirectorySeparatorChar, '/'),
            bytes,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            version.FileVersion,
            version.ProductVersion);
    }

    /// <summary>Validates literal values, containment, existing types, and reparse boundaries immediately before launch.</summary>
    private static List<ProcessExecutionFailure> Validate(ExternalProcessRequest request)
    {
        var failures = new List<ProcessExecutionFailure>();
        string? projectRoot = CanonicalizeDirectory(request.ProjectRoot, "projectRoot", failures);
        string? executablePath = CanonicalizeFile(request.ExecutablePath, "executable", failures);
        string? workingDirectory = CanonicalizeDirectory(request.WorkingDirectory, "workingDirectory", failures);
        string? temporaryDirectory = CanonicalizeDirectory(request.TemporaryDirectory, "temporaryDirectory", failures);
        string? cacheDirectory = CanonicalizeDirectory(request.CacheDirectory, "cacheDirectory", failures);
        string? logDirectory = CanonicalizeDirectory(request.LogDirectory, "logDirectory", failures);

        ValidateArguments(request.Arguments, failures);
        ValidateEnvironment(request.EnvironmentVariables, failures);
        ValidateExecutionPolicy(request.ExecutionPolicy, failures);
        if (request.MaximumCapturedCharactersPerStream is <= 0 or > MaximumCaptureLimit)
        {
            failures.Add(Failure(
                "PROCESS_CAPTURE_LIMIT_INVALID",
                "maximumCapturedCharactersPerStream",
                $"The capture limit must be between 1 and {MaximumCaptureLimit} characters."));
        }

        if (failures.Count != 0)
        {
            return failures;
        }

        ValidateExistingDirectory(projectRoot!, "projectRoot", failures);
        ValidateExistingFile(executablePath!, "executable", failures);
        ValidateExistingDirectory(workingDirectory!, "workingDirectory", failures);
        ValidateExistingDirectory(temporaryDirectory!, "temporaryDirectory", failures);
        ValidateExistingDirectory(cacheDirectory!, "cacheDirectory", failures);
        ValidateExistingDirectory(logDirectory!, "logDirectory", failures);

        foreach ((string path, string location) in new[]
                 {
                     (executablePath!, "executable"),
                     (workingDirectory!, "workingDirectory"),
                     (temporaryDirectory!, "temporaryDirectory"),
                     (cacheDirectory!, "cacheDirectory"),
                     (logDirectory!, "logDirectory"),
                 })
        {
            if (!IsStrictDescendant(path, projectRoot!))
            {
                failures.Add(Failure(
                    "PROCESS_PATH_OUTSIDE_PROJECT",
                    location,
                    "The process path must be a strict descendant of the project root."));
            }
        }

        if (failures.Count == 0)
        {
            ValidateNoReparseBoundary(projectRoot!, projectRoot!, "projectRoot", failures);
            ValidateNoReparseBoundary(projectRoot!, executablePath!, "executable", failures);
            ValidateNoReparseBoundary(projectRoot!, workingDirectory!, "workingDirectory", failures);
            ValidateNoReparseBoundary(projectRoot!, temporaryDirectory!, "temporaryDirectory", failures);
            ValidateNoReparseBoundary(projectRoot!, cacheDirectory!, "cacheDirectory", failures);
            ValidateNoReparseBoundary(projectRoot!, logDirectory!, "logDirectory", failures);
        }

        return failures;
    }

    /// <summary>Rejects nonpositive, excessive, or internally contradictory process deadlines.</summary>
    private static void ValidateExecutionPolicy(
        ProcessExecutionPolicy policy,
        List<ProcessExecutionFailure> failures)
    {
        foreach ((TimeSpan value, string location) in new[]
                 {
                     (policy.StartupTimeout, "executionPolicy.startupTimeout"),
                     (policy.IdleTimeout, "executionPolicy.idleTimeout"),
                     (policy.TotalTimeout, "executionPolicy.totalTimeout"),
                     (policy.GracefulTerminationTimeout, "executionPolicy.gracefulTerminationTimeout"),
                 })
        {
            if (value <= TimeSpan.Zero || value > MaximumTimeout)
            {
                failures.Add(Failure(
                    "PROCESS_TIMEOUT_INVALID",
                    location,
                    "A process timeout must be positive and no greater than seven days."));
            }
        }

        if (policy.StartupTimeout > policy.TotalTimeout)
        {
            failures.Add(Failure(
                "PROCESS_TIMEOUT_ORDER_INVALID",
                "executionPolicy.startupTimeout",
                "The startup timeout must not exceed the total timeout."));
        }

        if (policy.IdleTimeout > policy.TotalTimeout)
        {
            failures.Add(Failure(
                "PROCESS_TIMEOUT_ORDER_INVALID",
                "executionPolicy.idleTimeout",
                "The idle timeout must not exceed the total timeout."));
        }
    }

    /// <summary>Observes external cancellation and startup, inactivity, and total-runtime deadlines.</summary>
    private static async Task<ProcessCompletionKind> WaitForCompletionAsync(
        Task exitTask,
        ProcessActivityTracker activity,
        ProcessExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        while (!exitTask.IsCompleted)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ProcessCompletionKind.Cancelled;
            }

            TimeSpan elapsed = activity.Elapsed;
            if (elapsed >= policy.TotalTimeout)
            {
                return ProcessCompletionKind.TotalTimedOut;
            }

            if (!activity.HasActivity && elapsed >= policy.StartupTimeout)
            {
                return ProcessCompletionKind.StartupTimedOut;
            }

            if (activity.HasActivity && activity.Idle >= policy.IdleTimeout)
            {
                return ProcessCompletionKind.IdleTimedOut;
            }

            _ = await Task.WhenAny(
                    exitTask,
                    Task.Delay(_monitorInterval, CancellationToken.None))
                .ConfigureAwait(false);
        }

        await exitTask.ConfigureAwait(false);
        return ProcessCompletionKind.Exited;
    }

    /// <summary>Signals the contained worker cooperatively, then kills its entire tree after the grace period.</summary>
    private static async Task<ProcessCompletionMetadata> TerminateAsync(
        Process process,
        Task exitTask,
        ProcessCompletionKind completionKind,
        string controlFile,
        TimeSpan gracefulTerminationTimeout)
    {
        bool signalCreated = TryCreateCancellationSignal(controlFile);
        bool forced = false;

        if (!exitTask.IsCompleted)
        {
            _ = await Task.WhenAny(exitTask, Task.Delay(gracefulTerminationTimeout)).ConfigureAwait(false);
        }

        if (!exitTask.IsCompleted && !process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            forced = true;
        }

        await exitTask.ConfigureAwait(false);
        bool cleanupSucceeded = !signalCreated || TryDeleteCancellationSignal(controlFile);
        return new ProcessCompletionMetadata(
            completionKind,
            signalCreated,
            gracefulTerminationAcknowledged: signalCreated && !forced,
            forced,
            cleanupSucceeded);
    }

    /// <summary>Creates one empty cancellation request atomically without replacing existing state.</summary>
    private static bool TryCreateCancellationSignal(string controlFile)
    {
        try
        {
            using FileStream signal = new(
                controlFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 1,
                FileOptions.WriteThrough);
            signal.Flush(flushToDisk: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Removes only the exact runner-owned signal file and reports cleanup failure explicitly.</summary>
    private static bool TryDeleteCancellationSignal(string controlFile)
    {
        try
        {
            File.Delete(controlFile);
            return !File.Exists(controlFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Creates unique complete stream logs beneath the validated contained log root.</summary>
    private static ExecutionLogFiles CreateExecutionLogs(ExternalProcessRequest request, string executionId)
    {
        string jobKey = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(request.JobId.Value)))
            .ToLowerInvariant()[..16];
        string directory = Path.Combine(request.LogDirectory, "processes", jobKey, executionId);
        _ = Directory.CreateDirectory(directory);
        string standardOutput = Path.Combine(directory, "stdout.log");
        string standardError = Path.Combine(directory, "stderr.log");
        return new ExecutionLogFiles(
            request.ProjectRoot,
            standardOutput,
            standardError);
    }

    /// <summary>Validates arguments without trimming or interpreting shell metacharacters.</summary>
    private static void ValidateArguments(
        IReadOnlyList<string> arguments,
        List<ProcessExecutionFailure> failures)
    {
        for (int index = 0; index < arguments.Count; index++)
        {
            string? argument = arguments[index];
            if (argument is null || argument.Contains('\0'))
            {
                failures.Add(Failure(
                    "PROCESS_ARGUMENT_INVALID",
                    $"arguments[{index}]",
                    "A process argument must be non-null and contain no null character."));
            }
        }
    }

    /// <summary>Rejects ambiguous, duplicate, null-containing, or runner-owned environment entries.</summary>
    private static void ValidateEnvironment(
        IReadOnlyList<ProcessEnvironmentVariable> variables,
        List<ProcessExecutionFailure> failures)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < variables.Count; index++)
        {
            ProcessEnvironmentVariable? variable = variables[index];
            if (variable is null)
            {
                failures.Add(Failure(
                    "PROCESS_ENVIRONMENT_INVALID",
                    $"environmentVariables[{index}]",
                    "An environment entry is required."));
                continue;
            }

            string name = variable.Name;
            if (string.IsNullOrWhiteSpace(name)
                || !string.Equals(name, name.Trim(), StringComparison.Ordinal)
                || name.Contains('=')
                || name.Any(char.IsControl))
            {
                failures.Add(Failure(
                    "PROCESS_ENVIRONMENT_NAME_INVALID",
                    $"environmentVariables[{index}].name",
                    "The environment name is invalid."));
            }
            else if (!names.Add(name))
            {
                failures.Add(Failure(
                    "PROCESS_ENVIRONMENT_DUPLICATE",
                    $"environmentVariables[{index}].name",
                    "Environment names must be unique under Windows comparison."));
            }
            else if (_managedEnvironmentNames.Contains(name))
            {
                failures.Add(Failure(
                    "PROCESS_ENVIRONMENT_RESERVED",
                    $"environmentVariables[{index}].name",
                    "The environment name is controlled by the process runner."));
            }

            if (variable.Value.Contains('\0'))
            {
                failures.Add(Failure(
                    "PROCESS_ENVIRONMENT_VALUE_INVALID",
                    $"environmentVariables[{index}].value",
                    "The environment value contains a null character."));
            }
        }
    }

    private static string? CanonicalizeDirectory(
        string value,
        string location,
        List<ProcessExecutionFailure> failures)
    {
        string? full = Canonicalize(value, location, failures);
        if (full is null)
        {
            return null;
        }

        string canonical = Path.TrimEndingDirectorySeparator(full);
        if (!StringComparer.OrdinalIgnoreCase.Equals(canonical, Path.TrimEndingDirectorySeparator(value)))
        {
            failures.Add(Failure(
                "PROCESS_PATH_NOT_CANONICAL",
                location,
                "The process path must already be canonical."));
            return null;
        }

        return canonical;
    }

    private static string? CanonicalizeFile(
        string value,
        string location,
        List<ProcessExecutionFailure> failures)
    {
        string? full = Canonicalize(value, location, failures);
        if (full is not null && !StringComparer.OrdinalIgnoreCase.Equals(full, value))
        {
            failures.Add(Failure(
                "PROCESS_PATH_NOT_CANONICAL",
                location,
                "The process path must already be canonical."));
            return null;
        }

        return full;
    }

    private static string? Canonicalize(
        string value,
        string location,
        List<ProcessExecutionFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Path.IsPathFullyQualified(value)
            || value.StartsWith("\\\\", StringComparison.Ordinal))
        {
            failures.Add(Failure(
                "PROCESS_PATH_INVALID",
                location,
                "A fully qualified local path is required."));
            return null;
        }

        try
        {
            return Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            failures.Add(Failure("PROCESS_PATH_INVALID", location, "The process path is invalid."));
            return null;
        }
    }

    private static void ValidateExistingDirectory(
        string path,
        string location,
        List<ProcessExecutionFailure> failures)
    {
        if (!Directory.Exists(path))
        {
            failures.Add(Failure(
                "PROCESS_DIRECTORY_MISSING",
                location,
                "The required process directory does not exist."));
        }
    }

    private static void ValidateExistingFile(
        string path,
        string location,
        List<ProcessExecutionFailure> failures)
    {
        if (!File.Exists(path))
        {
            failures.Add(Failure("PROCESS_EXECUTABLE_MISSING", location, "The selected executable does not exist."));
        }
    }

    /// <summary>Inspects each existing component from the trusted root through the exact launch path.</summary>
    private static void ValidateNoReparseBoundary(
        string projectRoot,
        string candidate,
        string location,
        List<ProcessExecutionFailure> failures)
    {
        string current = projectRoot;
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
        {
            failures.Add(Failure(
                "PROCESS_REPARSE_BOUNDARY",
                "projectRoot",
                "The project root must not be a reparse point."));
            return;
        }

        string relative = Path.GetRelativePath(projectRoot, candidate);
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                failures.Add(Failure(
                    "PROCESS_REPARSE_BOUNDARY",
                    location,
                    "The process path must not cross a reparse point."));
                return;
            }
        }
    }

    private static bool IsStrictDescendant(string candidate, string boundary) =>
        !StringComparer.OrdinalIgnoreCase.Equals(candidate, boundary)
        && candidate.StartsWith(EnsureTrailingSeparator(boundary), StringComparison.OrdinalIgnoreCase);

    private static string EnsureTrailingSeparator(string path) =>
        path + Path.DirectorySeparatorChar;

    private static bool IsExpectedProcessFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception
            or OverflowException;

    private static ProcessExecutionFailure Failure(string code, string location, string diagnostic) =>
        new(code, location, diagnostic);

    private static ProcessExecutionOperationResult Failed(string code, string location, string diagnostic) =>
        ProcessExecutionResult.Failed(Failure(code, location, diagnostic));

    private sealed class ProcessActivityTracker
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        private long _lastActivity;

        public bool HasActivity => Volatile.Read(ref _lastActivity) != 0;

        public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_started);

        public TimeSpan Idle => Stopwatch.GetElapsedTime(Volatile.Read(ref _lastActivity));

        public void Record() => Volatile.Write(ref _lastActivity, Stopwatch.GetTimestamp());
    }

    private sealed class ExecutionLogFiles(
        string projectRoot,
        string standardOutputPath,
        string standardErrorPath) : IDisposable
    {
        public StreamWriter StandardOutput { get; } = CreateWriter(standardOutputPath);

        public StreamWriter StandardError { get; } = CreateWriter(standardErrorPath);

        public ProcessLogMetadata Metadata { get; } = new ProcessLogMetadata(
                RelativeReference(projectRoot, standardOutputPath),
                RelativeReference(projectRoot, standardErrorPath));

        public void Flush()
        {
            StandardOutput.Flush();
            StandardError.Flush();
        }

        public void Dispose()
        {
            StandardOutput.Dispose();
            StandardError.Dispose();
        }

        private static StreamWriter CreateWriter(string path) =>
            new(
                new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    StreamBufferSize,
                    FileOptions.SequentialScan),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        private static string RelativeReference(string projectRoot, string path) =>
            Path.GetRelativePath(projectRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    }
}
