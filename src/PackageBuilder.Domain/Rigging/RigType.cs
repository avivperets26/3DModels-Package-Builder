using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Rigging;

/// <summary>Identifies approved renderer-independent rig interpretation.</summary>
public sealed class RigType : IEquatable<RigType>
{
    private RigType(string canonicalIdentifier, string displayName)
    {
        CanonicalIdentifier = canonicalIdentifier;
        DisplayName = displayName;
    }

    /// <summary>Gets a general bone hierarchy with no humanoid mapping assumption.</summary>
    public static RigType Generic { get; } = new("generic", "Generic");

    /// <summary>Gets a rig explicitly declared and separately validated as humanoid.</summary>
    public static RigType Humanoid { get; } = new("humanoid", "Humanoid");

    private static readonly ReadOnlyCollection<RigType> _all =
        Array.AsReadOnly<RigType>([Generic, Humanoid]);

    /// <summary>Gets every approved rig type in stable canonical order.</summary>
    public static IReadOnlyList<RigType> All => _all;

    /// <summary>Gets the lowercase canonical identifier.</summary>
    public string CanonicalIdentifier { get; }

    /// <summary>Gets the human-readable canonical name.</summary>
    public string DisplayName { get; }

    /// <summary>Parses an exact ordinal canonical identifier.</summary>
    public static CanonicalIdentifierParseResult<RigType> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<RigType>.Failure(error);
        }

        foreach (RigType type in All)
        {
            if (string.Equals(identifier, type.CanonicalIdentifier, StringComparison.Ordinal))
            {
                return CanonicalIdentifierParseResult<RigType>.Success(type);
            }
        }

        return CanonicalIdentifierParseResult<RigType>.Failure(
            CanonicalIdentifierParseError.Unknown);
    }

    /// <inheritdoc />
    public bool Equals(RigType? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RigType other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    /// <inheritdoc />
    public override string ToString() => CanonicalIdentifier;
}
