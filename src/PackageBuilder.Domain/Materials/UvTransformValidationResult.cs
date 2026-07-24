namespace PackageBuilder.Domain.Materials;

/// <summary>Identifies why a renderer-independent UV transform could not be created.</summary>
public enum UvTransformValidationError
{
    /// <summary>The UV transform is valid.</summary>
    None = 0,

    /// <summary>The horizontal UV scale was not finite.</summary>
    ScaleUNotFinite,

    /// <summary>The vertical UV scale was not finite.</summary>
    ScaleVNotFinite,

    /// <summary>The horizontal UV offset was not finite.</summary>
    OffsetUNotFinite,

    /// <summary>The vertical UV offset was not finite.</summary>
    OffsetVNotFinite,
}

/// <summary>Represents expected success or failure when creating a UV transform.</summary>
public sealed class UvTransformValidationResult
{
    private UvTransformValidationResult(
        bool isValid,
        UvTransform? value,
        UvTransformValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    /// <summary>Gets a value indicating whether validation succeeded.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the immutable UV transform, or null on failure.</summary>
    public UvTransform? Value { get; }

    /// <summary>Gets the expected-input rejection reason.</summary>
    public UvTransformValidationError Error { get; }

    internal static UvTransformValidationResult Success(UvTransform value) =>
        new(true, value, UvTransformValidationError.None);

    internal static UvTransformValidationResult Failure(UvTransformValidationError error) =>
        new(false, null, error);
}
