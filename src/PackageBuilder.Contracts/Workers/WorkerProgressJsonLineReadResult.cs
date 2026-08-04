namespace PackageBuilder.Contracts.Workers;

/// <summary>
/// Describes the outcome of parsing one physical JSON Lines record from worker output.
/// </summary>
public sealed class WorkerProgressJsonLineReadResult
{
    private WorkerProgressJsonLineReadResult(
        long lineNumber,
        WorkerProgressEvent? value,
        WorkerJsonError error,
        string? details)
    {
        LineNumber = lineNumber;
        Value = value;
        Error = error;
        Details = details;
    }

    /// <summary>
    /// Gets the one-based physical line number in the worker stream.
    /// </summary>
    public long LineNumber { get; }

    /// <summary>
    /// Gets a value indicating whether the line contains a valid worker event.
    /// </summary>
    public bool IsSuccessful => Error == WorkerJsonError.None;

    /// <summary>
    /// Gets the parsed event when <see cref="IsSuccessful"/> is <see langword="true"/>.
    /// </summary>
    public WorkerProgressEvent? Value { get; }

    /// <summary>
    /// Gets the stable parse error without retaining or repeating untrusted line content.
    /// </summary>
    public WorkerJsonError Error { get; }

    /// <summary>
    /// Gets optional sanitized contract details supplied by the existing event parser.
    /// </summary>
    public string? Details { get; }

    internal static WorkerProgressJsonLineReadResult Success(
        long lineNumber,
        WorkerProgressEvent value) =>
        new(lineNumber, value, WorkerJsonError.None, null);

    internal static WorkerProgressJsonLineReadResult Failure(
        long lineNumber,
        WorkerJsonError error,
        string? details = null) =>
        new(lineNumber, null, error, details);
}
