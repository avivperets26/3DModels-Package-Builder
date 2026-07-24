namespace PackageBuilder.Domain.Rigging;

/// <summary>Identifies why a renderer-independent rig transform could not be created.</summary>
public enum RigTransformValidationError
{
    None = 0,
    TranslationXNotFinite,
    TranslationYNotFinite,
    TranslationZNotFinite,
    RotationXNotFinite,
    RotationYNotFinite,
    RotationZNotFinite,
    RotationWNotFinite,
    ScaleXNotFinite,
    ScaleYNotFinite,
    ScaleZNotFinite,
    ZeroLengthRotation,
}

/// <summary>Represents expected success or failure when creating a rig transform.</summary>
public sealed class RigTransformValidationResult
{
    private RigTransformValidationResult(
        bool isValid,
        RigTransform? value,
        RigTransformValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public RigTransform? Value { get; }

    public RigTransformValidationError Error { get; }

    internal static RigTransformValidationResult Success(RigTransform value) =>
        new(true, value, RigTransformValidationError.None);

    internal static RigTransformValidationResult Failure(RigTransformValidationError error) =>
        new(false, null, error);
}
