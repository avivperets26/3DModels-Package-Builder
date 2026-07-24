namespace PackageBuilder.Domain.Rigging;

/// <summary>Identifies why a bone pose could not be created.</summary>
public enum BonePoseValidationError
{
    None = 0,
    InvalidBoneIdentity,
    NullTransform,
}

/// <summary>Represents expected success or failure when creating a bone pose.</summary>
public sealed class BonePoseValidationResult
{
    private BonePoseValidationResult(
        bool isValid,
        BonePose? value,
        BonePoseValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public BonePose? Value { get; }

    public BonePoseValidationError Error { get; }

    internal static BonePoseValidationResult Success(BonePose value) =>
        new(true, value, BonePoseValidationError.None);

    internal static BonePoseValidationResult Failure(BonePoseValidationError error) =>
        new(false, null, error);
}
