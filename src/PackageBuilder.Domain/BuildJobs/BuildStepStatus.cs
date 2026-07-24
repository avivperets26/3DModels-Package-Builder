using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.BuildJobs;

/// <summary>
/// Describes the retained status of one step. It does not define execution retry or resume rules.
/// </summary>
public sealed class BuildStepStatus : IEquatable<BuildStepStatus>
{
    private static readonly ReadOnlyCollection<BuildStepStatus> _all;

    private BuildStepStatus(string canonicalIdentifier, bool isTerminal)
    {
        CanonicalIdentifier = canonicalIdentifier;
        IsTerminal = isTerminal;
    }

    public static BuildStepStatus Pending { get; } = new("pending", false);

    public static BuildStepStatus Running { get; } = new("running", false);

    public static BuildStepStatus Completed { get; } = new("completed", true);

    public static BuildStepStatus Failed { get; } = new("failed", true);

    public static BuildStepStatus Cancelled { get; } = new("cancelled", true);

    static BuildStepStatus()
    {
        _all = Array.AsReadOnly(
            [Pending, Running, Completed, Failed, Cancelled]);
    }

    public static IReadOnlyList<BuildStepStatus> All => _all;

    public string CanonicalIdentifier { get; }

    public bool IsTerminal { get; }

    public static CanonicalIdentifierParseResult<BuildStepStatus> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<BuildStepStatus>.Failure(error);
        }

        BuildStepStatus? value = _all.FirstOrDefault(
            candidate => string.Equals(
                candidate.CanonicalIdentifier,
                identifier,
                StringComparison.Ordinal));
        return value is null
            ? CanonicalIdentifierParseResult<BuildStepStatus>.Failure(
                CanonicalIdentifierParseError.Unknown)
            : CanonicalIdentifierParseResult<BuildStepStatus>.Success(value);
    }

    public bool Equals(BuildStepStatus? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is BuildStepStatus other && Equals(other);

    public override int GetHashCode() =>
        StableBuildHash.Create().Add(CanonicalIdentifier).ToHashCode();

    public override string ToString() => CanonicalIdentifier;
}
