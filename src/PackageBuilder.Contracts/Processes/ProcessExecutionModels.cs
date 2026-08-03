using System.Collections.ObjectModel;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Contracts.Processes;

/// <summary>Declares one literal child-process environment variable.</summary>
public sealed class ProcessEnvironmentVariable(string name, string value)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public string Value { get; } = value ?? throw new ArgumentNullException(nameof(value));
}

/// <summary>Describes one contained direct process invocation without a shell command string.</summary>
public sealed class ExternalProcessRequest
{
    private readonly ReadOnlyCollection<string> _arguments;
    private readonly ReadOnlyCollection<ProcessEnvironmentVariable> _environmentVariables;

    public ExternalProcessRequest(
        BuildJobId jobId,
        string projectRoot,
        string executablePath,
        string workingDirectory,
        string temporaryDirectory,
        string cacheDirectory,
        string logDirectory,
        IEnumerable<string> arguments,
        IEnumerable<ProcessEnvironmentVariable> environmentVariables,
        int maximumCapturedCharactersPerStream,
        ProcessExecutionPolicy? executionPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environmentVariables);

        JobId = jobId ?? throw new ArgumentNullException(nameof(jobId));
        ProjectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
        ExecutablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        WorkingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
        TemporaryDirectory = temporaryDirectory ?? throw new ArgumentNullException(nameof(temporaryDirectory));
        CacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        LogDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
        _arguments = Array.AsReadOnly(arguments.ToArray());
        _environmentVariables = Array.AsReadOnly(environmentVariables.ToArray());
        MaximumCapturedCharactersPerStream = maximumCapturedCharactersPerStream;
        ExecutionPolicy = executionPolicy ?? ProcessExecutionPolicy.Default;
    }

    public BuildJobId JobId { get; }

    public string ProjectRoot { get; }

    public string ExecutablePath { get; }

    public string WorkingDirectory { get; }

    public string TemporaryDirectory { get; }

    public string CacheDirectory { get; }

    public string LogDirectory { get; }

    public IReadOnlyList<string> Arguments => _arguments;

    public IReadOnlyList<ProcessEnvironmentVariable> EnvironmentVariables => _environmentVariables;

    public int MaximumCapturedCharactersPerStream { get; }

    public ProcessExecutionPolicy ExecutionPolicy { get; }
}

/// <summary>Records immutable identity metadata for the executable that was started.</summary>
public sealed class ExecutableMetadata(
    string projectRelativePath,
    long byteCount,
    string sha256,
    string? fileVersion,
    string? productVersion)
{
    public string ProjectRelativePath { get; } = ValidateRelativePath(projectRelativePath);

    public long ByteCount { get; } = byteCount >= 0
        ? byteCount
        : throw new ArgumentOutOfRangeException(nameof(byteCount));

    public string Sha256 { get; } = IsSha256(sha256)
        ? sha256!
        : throw new ArgumentException("A canonical lowercase SHA-256 digest is required.", nameof(sha256));

    public string? FileVersion { get; } = fileVersion;

    public string? ProductVersion { get; } = productVersion;

    private static string ValidateRelativePath(string? value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string[] segments = value.Split('/');
        return !string.IsNullOrWhiteSpace(value)
               && !Path.IsPathFullyQualified(value)
               && !value.Contains('\\')
               && !value.Contains(':')
               && !value.Any(char.IsControl)
               && segments.All(static segment => segment.Length > 0 && segment is not "." and not "..")
            ? value
            : throw new ArgumentException("A safe project-relative executable path is required.", nameof(value));
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 }
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

/// <summary>Contains one bounded standard stream capture and whether additional text was discarded.</summary>
public sealed class ProcessStreamCapture(string text, bool wasTruncated)
{
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));

    public bool WasTruncated { get; } = wasTruncated;
}

/// <summary>Records the observable result of one process that started and exited.</summary>
public sealed class ExternalProcessReceipt(
    BuildJobId jobId,
    ExecutableMetadata executable,
    int exitCode,
    ProcessStreamCapture standardOutput,
    ProcessStreamCapture standardError,
    ProcessCompletionMetadata? completion = null,
    ProcessLogMetadata? logs = null)
{
    public BuildJobId JobId { get; } = jobId ?? throw new ArgumentNullException(nameof(jobId));

    public ExecutableMetadata Executable { get; } = executable
        ?? throw new ArgumentNullException(nameof(executable));

    public int ExitCode { get; } = exitCode;

    public ProcessStreamCapture StandardOutput { get; } = standardOutput
        ?? throw new ArgumentNullException(nameof(standardOutput));

    public ProcessStreamCapture StandardError { get; } = standardError
        ?? throw new ArgumentNullException(nameof(standardError));

    public ProcessCompletionMetadata Completion { get; } = completion ?? ProcessCompletionMetadata.Exited;

    public ProcessLogMetadata? Logs { get; } = logs;
}
