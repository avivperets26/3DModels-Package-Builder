namespace PackageBuilder.Contracts.Profiles;

/// <summary>Identifies a strict profile JSON contract failure.</summary>
public enum ProfileJsonError
{
    None = 0,
    NullProfile,
    NullJson,
    EmptyJson,
    InputTooLarge,
    MalformedJson,
    RootMustBeObject,
    DuplicateProperty,
    SchemaViolation,
    DomainViolation,
}

/// <summary>Represents deterministic profile serialization.</summary>
public sealed class ProfileJsonSerializationResult
{
    private ProfileJsonSerializationResult(bool isSuccessful, string? json, ProfileJsonError error)
    {
        IsSuccessful = isSuccessful;
        Json = json;
        Error = error;
    }

    public bool IsSuccessful { get; }

    public string? Json { get; }

    public ProfileJsonError Error { get; }

    internal static ProfileJsonSerializationResult Success(string json) =>
        new(true, json, ProfileJsonError.None);

    internal static ProfileJsonSerializationResult Failure(ProfileJsonError error) =>
        new(false, null, error);
}

/// <summary>Represents strict profile deserialization into a Domain value.</summary>
public sealed class ProfileJsonDeserializationResult<T>
    where T : class
{
    private ProfileJsonDeserializationResult(
        bool isSuccessful,
        T? value,
        ProfileJsonError error,
        string? details)
    {
        IsSuccessful = isSuccessful;
        Value = value;
        Error = error;
        Details = details;
    }

    public bool IsSuccessful { get; }

    public T? Value { get; }

    public ProfileJsonError Error { get; }

    public string? Details { get; }

    internal static ProfileJsonDeserializationResult<T> Success(T value) =>
        new(true, value, ProfileJsonError.None, null);

    internal static ProfileJsonDeserializationResult<T> Failure(
        ProfileJsonError error,
        string? details = null) =>
        new(false, null, error, details);
}

/// <summary>Represents schema-definition or instance validation.</summary>
public sealed class ProfileJsonSchemaValidationResult
{
    internal ProfileJsonSchemaValidationResult(bool isValid, string? details)
    {
        IsValid = isValid;
        Details = details;
    }

    public bool IsValid { get; }

    public string? Details { get; }
}
