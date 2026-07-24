using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Materials;

/// <summary>Identifies renderer-independent surface transparency behavior.</summary>
public sealed class SurfaceMode : IEquatable<SurfaceMode>
{
    private SurfaceMode(string canonicalIdentifier, string displayName)
    {
        CanonicalIdentifier = canonicalIdentifier;
        DisplayName = displayName;
    }

    /// <summary>Gets the fully opaque surface mode.</summary>
    public static SurfaceMode Opaque { get; } = new("opaque", "Opaque");

    /// <summary>Gets the binary alpha-cutout surface mode.</summary>
    public static SurfaceMode Cutout { get; } = new("cutout", "Cutout");

    /// <summary>Gets the blended transparent surface mode.</summary>
    public static SurfaceMode Transparent { get; } = new("transparent", "Transparent");

    private static readonly ReadOnlyCollection<SurfaceMode> _all =
        Array.AsReadOnly<SurfaceMode>([Opaque, Cutout, Transparent]);

    /// <summary>Gets every supported mode in stable canonical order.</summary>
    public static IReadOnlyList<SurfaceMode> All => _all;

    /// <summary>Gets the lowercase canonical identifier.</summary>
    public string CanonicalIdentifier { get; }

    /// <summary>Gets the human-readable canonical name.</summary>
    public string DisplayName { get; }

    /// <summary>Parses an exact ordinal canonical identifier.</summary>
    public static CanonicalIdentifierParseResult<SurfaceMode> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<SurfaceMode>.Failure(error);
        }

        foreach (SurfaceMode mode in All)
        {
            if (string.Equals(identifier, mode.CanonicalIdentifier, StringComparison.Ordinal))
            {
                return CanonicalIdentifierParseResult<SurfaceMode>.Success(mode);
            }
        }

        return CanonicalIdentifierParseResult<SurfaceMode>.Failure(
            CanonicalIdentifierParseError.Unknown);
    }

    /// <inheritdoc />
    public bool Equals(SurfaceMode? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SurfaceMode other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    /// <inheritdoc />
    public override string ToString() => CanonicalIdentifier;
}
