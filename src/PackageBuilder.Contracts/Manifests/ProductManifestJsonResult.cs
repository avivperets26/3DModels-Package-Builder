using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contracts.Manifests;

public enum ProductManifestJsonError
{
    None = 0,
    NullManifest,
    NullJson,
    EmptyJson,
    InputTooLarge,
    MalformedJson,
    RootMustBeObject,
    DuplicateProperty,
    SchemaViolation,
    DomainViolation,
}

public sealed class ProductManifestSerializationResult
{
    private ProductManifestSerializationResult(
        bool isSuccessful,
        string? json,
        ProductManifestJsonError error)
    {
        IsSuccessful = isSuccessful;
        Json = json;
        Error = error;
    }

    public bool IsSuccessful { get; }

    public string? Json { get; }

    public ProductManifestJsonError Error { get; }

    internal static ProductManifestSerializationResult Success(string json) =>
        new(true, json, ProductManifestJsonError.None);

    internal static ProductManifestSerializationResult Failure(ProductManifestJsonError error) =>
        new(false, null, error);
}

public sealed class ProductManifestDeserializationResult
{
    private ProductManifestDeserializationResult(
        bool isSuccessful,
        ProductManifest? value,
        ProductManifestJsonError error,
        IReadOnlyList<ValidationFinding> findings)
    {
        IsSuccessful = isSuccessful;
        Value = value;
        Error = error;
        Findings = findings;
    }

    public bool IsSuccessful { get; }

    public ProductManifest? Value { get; }

    public ProductManifestJsonError Error { get; }

    public IReadOnlyList<ValidationFinding> Findings { get; }

    internal static ProductManifestDeserializationResult Success(ProductManifest value) =>
        new(true, value, ProductManifestJsonError.None, []);

    internal static ProductManifestDeserializationResult Failure(
        ProductManifestJsonError error,
        IReadOnlyList<ValidationFinding>? findings = null) =>
        new(false, null, error, findings ?? []);
}

public sealed class ProductManifestSchemaValidationResult
{
    internal ProductManifestSchemaValidationResult(bool isValid, string? details)
    {
        IsValid = isValid;
        Details = details;
    }

    public bool IsValid { get; }

    public string? Details { get; }
}
