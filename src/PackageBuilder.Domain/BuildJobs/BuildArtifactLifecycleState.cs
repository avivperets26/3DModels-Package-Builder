using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.BuildJobs;

/// <summary>
/// Describes the architecture's staged, validated, and atomically promoted artifact lifecycle.
/// </summary>
public sealed class BuildArtifactLifecycleState : IEquatable<BuildArtifactLifecycleState>
{
    private static readonly ReadOnlyCollection<BuildArtifactLifecycleState> _all;

    private BuildArtifactLifecycleState(string canonicalIdentifier) =>
        CanonicalIdentifier = canonicalIdentifier;

    public static BuildArtifactLifecycleState Staged { get; } = new("staged");

    public static BuildArtifactLifecycleState Validated { get; } = new("validated");

    public static BuildArtifactLifecycleState Promoted { get; } = new("promoted");

    static BuildArtifactLifecycleState()
    {
        _all = Array.AsReadOnly([Staged, Validated, Promoted]);
    }

    public static IReadOnlyList<BuildArtifactLifecycleState> All => _all;

    public string CanonicalIdentifier { get; }

    public static CanonicalIdentifierParseResult<BuildArtifactLifecycleState> TryParse(
        string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<BuildArtifactLifecycleState>.Failure(error);
        }

        BuildArtifactLifecycleState? value = _all.FirstOrDefault(
            candidate => string.Equals(
                candidate.CanonicalIdentifier,
                identifier,
                StringComparison.Ordinal));
        return value is null
            ? CanonicalIdentifierParseResult<BuildArtifactLifecycleState>.Failure(
                CanonicalIdentifierParseError.Unknown)
            : CanonicalIdentifierParseResult<BuildArtifactLifecycleState>.Success(value);
    }

    public bool Equals(BuildArtifactLifecycleState? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is BuildArtifactLifecycleState other && Equals(other);

    public override int GetHashCode() =>
        StableBuildHash.Create().Add(CanonicalIdentifier).ToHashCode();

    public override string ToString() => CanonicalIdentifier;
}
