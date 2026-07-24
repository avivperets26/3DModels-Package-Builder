namespace PackageBuilder.Domain.BuildJobs;

/// <summary>Identifies expected PB-0108 input failures without pre-empting PB-0109 findings.</summary>
public enum BuildModelValidationError
{
    None = 0,
    NullIdentity,
    EmptyIdentity,
    WhitespaceOnlyIdentity,
    IdentityEdgeWhitespace,
    IdentityContainsControlCharacter,
    NullStepType,
    EmptyStepType,
    WhitespaceOnlyStepType,
    MalformedStepType,
    NullArtifactRole,
    EmptyArtifactRole,
    WhitespaceOnlyArtifactRole,
    MalformedArtifactRole,
    NullLogicalReference,
    EmptyLogicalReference,
    WhitespaceOnlyLogicalReference,
    UnsafeLogicalReference,
    NullJobId,
    NullStepId,
    NullArtifactId,
    NullStepStage,
    InvalidStepStage,
    NullStepStatus,
    NegativeStepOrder,
    TimestampNotUtc,
    MissingStartTime,
    UnexpectedStartTime,
    MissingEndTime,
    UnexpectedEndTime,
    EndTimeBeforeStartTime,
    MissingCompletionMetadata,
    CompletionMetadataNotAllowed,
    NullMetadataReferences,
    NullMetadataReference,
    DuplicateMetadataReference,
    NullSteps,
    NullStep,
    DuplicateStepId,
    DuplicateStepOrder,
    UnknownJobReference,
    NullArtifacts,
    NullArtifact,
    DuplicateArtifactId,
    UnknownStepReference,
    NullArtifactLifecycleState,
    StateTimeBeforeCreationTime,
    TimestampBeforeJobCreation,
}

/// <summary>Represents a task-local expected PB-0108 validation outcome.</summary>
public sealed class BuildModelValidationResult<T>
    where T : class
{
    private BuildModelValidationResult(bool isValid, T? value, BuildModelValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public T? Value { get; }

    public BuildModelValidationError Error { get; }

    internal static BuildModelValidationResult<T> Success(T value) =>
        new(true, value, BuildModelValidationError.None);

    internal static BuildModelValidationResult<T> Failure(BuildModelValidationError error) =>
        new(false, null, error);
}
