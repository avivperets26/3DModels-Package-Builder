namespace PackageBuilder.Domain.Textures;

/// <summary>Identifies why a texture assignment could not be created.</summary>
public enum TextureAssignmentValidationError
{
    /// <summary>The assignment is valid.</summary>
    None = 0,

    /// <summary>The source asset was null.</summary>
    NullSourceAsset,

    /// <summary>The source asset was not an image.</summary>
    SourceAssetIsNotImage,

    /// <summary>The canonical texture role was null.</summary>
    NullRole,

    /// <summary>The colour space was null.</summary>
    NullColourSpace,

    /// <summary>The colour space was incompatible with the role.</summary>
    IncompatibleColourSpace,

    /// <summary>A Normal assignment did not declare a normal convention.</summary>
    MissingNormalConvention,

    /// <summary>A non-Normal assignment declared a normal convention.</summary>
    NormalConventionNotApplicable,
}

/// <summary>Represents expected success or failure when creating a texture assignment.</summary>
public sealed class TextureAssignmentValidationResult
{
    private TextureAssignmentValidationResult(
        bool isValid,
        TextureAssignment? value,
        TextureAssignmentValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    /// <summary>Gets a value indicating whether validation succeeded.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the immutable assignment, or <see langword="null"/> on failure.</summary>
    public TextureAssignment? Value { get; }

    /// <summary>Gets the expected-input rejection reason.</summary>
    public TextureAssignmentValidationError Error { get; }

    internal static TextureAssignmentValidationResult Success(TextureAssignment value) =>
        new(true, value, TextureAssignmentValidationError.None);

    internal static TextureAssignmentValidationResult Failure(
        TextureAssignmentValidationError error) =>
        new(false, null, error);
}
