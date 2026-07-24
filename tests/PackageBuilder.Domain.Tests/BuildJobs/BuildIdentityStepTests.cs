using System.Globalization;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Tests.BuildJobs;

[Trait("Task", "PB-0108")]
public sealed class BuildIdentityStepTests
{
    public static TheoryData<string?, BuildModelValidationError> InvalidIdentities =>
        new()
        {
            { null, BuildModelValidationError.NullIdentity },
            { string.Empty, BuildModelValidationError.EmptyIdentity },
            { " \t", BuildModelValidationError.WhitespaceOnlyIdentity },
            { " leading", BuildModelValidationError.IdentityEdgeWhitespace },
            { "trailing ", BuildModelValidationError.IdentityEdgeWhitespace },
            { "control\u0001", BuildModelValidationError.IdentityContainsControlCharacter },
        };

    [Theory]
    [MemberData(nameof(InvalidIdentities))]
    public void EveryIdentityTypeRejectsInvalidInput(
        string? value,
        BuildModelValidationError error)
    {
        BuildTestAssertions.Failure(BuildJobId.Create(value), error);
        BuildTestAssertions.Failure(BuildStepId.Create(value), error);
        BuildTestAssertions.Failure(BuildArtifactId.Create(value), error);
    }

    [Fact]
    public void IdentitiesPreserveUnicodeOrdinalCasingAndStableValueSemantics()
    {
        BuildJobId upper = BuildTestAssertions.JobId("Ã„Â°Job");
        BuildJobId equal = BuildTestAssertions.JobId("Ã„Â°Job");
        BuildJobId lower = BuildTestAssertions.JobId("iJob");

        Assert.Equal("Ã„Â°Job", upper.Value);
        Assert.Equal("Ã„Â°Job", upper.ToString());
        Assert.Equal(upper, equal);
        Assert.True(upper.Equals((object)equal));
        Assert.NotEqual(upper, lower);
        Assert.Equal(upper.GetHashCode(), equal.GetHashCode());
        Assert.False(upper.Equals((BuildJobId?)null));
        Assert.False(upper.Equals(new object()));

        BuildStepId step = BuildTestAssertions.StepId("Step");
        Assert.Equal(step, BuildTestAssertions.StepId("Step"));
        Assert.True(step.Equals((object)BuildTestAssertions.StepId("Step")));
        Assert.NotEqual(step, BuildTestAssertions.StepId("step"));
        Assert.Equal(step.GetHashCode(), BuildTestAssertions.StepId("Step").GetHashCode());
        Assert.Equal("Step", step.ToString());
        Assert.False(step.Equals((BuildStepId?)null));
        Assert.False(step.Equals(new object()));

        BuildArtifactId artifact = BuildTestAssertions.ArtifactId("Artifact");
        Assert.Equal(artifact, BuildTestAssertions.ArtifactId("Artifact"));
        Assert.True(artifact.Equals((object)BuildTestAssertions.ArtifactId("Artifact")));
        Assert.NotEqual(artifact, BuildTestAssertions.ArtifactId("artifact"));
        Assert.Equal(
            artifact.GetHashCode(),
            BuildTestAssertions.ArtifactId("Artifact").GetHashCode());
        Assert.Equal("Artifact", artifact.ToString());
        Assert.False(artifact.Equals((BuildArtifactId?)null));
        Assert.False(artifact.Equals(new object()));
    }

    [Theory]
    [InlineData(null, BuildModelValidationError.NullStepType)]
    [InlineData("", BuildModelValidationError.EmptyStepType)]
    [InlineData(" ", BuildModelValidationError.WhitespaceOnlyStepType)]
    [InlineData("Inspect", BuildModelValidationError.MalformedStepType)]
    [InlineData("inspect--source", BuildModelValidationError.MalformedStepType)]
    [InlineData("inspect_source", BuildModelValidationError.MalformedStepType)]
    public void StepTypeRejectsMalformedValues(
        string? value,
        BuildModelValidationError error) =>
        BuildTestAssertions.Failure(BuildStepType.Create(value), error);

    [Fact]
    public void StepTypeHasOrdinalStableValueSemantics()
    {
        BuildStepType type = BuildTestAssertions.StepType();
        Assert.Equal("inspect-source", type.CanonicalIdentifier);
        Assert.Equal("inspect-source", type.ToString());
        Assert.Equal(type, BuildTestAssertions.StepType());
        Assert.True(type.Equals((object)BuildTestAssertions.StepType()));
        Assert.NotEqual(type, BuildTestAssertions.StepType("inspect-model"));
        Assert.Equal(type.GetHashCode(), BuildTestAssertions.StepType().GetHashCode());
        Assert.False(type.Equals((BuildStepType?)null));
        Assert.False(type.Equals(new object()));
    }

    [Fact]
    public void StepStatusesAreExplicitParseableClassifiedAndImmutable()
    {
        Assert.Equal(
            ["pending", "running", "completed", "failed", "cancelled"],
            BuildStepStatus.All.Select(value => value.CanonicalIdentifier));
        Assert.False(BuildStepStatus.Pending.IsTerminal);
        Assert.True(BuildStepStatus.Completed.IsTerminal);
        Assert.Same(BuildStepStatus.Running, BuildStepStatus.TryParse("running").Value);
        Assert.Equal(
            CanonicalIdentifierParseError.Unknown,
            BuildStepStatus.TryParse("unknown").Error);
        Assert.Equal(
            CanonicalIdentifierParseError.Malformed,
            BuildStepStatus.TryParse("Running").Error);
        Assert.Equal("failed", BuildStepStatus.Failed.ToString());
        Assert.Equal(BuildStepStatus.Failed, BuildStepStatus.TryParse("failed").Value);
        Assert.True(
            BuildStepStatus.Failed.Equals(
                (object)BuildStepStatus.TryParse("failed").Value!));
        Assert.Equal(
            BuildStepStatus.Failed.GetHashCode(),
            BuildStepStatus.TryParse("failed").Value!.GetHashCode());
        Assert.False(BuildStepStatus.Failed.Equals((BuildStepStatus?)null));
        Assert.False(BuildStepStatus.Failed.Equals(new object()));
        BuildTestAssertions.AssertImmutable(BuildStepStatus.All, BuildStepStatus.Pending);
    }

    [Fact]
    public void StepRejectsEveryMissingOrInvalidRequiredValue()
    {
        BuildStepId id = BuildTestAssertions.StepId();
        BuildJobId jobId = BuildTestAssertions.JobId();
        BuildStepType type = BuildTestAssertions.StepType();

        AssertStepFailure(null, jobId, type, BuildJobState.Inspecting, BuildStepStatus.Pending, 0,
            BuildModelValidationError.NullStepId);
        AssertStepFailure(id, null, type, BuildJobState.Inspecting, BuildStepStatus.Pending, 0,
            BuildModelValidationError.NullJobId);
        AssertStepFailure(id, jobId, null, BuildJobState.Inspecting, BuildStepStatus.Pending, 0,
            BuildModelValidationError.NullStepType);
        AssertStepFailure(id, jobId, type, null, BuildStepStatus.Pending, 0,
            BuildModelValidationError.NullStepStage);
        AssertStepFailure(id, jobId, type, BuildJobState.Queued, BuildStepStatus.Pending, 0,
            BuildModelValidationError.InvalidStepStage);
        AssertStepFailure(id, jobId, type, BuildJobState.Completed, BuildStepStatus.Pending, 0,
            BuildModelValidationError.InvalidStepStage);
        AssertStepFailure(id, jobId, type, BuildJobState.Inspecting, null, 0,
            BuildModelValidationError.NullStepStatus);
        AssertStepFailure(id, jobId, type, BuildJobState.Inspecting, BuildStepStatus.Pending, -1,
            BuildModelValidationError.NegativeStepOrder);
    }

    [Fact]
    public void StepAcceptsEveryValidLifecycleTimingCombination()
    {
        BuildStep pending = BuildTestAssertions.Step();
        BuildStep running = BuildTestAssertions.Success(
            BuildStep.Create(
                BuildTestAssertions.StepId(),
                BuildTestAssertions.JobId(),
                BuildTestAssertions.StepType(),
                BuildJobState.Preflight,
                BuildStepStatus.Running,
                1,
                BuildTestAssertions.CreatedAt));
        BuildStep completed = BuildTestAssertions.Step(
            order: 2,
            status: BuildStepStatus.Completed);
        BuildStep failed = BuildTestAssertions.Success(
            BuildStep.Create(
                BuildTestAssertions.StepId("failed"),
                BuildTestAssertions.JobId(),
                BuildTestAssertions.StepType(),
                BuildJobState.Validating,
                BuildStepStatus.Failed,
                3,
                BuildTestAssertions.CreatedAt,
                BuildTestAssertions.CreatedAt));
        BuildStep cancelled = BuildTestAssertions.Success(
            BuildStep.Create(
                BuildTestAssertions.StepId("cancelled"),
                BuildTestAssertions.JobId(),
                BuildTestAssertions.StepType(),
                BuildJobState.Preflight,
                BuildStepStatus.Cancelled,
                4,
                BuildTestAssertions.CreatedAt,
                BuildTestAssertions.CreatedAt));

        Assert.Null(pending.StartedAtUtc);
        Assert.Equal(BuildTestAssertions.CreatedAt, running.StartedAtUtc);
        Assert.NotNull(completed.Completion);
        Assert.Equal(BuildStepStatus.Failed, failed.Status);
        Assert.Equal(BuildStepStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public void StepRejectsInvalidTimingAndCompletionCombinations()
    {
        DateTimeOffset utc = BuildTestAssertions.CreatedAt;
        DateTimeOffset local = utc.ToOffset(TimeSpan.FromHours(1));
        BuildStepCompletionMetadata completion = BuildTestAssertions.Completion();

        AssertTimingFailure(BuildStepStatus.Pending, local, null, null,
            BuildModelValidationError.TimestampNotUtc);
        AssertTimingFailure(BuildStepStatus.Pending, utc, null, null,
            BuildModelValidationError.UnexpectedStartTime);
        AssertTimingFailure(BuildStepStatus.Pending, null, utc, null,
            BuildModelValidationError.UnexpectedEndTime);
        AssertTimingFailure(BuildStepStatus.Pending, null, null, completion,
            BuildModelValidationError.CompletionMetadataNotAllowed);
        AssertTimingFailure(BuildStepStatus.Running, null, null, null,
            BuildModelValidationError.MissingStartTime);
        AssertTimingFailure(BuildStepStatus.Running, utc, utc, null,
            BuildModelValidationError.UnexpectedEndTime);
        AssertTimingFailure(BuildStepStatus.Running, utc, null, completion,
            BuildModelValidationError.CompletionMetadataNotAllowed);
        AssertTimingFailure(BuildStepStatus.Completed, utc, null, completion,
            BuildModelValidationError.MissingEndTime);
        AssertTimingFailure(BuildStepStatus.Completed, utc, utc.AddTicks(-1), completion,
            BuildModelValidationError.EndTimeBeforeStartTime);
        AssertTimingFailure(BuildStepStatus.Completed, utc, local, completion,
            BuildModelValidationError.TimestampNotUtc);
        AssertTimingFailure(BuildStepStatus.Completed, utc, utc, null,
            BuildModelValidationError.MissingCompletionMetadata);
        AssertTimingFailure(BuildStepStatus.Failed, utc, utc, completion,
            BuildModelValidationError.CompletionMetadataNotAllowed);
    }

    [Fact]
    public void CompletionMetadataIsSortedImmutableAndRejectsInvalidReferences()
    {
        var inputs = new List<string?> { "zeta", "alpha" };
        BuildStepCompletionMetadata metadata = BuildTestAssertions.Success(
            BuildStepCompletionMetadata.Create(inputs, ["output"], ["tool"], ["log"]));
        inputs.Clear();

        Assert.Equal(["alpha", "zeta"], metadata.InputReferences);
        BuildTestAssertions.AssertImmutable(metadata.InputReferences, "other");
        Assert.Equal(
            metadata,
            BuildTestAssertions.Success(
                BuildStepCompletionMetadata.Create(
                    ["zeta", "alpha"],
                    ["output"],
                    ["tool"],
                    ["log"])));
        Assert.Equal(
            metadata.GetHashCode(),
            BuildTestAssertions.Success(
                BuildStepCompletionMetadata.Create(
                    ["zeta", "alpha"],
                    ["output"],
                    ["tool"],
                    ["log"])).GetHashCode());
        Assert.False(metadata.Equals((BuildStepCompletionMetadata?)null));
        Assert.False(metadata.Equals(new object()));

        AssertMetadataFailure(null, [], [], [], BuildModelValidationError.NullMetadataReferences);
        AssertMetadataFailure([], null, [], [], BuildModelValidationError.NullMetadataReferences);
        AssertMetadataFailure([], [], null, [], BuildModelValidationError.NullMetadataReferences);
        AssertMetadataFailure([], [], [], null, BuildModelValidationError.NullMetadataReferences);
        AssertMetadataFailure([null], [], [], [], BuildModelValidationError.NullMetadataReference);
        AssertMetadataFailure(["same", "same"], [], [], [],
            BuildModelValidationError.DuplicateMetadataReference);
        AssertMetadataFailure([" bad"], [], [], [],
            BuildModelValidationError.IdentityEdgeWhitespace);
    }

    [Fact]
    public void StepEqualityHashingAndValidationAreCultureIndependent()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            BuildStep first = BuildTestAssertions.Step(
                status: BuildStepStatus.Completed);
            int hash = first.GetHashCode();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            BuildStep second = BuildTestAssertions.Step(
                status: BuildStepStatus.Completed);

            Assert.Equal(first, second);
            Assert.True(first.Equals((object)second));
            Assert.Equal(hash, second.GetHashCode());
            Assert.False(first.Equals((BuildStep?)null));
            Assert.False(first.Equals(new object()));
            Assert.Equal(
                BuildModelValidationError.MalformedStepType,
                BuildStepType.Create("Ã„Â°nspect").Error);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static void AssertStepFailure(
        BuildStepId? id,
        BuildJobId? jobId,
        BuildStepType? type,
        BuildJobState? stage,
        BuildStepStatus? status,
        int order,
        BuildModelValidationError error) =>
        BuildTestAssertions.Failure(
            BuildStep.Create(id, jobId, type, stage, status, order),
            error);

    private static void AssertTimingFailure(
        BuildStepStatus status,
        DateTimeOffset? start,
        DateTimeOffset? end,
        BuildStepCompletionMetadata? completion,
        BuildModelValidationError error) =>
        BuildTestAssertions.Failure(
            BuildStep.Create(
                BuildTestAssertions.StepId(),
                BuildTestAssertions.JobId(),
                BuildTestAssertions.StepType(),
                BuildJobState.Inspecting,
                status,
                0,
                start,
                end,
                completion),
            error);

    private static void AssertMetadataFailure(
        IEnumerable<string?>? inputs,
        IEnumerable<string?>? outputs,
        IEnumerable<string?>? tools,
        IEnumerable<string?>? logs,
        BuildModelValidationError error) =>
        BuildTestAssertions.Failure(
            BuildStepCompletionMetadata.Create(inputs, outputs, tools, logs),
            error);
}
