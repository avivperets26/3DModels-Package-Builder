namespace PackageBuilder.Domain.BuildJobs;

public sealed class BuildStep : IEquatable<BuildStep>
{
    private BuildStep(
        BuildStepId id,
        BuildJobId jobId,
        BuildStepType type,
        BuildJobState stage,
        BuildStepStatus status,
        int order,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? endedAtUtc,
        BuildStepCompletionMetadata? completion)
    {
        Id = id;
        JobId = jobId;
        Type = type;
        Stage = stage;
        Status = status;
        Order = order;
        StartedAtUtc = startedAtUtc;
        EndedAtUtc = endedAtUtc;
        Completion = completion;
    }

    public BuildStepId Id { get; }

    public BuildJobId JobId { get; }

    public BuildStepType Type { get; }

    public BuildJobState Stage { get; }

    public BuildStepStatus Status { get; }

    public int Order { get; }

    public DateTimeOffset? StartedAtUtc { get; }

    public DateTimeOffset? EndedAtUtc { get; }

    public BuildStepCompletionMetadata? Completion { get; }

    public static BuildModelValidationResult<BuildStep> Create(
        BuildStepId? id,
        BuildJobId? jobId,
        BuildStepType? type,
        BuildJobState? stage,
        BuildStepStatus? status,
        int order,
        DateTimeOffset? startedAtUtc = null,
        DateTimeOffset? endedAtUtc = null,
        BuildStepCompletionMetadata? completion = null)
    {
        BuildModelValidationError error = ValidateRequired(id, jobId, type, stage, status, order);
        if (error != BuildModelValidationError.None)
        {
            return Failure(error);
        }

        error = ValidateTiming(status!, startedAtUtc, endedAtUtc, completion);
        return error == BuildModelValidationError.None
            ? BuildModelValidationResult<BuildStep>.Success(
                new BuildStep(
                    id!,
                    jobId!,
                    type!,
                    stage!,
                    status!,
                    order,
                    startedAtUtc,
                    endedAtUtc,
                    completion))
            : Failure(error);
    }

    public bool Equals(BuildStep? other) =>
        other is not null &&
        Id.Equals(other.Id) &&
        JobId.Equals(other.JobId) &&
        Type.Equals(other.Type) &&
        Stage.Equals(other.Stage) &&
        Status.Equals(other.Status) &&
        Order == other.Order &&
        Nullable.Equals(StartedAtUtc, other.StartedAtUtc) &&
        Nullable.Equals(EndedAtUtc, other.EndedAtUtc) &&
        Equals(Completion, other.Completion);

    public override bool Equals(object? obj) => obj is BuildStep other && Equals(other);

    public override int GetHashCode() =>
        StableBuildHash.Create()
            .Add(Id.Value)
            .Add(JobId.Value)
            .Add(Type.CanonicalIdentifier)
            .Add(Stage.CanonicalIdentifier)
            .Add(Status.CanonicalIdentifier)
            .Add(Order)
            .Add(StartedAtUtc?.UtcTicks ?? long.MinValue)
            .Add(EndedAtUtc?.UtcTicks ?? long.MinValue)
            .Add(Completion?.GetHashCode() ?? 0)
            .ToHashCode();

    private static BuildModelValidationError ValidateRequired(
        BuildStepId? id,
        BuildJobId? jobId,
        BuildStepType? type,
        BuildJobState? stage,
        BuildStepStatus? status,
        int order) =>
        id is null
            ? BuildModelValidationError.NullStepId
            : jobId is null
            ? BuildModelValidationError.NullJobId
            : type is null
            ? BuildModelValidationError.NullStepType
            : stage is null
            ? BuildModelValidationError.NullStepStage
            : !stage.IsExecutionStage
            ? BuildModelValidationError.InvalidStepStage
            : status is null
            ? BuildModelValidationError.NullStepStatus
            : order < 0
            ? BuildModelValidationError.NegativeStepOrder
            : BuildModelValidationError.None;

    private static BuildModelValidationError ValidateTiming(
        BuildStepStatus status,
        DateTimeOffset? start,
        DateTimeOffset? end,
        BuildStepCompletionMetadata? completion)
    {
        return start.HasValue && !BuildValueValidator.IsUtc(start.Value) ||
            end.HasValue && !BuildValueValidator.IsUtc(end.Value)
            ? BuildModelValidationError.TimestampNotUtc
            : end < start
            ? BuildModelValidationError.EndTimeBeforeStartTime
            : status.Equals(BuildStepStatus.Pending)
            ? start.HasValue
                ? BuildModelValidationError.UnexpectedStartTime
                : end.HasValue
                ? BuildModelValidationError.UnexpectedEndTime
                : completion is not null
                ? BuildModelValidationError.CompletionMetadataNotAllowed
                : BuildModelValidationError.None
            : !start.HasValue
            ? BuildModelValidationError.MissingStartTime
            : status.Equals(BuildStepStatus.Running)
            ? end.HasValue
                ? BuildModelValidationError.UnexpectedEndTime
                : completion is not null
                ? BuildModelValidationError.CompletionMetadataNotAllowed
                : BuildModelValidationError.None
            : !end.HasValue
            ? BuildModelValidationError.MissingEndTime
            : status.Equals(BuildStepStatus.Completed)
            ? completion is null
                ? BuildModelValidationError.MissingCompletionMetadata
                : BuildModelValidationError.None
            : completion is not null
            ? BuildModelValidationError.CompletionMetadataNotAllowed
            : BuildModelValidationError.None;
    }

    private static BuildModelValidationResult<BuildStep> Failure(
        BuildModelValidationError error) =>
        BuildModelValidationResult<BuildStep>.Failure(error);
}
