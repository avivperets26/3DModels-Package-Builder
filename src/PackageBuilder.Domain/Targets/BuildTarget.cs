using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Targets;

/// <summary>Identifies a supported build-target family without target-specific settings.</summary>
public sealed class BuildTarget : IEquatable<BuildTarget>
{
    private BuildTarget(string canonicalIdentifier)
    {
        CanonicalIdentifier = canonicalIdentifier;
    }

    /// <summary>Gets the engine-independent FBX/GLB packaging target.</summary>
    public static BuildTarget Portable { get; } = new("portable");

    /// <summary>Gets the Unity target identity without Unity-specific settings or dependencies.</summary>
    public static BuildTarget Unity { get; } = new("unity");

    /// <summary>Gets the Unreal target identity without Unreal-specific settings or dependencies.</summary>
    public static BuildTarget Unreal { get; } = new("unreal");

    private static readonly ReadOnlyCollection<BuildTarget> _all = Array.AsReadOnly<BuildTarget>(
        [
            Portable,
            Unity,
            Unreal,
        ]);

    /// <summary>Gets every supported target in stable canonical order.</summary>
    public static IReadOnlyList<BuildTarget> All => _all;

    /// <summary>Gets the lowercase canonical identifier.</summary>
    public string CanonicalIdentifier { get; }

    /// <summary>Parses an exact ordinal canonical identifier without throwing for expected input.</summary>
    /// <param name="identifier">The canonical identifier to parse.</param>
    /// <returns>The matching singleton or an explicit expected-input error.</returns>
    public static CanonicalIdentifierParseResult<BuildTarget> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<BuildTarget>.Failure(error);
        }

        foreach (BuildTarget target in All)
        {
            if (string.Equals(
                identifier,
                target.CanonicalIdentifier,
                StringComparison.Ordinal))
            {
                return CanonicalIdentifierParseResult<BuildTarget>.Success(target);
            }
        }

        return CanonicalIdentifierParseResult<BuildTarget>.Failure(
            CanonicalIdentifierParseError.Unknown);
    }

    /// <inheritdoc />
    public bool Equals(BuildTarget? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BuildTarget other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    /// <inheritdoc />
    public override string ToString() => CanonicalIdentifier;
}
