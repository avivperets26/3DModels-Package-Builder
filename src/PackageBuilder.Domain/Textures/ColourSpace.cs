using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Textures;

/// <summary>Identifies a renderer-independent texture colour space.</summary>
public sealed class ColourSpace : IEquatable<ColourSpace>
{
    private ColourSpace(string canonicalIdentifier, string displayName)
    {
        CanonicalIdentifier = canonicalIdentifier;
        DisplayName = displayName;
    }

    /// <summary>Gets the sRGB colour space.</summary>
    public static ColourSpace Srgb { get; } = new("srgb", "sRGB");

    /// <summary>Gets the linear-data colour space.</summary>
    public static ColourSpace Linear { get; } = new("linear", "Linear");

    private static readonly ReadOnlyCollection<ColourSpace> _all =
        Array.AsReadOnly<ColourSpace>([Srgb, Linear]);

    /// <summary>Gets both colour spaces in stable canonical order.</summary>
    public static IReadOnlyList<ColourSpace> All => _all;

    /// <summary>Gets the lowercase canonical identifier.</summary>
    public string CanonicalIdentifier { get; }

    /// <summary>Gets the human-readable canonical name.</summary>
    public string DisplayName { get; }

    /// <summary>Parses an exact ordinal canonical identifier.</summary>
    public static CanonicalIdentifierParseResult<ColourSpace> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<ColourSpace>.Failure(error);
        }

        foreach (ColourSpace colourSpace in All)
        {
            if (string.Equals(
                identifier,
                colourSpace.CanonicalIdentifier,
                StringComparison.Ordinal))
            {
                return CanonicalIdentifierParseResult<ColourSpace>.Success(colourSpace);
            }
        }

        return CanonicalIdentifierParseResult<ColourSpace>.Failure(
            CanonicalIdentifierParseError.Unknown);
    }

    /// <inheritdoc />
    public bool Equals(ColourSpace? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ColourSpace other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    /// <inheritdoc />
    public override string ToString() => CanonicalIdentifier;
}
