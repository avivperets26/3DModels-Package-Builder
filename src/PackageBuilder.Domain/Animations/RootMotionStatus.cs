using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Animations;

/// <summary>Identifies whether a clip declares motion on its rig's validated root bone.</summary>
public sealed class RootMotionStatus : IEquatable<RootMotionStatus>
{
    private RootMotionStatus(string canonicalIdentifier, string displayName)
    {
        CanonicalIdentifier = canonicalIdentifier;
        DisplayName = displayName;
    }

    public static RootMotionStatus None { get; } = new("none", "None");

    public static RootMotionStatus RootBone { get; } = new("root-bone", "Root Bone");

    private static readonly ReadOnlyCollection<RootMotionStatus> _all =
        Array.AsReadOnly<RootMotionStatus>([None, RootBone]);

    public static IReadOnlyList<RootMotionStatus> All => _all;

    public string CanonicalIdentifier { get; }

    public string DisplayName { get; }

    public static CanonicalIdentifierParseResult<RootMotionStatus> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<RootMotionStatus>.Failure(error);
        }

        foreach (RootMotionStatus status in All)
        {
            if (string.Equals(identifier, status.CanonicalIdentifier, StringComparison.Ordinal))
            {
                return CanonicalIdentifierParseResult<RootMotionStatus>.Success(status);
            }
        }

        return CanonicalIdentifierParseResult<RootMotionStatus>.Failure(
            CanonicalIdentifierParseError.Unknown);
    }

    public bool Equals(RootMotionStatus? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is RootMotionStatus other && Equals(other);

    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    public override string ToString() => CanonicalIdentifier;
}
