using System.Globalization;
using System.Reflection;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Identifiers;
using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Domain.Tests.BuildJobs;

[Trait("Task", "PB-0108")]
public sealed class BuildArtifactJobTests
{
    [Theory]
    [InlineData(null, BuildModelValidationError.NullArtifactRole)]
    [InlineData("", BuildModelValidationError.EmptyArtifactRole)]
    [InlineData(" ", BuildModelValidationError.WhitespaceOnlyArtifactRole)]
    [InlineData("Output", BuildModelValidationError.MalformedArtifactRole)]
    [InlineData("output--file", BuildModelValidationError.MalformedArtifactRole)]
    [InlineData("output_file", BuildModelValidationError.MalformedArtifactRole)]
    public void ArtifactRoleRejectsMalformedValues(
        string? value,
        BuildModelValidationError error) =>
        BuildTestAssertions.Failure(BuildArtifactRole.Create(value), error);

    [Fact]
    public void ArtifactRoleHasOrdinalStableValueSemantics()
    {
        BuildArtifactRole role = BuildTestAssertions.Role();
        Assert.Equal("normalized-model", role.CanonicalIdentifier);
        Assert.Equal("normalized-model", role.ToString());
        Assert.Equal(role, BuildTestAssertions.Role());
        Assert.True(role.Equals((object)BuildTestAssertions.Role()));
        Assert.NotEqual(role, BuildTestAssertions.Role("validation-log"));
        Assert.Equal(role.GetHashCode(), BuildTestAssertions.Role().GetHashCode());
        Assert.False(role.Equals((BuildArtifactRole?)null));
        Assert.False(role.Equals(new object()));
    }

    [Fact]
    public void ArtifactLifecycleStatesAreExplicitParseableAndImmutable()
    {
        Assert.Equal(
            ["staged", "validated", "promoted"],
            BuildArtifactLifecycleState.All.Select(value => value.CanonicalIdentifier));
        Assert.Same(
            BuildArtifactLifecycleState.Validated,
            BuildArtifactLifecycleState.TryParse("validated").Value);
        Assert.Equal(
            CanonicalIdentifierParseError.Unknown,
            BuildArtifactLifecycleState.TryParse("unknown").Error);
        Assert.Equal(
            CanonicalIdentifierParseError.Malformed,
            BuildArtifactLifecycleState.TryParse("Validated").Error);
        Assert.Equal("promoted", BuildArtifactLifecycleState.Promoted.ToString());
        Assert.True(
            BuildArtifactLifecycleState.Promoted.Equals(
                (object)BuildArtifactLifecycleState.Promoted));
        Assert.Equal(
            BuildArtifactLifecycleState.Promoted.GetHashCode(),
            BuildArtifactLifecycleState.TryParse("promoted").Value!.GetHashCode());
        Assert.False(
            BuildArtifactLifecycleState.Promoted.Equals(
                (BuildArtifactLifecycleState?)null));
        Assert.False(BuildArtifactLifecycleState.Promoted.Equals(new object()));
        BuildTestAssertions.AssertImmutable(
            BuildArtifactLifecycleState.All,
            BuildArtifactLifecycleState.Staged);
    }

    [Fact]
    public void ArtifactRetainsOwnershipRoleTargetReferenceAndLifecycle()
    {
        BuildArtifact artifact = BuildTestAssertions.Artifact(
            target: BuildTarget.Unity,
            state: BuildArtifactLifecycleState.Validated);

        Assert.Equal("Artifact-01", artifact.Id.Value);
        Assert.Equal("Job-01", artifact.JobId.Value);
        Assert.Equal("Step-01", artifact.StepId.Value);
        Assert.Equal("normalized-model", artifact.Role.CanonicalIdentifier);
        Assert.Same(BuildTarget.Unity, artifact.Target);
        Assert.Same(BuildArtifactLifecycleState.Validated, artifact.LifecycleState);
        Assert.Equal("targets/output.glb", artifact.LogicalReference);
        Assert.Equal(BuildTestAssertions.CreatedAt.AddMinutes(2), artifact.CreatedAtUtc);
        Assert.Equal(artifact.CreatedAtUtc, artifact.StateChangedAtUtc);
        Assert.Equal(artifact, BuildTestAssertions.Artifact(
            target: BuildTarget.Unity,
            state: BuildArtifactLifecycleState.Validated));
        Assert.True(artifact.Equals((object)BuildTestAssertions.Artifact(
            target: BuildTarget.Unity,
            state: BuildArtifactLifecycleState.Validated)));
        Assert.NotEqual(artifact, BuildTestAssertions.Artifact(target: BuildTarget.Unreal));
        Assert.Equal(
            artifact.GetHashCode(),
            BuildTestAssertions.Artifact(
                target: BuildTarget.Unity,
                state: BuildArtifactLifecycleState.Validated).GetHashCode());
        Assert.False(artifact.Equals((BuildArtifact?)null));
        Assert.False(artifact.Equals(new object()));
    }

    [Fact]
    public void ArtifactRejectsMissingRequiredValuesUnsafeReferencesAndInvalidTimes()
    {
        BuildArtifactId id = BuildTestAssertions.ArtifactId();
        BuildJobId job = BuildTestAssertions.JobId();
        BuildStepId step = BuildTestAssertions.StepId();
        BuildArtifactRole role = BuildTestAssertions.Role();
        DateTimeOffset utc = BuildTestAssertions.CreatedAt;

        AssertArtifactFailure(null, job, step, role, BuildArtifactLifecycleState.Staged, "a",
            utc, utc, BuildModelValidationError.NullArtifactId);
        AssertArtifactFailure(id, null, step, role, BuildArtifactLifecycleState.Staged, "a",
            utc, utc, BuildModelValidationError.NullJobId);
        AssertArtifactFailure(id, job, null, role, BuildArtifactLifecycleState.Staged, "a",
            utc, utc, BuildModelValidationError.NullStepId);
        AssertArtifactFailure(id, job, step, null, BuildArtifactLifecycleState.Staged, "a",
            utc, utc, BuildModelValidationError.NullArtifactRole);
        AssertArtifactFailure(id, job, step, role, null, "a",
            utc, utc, BuildModelValidationError.NullArtifactLifecycleState);
        AssertArtifactFailure(id, job, step, role, BuildArtifactLifecycleState.Staged, null,
            utc, utc, BuildModelValidationError.NullLogicalReference);
        AssertArtifactFailure(id, job, step, role, BuildArtifactLifecycleState.Staged, "",
            utc, utc, BuildModelValidationError.EmptyLogicalReference);
        AssertArtifactFailure(id, job, step, role, BuildArtifactLifecycleState.Staged, " ",
            utc, utc, BuildModelValidationError.WhitespaceOnlyLogicalReference);

        foreach (string unsafeReference in new[]
                 {
                     "/rooted",
                     @"back\slash",
                     "C:drive",
                     "uri:https",
                     "a//b",
                     "a/./b",
                     "a/../b",
                     "a/ leading",
                     "a/trailing ",
                     "a/\u0001",
                 })
        {
            AssertArtifactFailure(
                id,
                job,
                step,
                role,
                BuildArtifactLifecycleState.Staged,
                unsafeReference,
                utc,
                utc,
                BuildModelValidationError.UnsafeLogicalReference);
        }

        AssertArtifactFailure(
            id,
            job,
            step,
            role,
            BuildArtifactLifecycleState.Staged,
            "safe/reference",
            utc.ToOffset(TimeSpan.FromHours(1)),
            utc,
            BuildModelValidationError.TimestampNotUtc);
        AssertArtifactFailure(
            id,
            job,
            step,
            role,
            BuildArtifactLifecycleState.Staged,
            "safe/reference",
            utc,
            utc.AddTicks(-1),
            BuildModelValidationError.StateTimeBeforeCreationTime);
    }

    [Fact]
    public void JobStartsQueuedAndReturnsDeterministicallyOrderedImmutableSnapshots()
    {
        var steps = new List<BuildStep?>
        {
            BuildTestAssertions.Step("Second", order: 1),
            BuildTestAssertions.Step("First", order: 0),
        };
        var artifacts = new List<BuildArtifact?>
        {
            BuildTestAssertions.Artifact("Zed", stepId: "Second"),
            BuildTestAssertions.Artifact("Alpha", stepId: "First"),
        };
        BuildJob job = BuildTestAssertions.Job(steps, artifacts);
        steps.Clear();
        artifacts.Clear();

        Assert.Same(BuildJobState.Queued, job.State);
        Assert.Empty(job.TransitionHistory);
        Assert.Equal(["First", "Second"], job.Steps.Select(value => value.Id.Value));
        Assert.Equal(["Alpha", "Zed"], job.Artifacts.Select(value => value.Id.Value));
        BuildTestAssertions.AssertImmutable(
            job.Steps,
            BuildTestAssertions.Step("Other", order: 2));
        BuildTestAssertions.AssertImmutable(
            job.Artifacts,
            BuildTestAssertions.Artifact("Other", stepId: "First"));
    }

    [Fact]
    public void JobRejectsInvalidIdentityTimestampStepsAndDuplicateOrdering()
    {
        BuildJobId jobId = BuildTestAssertions.JobId();
        DateTimeOffset utc = BuildTestAssertions.CreatedAt;
        AssertJobFailure(null, utc, [], [], BuildModelValidationError.NullJobId);
        AssertJobFailure(
            jobId,
            utc.ToOffset(TimeSpan.FromHours(1)),
            [],
            [],
            BuildModelValidationError.TimestampNotUtc);
        AssertJobFailure(jobId, utc, null, [], BuildModelValidationError.NullSteps);
        AssertJobFailure(jobId, utc, [null], [], BuildModelValidationError.NullStep);
        AssertJobFailure(
            jobId,
            utc,
            [BuildTestAssertions.Step(jobId: "Other")],
            [],
            BuildModelValidationError.UnknownJobReference);
        AssertJobFailure(
            jobId,
            utc,
            [BuildTestAssertions.Step(), BuildTestAssertions.Step()],
            [],
            BuildModelValidationError.DuplicateStepId);
        AssertJobFailure(
            jobId,
            utc,
            [BuildTestAssertions.Step("One"), BuildTestAssertions.Step("Two")],
            [],
            BuildModelValidationError.DuplicateStepOrder);
        AssertJobFailure(
            jobId,
            utc,
            [
                BuildTestAssertions.Step(
                    status: BuildStepStatus.Running,
                    start: utc.AddTicks(-1)),
            ],
            [],
            BuildModelValidationError.TimestampBeforeJobCreation);
    }

    [Fact]
    public void JobRejectsInvalidArtifactOwnershipDuplicatesAndUnknownReferences()
    {
        BuildJobId jobId = BuildTestAssertions.JobId();
        BuildStep step = BuildTestAssertions.Step();
        DateTimeOffset utc = BuildTestAssertions.CreatedAt;
        AssertJobFailure(jobId, utc, [step], null, BuildModelValidationError.NullArtifacts);
        AssertJobFailure(jobId, utc, [step], [null], BuildModelValidationError.NullArtifact);
        AssertJobFailure(
            jobId,
            utc,
            [step],
            [BuildTestAssertions.Artifact(jobId: "Other")],
            BuildModelValidationError.UnknownJobReference);
        AssertJobFailure(
            jobId,
            utc,
            [step],
            [BuildTestAssertions.Artifact(stepId: "Unknown")],
            BuildModelValidationError.UnknownStepReference);
        AssertJobFailure(
            jobId,
            utc,
            [step],
            [BuildTestAssertions.Artifact(), BuildTestAssertions.Artifact()],
            BuildModelValidationError.DuplicateArtifactId);
        AssertJobFailure(
            jobId,
            utc,
            [step],
            [
                BuildTestAssertions.Artifact(
                    created: utc.AddTicks(-1),
                    changed: utc.AddTicks(-1)),
            ],
            BuildModelValidationError.TimestampBeforeJobCreation);
    }

    [Fact]
    public void JobArtifactAndCollectionsUseOrdinalCultureIndependentEqualityAndHashing()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            BuildStep step = BuildTestAssertions.Step();
            BuildJob first = BuildTestAssertions.Job(
                [step],
                [BuildTestAssertions.Artifact()]);
            int jobHash = first.GetHashCode();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            BuildJob second = BuildTestAssertions.Job(
                [BuildTestAssertions.Step()],
                [BuildTestAssertions.Artifact()]);

            Assert.Equal(first, second);
            Assert.True(first.Equals((object)second));
            Assert.Equal(jobHash, second.GetHashCode());
            Assert.NotEqual(
                first,
                BuildTestAssertions.Job(
                    [BuildTestAssertions.Step("step-01")],
                    [BuildTestAssertions.Artifact(stepId: "step-01")]));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void DomainBuildModelsHaveNoForbiddenDependenciesOrHiddenClockAndIo()
    {
        Assembly assembly = typeof(BuildJob).Assembly;
        string[] references =
        [
            .. assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty),
        ];
        Assert.DoesNotContain(references, name => name.Contains("Wpf", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Unity", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Unreal", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Blender", StringComparison.Ordinal));
        Assert.DoesNotContain(
            references,
            name => name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            references,
            name => name.Contains("Marketplaces", StringComparison.Ordinal));

        string repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string buildsRoot = Path.Combine(
            repositoryRoot,
            "src",
            "PackageBuilder.Domain",
            "BuildJobs");
        string productionText = string.Join(
            "\n",
            Directory.GetFiles(buildsRoot, "*.cs")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        Assert.DoesNotContain("DateTime.UtcNow", productionText, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTimeOffset.UtcNow", productionText, StringComparison.Ordinal);
        Assert.DoesNotContain("System.IO", productionText, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", productionText, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidationFinding", productionText, StringComparison.Ordinal);
        Assert.DoesNotContain("SHA256", productionText, StringComparison.Ordinal);
    }

    private static void AssertArtifactFailure(
        BuildArtifactId? id,
        BuildJobId? jobId,
        BuildStepId? stepId,
        BuildArtifactRole? role,
        BuildArtifactLifecycleState? state,
        string? reference,
        DateTimeOffset created,
        DateTimeOffset changed,
        BuildModelValidationError error) =>
        BuildTestAssertions.Failure(
            BuildArtifact.Create(
                id,
                jobId,
                stepId,
                role,
                null,
                state,
                reference,
                created,
                changed),
            error);

    private static void AssertJobFailure(
        BuildJobId? id,
        DateTimeOffset created,
        IEnumerable<BuildStep?>? steps,
        IEnumerable<BuildArtifact?>? artifacts,
        BuildModelValidationError error) =>
        BuildTestAssertions.Failure(BuildJob.Create(id, created, steps, artifacts), error);
}
