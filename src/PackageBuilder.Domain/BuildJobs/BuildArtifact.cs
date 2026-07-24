using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Domain.BuildJobs;

/// <summary>
/// Represents logical artifact metadata only. The reference is never opened, resolved, or hashed.
/// </summary>
public sealed class BuildArtifact : IEquatable<BuildArtifact>
{
    private BuildArtifact(
        BuildArtifactId id,
        BuildJobId jobId,
        BuildStepId stepId,
        BuildArtifactRole role,
        BuildTarget? target,
        BuildArtifactLifecycleState lifecycleState,
        string logicalReference,
        DateTimeOffset createdAtUtc,
        DateTimeOffset stateChangedAtUtc)
    {
        Id = id;
        JobId = jobId;
        StepId = stepId;
        Role = role;
        Target = target;
        LifecycleState = lifecycleState;
        LogicalReference = logicalReference;
        CreatedAtUtc = createdAtUtc;
        StateChangedAtUtc = stateChangedAtUtc;
    }

    public BuildArtifactId Id { get; }

    public BuildJobId JobId { get; }

    public BuildStepId StepId { get; }

    public BuildArtifactRole Role { get; }

    public BuildTarget? Target { get; }

    public BuildArtifactLifecycleState LifecycleState { get; }

    public string LogicalReference { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset StateChangedAtUtc { get; }

    public static BuildModelValidationResult<BuildArtifact> Create(
        BuildArtifactId? id,
        BuildJobId? jobId,
        BuildStepId? stepId,
        BuildArtifactRole? role,
        BuildTarget? target,
        BuildArtifactLifecycleState? lifecycleState,
        string? logicalReference,
        DateTimeOffset createdAtUtc,
        DateTimeOffset stateChangedAtUtc)
    {
        BuildModelValidationError error = id is null
            ? BuildModelValidationError.NullArtifactId
            : jobId is null
            ? BuildModelValidationError.NullJobId
            : stepId is null
            ? BuildModelValidationError.NullStepId
            : role is null
            ? BuildModelValidationError.NullArtifactRole
            : lifecycleState is null
            ? BuildModelValidationError.NullArtifactLifecycleState
            : BuildValueValidator.ValidateLogicalReference(logicalReference);
        return error != BuildModelValidationError.None
            ? Failure(error)
            : !BuildValueValidator.IsUtc(createdAtUtc) ||
            !BuildValueValidator.IsUtc(stateChangedAtUtc)
            ? Failure(BuildModelValidationError.TimestampNotUtc)
            : stateChangedAtUtc < createdAtUtc
            ? Failure(BuildModelValidationError.StateTimeBeforeCreationTime)
            : BuildModelValidationResult<BuildArtifact>.Success(
                new BuildArtifact(
                    id!,
                    jobId!,
                    stepId!,
                    role!,
                    target,
                    lifecycleState!,
                    logicalReference!,
                    createdAtUtc,
                    stateChangedAtUtc));
    }

    public bool Equals(BuildArtifact? other) =>
        other is not null &&
        Id.Equals(other.Id) &&
        JobId.Equals(other.JobId) &&
        StepId.Equals(other.StepId) &&
        Role.Equals(other.Role) &&
        Equals(Target, other.Target) &&
        LifecycleState.Equals(other.LifecycleState) &&
        string.Equals(LogicalReference, other.LogicalReference, StringComparison.Ordinal) &&
        CreatedAtUtc.Equals(other.CreatedAtUtc) &&
        StateChangedAtUtc.Equals(other.StateChangedAtUtc);

    public override bool Equals(object? obj) => obj is BuildArtifact other && Equals(other);

    public override int GetHashCode() =>
        StableBuildHash.Create()
            .Add(Id.Value)
            .Add(JobId.Value)
            .Add(StepId.Value)
            .Add(Role.CanonicalIdentifier)
            .Add(Target?.CanonicalIdentifier ?? string.Empty)
            .Add(LifecycleState.CanonicalIdentifier)
            .Add(LogicalReference)
            .Add(CreatedAtUtc.UtcTicks)
            .Add(StateChangedAtUtc.UtcTicks)
            .ToHashCode();

    private static BuildModelValidationResult<BuildArtifact> Failure(
        BuildModelValidationError error) =>
        BuildModelValidationResult<BuildArtifact>.Failure(error);
}
