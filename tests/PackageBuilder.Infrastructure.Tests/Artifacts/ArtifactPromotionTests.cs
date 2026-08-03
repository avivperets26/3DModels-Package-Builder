using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Infrastructure.Artifacts;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

public sealed class ArtifactPromotionTests
{
    [Fact]
    public async Task OnlyValidatedArtifactPublishesAtomicallyAndTransitionsToPromoted()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageOnlyAsync();
        var service = new AtomicArtifactPromotionService();

        ArtifactPromotionOperationResult staged = await service.PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);
        _ = await workspace.Store.TransitionAsync(
            new ArtifactStoreTransitionRequest(
                workspace.ProjectRoot,
                workspace.ArtifactsRoot,
                ArtifactStoreTestWorkspace.JobId("Job-01"),
                ArtifactStoreTestWorkspace.ArtifactId("Artifact-01"),
                BuildArtifactLifecycleState.Staged,
                BuildArtifactLifecycleState.Validated,
                ArtifactPromotionTestWorkspace.Utc(12, 1)),
            TestContext.Current.CancellationToken);
        ArtifactPromotionOperationResult promoted = await service.PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_STATE_INVALID", Assert.Single(staged.Failures).Code);
        Assert.True(promoted.IsSuccess);
        Assert.Equal(BuildArtifactLifecycleState.Promoted, promoted.Value!.ArtifactRecord.Artifact.LifecycleState);
        Assert.Equal("packages/Silverwing.zip", promoted.Value.ReleaseRelativePath);
        Assert.Equal(
            "validated-package-content"u8.ToArray(),
            await File.ReadAllBytesAsync(promoted.Value.ReleasePath, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(Path.Combine(promoted.Value.ArtifactRecord.Layout.ArtifactRoot, "promotion.json")));
        Assert.Empty(Directory.GetFiles(
            Path.Combine(workspace.ArtifactsRoot, ".packagebuilder-promotion"),
            "*.partial",
            SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ExistingReleaseIsPreservedAndCollisionGetsDeterministicVersion()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        string original = Path.Combine(workspace.BuildsRoot, "packages", "Silverwing.zip");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(original)!);
        await File.WriteAllTextAsync(original, "existing", TestContext.Current.CancellationToken);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.CollisionVersion);
        Assert.Equal("packages/Silverwing (2).zip", result.Value.ReleaseRelativePath);
        Assert.Equal("existing", await File.ReadAllTextAsync(original, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(result.Value.ReleasePath));
    }

    [Fact]
    public async Task InterruptedAfterAtomicRenameRecoversWithoutDuplicateRelease()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        var store = new FailFirstTransitionStore(workspace.Store);
        var service = new AtomicArtifactPromotionService(store, new ArtifactHashService());

        ArtifactPromotionOperationResult interrupted = await service.PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);
        ArtifactPromotionOperationResult recovered = await service.PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("SIMULATED_TRANSITION_FAILURE", Assert.Single(interrupted.Failures).Code);
        Assert.True(File.Exists(Path.Combine(workspace.BuildsRoot, "packages", "Silverwing.zip")));
        Assert.True(recovered.IsSuccess);
        Assert.True(recovered.Value!.Recovered);
        Assert.Equal(1, recovered.Value.CollisionVersion);
        Assert.False(File.Exists(Path.Combine(workspace.BuildsRoot, "packages", "Silverwing (2).zip")));
    }

    [Fact]
    public async Task InterruptedCompletePartialResumesWithoutExposingPartialRelease()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        using var cancellation = new CancellationTokenSource();
        var interruptedService = new AtomicArtifactPromotionService(
            workspace.Store,
            new CancelOnHashService(cancellation));

        ArtifactPromotionOperationResult interrupted = await interruptedService.PromoteAsync(
            workspace.Request(),
            cancellation.Token);
        string partial = Directory.GetFiles(
            Path.Combine(workspace.ArtifactsRoot, ".packagebuilder-promotion"),
            "*.partial",
            SearchOption.AllDirectories).Single();

        Assert.Equal("ARTIFACT_PROMOTION_CANCELLED", Assert.Single(interrupted.Failures).Code);
        Assert.True(File.Exists(partial));
        Assert.False(File.Exists(Path.Combine(workspace.BuildsRoot, "packages", "Silverwing.zip")));

        ArtifactPromotionOperationResult recovered = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.True(recovered.IsSuccess);
        Assert.True(recovered.Value!.Recovered);
        Assert.False(File.Exists(partial));
        Assert.True(File.Exists(recovered.Value.ReleasePath));
    }

    [Fact]
    public async Task CorruptInterruptedPartialIsRebuiltFromValidatedPayload()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        using var cancellation = new CancellationTokenSource();
        _ = await new AtomicArtifactPromotionService(
            workspace.Store,
            new CancelOnHashService(cancellation)).PromoteAsync(
                workspace.Request(),
                cancellation.Token);
        string partial = Directory.GetFiles(
            Path.Combine(workspace.ArtifactsRoot, ".packagebuilder-promotion"),
            "*.partial",
            SearchOption.AllDirectories).Single();
        await File.WriteAllTextAsync(partial, "corrupt", TestContext.Current.CancellationToken);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "validated-package-content"u8.ToArray(),
            await File.ReadAllBytesAsync(result.Value!.ReleasePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CollisionAppearingDuringPublishAdvancesJournalAndKeepsBothFiles()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        string collidingPath = Path.Combine(workspace.BuildsRoot, "packages", "Silverwing.zip");
        var hash = new CreateCollisionOnFirstHashService(collidingPath);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService(
            workspace.Store,
            hash).PromoteAsync(workspace.Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.CollisionVersion);
        Assert.Equal("racing-file", await File.ReadAllTextAsync(collidingPath, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(result.Value.ReleasePath));
    }

    [Fact]
    public async Task CompletedPromotionIsIdempotentAndDetectsChangedRelease()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        var service = new AtomicArtifactPromotionService();
        ArtifactPromotionOperationResult first = await service.PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);
        ArtifactPromotionOperationResult second = await service.PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(first.Value!.ReleasePath, "changed", TestContext.Current.CancellationToken);
        ArtifactPromotionOperationResult changed = await service.PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.True(second.IsSuccess);
        Assert.True(second.Value!.Recovered);
        Assert.Equal(first.Value.ReleasePath, second.Value.ReleasePath);
        Assert.Equal("ARTIFACT_PROMOTION_RELEASE_INTEGRITY", Assert.Single(changed.Failures).Code);
    }

    [Theory]
    [InlineData("CON.zip")]
    [InlineData("packages/name.")]
    [InlineData("packages/bad?.zip")]
    [InlineData("LPT1/file.zip")]
    public async Task NonPortableLogicalReferencesFailBeforeBuildOutput(string logicalReference)
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync(logicalReference);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_REFERENCE_UNSAFE", Assert.Single(result.Failures).Code);
        Assert.False(Directory.Exists(workspace.BuildsRoot));
    }

    [Theory]
    [InlineData("non-utc")]
    [InlineData("earlier")]
    public async Task InvalidPromotionTimesFailWithoutPublishing(string scenario)
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        DateTimeOffset time = scenario == "non-utc"
            ? new DateTimeOffset(2026, 8, 3, 14, 2, 0, TimeSpan.FromHours(2))
            : ArtifactPromotionTestWorkspace.Utc(12, 0);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(promotedAtUtc: time),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_TIME_INVALID", Assert.Single(result.Failures).Code);
        Assert.False(Directory.Exists(workspace.BuildsRoot));
    }

    [Fact]
    public async Task InvalidOrInconsistentJournalFailsClosed()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        ArtifactStoreRecord record = await workspace.StageAndValidateAsync();
        using var cancellation = new CancellationTokenSource();
        _ = await new AtomicArtifactPromotionService(
            workspace.Store,
            new CancelOnHashService(cancellation)).PromoteAsync(workspace.Request(), cancellation.Token);
        string journalPath = Path.Combine(record.Layout.ArtifactRoot, "promotion.json");
        JsonNode journal = JsonNode.Parse(await File.ReadAllTextAsync(
            journalPath,
            TestContext.Current.CancellationToken))!;
        journal["artifactId"] = "Another-Artifact";
        await File.WriteAllTextAsync(
            journalPath,
            journal.ToJsonString(),
            TestContext.Current.CancellationToken);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_JOURNAL_INVALID", Assert.Single(result.Failures).Code);
        Assert.False(File.Exists(Path.Combine(workspace.BuildsRoot, "packages", "Silverwing.zip")));
    }

    [Theory]
    [InlineData("not-object")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("schema-type")]
    [InlineData("schema-version")]
    [InlineData("job-type")]
    [InlineData("artifact-type")]
    [InlineData("release-type")]
    [InlineData("collision-type")]
    [InlineData("bytes-type")]
    [InlineData("sha-type")]
    [InlineData("empty")]
    [InlineData("oversized")]
    public async Task HostileJournalSyntaxFailsClosed(string scenario)
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        ArtifactStoreRecord record = await CreateInterruptedPromotionAsync(workspace);
        string journalPath = Path.Combine(record.Layout.ArtifactRoot, "promotion.json");
        string valid = await File.ReadAllTextAsync(journalPath, TestContext.Current.CancellationToken);
        string json = scenario switch
        {
            "not-object" => "[]",
            "malformed" => "{",
            "unknown" => valid.Replace("{", "{\"unknown\":true,", StringComparison.Ordinal),
            "duplicate" => valid.Replace("{", "{\"schemaVersion\":1,", StringComparison.Ordinal),
            "missing" => RemoveJsonProperty(valid, "jobId"),
            "schema-type" => ReplaceJsonProperty(valid, "schemaVersion", "\"one\""),
            "schema-version" => ReplaceJsonProperty(valid, "schemaVersion", "2"),
            "job-type" => ReplaceJsonProperty(valid, "jobId", "1"),
            "artifact-type" => ReplaceJsonProperty(valid, "artifactId", "1"),
            "release-type" => ReplaceJsonProperty(valid, "releaseRelativePath", "1"),
            "collision-type" => ReplaceJsonProperty(valid, "collisionVersion", "\"one\""),
            "bytes-type" => ReplaceJsonProperty(valid, "bytes", "\"many\""),
            "sha-type" => ReplaceJsonProperty(valid, "sha256", "1"),
            "empty" => string.Empty,
            _ => new string('x', 65_537),
        };
        await File.WriteAllTextAsync(journalPath, json, TestContext.Current.CancellationToken);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_JOURNAL_INVALID", Assert.Single(result.Failures).Code);
    }

    [Theory]
    [InlineData("job")]
    [InlineData("artifact")]
    [InlineData("collision-low")]
    [InlineData("collision-high")]
    [InlineData("bytes")]
    [InlineData("digest")]
    [InlineData("release-unsafe")]
    [InlineData("release-control")]
    [InlineData("release-mismatch")]
    public async Task InconsistentJournalFactsFailClosed(string scenario)
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        ArtifactStoreRecord record = await CreateInterruptedPromotionAsync(workspace);
        string journalPath = Path.Combine(record.Layout.ArtifactRoot, "promotion.json");
        JsonNode journal = JsonNode.Parse(await File.ReadAllTextAsync(
            journalPath,
            TestContext.Current.CancellationToken))!;
        switch (scenario)
        {
            case "job":
                journal["jobId"] = "Another-Job";
                break;
            case "artifact":
                journal["artifactId"] = "Another-Artifact";
                break;
            case "collision-low":
                journal["collisionVersion"] = 0;
                break;
            case "collision-high":
                journal["collisionVersion"] = 10_001;
                break;
            case "bytes":
                journal["bytes"] = record.ContentIdentity.Bytes + 1;
                break;
            case "digest":
                journal["sha256"] = new string('0', 64);
                break;
            case "release-unsafe":
                journal["releaseRelativePath"] = "CON.zip";
                break;
            case "release-control":
                journal["releaseRelativePath"] = "packages/bad\u0001.zip";
                break;
            default:
                journal["releaseRelativePath"] = "packages/Other.zip";
                break;
        }

        await File.WriteAllTextAsync(journalPath, journal.ToJsonString(), TestContext.Current.CancellationToken);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_JOURNAL_INVALID", Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task MissingJournalForPromotedArtifactFailsClosed()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        ArtifactPromotionOperationResult first = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);
        File.Delete(Path.Combine(first.Value!.ArtifactRecord.Layout.ArtifactRoot, "promotion.json"));

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_JOURNAL_MISSING", Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task InterruptedJournalWriteIsReplacedAtomicallyOnRetry()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        ArtifactStoreRecord record = await workspace.StageAndValidateAsync();
        string unfinishedJournal = Path.Combine(record.Layout.ArtifactRoot, "promotion.json.next");
        await File.WriteAllTextAsync(
            unfinishedJournal,
            "incomplete",
            TestContext.Current.CancellationToken);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(unfinishedJournal));
        Assert.True(File.Exists(Path.Combine(record.Layout.ArtifactRoot, "promotion.json")));
    }

    [Theory]
    [InlineData("outside-artifacts")]
    [InlineData("builds-equals-artifacts")]
    [InlineData("missing-artifacts")]
    [InlineData("relative-project")]
    [InlineData("empty-project")]
    public async Task InvalidRootRelationshipsFailClosed(string scenario)
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        string project = scenario switch
        {
            "relative-project" => ".",
            "empty-project" => string.Empty,
            _ => workspace.ProjectRoot,
        };
        string artifacts = scenario == "missing-artifacts"
            ? Path.Combine(workspace.Root, "missing")
            : workspace.ArtifactsRoot;
        string builds = scenario switch
        {
            "outside-artifacts" => Path.Combine(workspace.Root, "outside", "Builds"),
            "builds-equals-artifacts" => workspace.ArtifactsRoot,
            _ => workspace.BuildsRoot,
        };

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(projectRoot: project, artifactsRoot: artifacts, buildsRoot: builds),
            TestContext.Current.CancellationToken);

        Assert.StartsWith("ARTIFACT_PROMOTION_", Assert.Single(result.Failures).Code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingProjectAndInvalidCanonicalSyntaxFailBeforeStoreAccess()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        string missingProject = Path.Combine(workspace.Root, "missing-project");
        ArtifactPromotionOperationResult missing = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(
                projectRoot: missingProject,
                artifactsRoot: Path.Combine(missingProject, "artifacts"),
                buildsRoot: Path.Combine(missingProject, "artifacts", "Builds")),
            TestContext.Current.CancellationToken);
        ArtifactPromotionOperationResult malformed = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(projectRoot: workspace.ProjectRoot + '\0'),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_PROJECT_MISSING", Assert.Single(missing.Failures).Code);
        Assert.Equal("ARTIFACT_PROMOTION_PATH_INVALID", Assert.Single(malformed.Failures).Code);
    }

    [Fact]
    public async Task StoreReadFailureIsPreservedAndNoBuildRootIsCreated()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(artifactId: "Missing-Artifact"),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_STORE_ENTRY_INCOMPLETE", Assert.Single(result.Failures).Code);
        Assert.False(Directory.Exists(workspace.BuildsRoot));
    }

    [Fact]
    public async Task FilesystemCollisionAtBuildRootReturnsSanitizedIoFailure()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        await File.WriteAllTextAsync(workspace.BuildsRoot, "not-a-directory", TestContext.Current.CancellationToken);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_IO_FAILED", Assert.Single(result.Failures).Code);
        Assert.DoesNotContain(workspace.ProjectRoot, Assert.Single(result.Failures).Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReparseBuildBoundaryIsRejectedBeforePublication()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        string physical = Path.Combine(workspace.Root, "physical-builds");
        _ = Directory.CreateDirectory(physical);
        _ = Directory.CreateSymbolicLink(workspace.BuildsRoot, physical);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_IO_FAILED", Assert.Single(result.Failures).Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(physical));
    }

    [Fact]
    public async Task HashFailureAfterCopyDoesNotExposeARelease()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        var service = new AtomicArtifactPromotionService(workspace.Store, new FailedHashService());

        ArtifactPromotionOperationResult result = await service.PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_STAGING_INTEGRITY", Assert.Single(result.Failures).Code);
        Assert.False(File.Exists(Path.Combine(workspace.BuildsRoot, "packages", "Silverwing.zip")));
    }

    [Fact]
    public async Task RecoveryJournalCollisionMovesToNextVersion()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await CreateInterruptedPromotionAsync(workspace);
        string baseRelease = Path.Combine(workspace.BuildsRoot, "packages", "Silverwing.zip");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(baseRelease)!);
        await File.WriteAllTextAsync(baseRelease, "appeared-after-interruption", TestContext.Current.CancellationToken);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.CollisionVersion);
        Assert.Equal("appeared-after-interruption", await File.ReadAllTextAsync(
            baseRelease,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecoveryAtMaximumCollisionVersionFailsWithBoundedDiagnostic()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        ArtifactStoreRecord record = await CreateInterruptedPromotionAsync(workspace);
        string journalPath = Path.Combine(record.Layout.ArtifactRoot, "promotion.json");
        JsonNode journal = JsonNode.Parse(await File.ReadAllTextAsync(
            journalPath,
            TestContext.Current.CancellationToken))!;
        journal["collisionVersion"] = 10_000;
        journal["releaseRelativePath"] = "packages/Silverwing (10000).zip";
        await File.WriteAllTextAsync(journalPath, journal.ToJsonString(), TestContext.Current.CancellationToken);
        string occupied = Path.Combine(workspace.BuildsRoot, "packages", "Silverwing (10000).zip");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(occupied)!);
        await File.WriteAllTextAsync(occupied, "occupied", TestContext.Current.CancellationToken);

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_COLLISIONS_EXHAUSTED", Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task RaceAtMaximumCollisionVersionFailsWithoutOverwritingRelease()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        ArtifactStoreRecord record = await CreateInterruptedPromotionAsync(workspace);
        string journalPath = Path.Combine(record.Layout.ArtifactRoot, "promotion.json");
        JsonNode journal = JsonNode.Parse(await File.ReadAllTextAsync(
            journalPath,
            TestContext.Current.CancellationToken))!;
        journal["collisionVersion"] = 10_000;
        journal["releaseRelativePath"] = "packages/Silverwing (10000).zip";
        await File.WriteAllTextAsync(journalPath, journal.ToJsonString(), TestContext.Current.CancellationToken);
        string collision = Path.Combine(workspace.BuildsRoot, "packages", "Silverwing (10000).zip");

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService(
            workspace.Store,
            new CreateCollisionOnFirstHashService(collision)).PromoteAsync(
                workspace.Request(),
                TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_COLLISIONS_EXHAUSTED", Assert.Single(result.Failures).Code);
        Assert.Equal("racing-file", await File.ReadAllTextAsync(collision, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NonReservedDeviceLikeNameRemainsPortable()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync("packages/COM0.zip");

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("packages/COM0.zip", result.Value!.ReleaseRelativePath);
    }

    [Fact]
    public async Task InitialCollisionSearchIsBounded()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        string directory = Path.Combine(workspace.BuildsRoot, "packages");
        _ = Directory.CreateDirectory(directory);
        for (int version = 1; version <= 10_000; version++)
        {
            string name = version == 1 ? "Silverwing.zip" : $"Silverwing ({version}).zip";
            using FileStream stream = File.Create(Path.Combine(directory, name));
        }

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_PROMOTION_COLLISIONS_EXHAUSTED", Assert.Single(result.Failures).Code);
    }

    private sealed class FailFirstTransitionStore(IArtifactStore inner) : IArtifactStore
    {
        private bool _failed;

        public Task<ArtifactStoreOperationResult<ArtifactStoreRecord>> StageAsync(
            ArtifactStoreStageRequest request,
            CancellationToken cancellationToken = default) =>
            inner.StageAsync(request, cancellationToken);

        public Task<ArtifactStoreOperationResult<ArtifactStoreRecord>> ReadAsync(
            ArtifactStoreReadRequest request,
            CancellationToken cancellationToken = default) =>
            inner.ReadAsync(request, cancellationToken);

        public Task<ArtifactStoreOperationResult<ArtifactStoreRecord>> TransitionAsync(
            ArtifactStoreTransitionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!_failed)
            {
                _failed = true;
                return Task.FromResult(ArtifactStoreOperationResult.Failed<ArtifactStoreRecord>(
                    new ArtifactStoreFailure(
                        "SIMULATED_TRANSITION_FAILURE",
                        "artifact",
                        "The simulated metadata transition failed.")));
            }

            return inner.TransitionAsync(request, cancellationToken);
        }
    }

    private sealed class CancelOnHashService(CancellationTokenSource cancellation) : IArtifactHashService
    {
        public Task<ArtifactHashOperationResult<ArtifactHashReceipt>> HashAsync(
            ArtifactHashRequest request,
            CancellationToken cancellationToken = default)
        {
            _ = request;
            cancellation.Cancel();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class CreateCollisionOnFirstHashService(string collisionPath) : IArtifactHashService
    {
        private readonly ArtifactHashService _inner = new();
        private bool _created;

        public Task<ArtifactHashOperationResult<ArtifactHashReceipt>> HashAsync(
            ArtifactHashRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!_created)
            {
                _created = true;
                _ = Directory.CreateDirectory(Path.GetDirectoryName(collisionPath)!);
                File.WriteAllText(collisionPath, "racing-file");
            }

            return _inner.HashAsync(request, cancellationToken);
        }
    }

    private sealed class FailedHashService : IArtifactHashService
    {
        public Task<ArtifactHashOperationResult<ArtifactHashReceipt>> HashAsync(
            ArtifactHashRequest request,
            CancellationToken cancellationToken = default)
        {
            _ = request;
            _ = cancellationToken;
            return Task.FromResult(ArtifactHashOperationResult.Failed<ArtifactHashReceipt>(
                new ArtifactHashFailure("SIMULATED_HASH_FAILURE", "staging", "Hashing failed safely.")));
        }
    }

    private static async Task<ArtifactStoreRecord> CreateInterruptedPromotionAsync(
        ArtifactPromotionTestWorkspace workspace)
    {
        ArtifactStoreRecord record = await workspace.StageAndValidateAsync();
        using var cancellation = new CancellationTokenSource();
        _ = await new AtomicArtifactPromotionService(
            workspace.Store,
            new CancelOnHashService(cancellation)).PromoteAsync(workspace.Request(), cancellation.Token);
        return record;
    }

    private static string RemoveJsonProperty(string json, string name)
    {
        JsonNode node = JsonNode.Parse(json)!;
        _ = node.AsObject().Remove(name);
        return node.ToJsonString();
    }

    private static string ReplaceJsonProperty(string json, string name, string rawValue)
    {
        JsonNode node = JsonNode.Parse(json)!;
        node[name] = JsonNode.Parse(rawValue);
        return node.ToJsonString();
    }
}
