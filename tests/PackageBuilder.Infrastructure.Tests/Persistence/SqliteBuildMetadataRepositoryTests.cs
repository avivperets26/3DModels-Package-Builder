using Microsoft.Data.Sqlite;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Validation;
using PackageBuilder.Infrastructure.Persistence;

namespace PackageBuilder.Infrastructure.Tests.Persistence;

[Collection(SqliteMigrationFixture.Name)]
public sealed class SqliteBuildMetadataRepositoryTests
{
    private static DateTimeOffset CreatedAt { get; } =
        new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task JobsCanBeCreatedQueriedResumedFailedAndCancelled()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedBuildJob failedJob = Job("job-failed");
        PersistedBuildJob cancelledJob = Job("job-cancelled", CreatedAt.AddMinutes(1));
        Assert.True((await repository.CreateAsync(failedJob, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await repository.CreateAsync(cancelledJob, TestContext.Current.CancellationToken)).IsSuccess);

        RepositoryOperationResult<PersistedBuildJob> preflight = await repository.TransitionAsync(
            failedJob.Id,
            BuildJobState.Queued,
            BuildJobState.Preflight,
            CreatedAt.AddMinutes(2),
            cancellationToken: TestContext.Current.CancellationToken);
        RepositoryOperationResult<PersistedBuildJob> failed = await repository.TransitionAsync(
            failedJob.Id,
            BuildJobState.Preflight,
            BuildJobState.Failed,
            CreatedAt.AddMinutes(3),
            "WORKER_FAILED",
            TestContext.Current.CancellationToken);
        RepositoryOperationResult<PersistedBuildJob> cancelled = await repository.TransitionAsync(
            cancelledJob.Id,
            BuildJobState.Queued,
            BuildJobState.Cancelled,
            CreatedAt.AddMinutes(3),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(preflight.IsSuccess);
        Assert.True(failed.IsSuccess);
        Assert.Equal(BuildJobState.Failed, failed.Value!.State);
        Assert.Equal("WORKER_FAILED", failed.Value.FailureCode);
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(BuildJobState.Cancelled, cancelled.Value!.State);
        RepositoryOperationResult<IReadOnlyList<PersistedBuildJob>> resumable =
            await repository.GetResumableAsync(TestContext.Current.CancellationToken);
        Assert.True(resumable.IsSuccess);
        Assert.Empty(resumable.Value!);
        RepositoryOperationResult<IReadOnlyList<PersistedBuildJob>> failedQuery =
            await repository.QueryAsync(BuildJobState.Failed, TestContext.Current.CancellationToken);
        Assert.Equal(["job-failed"], failedQuery.Value!.Select(value => value.Id.Value));
    }

    [Fact]
    public async Task ResumableQueryReturnsOnlyNonTerminalJobsInDeterministicOrder()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedBuildJob later = Job("job-b", CreatedAt.AddMinutes(1));
        PersistedBuildJob earlier = Job("job-a");
        Assert.True((await repository.CreateAsync(later, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await repository.CreateAsync(earlier, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await repository.TransitionAsync(
            later.Id,
            BuildJobState.Queued,
            BuildJobState.Cancelled,
            CreatedAt.AddMinutes(2),
            cancellationToken: TestContext.Current.CancellationToken)).IsSuccess);

        RepositoryOperationResult<IReadOnlyList<PersistedBuildJob>> result =
            await repository.GetResumableAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["job-a"], result.Value!.Select(value => value.Id.Value));
    }

    [Fact]
    public async Task ExpectedStateAndTimestampProtectConcurrentTransitions()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedBuildJob job = Job("job-concurrent");
        Assert.True((await repository.CreateAsync(job, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await repository.TransitionAsync(
            job.Id,
            BuildJobState.Queued,
            BuildJobState.Preflight,
            CreatedAt.AddMinutes(1),
            cancellationToken: TestContext.Current.CancellationToken)).IsSuccess);

        RepositoryOperationResult<PersistedBuildJob> stale = await repository.TransitionAsync(
            job.Id,
            BuildJobState.Queued,
            BuildJobState.Cancelled,
            CreatedAt.AddMinutes(2),
            cancellationToken: TestContext.Current.CancellationToken);
        RepositoryOperationResult<PersistedBuildJob> backwards = await repository.TransitionAsync(
            job.Id,
            BuildJobState.Preflight,
            BuildJobState.Failed,
            CreatedAt,
            "WORKER_FAILED",
            TestContext.Current.CancellationToken);

        Assert.False(stale.IsSuccess);
        Assert.Equal("REPOSITORY_STATE_CONFLICT", stale.Error!.Code);
        Assert.False(backwards.IsSuccess);
        Assert.Equal("REPOSITORY_INPUT_INVALID", backwards.Error!.Code);
    }

    [Fact]
    public async Task InvalidTransitionsAndFailureCodesAreRejectedBeforeStorage()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedBuildJob job = Job("job-invalid-transition");
        Assert.True((await repository.CreateAsync(job, TestContext.Current.CancellationToken)).IsSuccess);

        RepositoryOperationResult<PersistedBuildJob> skipped = await repository.TransitionAsync(
            job.Id,
            BuildJobState.Queued,
            BuildJobState.Completed,
            CreatedAt.AddMinutes(1),
            cancellationToken: TestContext.Current.CancellationToken);
        RepositoryOperationResult<PersistedBuildJob> missingCode = await repository.TransitionAsync(
            job.Id,
            BuildJobState.Queued,
            BuildJobState.Preflight,
            CreatedAt.AddMinutes(1),
            "UNEXPECTED_CODE",
            TestContext.Current.CancellationToken);

        Assert.Equal("REPOSITORY_INPUT_INVALID", skipped.Error!.Code);
        Assert.Equal("REPOSITORY_INPUT_INVALID", missingCode.Error!.Code);
        Assert.Equal(
            BuildJobState.Queued,
            (await repository.GetAsync(job.Id, TestContext.Current.CancellationToken)).Value!.State);
    }

    [Fact]
    public async Task ArtifactAndFindingMetadataRemainCorrelatedWithOwningJob()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedBuildJob job = Job("job-related");
        Assert.True((await repository.CreateAsync(job, TestContext.Current.CancellationToken)).IsSuccess);
        PersistedBuildArtifact artifact = Artifact(job.Id, "artifact-01");
        Assert.True((await ((IBuildArtifactRepository)repository).AddAsync(
            artifact,
            TestContext.Current.CancellationToken)).IsSuccess);
        PersistedValidationFinding finding = Finding(job.Id, artifact.Id, "finding-01");
        Assert.True((await ((IValidationFindingRepository)repository).AddAsync(
            finding,
            TestContext.Current.CancellationToken)).IsSuccess);

        RepositoryOperationResult<IReadOnlyList<PersistedBuildArtifact>> artifacts =
            await ((IBuildArtifactRepository)repository).GetByJobAsync(
                job.Id,
                TestContext.Current.CancellationToken);
        RepositoryOperationResult<IReadOnlyList<PersistedValidationFinding>> findings =
            await ((IValidationFindingRepository)repository).GetByJobAsync(
                job.Id,
                TestContext.Current.CancellationToken);

        PersistedBuildArtifact storedArtifact = Assert.Single(artifacts.Value!);
        Assert.Equal(artifact, storedArtifact);
        PersistedValidationFinding storedFinding = Assert.Single(findings.Value!);
        Assert.Equal(finding, storedFinding);
        Assert.Equal(storedArtifact.Id, storedFinding.Finding.RelatedArtifactId);
    }

    [Fact]
    public async Task CrossJobArtifactFindingCorrelationIsRejected()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedBuildJob first = Job("job-first");
        PersistedBuildJob second = Job("job-second", CreatedAt.AddMinutes(1));
        Assert.True((await repository.CreateAsync(first, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await repository.CreateAsync(second, TestContext.Current.CancellationToken)).IsSuccess);
        PersistedBuildArtifact artifact = Artifact(first.Id, "artifact-first");
        Assert.True((await ((IBuildArtifactRepository)repository).AddAsync(
            artifact,
            TestContext.Current.CancellationToken)).IsSuccess);

        RepositoryOperationResult result = await ((IValidationFindingRepository)repository).AddAsync(
            Finding(second.Id, artifact.Id, "finding-cross-job"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("REPOSITORY_INPUT_INVALID", result.Error!.Code);
    }

    [Fact]
    public async Task ToolUpsertAndApprovalQueriesAreDeterministic()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedToolInstallation blender = Tool("tool-blender", "Blender", approved: false);
        PersistedToolInstallation unity = Tool("tool-unity", "Unity", approved: true);
        Assert.True((await repository.UpsertAsync(blender, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await repository.UpsertAsync(unity, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await repository.UpsertAsync(
            blender with { IsApproved = true },
            TestContext.Current.CancellationToken)).IsSuccess);

        RepositoryOperationResult<IReadOnlyList<PersistedToolInstallation>> approved =
            await repository.QueryAsync(
                approved: true,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(approved.IsSuccess);
        Assert.Equal(["Blender", "Unity"], approved.Value!.Select(value => value.ToolName));
        Assert.All(approved.Value!, value => Assert.True(value.IsApproved));
    }

    [Fact]
    public async Task DuplicateAndMissingReferencesReturnSanitizedFailures()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedBuildJob job = Job("job-duplicate");
        Assert.True((await repository.CreateAsync(job, TestContext.Current.CancellationToken)).IsSuccess);

        RepositoryOperationResult duplicate = await repository.CreateAsync(
            job,
            TestContext.Current.CancellationToken);
        RepositoryOperationResult missingJobArtifact =
            await ((IBuildArtifactRepository)repository).AddAsync(
                Artifact(Id<BuildJobId>("missing-job"), "artifact-missing"),
                TestContext.Current.CancellationToken);

        Assert.Equal("REPOSITORY_JOB_CONFLICT", duplicate.Error!.Code);
        Assert.Equal("REPOSITORY_ARTIFACT_CONFLICT", missingJobArtifact.Error!.Code);
        Assert.DoesNotContain(workspace.Root, duplicate.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", missingJobArtifact.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CorruptStoredTimestampFailsClosedWithSanitizedDiagnostic()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedBuildJob job = Job("job-corrupt");
        Assert.True((await repository.CreateAsync(job, TestContext.Current.CancellationToken)).IsSuccess);
        workspace.Execute("UPDATE BuildJobs SET UpdatedAtUtc = 'not-a-timestamp' WHERE BuildJobId = 'job-corrupt';");

        RepositoryOperationResult<PersistedBuildJob?> result = await repository.GetAsync(
            job.Id,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("REPOSITORY_DATA_INVALID", result.Error!.Code);
        Assert.DoesNotContain("not-a-timestamp", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelledTokenPropagatesWithoutWriting()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        using CancellationTokenSource source = new();
        source.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.CreateAsync(Job("job-cancelled-token"), source.Token));

        Assert.Empty((await repository.QueryAsync(
            state: null,
            cancellationToken: TestContext.Current.CancellationToken)).Value!);
    }

    [Fact]
    public async Task JobInputValidationAndMissingJobAreExplicit()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedBuildJob baseline = Job("job-input");

        RepositoryOperationResult nonQueued = await repository.CreateAsync(
            baseline with { State = BuildJobState.Preflight },
            TestContext.Current.CancellationToken);
        RepositoryOperationResult invalidTime = await repository.CreateAsync(
            baseline with { UpdatedAtUtc = baseline.CreatedAtUtc.AddMinutes(-1) },
            TestContext.Current.CancellationToken);
        RepositoryOperationResult invalidProduct = await repository.CreateAsync(
            baseline with { ProductVersionId = " invalid " },
            TestContext.Current.CancellationToken);
        RepositoryOperationResult<PersistedBuildJob> missing = await repository.TransitionAsync(
            Id<BuildJobId>("missing-job"),
            BuildJobState.Queued,
            BuildJobState.Cancelled,
            CreatedAt.AddMinutes(1),
            cancellationToken: TestContext.Current.CancellationToken);
        RepositoryOperationResult<PersistedBuildJob?> absent = await repository.GetAsync(
            Id<BuildJobId>("missing-job"),
            TestContext.Current.CancellationToken);

        Assert.Equal("REPOSITORY_INPUT_INVALID", nonQueued.Error!.Code);
        Assert.Equal("REPOSITORY_INPUT_INVALID", invalidTime.Error!.Code);
        Assert.Equal("REPOSITORY_INPUT_INVALID", invalidProduct.Error!.Code);
        Assert.Equal("REPOSITORY_NOT_FOUND", missing.Error!.Code);
        Assert.True(absent.IsSuccess);
        Assert.Null(absent.Value);
    }

    [Fact]
    public async Task ArtifactFindingAndToolInputValidationRejectsUnsafeMetadata()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedBuildJob job = Job("job-unsafe");
        Assert.True((await repository.CreateAsync(job, TestContext.Current.CancellationToken)).IsSuccess);
        PersistedBuildArtifact artifact = Artifact(job.Id, "artifact-safe");

        RepositoryOperationResult unsafeArtifact =
            await ((IBuildArtifactRepository)repository).AddAsync(
                artifact with { LogicalReference = "../escape.bin" },
                TestContext.Current.CancellationToken);
        RepositoryOperationResult invalidHash =
            await ((IBuildArtifactRepository)repository).AddAsync(
                artifact with { Sha256 = new string('A', 64) },
                TestContext.Current.CancellationToken);
        RepositoryOperationResult invalidSize =
            await ((IBuildArtifactRepository)repository).AddAsync(
                artifact with { ByteCount = -1 },
                TestContext.Current.CancellationToken);
        RepositoryOperationResult unsafeTool = await repository.UpsertAsync(
            Tool("tool-unsafe", "Unsafe", approved: false) with
            {
                ExecutableReference = "C:\\outside.exe",
            },
            TestContext.Current.CancellationToken);
        RepositoryOperationResult invalidToolHash = await repository.UpsertAsync(
            Tool("tool-hash", "Hash", approved: false) with { Sha256 = "bad" },
            TestContext.Current.CancellationToken);
        PersistedValidationFinding finding = Finding(job.Id, artifact.Id, "finding-safe");
        RepositoryOperationResult invalidFinding =
            await ((IValidationFindingRepository)repository).AddAsync(
                finding with { Id = " invalid " },
                TestContext.Current.CancellationToken);

        Assert.All(
            [unsafeArtifact, invalidHash, invalidSize, unsafeTool, invalidToolHash, invalidFinding],
            result => Assert.Equal("REPOSITORY_INPUT_INVALID", result.Error!.Code));
    }

    [Fact]
    public async Task ArtifactStepMustBelongToSameJob()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedBuildJob first = Job("job-step-first");
        PersistedBuildJob second = Job("job-step-second", CreatedAt.AddMinutes(1));
        Assert.True((await repository.CreateAsync(first, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await repository.CreateAsync(second, TestContext.Current.CancellationToken)).IsSuccess);
        workspace.Execute("""
            INSERT INTO BuildSteps
                (BuildStepId, BuildJobId, StepOrder, Stage, Status)
            VALUES ('step-first', 'job-step-first', 0, 'preflight', 'pending');
            """);
        PersistedBuildArtifact artifact = Artifact(second.Id, "artifact-wrong-step") with
        {
            StepId = BuildStepId.Create("step-first").Value,
        };

        RepositoryOperationResult result =
            await ((IBuildArtifactRepository)repository).AddAsync(
                artifact,
                TestContext.Current.CancellationToken);

        Assert.Equal("REPOSITORY_INPUT_INVALID", result.Error!.Code);
    }

    [Fact]
    public async Task ToolUniqueIdentityConflictDoesNotOverwriteAnotherRecord()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedToolInstallation first = Tool("tool-one", "Blender", approved: false);
        PersistedToolInstallation second = first with { Id = "tool-two", IsApproved = true };
        Assert.True((await repository.UpsertAsync(first, TestContext.Current.CancellationToken)).IsSuccess);

        RepositoryOperationResult conflict = await repository.UpsertAsync(
            second,
            TestContext.Current.CancellationToken);
        RepositoryOperationResult<IReadOnlyList<PersistedToolInstallation>> all =
            await repository.QueryAsync(
                approved: null,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("REPOSITORY_TOOL_CONFLICT", conflict.Error!.Code);
        PersistedToolInstallation stored = Assert.Single(all.Value!);
        Assert.Equal("tool-one", stored.Id);
        Assert.False(stored.IsApproved);
    }

    [Fact]
    public async Task OptionalMetadataAndOwnedStepRoundTrip()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        workspace.Execute("""
            INSERT INTO Products
                (ProductId, DisplayName, AssetId, FolderName, ProductCase, CreatedAtUtc)
            VALUES
                ('product-01', 'Product', 'product-asset', 'product-folder', 'static',
                 '2026-08-04T12:00:00.0000000+00:00');
            INSERT INTO ProductVersions
                (ProductVersionId, ProductId, Version, ManifestReference, ManifestSha256, CreatedAtUtc)
            VALUES
                ('version-01', 'product-01', '1.0.0', 'manifest.json',
                 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
                 '2026-08-04T12:00:00.0000000+00:00');
            """);
        PersistedBuildJob job = Job("job-optional") with { ProductVersionId = "version-01" };
        Assert.True((await repository.CreateAsync(job, TestContext.Current.CancellationToken)).IsSuccess);
        workspace.Execute("""
            INSERT INTO BuildSteps
                (BuildStepId, BuildJobId, StepOrder, Stage, Status)
            VALUES ('step-owned', 'job-optional', 0, 'preflight', 'pending');
            """);
        PersistedBuildArtifact artifact = Artifact(job.Id, "artifact-optional") with
        {
            StepId = BuildStepId.Create("step-owned").Value,
            Target = null,
            Sha256 = null,
            ByteCount = null,
        };
        Assert.True((await ((IBuildArtifactRepository)repository).AddAsync(
            artifact,
            TestContext.Current.CancellationToken)).IsSuccess);
        PersistedValidationFinding finding = new(
            "finding-optional",
            job.Id,
            ValidationFinding.Create(
                FindingCode.Create("JOB_NOTICE").Value,
                FindingSeverity.Info,
                FindingExplanation.Create("The job has an informational finding.").Value,
                FindingSourceComponent.Create("repository-test").Value,
                null,
                null,
                blocksRelease: false).Value!,
            CreatedAt.AddMinutes(2));
        Assert.True((await ((IValidationFindingRepository)repository).AddAsync(
            finding,
            TestContext.Current.CancellationToken)).IsSuccess);
        PersistedToolInstallation tool = Tool("tool-no-hash", "NoHash", approved: false) with
        {
            Sha256 = null,
        };
        Assert.True((await repository.UpsertAsync(tool, TestContext.Current.CancellationToken)).IsSuccess);

        PersistedBuildJob storedJob = (await repository.GetAsync(
            job.Id,
            TestContext.Current.CancellationToken)).Value!;
        PersistedBuildArtifact storedArtifact = Assert.Single(
            (await ((IBuildArtifactRepository)repository).GetByJobAsync(
                job.Id,
                TestContext.Current.CancellationToken)).Value!);
        PersistedValidationFinding storedFinding = Assert.Single(
            (await ((IValidationFindingRepository)repository).GetByJobAsync(
                job.Id,
                TestContext.Current.CancellationToken)).Value!);
        PersistedToolInstallation storedTool = Assert.Single(
            (await repository.QueryAsync(
                approved: false,
                cancellationToken: TestContext.Current.CancellationToken)).Value!);

        Assert.Equal("version-01", storedJob.ProductVersionId);
        Assert.Equal(artifact, storedArtifact);
        Assert.Equal(finding, storedFinding);
        Assert.Equal(tool, storedTool);
    }

    [Fact]
    public async Task AdditionalTimestampAndRequiredValueGuardsFailClosed()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteBuildMetadataRepository repository = CreateRepository(workspace);
        PersistedBuildJob job = Job("job-guards");
        RepositoryOperationResult initialFailure = await repository.CreateAsync(
            job with { FailureCode = "FAILED_EARLY" },
            TestContext.Current.CancellationToken);
        RepositoryOperationResult nonUtcJob = await repository.CreateAsync(
            job with { CreatedAtUtc = CreatedAt.ToOffset(TimeSpan.FromHours(1)) },
            TestContext.Current.CancellationToken);
        Assert.True((await repository.CreateAsync(job, TestContext.Current.CancellationToken)).IsSuccess);
        RepositoryOperationResult<PersistedBuildJob> nonUtcTransition =
            await repository.TransitionAsync(
                job.Id,
                BuildJobState.Queued,
                BuildJobState.Cancelled,
                CreatedAt.ToOffset(TimeSpan.FromHours(1)),
                cancellationToken: TestContext.Current.CancellationToken);
        RepositoryOperationResult<PersistedBuildJob> malformedFailure =
            await repository.TransitionAsync(
                job.Id,
                BuildJobState.Queued,
                BuildJobState.Preflight,
                CreatedAt.AddMinutes(1),
                cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(malformedFailure.IsSuccess);
        malformedFailure = await repository.TransitionAsync(
            job.Id,
            BuildJobState.Preflight,
            BuildJobState.Failed,
            CreatedAt.AddMinutes(2),
            "not-a-code",
            TestContext.Current.CancellationToken);
        RepositoryOperationResult invalidArtifactTime =
            await ((IBuildArtifactRepository)repository).AddAsync(
                Artifact(job.Id, "artifact-time") with
                {
                    CreatedAtUtc = CreatedAt.ToOffset(TimeSpan.FromHours(1)),
                },
                TestContext.Current.CancellationToken);
        RepositoryOperationResult invalidFindingTime =
            await ((IValidationFindingRepository)repository).AddAsync(
                Finding(job.Id, Id<BuildArtifactId>("artifact-time"), "finding-time") with
                {
                    CreatedAtUtc = CreatedAt.ToOffset(TimeSpan.FromHours(1)),
                },
                TestContext.Current.CancellationToken);
        RepositoryOperationResult invalidToolTime = await repository.UpsertAsync(
            Tool("tool-time", "Time", approved: false) with
            {
                DiscoveredAtUtc = CreatedAt.ToOffset(TimeSpan.FromHours(1)),
            },
            TestContext.Current.CancellationToken);

        Assert.All(
            [
                initialFailure,
                nonUtcJob,
                RepositoryOperationResult.Failure(
                    nonUtcTransition.Error!.Code,
                    nonUtcTransition.Error.Message),
                RepositoryOperationResult.Failure(
                    malformedFailure.Error!.Code,
                    malformedFailure.Error.Message),
                invalidArtifactTime,
                invalidFindingTime,
                invalidToolTime,
            ],
            result => Assert.Equal("REPOSITORY_INPUT_INVALID", result.Error!.Code));
    }

    [Fact]
    public void RepositoryCreationRejectsOutsideMissingAndUnsupportedDatabases()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        string outside = Path.Combine(Path.GetDirectoryName(workspace.Root)!, "outside.db");
        RepositoryOperationResult<SqliteBuildMetadataRepository> missing =
            SqliteBuildMetadataRepository.Create(workspace.Root, workspace.DatabasePath);
        File.WriteAllText(outside, string.Empty);
        RepositoryOperationResult<SqliteBuildMetadataRepository> escaped =
            SqliteBuildMetadataRepository.Create(workspace.Root, outside);
        Assert.True(new SqliteDatabaseMigrator().Migrate(
            workspace.Root,
            workspace.DatabasePath,
            workspace.BackupDirectory,
            TestContext.Current.CancellationToken).IsSuccess);
        workspace.Execute("PRAGMA user_version = 3;");
        RepositoryOperationResult<SqliteBuildMetadataRepository> unsupported =
            SqliteBuildMetadataRepository.Create(workspace.Root, workspace.DatabasePath);
        workspace.Execute("PRAGMA user_version = 2; DROP TABLE Settings;");
        RepositoryOperationResult<SqliteBuildMetadataRepository> incomplete =
            SqliteBuildMetadataRepository.Create(workspace.Root, workspace.DatabasePath);
        File.Delete(outside);

        Assert.Equal("REPOSITORY_PATH_INVALID", missing.Error!.Code);
        Assert.Equal("REPOSITORY_PATH_INVALID", escaped.Error!.Code);
        Assert.Equal("REPOSITORY_SCHEMA_UNSUPPORTED", unsupported.Error!.Code);
        Assert.Equal("REPOSITORY_SCHEMA_INVALID", incomplete.Error!.Code);
    }

    private static SqliteBuildMetadataRepository CreateRepository(
        SqliteMigrationTestWorkspace workspace)
    {
        SqliteMigrationResult migration = new SqliteDatabaseMigrator().Migrate(
            workspace.Root,
            workspace.DatabasePath,
            workspace.BackupDirectory,
            TestContext.Current.CancellationToken);
        Assert.True(migration.IsSuccess);
        RepositoryOperationResult<SqliteBuildMetadataRepository> result =
            SqliteBuildMetadataRepository.Create(workspace.Root, workspace.DatabasePath);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static PersistedBuildJob Job(string id, DateTimeOffset? createdAt = null)
    {
        DateTimeOffset timestamp = createdAt ?? CreatedAt;
        return new PersistedBuildJob(
            Id<BuildJobId>(id),
            null,
            BuildJobState.Queued,
            timestamp,
            timestamp,
            null);
    }

    private static PersistedBuildArtifact Artifact(BuildJobId jobId, string id) =>
        new(
            Id<BuildArtifactId>(id),
            jobId,
            null,
            BuildArtifactRole.Create("validation-report").Value!,
            BuildTarget.Portable,
            $"artifacts/{id}.json",
            new string('a', 64),
            128,
            BuildArtifactLifecycleState.Validated,
            CreatedAt.AddMinutes(1));

    private static PersistedValidationFinding Finding(
        BuildJobId jobId,
        BuildArtifactId artifactId,
        string id) =>
        new(
            id,
            jobId,
            ValidationFinding.Create(
                FindingCode.Create("ARTIFACT_INVALID").Value,
                FindingSeverity.Error,
                FindingExplanation.Create("The artifact failed validation.").Value,
                FindingSourceComponent.Create("repository-test").Value,
                artifactId,
                CorrectiveAction.Create("Rebuild the artifact.").Value,
                blocksRelease: true).Value!,
            CreatedAt.AddMinutes(2));

    private static PersistedToolInstallation Tool(string id, string name, bool approved) =>
        new(
            id,
            name,
            "1.0.0",
            $"tools/{name.ToLowerInvariant()}/{name}.exe",
            new string('b', 64),
            approved,
            CreatedAt);

    private static T Id<T>(string value)
        where T : class =>
        typeof(T) == typeof(BuildJobId)
            ? (BuildJobId.Create(value).Value as T)!
            : typeof(T) == typeof(BuildArtifactId)
            ? (BuildArtifactId.Create(value).Value as T)!
            : throw new InvalidOperationException("Unsupported test identity type.");
}
