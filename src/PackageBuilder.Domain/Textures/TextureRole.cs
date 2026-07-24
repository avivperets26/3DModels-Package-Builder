using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Textures;

/// <summary>Identifies one canonical renderer-independent source texture role.</summary>
public sealed class TextureRole : IEquatable<TextureRole>
{
    private TextureRole(
        string canonicalIdentifier,
        string displayName,
        ColourSpace requiredColourSpace,
        bool isNormalMapData = false)
    {
        CanonicalIdentifier = canonicalIdentifier;
        DisplayName = displayName;
        RequiredColourSpace = requiredColourSpace;
        IsNormalMapData = isNormalMapData;
    }

    /// <summary>Gets the canonical Albedo role.</summary>
    public static TextureRole Albedo { get; } =
        new("albedo", "Albedo", ColourSpace.Srgb);

    /// <summary>Gets the normal-map data role.</summary>
    public static TextureRole Normal { get; } =
        new("normal", "Normal", ColourSpace.Linear, isNormalMapData: true);

    /// <summary>Gets the metallic-data role.</summary>
    public static TextureRole Metallic { get; } =
        new("metallic", "Metallic", ColourSpace.Linear);

    /// <summary>Gets the roughness-data role.</summary>
    public static TextureRole Roughness { get; } =
        new("roughness", "Roughness", ColourSpace.Linear);

    /// <summary>Gets the emission-colour role.</summary>
    public static TextureRole Emission { get; } =
        new("emission", "Emission", ColourSpace.Srgb);

    /// <summary>Gets the ambient-occlusion data role.</summary>
    public static TextureRole AmbientOcclusion { get; } =
        new("ambient-occlusion", "Ambient Occlusion", ColourSpace.Linear);

    /// <summary>Gets the opacity-data role.</summary>
    public static TextureRole Opacity { get; } =
        new("opacity", "Opacity", ColourSpace.Linear);

    /// <summary>Gets the height-data role.</summary>
    public static TextureRole Height { get; } =
        new("height", "Height", ColourSpace.Linear);

    private static readonly ReadOnlyCollection<TextureRole> _all =
        Array.AsReadOnly<TextureRole>(
            [Albedo, Normal, Metallic, Roughness, Emission, AmbientOcclusion, Opacity, Height]);

    /// <summary>Gets every canonical source role in stable order.</summary>
    public static IReadOnlyList<TextureRole> All => _all;

    /// <summary>Gets the lowercase, hyphen-separated canonical identifier.</summary>
    public string CanonicalIdentifier { get; }

    /// <summary>Gets the exact canonical human-readable role name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the only colour space compatible with this role.</summary>
    public ColourSpace RequiredColourSpace { get; }

    /// <summary>Gets a value indicating whether the role represents normal-map data.</summary>
    public bool IsNormalMapData { get; }

    /// <summary>Parses an exact ordinal canonical identifier.</summary>
    public static CanonicalIdentifierParseResult<TextureRole> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<TextureRole>.Failure(error);
        }

        foreach (TextureRole role in All)
        {
            if (string.Equals(identifier, role.CanonicalIdentifier, StringComparison.Ordinal))
            {
                return CanonicalIdentifierParseResult<TextureRole>.Success(role);
            }
        }

        return CanonicalIdentifierParseResult<TextureRole>.Failure(
            CanonicalIdentifierParseError.Unknown);
    }

    /// <inheritdoc />
    public bool Equals(TextureRole? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TextureRole other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    /// <inheritdoc />
    public override string ToString() => CanonicalIdentifier;
}
