namespace PackageBuilder.Domain.Rigging;

/// <summary>Identifies why a skeleton hierarchy could not be created.</summary>
public enum SkeletonDefinitionValidationError
{
    None = 0,
    NullBones,
    NullBone,
    DuplicateBone,
    SelfParenting,
    OrphanedParent,
    MissingRoot,
    MultipleRoots,
    HierarchyCycle,
}

/// <summary>Represents expected success or failure when creating a skeleton.</summary>
public sealed class SkeletonDefinitionValidationResult
{
    private SkeletonDefinitionValidationResult(
        bool isValid,
        SkeletonDefinition? value,
        SkeletonDefinitionValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public SkeletonDefinition? Value { get; }

    public SkeletonDefinitionValidationError Error { get; }

    internal static SkeletonDefinitionValidationResult Success(SkeletonDefinition value) =>
        new(true, value, SkeletonDefinitionValidationError.None);

    internal static SkeletonDefinitionValidationResult Failure(
        SkeletonDefinitionValidationError error) =>
        new(false, null, error);
}
