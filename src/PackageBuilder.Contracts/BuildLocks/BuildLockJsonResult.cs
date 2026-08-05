namespace PackageBuilder.Contracts.BuildLocks;

public enum BuildLockJsonError
{
    None = 0,
    NullValue,
    NullJson,
    EmptyJson,
    InputTooLarge,
    MalformedJson,
    RootMustBeObject,
    DuplicateProperty,
    SchemaViolation,
    DomainViolation,
}

public sealed record BuildLockJsonResult(BuildLock? Value, string? Json, BuildLockJsonError Error)
{
    public bool IsSuccessful => Error == BuildLockJsonError.None;

    internal static BuildLockJsonResult Success(BuildLock value, string json) =>
        new(value, json, BuildLockJsonError.None);

    internal static BuildLockJsonResult Failure(BuildLockJsonError error) => new(null, null, error);
}

public sealed record BuildLockSchemaValidationResult(bool IsValid, string? Details);
