namespace PackageBuilder.Domain.Rigging;

/// <summary>Associates an exact ordinal bone identity with one immutable local transform.</summary>
public sealed class BonePose : IEquatable<BonePose>
{
    private BonePose(string boneIdentity, RigTransform transform)
    {
        BoneIdentity = boneIdentity;
        Transform = transform;
    }

    public string BoneIdentity { get; }

    public RigTransform Transform { get; }

    /// <summary>Creates a pose entry from an already validated bone identity and transform.</summary>
    public static BonePoseValidationResult Create(
        string? boneIdentity,
        RigTransform? transform)
    {
        BoneDefinitionValidationResult identityResult =
            BoneDefinition.Create(boneIdentity, parentIdentity: null);
        return !identityResult.IsValid
            ? BonePoseValidationResult.Failure(
                BonePoseValidationError.InvalidBoneIdentity)
            : transform is null
            ? BonePoseValidationResult.Failure(BonePoseValidationError.NullTransform)
            : BonePoseValidationResult.Success(new BonePose(boneIdentity!, transform));
    }

    /// <inheritdoc />
    public bool Equals(BonePose? other) =>
        other is not null &&
        string.Equals(BoneIdentity, other.BoneIdentity, StringComparison.Ordinal) &&
        Transform.Equals(other.Transform);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BonePose other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        Transform.AddToHash(StableRigHash.Create().Add(BoneIdentity)).ToHashCode();
}
