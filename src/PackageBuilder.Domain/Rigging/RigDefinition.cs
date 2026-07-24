namespace PackageBuilder.Domain.Rigging;

/// <summary>Represents an immutable rig type, skeleton, and complete reference/rest pose.</summary>
public sealed class RigDefinition : IEquatable<RigDefinition>
{
    private RigDefinition(
        RigType rigType,
        SkeletonDefinition skeleton,
        PoseDefinition referencePose)
    {
        RigType = rigType;
        Skeleton = skeleton;
        ReferencePose = referencePose;
    }

    public RigType RigType { get; }

    public SkeletonDefinition Skeleton { get; }

    public PoseDefinition ReferencePose { get; }

    /// <summary>
    /// Creates a rig. A skeleton never implies Humanoid; callers must select that approved type
    /// explicitly after the separate mapping validation owned by later engine tasks.
    /// </summary>
    public static RigDefinitionValidationResult Create(
        RigType? rigType,
        SkeletonDefinition? skeleton,
        PoseDefinition? referencePose)
    {
        return rigType is null
            ? RigDefinitionValidationResult.Failure(
                RigDefinitionValidationError.NullRigType)
            : skeleton is null
            ? RigDefinitionValidationResult.Failure(
                RigDefinitionValidationError.NullSkeleton)
            : referencePose is null
            ? RigDefinitionValidationResult.Failure(
                RigDefinitionValidationError.NullReferencePose)
            : !skeleton.Equals(referencePose.Skeleton)
            ? RigDefinitionValidationResult.Failure(
                RigDefinitionValidationError.ReferencePoseSkeletonMismatch)
            : RigDefinitionValidationResult.Success(
                new RigDefinition(rigType, skeleton, referencePose));
    }

    /// <inheritdoc />
    public bool Equals(RigDefinition? other) =>
        other is not null &&
        RigType.Equals(other.RigType) &&
        Skeleton.Equals(other.Skeleton) &&
        ReferencePose.Equals(other.ReferencePose);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RigDefinition other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StableRigHash.Create()
            .Add(RigType.CanonicalIdentifier)
            .Add(Skeleton.GetHashCode())
            .Add(ReferencePose.GetHashCode())
            .ToHashCode();
}
