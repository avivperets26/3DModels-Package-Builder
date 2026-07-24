using System.Collections.ObjectModel;

namespace PackageBuilder.Domain.BuildJobs;

/// <summary>
/// Immutable build aggregate. State changes construct a new value and append one deterministic
/// history entry; no clock, persistence, filesystem, worker, or orchestration service is used.
/// </summary>
public sealed class BuildJob : IEquatable<BuildJob>
{
    private BuildJob(
        BuildJobId id,
        DateTimeOffset createdAtUtc,
        BuildJobState state,
        ReadOnlyCollection<BuildJobTransition> transitionHistory,
        ReadOnlyCollection<BuildStep> steps,
        ReadOnlyCollection<BuildArtifact> artifacts)
    {
        Id = id;
        CreatedAtUtc = createdAtUtc;
        State = state;
        TransitionHistory = transitionHistory;
        Steps = steps;
        Artifacts = artifacts;
    }

    public BuildJobId Id { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public BuildJobState State { get; }

    public IReadOnlyList<BuildJobTransition> TransitionHistory { get; }

    public IReadOnlyList<BuildStep> Steps { get; }

    public IReadOnlyList<BuildArtifact> Artifacts { get; }

    public static BuildModelValidationResult<BuildJob> Create(
        BuildJobId? id,
        DateTimeOffset createdAtUtc,
        IEnumerable<BuildStep?>? steps,
        IEnumerable<BuildArtifact?>? artifacts)
    {
        if (id is null)
        {
            return Failure(BuildModelValidationError.NullJobId);
        }

        if (!BuildValueValidator.IsUtc(createdAtUtc))
        {
            return Failure(BuildModelValidationError.TimestampNotUtc);
        }

        BuildModelValidationResult<ReadOnlyCollection<BuildStep>> stepResult =
            SnapshotSteps(id, createdAtUtc, steps);
        if (!stepResult.IsValid)
        {
            return Failure(stepResult.Error);
        }

        BuildModelValidationResult<ReadOnlyCollection<BuildArtifact>> artifactResult =
            SnapshotArtifacts(id, createdAtUtc, stepResult.Value!, artifacts);
        return !artifactResult.IsValid
            ? Failure(artifactResult.Error)
            : BuildModelValidationResult<BuildJob>.Success(
                new BuildJob(
                    id,
                    createdAtUtc,
                    BuildJobState.Queued,
                    Array.AsReadOnly(Array.Empty<BuildJobTransition>()),
                    stepResult.Value!,
                    artifactResult.Value!));
    }

    public BuildJobTransitionResult TryTransition(
        BuildJobState? targetState,
        DateTimeOffset occurredAtUtc)
    {
        if (targetState is null)
        {
            return BuildJobTransitionResult.Failure(BuildJobTransitionError.NullTargetState);
        }

        if (State.Equals(targetState))
        {
            return BuildJobTransitionResult.Failure(BuildJobTransitionError.SameState);
        }

        // Terminal success, failure, and cancellation are deliberately absorbing.
        if (State.IsTerminal)
        {
            return BuildJobTransitionResult.Failure(BuildJobTransitionError.TerminalState);
        }

        if (!BuildJobTransitionPolicy.IsApproved(State, targetState))
        {
            return BuildJobTransitionResult.Failure(
                BuildJobTransitionError.TransitionNotApproved);
        }

        if (!BuildValueValidator.IsUtc(occurredAtUtc))
        {
            return BuildJobTransitionResult.Failure(BuildJobTransitionError.TimestampNotUtc);
        }

        if (occurredAtUtc < CreatedAtUtc)
        {
            return BuildJobTransitionResult.Failure(
                BuildJobTransitionError.TimestampBeforeJobCreation);
        }

        if (TransitionHistory.Count > 0 &&
            occurredAtUtc < TransitionHistory[^1].OccurredAtUtc)
        {
            return BuildJobTransitionResult.Failure(
                BuildJobTransitionError.TimestampBeforePreviousTransition);
        }

        // Copy-on-transition keeps prior job values and their returned history immutable.
        var history = new List<BuildJobTransition>(TransitionHistory)
        {
            new(TransitionHistory.Count + 1, State, targetState, occurredAtUtc),
        };
        return BuildJobTransitionResult.Success(
            new BuildJob(
                Id,
                CreatedAtUtc,
                targetState,
                new ReadOnlyCollection<BuildJobTransition>(history),
                (ReadOnlyCollection<BuildStep>)Steps,
                (ReadOnlyCollection<BuildArtifact>)Artifacts));
    }

    public bool Equals(BuildJob? other) =>
        other is not null &&
        Id.Equals(other.Id) &&
        CreatedAtUtc.Equals(other.CreatedAtUtc) &&
        State.Equals(other.State) &&
        TransitionHistory.SequenceEqual(other.TransitionHistory) &&
        Steps.SequenceEqual(other.Steps) &&
        Artifacts.SequenceEqual(other.Artifacts);

    public override bool Equals(object? obj) => obj is BuildJob other && Equals(other);

    public override int GetHashCode()
    {
        StableBuildHash hash = StableBuildHash.Create()
            .Add(Id.Value)
            .Add(CreatedAtUtc.UtcTicks)
            .Add(State.CanonicalIdentifier);
        foreach (BuildJobTransition transition in TransitionHistory)
        {
            hash = hash.Add(transition.GetHashCode());
        }

        foreach (BuildStep step in Steps)
        {
            hash = hash.Add(step.GetHashCode());
        }

        foreach (BuildArtifact artifact in Artifacts)
        {
            hash = hash.Add(artifact.GetHashCode());
        }

        return hash.ToHashCode();
    }

    private static BuildModelValidationResult<ReadOnlyCollection<BuildStep>> SnapshotSteps(
        BuildJobId jobId,
        DateTimeOffset createdAtUtc,
        IEnumerable<BuildStep?>? steps)
    {
        if (steps is null)
        {
            return BuildModelValidationResult<ReadOnlyCollection<BuildStep>>.Failure(
                BuildModelValidationError.NullSteps);
        }

        var values = new List<BuildStep>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var orders = new HashSet<int>();
        foreach (BuildStep? step in steps)
        {
            if (step is null)
            {
                return StepFailure(BuildModelValidationError.NullStep);
            }

            if (!step.JobId.Equals(jobId))
            {
                return StepFailure(BuildModelValidationError.UnknownJobReference);
            }

            if (!ids.Add(step.Id.Value))
            {
                return StepFailure(BuildModelValidationError.DuplicateStepId);
            }

            if (!orders.Add(step.Order))
            {
                return StepFailure(BuildModelValidationError.DuplicateStepOrder);
            }

            if (step.StartedAtUtc < createdAtUtc)
            {
                return StepFailure(BuildModelValidationError.TimestampBeforeJobCreation);
            }

            values.Add(step);
        }

        values.Sort((left, right) => left.Order.CompareTo(right.Order));
        return BuildModelValidationResult<ReadOnlyCollection<BuildStep>>.Success(
            new ReadOnlyCollection<BuildStep>(values));
    }

    private static BuildModelValidationResult<ReadOnlyCollection<BuildArtifact>>
        SnapshotArtifacts(
            BuildJobId jobId,
            DateTimeOffset createdAtUtc,
            IReadOnlyCollection<BuildStep> steps,
            IEnumerable<BuildArtifact?>? artifacts)
    {
        if (artifacts is null)
        {
            return BuildModelValidationResult<ReadOnlyCollection<BuildArtifact>>.Failure(
                BuildModelValidationError.NullArtifacts);
        }

        var knownSteps = new HashSet<string>(
            steps.Select(step => step.Id.Value),
            StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var values = new List<BuildArtifact>();
        foreach (BuildArtifact? artifact in artifacts)
        {
            if (artifact is null)
            {
                return ArtifactFailure(BuildModelValidationError.NullArtifact);
            }

            if (!artifact.JobId.Equals(jobId))
            {
                return ArtifactFailure(BuildModelValidationError.UnknownJobReference);
            }

            if (!knownSteps.Contains(artifact.StepId.Value))
            {
                return ArtifactFailure(BuildModelValidationError.UnknownStepReference);
            }

            if (!ids.Add(artifact.Id.Value))
            {
                return ArtifactFailure(BuildModelValidationError.DuplicateArtifactId);
            }

            if (artifact.CreatedAtUtc < createdAtUtc)
            {
                return ArtifactFailure(BuildModelValidationError.TimestampBeforeJobCreation);
            }

            values.Add(artifact);
        }

        values.Sort(
            (left, right) => StringComparer.Ordinal.Compare(left.Id.Value, right.Id.Value));
        return BuildModelValidationResult<ReadOnlyCollection<BuildArtifact>>.Success(
            new ReadOnlyCollection<BuildArtifact>(values));
    }

    private static BuildModelValidationResult<BuildJob> Failure(
        BuildModelValidationError error) =>
        BuildModelValidationResult<BuildJob>.Failure(error);

    private static BuildModelValidationResult<ReadOnlyCollection<BuildStep>> StepFailure(
        BuildModelValidationError error) =>
        BuildModelValidationResult<ReadOnlyCollection<BuildStep>>.Failure(error);

    private static BuildModelValidationResult<ReadOnlyCollection<BuildArtifact>> ArtifactFailure(
        BuildModelValidationError error) =>
        BuildModelValidationResult<ReadOnlyCollection<BuildArtifact>>.Failure(error);
}
