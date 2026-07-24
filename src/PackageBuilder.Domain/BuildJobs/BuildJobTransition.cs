namespace PackageBuilder.Domain.BuildJobs;

public sealed class BuildJobTransition : IEquatable<BuildJobTransition>
{
    internal BuildJobTransition(
        int ordinal,
        BuildJobState from,
        BuildJobState to,
        DateTimeOffset occurredAtUtc)
    {
        Ordinal = ordinal;
        From = from;
        To = to;
        OccurredAtUtc = occurredAtUtc;
    }

    public int Ordinal { get; }

    public BuildJobState From { get; }

    public BuildJobState To { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public bool Equals(BuildJobTransition? other) =>
        other is not null &&
        Ordinal == other.Ordinal &&
        From.Equals(other.From) &&
        To.Equals(other.To) &&
        OccurredAtUtc.Equals(other.OccurredAtUtc);

    public override bool Equals(object? obj) => obj is BuildJobTransition other && Equals(other);

    public override int GetHashCode() =>
        StableBuildHash.Create()
            .Add(Ordinal)
            .Add(From.CanonicalIdentifier)
            .Add(To.CanonicalIdentifier)
            .Add(OccurredAtUtc.UtcTicks)
            .ToHashCode();
}

public enum BuildJobTransitionError
{
    None = 0,
    NullTargetState,
    SameState,
    TerminalState,
    TransitionNotApproved,
    TimestampNotUtc,
    TimestampBeforeJobCreation,
    TimestampBeforePreviousTransition,
}

/// <summary>Returns expected transition rejection without throwing.</summary>
public sealed class BuildJobTransitionResult
{
    private BuildJobTransitionResult(
        bool isSuccessful,
        BuildJob? value,
        BuildJobTransitionError error)
    {
        IsSuccessful = isSuccessful;
        Value = value;
        Error = error;
    }

    public bool IsSuccessful { get; }

    public BuildJob? Value { get; }

    public BuildJobTransitionError Error { get; }

    internal static BuildJobTransitionResult Success(BuildJob value) =>
        new(true, value, BuildJobTransitionError.None);

    internal static BuildJobTransitionResult Failure(BuildJobTransitionError error) =>
        new(false, null, error);
}
