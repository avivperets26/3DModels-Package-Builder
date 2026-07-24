using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Textures;

/// <summary>Identifies the orientation convention of normal-map data.</summary>
public sealed class NormalConvention : IEquatable<NormalConvention>
{
    private NormalConvention(string canonicalIdentifier, string displayName)
    {
        CanonicalIdentifier = canonicalIdentifier;
        DisplayName = displayName;
    }

    /// <summary>Gets the convention requiring later inspection or user review.</summary>
    public static NormalConvention Auto { get; } = new("auto", "Auto");

    /// <summary>Gets the OpenGL normal orientation.</summary>
    public static NormalConvention OpenGl { get; } = new("open-gl", "OpenGL");

    /// <summary>Gets the DirectX normal orientation.</summary>
    public static NormalConvention DirectX { get; } = new("direct-x", "DirectX");

    private static readonly ReadOnlyCollection<NormalConvention> _all =
        Array.AsReadOnly<NormalConvention>([Auto, OpenGl, DirectX]);

    /// <summary>Gets every supported convention in stable canonical order.</summary>
    public static IReadOnlyList<NormalConvention> All => _all;

    /// <summary>Gets the lowercase, hyphen-separated canonical identifier.</summary>
    public string CanonicalIdentifier { get; }

    /// <summary>Gets the human-readable canonical name.</summary>
    public string DisplayName { get; }

    /// <summary>Parses an exact ordinal canonical identifier.</summary>
    public static CanonicalIdentifierParseResult<NormalConvention> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<NormalConvention>.Failure(error);
        }

        foreach (NormalConvention convention in All)
        {
            if (string.Equals(
                identifier,
                convention.CanonicalIdentifier,
                StringComparison.Ordinal))
            {
                return CanonicalIdentifierParseResult<NormalConvention>.Success(convention);
            }
        }

        return CanonicalIdentifierParseResult<NormalConvention>.Failure(
            CanonicalIdentifierParseError.Unknown);
    }

    /// <inheritdoc />
    public bool Equals(NormalConvention? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is NormalConvention other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    /// <inheritdoc />
    public override string ToString() => CanonicalIdentifier;
}
