using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Contracts.Migrations;

public enum MigrationStatus
{
    CurrentDocument = 0,
    MigrationAvailable,
    SuccessfullyMigrated,
    InvalidDocument,
    UnsupportedOlderVersion,
    UnsupportedNewerVersion,
    MissingMigrationStep,
    AmbiguousMigrationPath,
    MigrationFailure,
}

public sealed class MigrationFinalDocument
{
    private MigrationFinalDocument(
        string canonicalJson,
        ProductManifest? productManifest,
        PublisherProfile? publisherProfile,
        MarketplaceProfile? marketplaceProfile)
    {
        CanonicalJson = canonicalJson;
        ProductManifest = productManifest;
        PublisherProfile = publisherProfile;
        MarketplaceProfile = marketplaceProfile;
    }

    public string CanonicalJson { get; }

    public ProductManifest? ProductManifest { get; }

    public PublisherProfile? PublisherProfile { get; }

    public MarketplaceProfile? MarketplaceProfile { get; }

    public static MigrationFinalDocument Product(string canonicalJson, ProductManifest value) =>
        new(canonicalJson, value, null, null);

    public static MigrationFinalDocument Publisher(string canonicalJson, PublisherProfile value) =>
        new(canonicalJson, null, value, null);

    public static MigrationFinalDocument Marketplace(
        string canonicalJson,
        MarketplaceProfile value) =>
        new(canonicalJson, null, null, value);

    public static MigrationFinalDocument Representative(string canonicalJson) =>
        new(canonicalJson, null, null, null);
}

public sealed class MigrationFinalizationResult
{
    private MigrationFinalizationResult(
        bool isSuccessful,
        MigrationFinalDocument? document,
        string? diagnosticCode)
    {
        IsSuccessful = isSuccessful;
        Document = document;
        DiagnosticCode = diagnosticCode;
    }

    public bool IsSuccessful { get; }

    public MigrationFinalDocument? Document { get; }

    public string? DiagnosticCode { get; }

    public static MigrationFinalizationResult Success(MigrationFinalDocument document) =>
        new(true, document, null);

    public static MigrationFinalizationResult Failure(string diagnosticCode) =>
        new(false, null, diagnosticCode);
}

public sealed class MigrationResult
{
    internal MigrationResult(
        MigrationStatus status,
        MigrationDocumentFamily? family,
        SchemaVersion? sourceVersion,
        SchemaVersion? targetVersion,
        string? originalJson,
        MigrationFinalDocument? finalDocument,
        IEnumerable<MigrationChange>? changes,
        string diagnosticCode)
    {
        Status = status;
        Family = family;
        SourceVersion = sourceVersion;
        TargetVersion = targetVersion;
        OriginalJson = originalJson;
        FinalDocument = finalDocument;
        Changes = Array.AsReadOnly((changes ?? []).ToArray());
        DiagnosticCode = diagnosticCode;
    }

    public MigrationStatus Status { get; }

    public MigrationDocumentFamily? Family { get; }

    public SchemaVersion? SourceVersion { get; }

    public SchemaVersion? TargetVersion { get; }

    /// <summary>Exact immutable source evidence. It is never copied into diagnostics.</summary>
    public string? OriginalJson { get; }

    public MigrationFinalDocument? FinalDocument { get; }

    public IReadOnlyList<MigrationChange> Changes { get; }

    public string DiagnosticCode { get; }
}
