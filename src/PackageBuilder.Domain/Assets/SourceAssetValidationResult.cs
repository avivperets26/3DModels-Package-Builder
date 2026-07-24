namespace PackageBuilder.Domain.Assets;

/// <summary>Identifies why a source asset could not be created.</summary>
public enum SourceAssetValidationError
{
    /// <summary>The source asset is valid.</summary>
    None = 0,

    /// <summary>The source kind was null.</summary>
    NullKind,

    /// <summary>The logical reference was null.</summary>
    NullLogicalReference,

    /// <summary>The logical reference was empty.</summary>
    EmptyLogicalReference,

    /// <summary>The logical reference contained only whitespace.</summary>
    WhitespaceOnlyLogicalReference,

    /// <summary>The logical reference used a rooted path form.</summary>
    RootedLogicalReference,

    /// <summary>The logical reference used a drive-relative path form.</summary>
    DriveRelativeLogicalReference,

    /// <summary>The logical reference used a URI-like form.</summary>
    UriLikeLogicalReference,

    /// <summary>The logical reference used a backslash instead of its canonical separator.</summary>
    InvalidSeparator,

    /// <summary>The logical reference contained an empty segment.</summary>
    EmptySegment,

    /// <summary>The logical reference contained a current- or parent-directory segment.</summary>
    TraversalSegment,

    /// <summary>A logical-reference segment began or ended with whitespace.</summary>
    LeadingOrTrailingWhitespace,

    /// <summary>The logical reference contained a control character.</summary>
    ControlCharacter,

    /// <summary>The logical-reference extension did not match the explicit source kind.</summary>
    ExtensionKindMismatch,

    /// <summary>An explicitly supplied original filename was empty.</summary>
    EmptyOriginalFileName,

    /// <summary>The original filename was not one valid logical-reference segment.</summary>
    InvalidOriginalFileName,

    /// <summary>The original-filename extension did not match the explicit source kind.</summary>
    OriginalFileNameExtensionKindMismatch,
}

/// <summary>Represents expected success or failure when creating a source asset.</summary>
public sealed class SourceAssetValidationResult
{
    private SourceAssetValidationResult(
        bool isValid,
        SourceAsset? value,
        SourceAssetValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    /// <summary>Gets a value indicating whether validation succeeded.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the immutable source asset, or <see langword="null"/> on failure.</summary>
    public SourceAsset? Value { get; }

    /// <summary>Gets the expected-input rejection reason.</summary>
    public SourceAssetValidationError Error { get; }

    internal static SourceAssetValidationResult Success(SourceAsset value) =>
        new(true, value, SourceAssetValidationError.None);

    internal static SourceAssetValidationResult Failure(SourceAssetValidationError error) =>
        new(false, null, error);
}
