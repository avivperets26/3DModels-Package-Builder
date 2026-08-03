using System.Collections.ObjectModel;

namespace PackageBuilder.Contracts.Processes;

/// <summary>Describes an expected process failure without exposing a rejected physical path or environment value.</summary>
public sealed class ProcessExecutionFailure(string code, string location, string diagnostic)
{
    public string Code { get; } = string.IsNullOrWhiteSpace(code)
        ? throw new ArgumentException("A failure code is required.", nameof(code))
        : code;

    public string Location { get; } = string.IsNullOrWhiteSpace(location)
        ? throw new ArgumentException("A safe failure location is required.", nameof(location))
        : location;

    public string Diagnostic { get; } = string.IsNullOrWhiteSpace(diagnostic)
        ? throw new ArgumentException("A safe diagnostic is required.", nameof(diagnostic))
        : diagnostic;
}

/// <summary>Represents either one completed process receipt or structured expected failures.</summary>
public sealed class ProcessExecutionOperationResult
{
    internal ProcessExecutionOperationResult(
        ExternalProcessReceipt? value,
        IEnumerable<ProcessExecutionFailure> failures)
    {
        Value = value;
        Failures = new ReadOnlyCollection<ProcessExecutionFailure>(failures.ToArray());
    }

    public bool IsSuccess => Value is not null;

    public ExternalProcessReceipt? Value { get; }

    public IReadOnlyList<ProcessExecutionFailure> Failures { get; }
}

/// <summary>Creates process results without throwing for expected validation, launch, or I/O failures.</summary>
public static class ProcessExecutionResult
{
    public static ProcessExecutionOperationResult Success(ExternalProcessReceipt value) =>
        new(value ?? throw new ArgumentNullException(nameof(value)), []);

    public static ProcessExecutionOperationResult Failed(params ProcessExecutionFailure[] failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return failures.Length == 0 || failures.Any(static failure => failure is null)
            ? throw new ArgumentException("At least one non-null failure is required.", nameof(failures))
            : new ProcessExecutionOperationResult(null, failures);
    }
}
