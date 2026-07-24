using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Animations;

/// <summary>Identifies whether playback stops or repeats after the inclusive clip range.</summary>
public sealed class LoopBehavior : IEquatable<LoopBehavior>
{
    private LoopBehavior(string canonicalIdentifier, string displayName)
    {
        CanonicalIdentifier = canonicalIdentifier;
        DisplayName = displayName;
    }

    public static LoopBehavior Once { get; } = new("once", "Once");

    public static LoopBehavior Loop { get; } = new("loop", "Loop");

    private static readonly ReadOnlyCollection<LoopBehavior> _all =
        Array.AsReadOnly<LoopBehavior>([Once, Loop]);

    public static IReadOnlyList<LoopBehavior> All => _all;

    public string CanonicalIdentifier { get; }

    public string DisplayName { get; }

    public static CanonicalIdentifierParseResult<LoopBehavior> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<LoopBehavior>.Failure(error);
        }

        foreach (LoopBehavior behavior in All)
        {
            if (string.Equals(
                identifier,
                behavior.CanonicalIdentifier,
                StringComparison.Ordinal))
            {
                return CanonicalIdentifierParseResult<LoopBehavior>.Success(behavior);
            }
        }

        return CanonicalIdentifierParseResult<LoopBehavior>.Failure(
            CanonicalIdentifierParseError.Unknown);
    }

    public bool Equals(LoopBehavior? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is LoopBehavior other && Equals(other);

    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    public override string ToString() => CanonicalIdentifier;
}
