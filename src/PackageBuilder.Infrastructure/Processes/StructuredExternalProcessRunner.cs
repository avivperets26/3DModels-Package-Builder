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

    private static readonly HashSet<string> _managedEnvironmentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "APPDATA",
        "DOTNET_CLI_HOME",
        "HOME",
        "LOCALAPPDATA",
        "NUGET_PACKAGES",
        "PACKAGEBUILDER_CACHE_ROOT",
        "PACKAGEBUILDER_LOG_ROOT",
        "TEMP",
        "TMP",
        "TMPDIR",
        "USERPROFILE",
        "SystemRoot",
        "WINDIR",
        "XDG_CACHE_HOME",
    };

    public async Task<ProcessExecutionOperationResult> RunAsync(ExternalProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            List<ProcessExecutionFailure> validationFailures = Validate(request);
            if (validationFailures.Count != 0)
            {
                return ProcessExecutionResult.Failed([.. validationFailures]);
            }

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
                    executable)
                .ConfigureAwait(false);

            ProcessStartInfo startInfo = CreateStartInfo(request);
            using var process = new Process { StartInfo = startInfo };
            _ = process.Start();

            Task<ProcessStreamCapture> outputTask = CaptureAsync(
                process.StandardOutput,
                request.MaximumCapturedCharactersPerStream);
            Task<ProcessStreamCapture> errorTask = CaptureAsync(
                process.StandardError,
                request.MaximumCapturedCharactersPerStream);

            await process.WaitForExitAsync().ConfigureAwait(false);
            ProcessStreamCapture[] captures = await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);

            return ProcessExecutionResult.Success(
                new ExternalProcessReceipt(
                    request.JobId,
                    metadata,
                    process.ExitCode,
                    captures[0],
                    captures[1]));
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
    private static ProcessStartInfo CreateStartInfo(ExternalProcessRequest request)
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
        SetManagedEnvironment(startInfo, request.TemporaryDirectory, request.CacheDirectory, request.LogDirectory);
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
        string logDirectory)
    {
        foreach (string name in new[] { "APPDATA", "HOME", "LOCALAPPDATA", "TEMP", "TMP", "TMPDIR", "USERPROFILE" })
        {
            startInfo.Environment[name] = temporaryDirectory;
        }

        foreach (string name in new[] { "DOTNET_CLI_HOME", "NUGET_PACKAGES", "PACKAGEBUILDER_CACHE_ROOT", "XDG_CACHE_HOME" })
        {
            startInfo.Environment[name] = cacheDirectory;
        }

        startInfo.Environment["PACKAGEBUILDER_LOG_ROOT"] = logDirectory;
    }

    /// <summary>Drains one stream completely while retaining at most the caller-approved character limit.</summary>
    private static async Task<ProcessStreamCapture> CaptureAsync(StreamReader reader, int maximumCharacters)
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
        Stream executable)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(HashBufferSize);
        long bytes = 0;
        try
        {
            while (true)
            {
                int read = await executable.ReadAsync(buffer.AsMemory(0, HashBufferSize)).ConfigureAwait(false);
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
}
