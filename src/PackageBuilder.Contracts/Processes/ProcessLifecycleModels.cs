namespace PackageBuilder.Contracts.Processes;

/// <summary>Defines bounded startup, inactivity, total-runtime, and cooperative-shutdown intervals.</summary>
public sealed class ProcessExecutionPolicy(
    TimeSpan startupTimeout,
    TimeSpan idleTimeout,
    TimeSpan totalTimeout,
    TimeSpan gracefulTerminationTimeout)
{
    public static ProcessExecutionPolicy Default { get; } = new(
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(4),
        TimeSpan.FromSeconds(15));

    public TimeSpan StartupTimeout { get; } = startupTimeout;

    public TimeSpan IdleTimeout { get; } = idleTimeout;

    public TimeSpan TotalTimeout { get; } = totalTimeout;

    public TimeSpan GracefulTerminationTimeout { get; } = gracefulTerminationTimeout;
}

/// <summary>Identifies why a started process stopped being allowed to run.</summary>
public enum ProcessCompletionKind
{
    Exited = 0,
    Cancelled = 1,
    StartupTimedOut = 2,
    IdleTimedOut = 3,
    TotalTimedOut = 4,
}

/// <summary>Records cooperative signalling, forced escalation, and control-file cleanup.</summary>
public sealed class ProcessCompletionMetadata(
    ProcessCompletionKind kind,
    bool cancellationSignalCreated,
    bool gracefulTerminationAcknowledged,
    bool forcedTermination,
    bool controlFileCleanupSucceeded)
{
    public static ProcessCompletionMetadata Exited { get; } = new(
        ProcessCompletionKind.Exited,
        cancellationSignalCreated: false,
        gracefulTerminationAcknowledged: false,
        forcedTermination: false,
        controlFileCleanupSucceeded: true);

    public ProcessCompletionKind Kind { get; } = kind;

    public bool CancellationSignalCreated { get; } = cancellationSignalCreated;

    public bool GracefulTerminationAcknowledged { get; } = gracefulTerminationAcknowledged;

    public bool ForcedTermination { get; } = forcedTermination;

    public bool ControlFileCleanupSucceeded { get; } = controlFileCleanupSucceeded;
}

/// <summary>Records safe project-relative references to the complete preserved process streams.</summary>
public sealed class ProcessLogMetadata(string standardOutputReference, string standardErrorReference)
{
    public string StandardOutputReference { get; } = ValidateReference(standardOutputReference);

    public string StandardErrorReference { get; } = ValidateReference(standardErrorReference);

    private static string ValidateReference(string? value)
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
            : throw new ArgumentException("A safe project-relative log reference is required.", nameof(value));
    }
}
