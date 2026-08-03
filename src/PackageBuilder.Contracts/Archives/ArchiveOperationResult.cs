using System.Collections.ObjectModel;

namespace PackageBuilder.Contracts.Archives;

/// <summary>Represents either one immutable archive result value or one or more structured failures.</summary>
public sealed class ArchiveOperationResult<T>
    where T : class
{
    internal ArchiveOperationResult(T? value, IEnumerable<ArchiveFailure> failures)
    {
        Value = value;
        Failures = new ReadOnlyCollection<ArchiveFailure>(failures.ToArray());
    }

    public bool IsSuccess => Value is not null;

    public T? Value { get; }

    public IReadOnlyList<ArchiveFailure> Failures { get; }

}

/// <summary>Creates strongly typed archive operation results without static members on generic types.</summary>
public static class ArchiveOperationResult
{
    public static ArchiveOperationResult<T> Success<T>(T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ArchiveOperationResult<T>(value, []);
    }

    public static ArchiveOperationResult<T> Failed<T>(params ArchiveFailure[] failures)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(failures);
        return failures.Length == 0 || failures.Any(static failure => failure is null)
            ? throw new ArgumentException("At least one non-null failure is required.", nameof(failures))
            : new ArchiveOperationResult<T>(null, failures);
    }
}
