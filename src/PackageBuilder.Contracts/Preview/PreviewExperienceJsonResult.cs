using PackageBuilder.Domain.Preview;

namespace PackageBuilder.Contracts.Preview;

/// <summary>Identifies fail-closed preview contract JSON errors.</summary>
public enum PreviewExperienceJsonError
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

/// <summary>Returns a canonical preview contract or an explicit JSON rejection.</summary>
public sealed record PreviewExperienceJsonResult(
    PreviewExperienceContract? Value,
    string? Json,
    PreviewExperienceJsonError Error)
{
    public bool IsSuccessful => Error == PreviewExperienceJsonError.None;

    internal static PreviewExperienceJsonResult Success(
        PreviewExperienceContract value,
        string json) => new(value, json, PreviewExperienceJsonError.None);

    internal static PreviewExperienceJsonResult Failure(PreviewExperienceJsonError error) =>
        new(null, null, error);
}

/// <summary>Reports whether the embedded schema is the pinned approved definition.</summary>
public sealed record PreviewExperienceSchemaValidationResult(bool IsValid, string? Details);
