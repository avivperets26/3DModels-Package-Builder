using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Profiles;

/// <summary>Identifies the approved intent for copyright-year data.</summary>
public sealed class CopyrightYearPolicyKind : IEquatable<CopyrightYearPolicyKind>
{
    private CopyrightYearPolicyKind(string canonicalIdentifier)
    {
        CanonicalIdentifier = canonicalIdentifier;
    }

    public static CopyrightYearPolicyKind SingleYear { get; } = new("single-year");

    public static CopyrightYearPolicyKind YearRange { get; } = new("year-range");

    public static CopyrightYearPolicyKind PublicationYear { get; } = new("publication-year");

    private static readonly ReadOnlyCollection<CopyrightYearPolicyKind> _all =
        Array.AsReadOnly([SingleYear, YearRange, PublicationYear]);

    public static IReadOnlyList<CopyrightYearPolicyKind> All => _all;

    public string CanonicalIdentifier { get; }

    public static CanonicalIdentifierParseResult<CopyrightYearPolicyKind> TryParse(
        string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<CopyrightYearPolicyKind>.Failure(error);
        }

        CopyrightYearPolicyKind? value = All.FirstOrDefault(
            item => string.Equals(
                item.CanonicalIdentifier,
                identifier,
                StringComparison.Ordinal));
        return value is null
            ? CanonicalIdentifierParseResult<CopyrightYearPolicyKind>.Failure(
                CanonicalIdentifierParseError.Unknown)
            : CanonicalIdentifierParseResult<CopyrightYearPolicyKind>.Success(value);
    }

    public bool Equals(CopyrightYearPolicyKind? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is CopyrightYearPolicyKind other && Equals(other);

    public override int GetHashCode() =>
        StableProfileHash.Create().Add(CanonicalIdentifier).ToHashCode();

    public override string ToString() => CanonicalIdentifier;
}
