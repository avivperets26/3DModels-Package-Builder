namespace PackageBuilder.Domain.Rigging;

/// <summary>Identifies why a rig definition could not be created.</summary>
public enum RigDefinitionValidationError
{
    None = 0,
    NullRigType,
    NullSkeleton,
    NullReferencePose,
    ReferencePoseSkeletonMismatch,
}

/// <summary>Represents expected success or failure when creating a rig definition.</summary>
public sealed class RigDefinitionValidationResult
{
    private RigDefinitionValidationResult(
        bool isValid,
        RigDefinition? value,
        RigDefinitionValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public RigDefinition? Value { get; }

    public RigDefinitionValidationError Error { get; }

    internal static RigDefinitionValidationResult Success(RigDefinition value) =>
        new(true, value, RigDefinitionValidationError.None);

    internal static RigDefinitionValidationResult Failure(
        RigDefinitionValidationError error) =>
        new(false, null, error);
}
