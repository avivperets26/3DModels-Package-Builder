using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Domain.Tests.BuildJobs;

internal static class BuildTestAssertions
{
    public static readonly DateTimeOffset CreatedAt =
        new(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);

    public static T Success<T>(BuildModelValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid);
        Assert.Equal(BuildModelValidationError.None, result.Error);
        return Assert.IsType<T>(result.Value);
    }

    public static void Failure<T>(
        BuildModelValidationResult<T> result,
        BuildModelValidationError error)
        where T : class
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static BuildJobId JobId(string value = "Job-01") =>
        Success(BuildJobId.Create(value));

    public static BuildStepId StepId(string value = "Step-01") =>
        Success(BuildStepId.Create(value));

    public static BuildArtifactId ArtifactId(string value = "Artifact-01") =>
        Success(BuildArtifactId.Create(value));

    public static BuildStepType StepType(string value = "inspect-source") =>
        Success(BuildStepType.Create(value));

    public static BuildArtifactRole Role(string value = "normalized-model") =>
        Success(BuildArtifactRole.Create(value));

    public static BuildStepCompletionMetadata Completion() =>
        Success(
            BuildStepCompletionMetadata.Create(
                ["source-b"],
                ["output-b"],
                ["blender-5"],
                ["job-log"]));

    public static BuildStep Step(
        string id = "Step-01",
        string jobId = "Job-01",
        int order = 0,
        BuildStepStatus? status = null,
        BuildJobState? stage = null,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        BuildStepCompletionMetadata? completion = null)
    {
        BuildStepStatus actualStatus = status ?? BuildStepStatus.Pending;
        DateTimeOffset? actualStart = start;
        DateTimeOffset? actualEnd = end;
        BuildStepCompletionMetadata? actualCompletion = completion;
        if (actualStatus.Equals(BuildStepStatus.Completed))
        {
            actualStart ??= CreatedAt.AddMinutes(1);
            actualEnd ??= CreatedAt.AddMinutes(2);
            actualCompletion ??= Completion();
        }

        return Success(
            BuildStep.Create(
                StepId(id),
                JobId(jobId),
                StepType(),
                stage ?? BuildJobState.Inspecting,
                actualStatus,
                order,
                actualStart,
                actualEnd,
                actualCompletion));
    }

    public static BuildArtifact Artifact(
        string id = "Artifact-01",
        string jobId = "Job-01",
        string stepId = "Step-01",
        BuildTarget? target = null,
        string logicalReference = "targets/output.glb",
        BuildArtifactLifecycleState? state = null,
        DateTimeOffset? created = null,
        DateTimeOffset? changed = null) =>
        Success(
            BuildArtifact.Create(
                ArtifactId(id),
                JobId(jobId),
                StepId(stepId),
                Role(),
                target,
                state ?? BuildArtifactLifecycleState.Staged,
                logicalReference,
                created ?? CreatedAt.AddMinutes(2),
                changed ?? CreatedAt.AddMinutes(2)));

    public static BuildJob Job(
        IEnumerable<BuildStep?>? steps = null,
        IEnumerable<BuildArtifact?>? artifacts = null,
        string id = "Job-01") =>
        Success(
            BuildJob.Create(
                JobId(id),
                CreatedAt,
                steps ?? [],
                artifacts ?? []));

    public static BuildJob Transition(
        BuildJob job,
        BuildJobState state,
        DateTimeOffset? timestamp = null)
    {
        BuildJobTransitionResult result =
            job.TryTransition(targetState: state, occurredAtUtc: timestamp ?? CreatedAt);
        Assert.True(result.IsSuccessful);
        Assert.Equal(BuildJobTransitionError.None, result.Error);
        return Assert.IsType<BuildJob>(result.Value);
    }

    public static void AssertImmutable<T>(IReadOnlyList<T> values, T newValue)
    {
        IList<T> list = Assert.IsType<IList<T>>(values, exactMatch: false);
        Assert.True(list.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => list.Add(newValue));
    }
}
