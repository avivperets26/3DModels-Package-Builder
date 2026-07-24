using System.Collections.ObjectModel;

namespace PackageBuilder.Domain.Rigging;

/// <summary>Represents one complete deterministic pose for a validated skeleton.</summary>
public sealed class PoseDefinition : IEquatable<PoseDefinition>
{
    private PoseDefinition(
        SkeletonDefinition skeleton,
        ReadOnlyCollection<BonePose> bones)
    {
        Skeleton = skeleton;
        Bones = bones;
    }

    public SkeletonDefinition Skeleton { get; }

    /// <summary>Gets one pose per skeleton bone in the skeleton's deterministic order.</summary>
    public IReadOnlyList<BonePose> Bones { get; }

    /// <summary>
    /// Creates a complete pose. Partial reference/rest poses are rejected because silently
    /// inventing missing transforms would make the canonical rig ambiguous.
    /// </summary>
    public static PoseDefinitionValidationResult Create(
        SkeletonDefinition? skeleton,
        IEnumerable<BonePose?>? bones)
    {
        if (skeleton is null)
        {
            return PoseDefinitionValidationResult.Failure(
                PoseDefinitionValidationError.NullSkeleton);
        }

        if (bones is null)
        {
            return PoseDefinitionValidationResult.Failure(
                PoseDefinitionValidationError.NullBones);
        }

        var byIdentity = new Dictionary<string, BonePose>(StringComparer.Ordinal);
        foreach (BonePose? bone in bones)
        {
            if (bone is null)
            {
                return PoseDefinitionValidationResult.Failure(
                    PoseDefinitionValidationError.NullBone);
            }

            if (!skeleton.ContainsBone(bone.BoneIdentity))
            {
                return PoseDefinitionValidationResult.Failure(
                    PoseDefinitionValidationError.UnknownBone);
            }

            if (!byIdentity.TryAdd(bone.BoneIdentity, bone))
            {
                return PoseDefinitionValidationResult.Failure(
                    PoseDefinitionValidationError.DuplicateBone);
            }
        }

        if (byIdentity.Count != skeleton.Bones.Count)
        {
            return PoseDefinitionValidationResult.Failure(
                PoseDefinitionValidationError.MissingBone);
        }

        BonePose[] ordered =
        [
            .. skeleton.Bones.Select(bone => byIdentity[bone.Identity]),
        ];
        return PoseDefinitionValidationResult.Success(
            new PoseDefinition(skeleton, Array.AsReadOnly(ordered)));
    }

    /// <inheritdoc />
    public bool Equals(PoseDefinition? other) =>
        other is not null &&
        Skeleton.Equals(other.Skeleton) &&
        Bones.SequenceEqual(other.Bones);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PoseDefinition other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = StableRigHash.Create();
        foreach (BonePose bone in Bones)
        {
            hash = bone.Transform.AddToHash(hash.Add(bone.BoneIdentity));
        }

        return hash.ToHashCode();
    }
}
