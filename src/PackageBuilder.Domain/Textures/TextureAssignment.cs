using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Textures;

/// <summary>
/// Associates one image source with a canonical renderer-independent texture interpretation.
/// </summary>
public sealed class TextureAssignment : IEquatable<TextureAssignment>
{
    private TextureAssignment(
        SourceAsset sourceAsset,
        TextureRole role,
        ColourSpace colourSpace,
        NormalConvention? normalConvention)
    {
        SourceAsset = sourceAsset;
        Role = role;
        ColourSpace = colourSpace;
        NormalConvention = normalConvention;
    }

    /// <summary>Gets the assigned image source.</summary>
    public SourceAsset SourceAsset { get; }

    /// <summary>Gets the canonical source texture role.</summary>
    public TextureRole Role { get; }

    /// <summary>Gets the validated role-compatible colour space.</summary>
    public ColourSpace ColourSpace { get; }

    /// <summary>Gets the normal orientation for a Normal assignment; otherwise null.</summary>
    public NormalConvention? NormalConvention { get; }

    /// <summary>Validates and creates a renderer-independent texture assignment.</summary>
    public static TextureAssignmentValidationResult Create(
        SourceAsset? sourceAsset,
        TextureRole? role,
        ColourSpace? colourSpace,
        NormalConvention? normalConvention = null)
    {
        return sourceAsset is null
            ? TextureAssignmentValidationResult.Failure(
                TextureAssignmentValidationError.NullSourceAsset)
            : !sourceAsset.Kind.Equals(SourceAssetKind.Image)
            ? TextureAssignmentValidationResult.Failure(
                TextureAssignmentValidationError.SourceAssetIsNotImage)
            : role is null
            ? TextureAssignmentValidationResult.Failure(
                TextureAssignmentValidationError.NullRole)
            : colourSpace is null
            ? TextureAssignmentValidationResult.Failure(
                TextureAssignmentValidationError.NullColourSpace)
            : !role.RequiredColourSpace.Equals(colourSpace)
            ? TextureAssignmentValidationResult.Failure(
                TextureAssignmentValidationError.IncompatibleColourSpace)
            : role.IsNormalMapData && normalConvention is null
            ? TextureAssignmentValidationResult.Failure(
                TextureAssignmentValidationError.MissingNormalConvention)
            : !role.IsNormalMapData && normalConvention is not null
            ? TextureAssignmentValidationResult.Failure(
                TextureAssignmentValidationError.NormalConventionNotApplicable)
            : TextureAssignmentValidationResult.Success(
                new TextureAssignment(sourceAsset, role, colourSpace, normalConvention));
    }

    /// <inheritdoc />
    public bool Equals(TextureAssignment? other) =>
        other is not null &&
        SourceAsset.Equals(other.SourceAsset) &&
        Role.Equals(other.Role) &&
        ColourSpace.Equals(other.ColourSpace) &&
        Equals(NormalConvention, other.NormalConvention);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TextureAssignment other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        string hashInput = string.Concat(
            SourceAsset.Kind.CanonicalIdentifier,
            "\u001f",
            SourceAsset.LogicalReference,
            "\u001f",
            SourceAsset.OriginalFileName ?? "\u001e",
            "\u001f",
            Role.CanonicalIdentifier,
            "\u001f",
            ColourSpace.CanonicalIdentifier,
            "\u001f",
            NormalConvention?.CanonicalIdentifier ?? "\u001e");
        return CanonicalIdentifierParser.GetStableOrdinalHashCode(hashInput);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Role.CanonicalIdentifier}:{SourceAsset.LogicalReference}";
}
