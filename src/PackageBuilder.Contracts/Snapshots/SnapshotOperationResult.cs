using System.Collections.ObjectModel;

namespace PackageBuilder.Contracts.Snapshots;

/// <summary>Represents either one immutable snapshot value or structured expected failures.</summary>
public sealed class SnapshotOperationResult<T>
    where T : class
{
    internal SnapshotOperationResult(T? value, IEnumerable<SnapshotFailure> failures)
    {
        Value = value;
        Failures = new ReadOnlyCollection<SnapshotFailure>(failures.ToArray());
    }

    public bool IsSuccess => Value is not null;

    public T? Value { get; }

    public IReadOnlyList<SnapshotFailure> Failures { get; }
}

/// <summary>Creates typed source-snapshot results without throwing for expected validation failures.</summary>
public static class SnapshotOperationResult
{
    public static SnapshotOperationResult<T> Success<T>(T value)
        where T : class => new(value ?? throw new ArgumentNullException(nameof(value)), []);

    public static SnapshotOperationResult<T> Failed<T>(params SnapshotFailure[] failures)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(failures);
        return failures.Length == 0 || failures.Any(static failure => failure is null)
            ? throw new ArgumentException("At least one non-null failure is required.", nameof(failures))
            : new SnapshotOperationResult<T>(null, failures);
    }
}
