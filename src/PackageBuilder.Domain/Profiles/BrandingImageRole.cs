using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Profiles;

/// <summary>Identifies the approved semantic use of a branding image.</summary>
public sealed class BrandingImageRole : IEquatable<BrandingImageRole>
{
    private BrandingImageRole(string canonicalIdentifier)
    {
        CanonicalIdentifier = canonicalIdentifier;
    }

    public static BrandingImageRole Logo { get; } = new("logo");

    public static BrandingImageRole Watermark { get; } = new("watermark");

    private static readonly ReadOnlyCollection<BrandingImageRole> _all =
        Array.AsReadOnly([Logo, Watermark]);

    public static IReadOnlyList<BrandingImageRole> All => _all;

    public string CanonicalIdentifier { get; }

    public static CanonicalIdentifierParseResult<BrandingImageRole> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<BrandingImageRole>.Failure(error);
        }

        BrandingImageRole? value = All.FirstOrDefault(
            item => string.Equals(
                item.CanonicalIdentifier,
                identifier,
                StringComparison.Ordinal));
        return value is null
            ? CanonicalIdentifierParseResult<BrandingImageRole>.Failure(
                CanonicalIdentifierParseError.Unknown)
            : CanonicalIdentifierParseResult<BrandingImageRole>.Success(value);
    }

    public bool Equals(BrandingImageRole? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is BrandingImageRole other && Equals(other);

    public override int GetHashCode() =>
        StableProfileHash.Create().Add(CanonicalIdentifier).ToHashCode();

    public override string ToString() => CanonicalIdentifier;
}
