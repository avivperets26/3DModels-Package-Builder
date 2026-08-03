using System.Collections.ObjectModel;

namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Describes one expected artifact-store validation or I/O failure.</summary>
public sealed record ArtifactStoreFailure(string Code, string Location, string Diagnostic);

/// <summary>Represents either one artifact-store value or structured expected failures.</summary>
public sealed class ArtifactStoreOperationResult<T>
    where T : class
{
    internal ArtifactStoreOperationResult(T? value, IEnumerable<ArtifactStoreFailure> failures)
    {
        Value = value;
        Failures = new ReadOnlyCollection<ArtifactStoreFailure>(failures.ToArray());
    }

    public bool IsSuccess => Value is not null;

    public T? Value { get; }

    public IReadOnlyList<ArtifactStoreFailure> Failures { get; }
}

/// <summary>Creates artifact-store results without throwing for expected failures.</summary>
public static class ArtifactStoreOperationResult
{
    public static ArtifactStoreOperationResult<T> Success<T>(T value)
        where T : class => new(value ?? throw new ArgumentNullException(nameof(value)), []);

    public static ArtifactStoreOperationResult<T> Failed<T>(params ArtifactStoreFailure[] failures)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(failures);
        return failures.Length == 0 || failures.Any(static failure => failure is null)
            ? throw new ArgumentException("At least one non-null failure is required.", nameof(failures))
            : new ArtifactStoreOperationResult<T>(null, failures);
    }
}
