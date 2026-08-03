using System.Collections.ObjectModel;

namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Describes one expected hash failure without exposing an untrusted physical path.</summary>
public sealed class ArtifactHashFailure(string code, string location, string diagnostic)
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

/// <summary>Represents either one successful hash value or structured expected failures.</summary>
public sealed class ArtifactHashOperationResult<T>
    where T : class
{
    internal ArtifactHashOperationResult(T? value, IEnumerable<ArtifactHashFailure> failures)
    {
        Value = value;
        Failures = new ReadOnlyCollection<ArtifactHashFailure>(failures.ToArray());
    }

    public bool IsSuccess => Value is not null;

    public T? Value { get; }

    public IReadOnlyList<ArtifactHashFailure> Failures { get; }
}

/// <summary>Creates artifact-hash results without throwing for expected filesystem failures.</summary>
public static class ArtifactHashOperationResult
{
    public static ArtifactHashOperationResult<T> Success<T>(T value)
        where T : class => new(value ?? throw new ArgumentNullException(nameof(value)), []);

    public static ArtifactHashOperationResult<T> Failed<T>(params ArtifactHashFailure[] failures)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(failures);
        return failures.Length == 0 || failures.Any(static failure => failure is null)
            ? throw new ArgumentException("At least one non-null failure is required.", nameof(failures))
            : new ArtifactHashOperationResult<T>(null, failures);
    }
}
