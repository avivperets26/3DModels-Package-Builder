namespace PackageBuilder.Domain.Rigging;

/// <summary>Represents one immutable bone identity and optional parent reference.</summary>
public sealed class BoneDefinition : IEquatable<BoneDefinition>
{
    private BoneDefinition(string identity, string? parentIdentity)
    {
        Identity = identity;
        ParentIdentity = parentIdentity;
    }

    /// <summary>Gets the exact ordinal bone identity.</summary>
    public string Identity { get; }

    /// <summary>Gets the exact ordinal parent identity, or null for a root candidate.</summary>
    public string? ParentIdentity { get; }

    /// <summary>
    /// Validates a bone. Identities preserve Unicode and casing exactly; surrounding whitespace,
    /// control characters, and whitespace-only identities are rejected rather than normalized.
    /// Hierarchy relationships are validated by <see cref="SkeletonDefinition"/>.
    /// </summary>
    public static BoneDefinitionValidationResult Create(
        string? identity,
        string? parentIdentity)
    {
        BoneIdentityValidationError identityError = ValidateIdentity(identity, allowNull: false);
        if (identityError != BoneIdentityValidationError.None)
        {
            return BoneDefinitionValidationResult.Failure(
                identityError switch
                {
                    BoneIdentityValidationError.Null => BoneDefinitionValidationError.NullIdentity,
                    BoneIdentityValidationError.Empty => BoneDefinitionValidationError.EmptyIdentity,
                    BoneIdentityValidationError.WhitespaceOnly =>
                        BoneDefinitionValidationError.WhitespaceOnlyIdentity,
                    BoneIdentityValidationError.EdgeWhitespace =>
                        BoneDefinitionValidationError.IdentityEdgeWhitespace,
                    _ => BoneDefinitionValidationError.IdentityContainsControlCharacter,
                });
        }

        BoneIdentityValidationError parentError = ValidateIdentity(parentIdentity, allowNull: true);
        return parentError != BoneIdentityValidationError.None
            ? BoneDefinitionValidationResult.Failure(
                parentError switch
                {
                    BoneIdentityValidationError.Empty =>
                        BoneDefinitionValidationError.EmptyParentIdentity,
                    BoneIdentityValidationError.WhitespaceOnly =>
                        BoneDefinitionValidationError.WhitespaceOnlyParentIdentity,
                    BoneIdentityValidationError.EdgeWhitespace =>
                        BoneDefinitionValidationError.ParentIdentityEdgeWhitespace,
                    _ => BoneDefinitionValidationError.ParentIdentityContainsControlCharacter,
                })
            : BoneDefinitionValidationResult.Success(
            new BoneDefinition(identity!, parentIdentity));
    }

    /// <inheritdoc />
    public bool Equals(BoneDefinition? other) =>
        other is not null &&
        string.Equals(Identity, other.Identity, StringComparison.Ordinal) &&
        string.Equals(ParentIdentity, other.ParentIdentity, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BoneDefinition other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StableRigHash.Create()
            .Add(Identity)
            .Add(ParentIdentity ?? string.Empty)
            .ToHashCode();

    private static BoneIdentityValidationError ValidateIdentity(string? value, bool allowNull)
    {
        return value is null
            ? allowNull ? BoneIdentityValidationError.None : BoneIdentityValidationError.Null
            : value.Length == 0
            ? BoneIdentityValidationError.Empty
            : string.IsNullOrWhiteSpace(value)
            ? BoneIdentityValidationError.WhitespaceOnly
            : char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])
            ? BoneIdentityValidationError.EdgeWhitespace
            : value.Any(char.IsControl)
            ? BoneIdentityValidationError.ControlCharacter
            : BoneIdentityValidationError.None;
    }

    private enum BoneIdentityValidationError
    {
        None,
        Null,
        Empty,
        WhitespaceOnly,
        EdgeWhitespace,
        ControlCharacter,
    }
}
