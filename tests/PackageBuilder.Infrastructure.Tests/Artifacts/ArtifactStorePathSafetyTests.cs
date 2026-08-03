using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Infrastructure.Artifacts;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

public sealed class ArtifactStorePathSafetyTests
{
    [Fact]
    public async Task StoreRejectsRootsOutsideProjectAndMissingProject()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        string source = workspace.AddSource("asset.bin", "content"u8);
        var store = new ArtifactStore();
        string outside = Path.GetPathRoot(workspace.ProjectRoot)!;
        var outsideRequest = new ArtifactStoreStageRequest(
            workspace.ProjectRoot,
            outside,
            source,
            ArtifactStoreTestWorkspace.Artifact("Job", "Artifact"));
        var missingProjectRequest = new ArtifactStoreStageRequest(
            Path.Combine(workspace.Root, "missing"),
            Path.Combine(workspace.Root, "missing", "artifacts"),
            source,
            ArtifactStoreTestWorkspace.Artifact("Job", "Artifact"));

        ArtifactStoreOperationResult<ArtifactStoreRecord> outsideResult = await store.StageAsync(
            outsideRequest,
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> missingResult = await store.StageAsync(
            missingProjectRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_STORE_OUTSIDE_PROJECT", Assert.Single(outsideResult.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_PROJECT_MISSING", Assert.Single(missingResult.Failures).Code);
    }

    [Fact]
    public async Task StoreRejectsMissingOutsideAndStoreContainedSources()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var store = new ArtifactStore();
        string missing = Path.Combine(workspace.SourceRoot, "missing.bin");
        string outside = Path.Combine(Path.GetPathRoot(workspace.ProjectRoot)!, "outside.bin");
        _ = Directory.CreateDirectory(workspace.StoreRoot);
        string inStore = Path.Combine(workspace.StoreRoot, "source.bin");
        await File.WriteAllTextAsync(inStore, "content", TestContext.Current.CancellationToken);

        ArtifactStoreOperationResult<ArtifactStoreRecord> missingResult = await store.StageAsync(
            workspace.StageRequest(missing),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> outsideResult = await store.StageAsync(
            workspace.StageRequest(outside),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> storeResult = await store.StageAsync(
            workspace.StageRequest(inStore),
            TestContext.Current.CancellationToken);
        string systemFile = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
        ArtifactStoreOperationResult<ArtifactStoreRecord> systemResult = await store.StageAsync(
            workspace.StageRequest(systemFile),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_STORE_SOURCE_MISSING", Assert.Single(missingResult.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_SOURCE_MISSING", Assert.Single(outsideResult.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_SOURCE_LOCATION", Assert.Single(storeResult.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_SOURCE_LOCATION", Assert.Single(systemResult.Failures).Code);
    }

    [Fact]
    public async Task ReadRejectsMissingStoreAndIncompleteEntries()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var store = new ArtifactStore();

        ArtifactStoreOperationResult<ArtifactStoreRecord> missing = await store.ReadAsync(
            workspace.ReadRequest(),
            TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(workspace.StoreRoot);
        ArtifactStoreLayout layout = ArtifactStoreLayoutFactory.Create(
            workspace.StoreRoot,
            ArtifactStoreTestWorkspace.JobId("Job-01"),
            ArtifactStoreTestWorkspace.ArtifactId("Artifact-01")).Value!;
        _ = Directory.CreateDirectory(layout.ArtifactRoot);
        await File.WriteAllTextAsync(layout.PayloadPath, "content", TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> incomplete = await store.ReadAsync(
            workspace.ReadRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_STORE_MISSING", Assert.Single(missing.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_ENTRY_INCOMPLETE", Assert.Single(incomplete.Failures).Code);
    }

    [Fact]
    public async Task StoreRejectsInvalidAndNonCanonicalOperationPaths()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var store = new ArtifactStore();
        BuildArtifact artifact = ArtifactStoreTestWorkspace.Artifact("Job", "Artifact");
        string source = workspace.AddSource("asset.bin", "content"u8);

        ArtifactStoreOperationResult<ArtifactStoreRecord> invalidRoot = await store.StageAsync(
            new ArtifactStoreStageRequest("relative", workspace.StoreRoot, source, artifact),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> invalidStore = await store.StageAsync(
            new ArtifactStoreStageRequest(workspace.ProjectRoot, "relative", source, artifact),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> nonCanonicalStore = await store.StageAsync(
            new ArtifactStoreStageRequest(
                workspace.ProjectRoot,
                Path.Combine(workspace.Root, "folder", "..", "store"),
                source,
                artifact),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> invalidSource = await store.StageAsync(
            new ArtifactStoreStageRequest(workspace.ProjectRoot, workspace.StoreRoot, "relative", artifact),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> equalRoot = await store.StageAsync(
            new ArtifactStoreStageRequest(workspace.ProjectRoot, workspace.ProjectRoot, source, artifact),
            TestContext.Current.CancellationToken);
        string tooLong = Path.GetPathRoot(workspace.ProjectRoot)! + new string('a', 40_000);
        ArtifactStoreOperationResult<ArtifactStoreRecord> tooLongRoot = await store.StageAsync(
            new ArtifactStoreStageRequest(tooLong, workspace.StoreRoot, source, artifact),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_STORE_PATH_INVALID", Assert.Single(invalidRoot.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_PATH_INVALID", Assert.Single(invalidStore.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_PATH_NOT_CANONICAL", Assert.Single(nonCanonicalStore.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_PATH_INVALID", Assert.Single(invalidSource.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_OUTSIDE_PROJECT", Assert.Single(equalRoot.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_PATH_INVALID", Assert.Single(tooLongRoot.Failures).Code);
    }

    [Fact]
    public async Task MetadataSizeAndFilesystemErrorsAreStructured()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var store = new ArtifactStore();
        ArtifactStoreRecord staged = (await store.StageAsync(
            workspace.StageRequest(workspace.AddSource("asset.bin", "content"u8)),
            TestContext.Current.CancellationToken)).Value!;
        await File.WriteAllBytesAsync(staged.Layout.MetadataPath, [], TestContext.Current.CancellationToken);

        ArtifactStoreOperationResult<ArtifactStoreRecord> empty = await store.ReadAsync(
            workspace.ReadRequest(),
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            staged.Layout.MetadataPath,
            new byte[65_537],
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> large = await store.ReadAsync(
            workspace.ReadRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_STORE_IO_FAILED", Assert.Single(empty.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_IO_FAILED", Assert.Single(large.Failures).Code);
    }

    [Fact]
    public async Task LockedFilesAndBlockedMetadataReplacementReturnIoFailures()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var store = new ArtifactStore();
        string source = workspace.AddSource("asset.bin", "content"u8);
        await using (var sourceLock = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            ArtifactStoreOperationResult<ArtifactStoreRecord> stage = await store.StageAsync(
                workspace.StageRequest(source),
                TestContext.Current.CancellationToken);
            Assert.Equal("ARTIFACT_STORE_IO_FAILED", Assert.Single(stage.Failures).Code);
        }

        using var second = new ArtifactStoreTestWorkspace();
        ArtifactStoreRecord staged = (await store.StageAsync(
            second.StageRequest(second.AddSource("asset.bin", "content"u8)),
            TestContext.Current.CancellationToken)).Value!;
        await using (var metadataLock = new FileStream(
            staged.Layout.MetadataPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None))
        {
            ArtifactStoreOperationResult<ArtifactStoreRecord> read = await store.ReadAsync(
                second.ReadRequest(),
                TestContext.Current.CancellationToken);
            Assert.Equal("ARTIFACT_STORE_IO_FAILED", Assert.Single(read.Failures).Code);
        }

        _ = Directory.CreateDirectory(staged.Layout.MetadataPath + ".next");
        ArtifactStoreOperationResult<ArtifactStoreRecord> transition = await store.TransitionAsync(
            second.TransitionRequest(BuildArtifactLifecycleState.Staged, BuildArtifactLifecycleState.Validated),
            TestContext.Current.CancellationToken);
        Assert.Equal("ARTIFACT_STORE_IO_FAILED", Assert.Single(transition.Failures).Code);
    }

    [Fact]
    public async Task SourceSymbolicLinkIsRejectedAsAReparseBoundary()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        string target = workspace.AddSource("target.bin", "content"u8);
        string link = Path.Combine(workspace.SourceRoot, "link.bin");
        _ = File.CreateSymbolicLink(link, target);

        ArtifactStoreOperationResult<ArtifactStoreRecord> result = await new ArtifactStore().StageAsync(
            workspace.StageRequest(link),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_STORE_REPARSE_BOUNDARY", Assert.Single(result.Failures).Code);
    }
}
