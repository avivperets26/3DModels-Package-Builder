using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Assets;

/// <summary>
/// Represents immutable source metadata without reading, extracting, or modifying physical files.
/// </summary>
public sealed class SourceAsset : IEquatable<SourceAsset>
{
    private SourceAsset(
        SourceAssetKind kind,
        string logicalReference,
        string? originalFileName)
    {
        Kind = kind;
        LogicalReference = logicalReference;
        OriginalFileName = originalFileName;
    }

    /// <summary>Gets the declared source kind.</summary>
    public SourceAssetKind Kind { get; }

    /// <summary>Gets the validated source-relative reference using forward-slash separators.</summary>
    public string LogicalReference { get; }

    /// <summary>Gets the validated original filename when the caller supplied one.</summary>
    public string? OriginalFileName { get; }

    /// <summary>Validates and creates immutable source metadata without filesystem access.</summary>
    public static SourceAssetValidationResult Create(
        SourceAssetKind? kind,
        string? logicalReference,
        string? originalFileName = null)
    {
        if (kind is null)
        {
            return SourceAssetValidationResult.Failure(SourceAssetValidationError.NullKind);
        }

        SourceAssetValidationError referenceError =
            SourceReferenceValidator.Validate(logicalReference);
        if (referenceError != SourceAssetValidationError.None)
        {
            return SourceAssetValidationResult.Failure(referenceError);
        }

        if (!SourceReferenceValidator.HasCompatibleExtension(kind, logicalReference!))
        {
            return SourceAssetValidationResult.Failure(
                SourceAssetValidationError.ExtensionKindMismatch);
        }

        if (originalFileName is not null)
        {
            if (originalFileName.Length == 0)
            {
                return SourceAssetValidationResult.Failure(
                    SourceAssetValidationError.EmptyOriginalFileName);
            }

            SourceAssetValidationError originalNameError =
                SourceReferenceValidator.Validate(originalFileName);
            if (originalNameError != SourceAssetValidationError.None ||
                originalFileName.Contains('/', StringComparison.Ordinal))
            {
                return SourceAssetValidationResult.Failure(
                    SourceAssetValidationError.InvalidOriginalFileName);
            }

            if (!SourceReferenceValidator.HasCompatibleExtension(kind, originalFileName))
            {
                return SourceAssetValidationResult.Failure(
                    SourceAssetValidationError.OriginalFileNameExtensionKindMismatch);
            }
        }

        return SourceAssetValidationResult.Success(
            new SourceAsset(kind, logicalReference!, originalFileName));
    }

    /// <inheritdoc />
    public bool Equals(SourceAsset? other) =>
        other is not null &&
        Kind.Equals(other.Kind) &&
        string.Equals(LogicalReference, other.LogicalReference, StringComparison.Ordinal) &&
        string.Equals(OriginalFileName, other.OriginalFileName, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SourceAsset other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        string hashInput = string.Concat(
            Kind.CanonicalIdentifier,
            "\u001f",
            LogicalReference,
            "\u001f",
            OriginalFileName ?? "\u001e");
        return CanonicalIdentifierParser.GetStableOrdinalHashCode(hashInput);
    }

    /// <inheritdoc />
    public override string ToString() => LogicalReference;
}
