namespace PackageBuilder.Contracts.Persistence;

/// <summary>Stable, sanitized repository failure safe to expose to logs and user interfaces.</summary>
public sealed record RepositoryOperationError(string Code, string Message);

/// <summary>Represents a non-throwing repository operation that does not return a value.</summary>
public sealed class RepositoryOperationResult
{
    private RepositoryOperationResult(RepositoryOperationError? error) => Error = error;

    public bool IsSuccess => Error is null;

    public RepositoryOperationError? Error { get; }

    public static RepositoryOperationResult Success() => new(null);

    public static RepositoryOperationResult Failure(string code, string message) =>
        new(new RepositoryOperationError(code, message));

    public static RepositoryOperationResult<T> Success<T>(T value) =>
        new(value, null);

    public static RepositoryOperationResult<T> Failure<T>(string code, string message) =>
        new(default, new RepositoryOperationError(code, message));
}

/// <summary>Represents a non-throwing repository operation that returns a typed value.</summary>
public sealed class RepositoryOperationResult<T>
{
    internal RepositoryOperationResult(T? value, RepositoryOperationError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public RepositoryOperationError? Error { get; }

}
