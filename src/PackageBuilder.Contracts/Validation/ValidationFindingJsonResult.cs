using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contracts.Validation;

/// <summary>Identifies expected PB-0109 JSON contract failures.</summary>
public enum ValidationFindingJsonError
{
    None = 0,
    NullFinding,
    NullJson,
    EmptyJson,
    MalformedJson,
    RootMustBeObject,
    UnknownProperty,
    DuplicateProperty,
    MissingCode,
    InvalidCode,
    MissingSeverity,
    InvalidSeverity,
    MissingExplanation,
    InvalidExplanation,
    MissingSource,
    InvalidSource,
    InvalidRelatedArtifactId,
    InvalidSuggestedAction,
    MissingBlocksRelease,
    InvalidBlocksRelease,
}

/// <summary>Represents deterministic serialization without using exceptions for expected input.</summary>
public sealed class ValidationFindingSerializationResult
{
    private ValidationFindingSerializationResult(
        bool isSuccessful,
        string? json,
        ValidationFindingJsonError error)
    {
        IsSuccessful = isSuccessful;
        Json = json;
        Error = error;
    }

    public bool IsSuccessful { get; }

    public string? Json { get; }

    public ValidationFindingJsonError Error { get; }

    internal static ValidationFindingSerializationResult Success(string json) =>
        new(true, json, ValidationFindingJsonError.None);

    internal static ValidationFindingSerializationResult Failure(
        ValidationFindingJsonError error) =>
        new(false, null, error);
}

/// <summary>
/// Represents non-throwing deserialization, retaining the precise Domain validation reason where
/// one exists.
/// </summary>
public sealed class ValidationFindingDeserializationResult
{
    private ValidationFindingDeserializationResult(
        bool isSuccessful,
        PackageBuilder.Domain.Validation.ValidationFinding? value,
        ValidationFindingJsonError error,
        ValidationFindingError domainError,
        BuildModelValidationError artifactIdError)
    {
        IsSuccessful = isSuccessful;
        Value = value;
        Error = error;
        DomainError = domainError;
        ArtifactIdError = artifactIdError;
    }

    public bool IsSuccessful { get; }

    public PackageBuilder.Domain.Validation.ValidationFinding? Value { get; }

    public ValidationFindingJsonError Error { get; }

    public ValidationFindingError DomainError { get; }

    public BuildModelValidationError ArtifactIdError { get; }

    internal static ValidationFindingDeserializationResult Success(
        PackageBuilder.Domain.Validation.ValidationFinding value) =>
        new(
            true,
            value,
            ValidationFindingJsonError.None,
            ValidationFindingError.None,
            BuildModelValidationError.None);

    internal static ValidationFindingDeserializationResult Failure(
        ValidationFindingJsonError error,
        ValidationFindingError domainError = ValidationFindingError.None,
        BuildModelValidationError artifactIdError = BuildModelValidationError.None) =>
        new(false, null, error, domainError, artifactIdError);
}
