namespace PackageBuilder.Contracts.Workers;

public enum WorkerJsonError
{
    None = 0,
    NullValue,
    NullJson,
    EmptyJson,
    InputTooLarge,
    LineTooLarge,
    MalformedJson,
    RootMustBeObject,
    DuplicateProperty,
    SchemaViolation,
    DomainViolation,
    SemanticViolation,
}

public sealed class WorkerJsonSerializationResult
{
    private WorkerJsonSerializationResult(bool isSuccessful, string? json, WorkerJsonError error)
    {
        IsSuccessful = isSuccessful;
        Json = json;
        Error = error;
    }

    public bool IsSuccessful { get; }
    public string? Json { get; }
    public WorkerJsonError Error { get; }

    internal static WorkerJsonSerializationResult Success(string json) =>
        new(true, json, WorkerJsonError.None);

    internal static WorkerJsonSerializationResult Failure(WorkerJsonError error) =>
        new(false, null, error);
}

public sealed class WorkerJsonDeserializationResult<T>
    where T : class
{
    private WorkerJsonDeserializationResult(
        bool isSuccessful,
        T? value,
        WorkerJsonError error,
        string? details)
    {
        IsSuccessful = isSuccessful;
        Value = value;
        Error = error;
        Details = details;
    }

    public bool IsSuccessful { get; }
    public T? Value { get; }
    public WorkerJsonError Error { get; }
    public string? Details { get; }

    internal static WorkerJsonDeserializationResult<T> Success(T value) =>
        new(true, value, WorkerJsonError.None, null);

    internal static WorkerJsonDeserializationResult<T> Failure(
        WorkerJsonError error,
        string? details = null) =>
        new(false, null, error, details);
}

public sealed class WorkerJsonSchemaValidationResult
{
    internal WorkerJsonSchemaValidationResult(bool isValid, string? details)
    {
        IsValid = isValid;
        Details = details;
    }

    public bool IsValid { get; }
    public string? Details { get; }
}
