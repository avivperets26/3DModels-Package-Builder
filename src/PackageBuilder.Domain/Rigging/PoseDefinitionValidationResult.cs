namespace PackageBuilder.Domain.Rigging;

/// <summary>Identifies why a complete pose could not be created.</summary>
public enum PoseDefinitionValidationError
{
    None = 0,
    NullSkeleton,
    NullBones,
    NullBone,
    UnknownBone,
    DuplicateBone,
    MissingBone,
}

/// <summary>Represents expected success or failure when creating a complete pose.</summary>
public sealed class PoseDefinitionValidationResult
{
    private PoseDefinitionValidationResult(
        bool isValid,
        PoseDefinition? value,
        PoseDefinitionValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public PoseDefinition? Value { get; }

    public PoseDefinitionValidationError Error { get; }

    internal static PoseDefinitionValidationResult Success(PoseDefinition value) =>
        new(true, value, PoseDefinitionValidationError.None);

    internal static PoseDefinitionValidationResult Failure(
        PoseDefinitionValidationError error) =>
        new(false, null, error);
}
