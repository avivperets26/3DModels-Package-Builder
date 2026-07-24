using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.BuildJobs;

public enum BuildJobStateCategory
{
    Active = 0,
    ReviewWaiting,
    TerminalSuccess,
    TerminalFailure,
    TerminalCancelled,
}

/// <summary>Represents every state in the authoritative architecture job state machine.</summary>
public sealed class BuildJobState : IEquatable<BuildJobState>
{
    private static readonly ReadOnlyCollection<BuildJobState> _all;

    private BuildJobState(
        string canonicalIdentifier,
        BuildJobStateCategory category,
        bool isExecutionStage)
    {
        CanonicalIdentifier = canonicalIdentifier;
        Category = category;
        IsExecutionStage = isExecutionStage;
    }

    public static BuildJobState Queued { get; } =
        new("queued", BuildJobStateCategory.Active, false);

    public static BuildJobState Preflight { get; } =
        new("preflight", BuildJobStateCategory.Active, true);

    public static BuildJobState Inspecting { get; } =
        new("inspecting", BuildJobStateCategory.Active, true);

    public static BuildJobState AwaitingReview { get; } =
        new("awaiting-review", BuildJobStateCategory.ReviewWaiting, false);

    public static BuildJobState Normalizing { get; } =
        new("normalizing", BuildJobStateCategory.Active, true);

    public static BuildJobState BuildingTargets { get; } =
        new("building-targets", BuildJobStateCategory.Active, true);

    public static BuildJobState RenderingPreviews { get; } =
        new("rendering-previews", BuildJobStateCategory.Active, true);

    public static BuildJobState Validating { get; } =
        new("validating", BuildJobStateCategory.Active, true);

    public static BuildJobState PackagingMarketplace { get; } =
        new("packaging-marketplace", BuildJobStateCategory.Active, true);

    public static BuildJobState CleanReimport { get; } =
        new("clean-reimport", BuildJobStateCategory.Active, true);

    public static BuildJobState Completed { get; } =
        new("completed", BuildJobStateCategory.TerminalSuccess, false);

    public static BuildJobState Failed { get; } =
        new("failed", BuildJobStateCategory.TerminalFailure, false);

    public static BuildJobState Cancelled { get; } =
        new("cancelled", BuildJobStateCategory.TerminalCancelled, false);

    static BuildJobState()
    {
        _all = Array.AsReadOnly(
            [
                Queued,
                Preflight,
                Inspecting,
                AwaitingReview,
                Normalizing,
                BuildingTargets,
                RenderingPreviews,
                Validating,
                PackagingMarketplace,
                CleanReimport,
                Completed,
                Failed,
                Cancelled,
            ]);
    }

    public static IReadOnlyList<BuildJobState> All => _all;

    public string CanonicalIdentifier { get; }

    public BuildJobStateCategory Category { get; }

    public bool IsExecutionStage { get; }

    public bool IsTerminal =>
        Category is BuildJobStateCategory.TerminalSuccess
            or BuildJobStateCategory.TerminalFailure
            or BuildJobStateCategory.TerminalCancelled;

    public static CanonicalIdentifierParseResult<BuildJobState> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<BuildJobState>.Failure(error);
        }

        BuildJobState? value = _all.FirstOrDefault(
            candidate => string.Equals(
                candidate.CanonicalIdentifier,
                identifier,
                StringComparison.Ordinal));
        return value is null
            ? CanonicalIdentifierParseResult<BuildJobState>.Failure(
                CanonicalIdentifierParseError.Unknown)
            : CanonicalIdentifierParseResult<BuildJobState>.Success(value);
    }

    public bool Equals(BuildJobState? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is BuildJobState other && Equals(other);

    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    public override string ToString() => CanonicalIdentifier;
}
