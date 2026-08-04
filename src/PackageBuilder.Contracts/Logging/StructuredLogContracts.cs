using System.Collections.ObjectModel;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Contracts.Logging;

/// <summary>Stable severity levels written to structured application and job logs.</summary>
public enum StructuredLogSeverity
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Fatal,
}

/// <summary>A caller-provided structured property. Values are redacted before persistence.</summary>
public sealed record StructuredLogProperty(string Name, string? Value);

/// <summary>A validated immutable log event independent of any logging implementation.</summary>
public sealed class StructuredLogEvent
{
    private StructuredLogEvent(
        DateTimeOffset timestampUtc,
        string correlationId,
        string component,
        string? step,
        StructuredLogSeverity severity,
        string message,
        ReadOnlyCollection<StructuredLogProperty> properties)
    {
        TimestampUtc = timestampUtc;
        CorrelationId = correlationId;
        Component = component;
        Step = step;
        Severity = severity;
        Message = message;
        Properties = properties;
    }

    public DateTimeOffset TimestampUtc { get; }

    public string CorrelationId { get; }

    public string Component { get; }

    public string? Step { get; }

    public StructuredLogSeverity Severity { get; }

    public string Message { get; }

    public IReadOnlyList<StructuredLogProperty> Properties { get; }

    /// <summary>Validates and snapshots one event without throwing for expected input failures.</summary>
    public static StructuredLogResult<StructuredLogEvent> Create(
        DateTimeOffset timestampUtc,
        string? correlationId,
        string? component,
        string? step,
        StructuredLogSeverity severity,
        string? message,
        IEnumerable<StructuredLogProperty?>? properties = null)
    {
        if (timestampUtc.Offset != TimeSpan.Zero)
        {
            return StructuredLogResult.Failure<StructuredLogEvent>(
                "LOG_TIMESTAMP_NOT_UTC",
                "Log timestamps must use the UTC offset.");
        }

        if (!StructuredLogValueValidator.IsToken(correlationId, 128))
        {
            return StructuredLogResult.Failure<StructuredLogEvent>(
                "LOG_CORRELATION_ID_INVALID",
                "The correlation ID is missing or malformed.");
        }

        if (!StructuredLogValueValidator.IsToken(component, 128))
        {
            return StructuredLogResult.Failure<StructuredLogEvent>(
                "LOG_COMPONENT_INVALID",
                "The component is missing or malformed.");
        }

        if (step is not null && !StructuredLogValueValidator.IsToken(step, 128))
        {
            return StructuredLogResult.Failure<StructuredLogEvent>(
                "LOG_STEP_INVALID",
                "The optional step is malformed.");
        }

        if (!Enum.IsDefined(severity))
        {
            return StructuredLogResult.Failure<StructuredLogEvent>(
                "LOG_SEVERITY_INVALID",
                "The log severity is not supported.");
        }

        if (string.IsNullOrWhiteSpace(message) || message.Length > 16_384 || message.Any(char.IsControl))
        {
            return StructuredLogResult.Failure<StructuredLogEvent>(
                "LOG_MESSAGE_INVALID",
                "The log message is missing, too long, or contains control characters.");
        }

        StructuredLogResult<ReadOnlyCollection<StructuredLogProperty>> propertyResult =
            StructuredLogValueValidator.SnapshotProperties(properties);
        return propertyResult.IsSuccess
            ? StructuredLogResult.Success(
                new StructuredLogEvent(
                    timestampUtc,
                    correlationId!,
                    component!,
                    step,
                    severity,
                    message,
                    propertyResult.Value!))
            : StructuredLogResult.Failure<StructuredLogEvent>(
                propertyResult.Error!.Code,
                propertyResult.Error.Message);
    }
}

/// <summary>Persistence-neutral writer for contained application and per-job logs.</summary>
public interface IStructuredLogWriter
{
    Task<StructuredLogResult> WriteApplicationAsync(
        StructuredLogEvent logEvent,
        CancellationToken cancellationToken = default);

    Task<StructuredLogResult> WriteJobAsync(
        BuildJobId jobId,
        StructuredLogEvent logEvent,
        CancellationToken cancellationToken = default);
}

/// <summary>Sanitized failure safe to display without exposing rejected log content.</summary>
public sealed record StructuredLogError(string Code, string Message);

/// <summary>Non-throwing result for an expected structured logging operation.</summary>
public sealed class StructuredLogResult
{
    private StructuredLogResult(StructuredLogError? error) => Error = error;

    public bool IsSuccess => Error is null;

    public StructuredLogError? Error { get; }

    public static StructuredLogResult Success() => new(null);

    public static StructuredLogResult Failure(string code, string message) =>
        new(new StructuredLogError(code, message));

    public static StructuredLogResult<T> Success<T>(T value) => new(value, null);

    public static StructuredLogResult<T> Failure<T>(string code, string message) =>
        new(default, new StructuredLogError(code, message));
}

/// <summary>Typed non-throwing result for structured logging validation.</summary>
public sealed class StructuredLogResult<T>
{
    internal StructuredLogResult(T? value, StructuredLogError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public StructuredLogError? Error { get; }

}
