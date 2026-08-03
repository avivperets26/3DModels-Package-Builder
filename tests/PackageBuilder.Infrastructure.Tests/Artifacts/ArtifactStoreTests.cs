using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Infrastructure.Artifacts;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

public sealed class ArtifactStoreTests
{
    private static readonly string[] _metadataFailureCodes =
    [
        "ARTIFACT_STORE_METADATA_INVALID",
        "ARTIFACT_STORE_METADATA_VERSION",
    ];

    [Fact]
    public async Task StagePersistsPayloadTypedMetadataHashSizeRoleAndLifecycle()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        byte[] content = "portable-package-content"u8.ToArray();
        string source = workspace.AddSource("Silverwing.zip", content);
        var store = new ArtifactStore();

        ArtifactStoreOperationResult<ArtifactStoreRecord> staged = await store.StageAsync(
            workspace.StageRequest(source),
            TestContext.Current.CancellationToken);

        Assert.True(staged.IsSuccess);
        ArtifactStoreRecord value = staged.Value!;
        Assert.Equal(content.LongLength, value.ContentIdentity.Bytes);
        Assert.Equal(Sha256(content), value.ContentIdentity.Sha256.Value);
        Assert.Equal("portable-package", value.Artifact.Role.CanonicalIdentifier);
        Assert.Equal(BuildArtifactLifecycleState.Staged, value.Artifact.LifecycleState);
        Assert.Equal("Job-01", value.Artifact.JobId.Value);
        Assert.Equal("Artifact-01", value.Artifact.Id.Value);
        Assert.Equal(content, await File.ReadAllBytesAsync(value.Layout.PayloadPath, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(value.Layout.MetadataPath));
        Assert.False(File.Exists(value.Layout.MetadataPath + ".next"));

        using var metadata = JsonDocument.Parse(await File.ReadAllBytesAsync(
            value.Layout.MetadataPath,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, metadata.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("portable-package", metadata.RootElement.GetProperty("role").GetString());
        Assert.Equal("staged", metadata.RootElement.GetProperty("lifecycleState").GetString());
        Assert.Equal(content.Length, metadata.RootElement.GetProperty("bytes").GetInt64());
        Assert.Equal(Sha256(content), metadata.RootElement.GetProperty("sha256").GetString());
        Assert.Equal(value.Layout.PayloadRelativePath, metadata.RootElement.GetProperty("payloadRelativePath").GetString());
    }

    [Fact]
    public async Task ReadRevalidatesMetadataIdentityAndPayloadIntegrity()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        string source = workspace.AddSource("asset.bin", "content"u8);
        var store = new ArtifactStore();
        ArtifactStoreRecord staged = (await store.StageAsync(
            workspace.StageRequest(source),
            TestContext.Current.CancellationToken)).Value!;

        ArtifactStoreOperationResult<ArtifactStoreRecord> read = await store.ReadAsync(
            workspace.ReadRequest(),
            TestContext.Current.CancellationToken);

        Assert.True(read.IsSuccess);
        Assert.Equal(staged.ContentIdentity, read.Value!.ContentIdentity);
        Assert.Equal(staged.Artifact.Id, read.Value.Artifact.Id);
        Assert.Equal(staged.Layout.ArtifactRoot, read.Value.Layout.ArtifactRoot);
    }

    [Fact]
    public async Task OptionalTargetRoundTripsAsNull()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var store = new ArtifactStore();

        ArtifactStoreOperationResult<ArtifactStoreRecord> staged = await store.StageAsync(
            workspace.StageRequest(
                workspace.AddSource("asset.bin", "content"u8),
                includeTarget: false),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> read = await store.ReadAsync(
            workspace.ReadRequest(),
            TestContext.Current.CancellationToken);

        Assert.Null(staged.Value!.Artifact.Target);
        Assert.Null(read.Value!.Artifact.Target);
    }

    [Fact]
    public async Task LifecycleAdvancesOnlyThroughApprovedOptimisticTransitions()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var store = new ArtifactStore();
        _ = await store.StageAsync(
            workspace.StageRequest(workspace.AddSource("asset.bin", "content"u8)),
            TestContext.Current.CancellationToken);

        ArtifactStoreOperationResult<ArtifactStoreRecord> validated = await store.TransitionAsync(
            workspace.TransitionRequest(BuildArtifactLifecycleState.Staged, BuildArtifactLifecycleState.Validated),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> promoted = await store.TransitionAsync(
            workspace.TransitionRequest(
                BuildArtifactLifecycleState.Validated,
                BuildArtifactLifecycleState.Promoted,
                new DateTimeOffset(2026, 8, 3, 12, 2, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> persisted = await store.ReadAsync(
            workspace.ReadRequest(),
            TestContext.Current.CancellationToken);

        Assert.True(validated.IsSuccess);
        Assert.Equal(BuildArtifactLifecycleState.Validated, validated.Value!.Artifact.LifecycleState);
        Assert.True(promoted.IsSuccess);
        Assert.Equal(BuildArtifactLifecycleState.Promoted, promoted.Value!.Artifact.LifecycleState);
        Assert.Equal(BuildArtifactLifecycleState.Promoted, persisted.Value!.Artifact.LifecycleState);
    }

    [Theory]
    [InlineData("same")]
    [InlineData("skip")]
    [InlineData("backward")]
    public async Task LifecycleRejectsUnapprovedTransitions(string scenario)
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var store = new ArtifactStore();
        _ = await store.StageAsync(
            workspace.StageRequest(workspace.AddSource("asset.bin", "content"u8)),
            TestContext.Current.CancellationToken);
        if (scenario == "backward")
        {
            _ = await store.TransitionAsync(
                workspace.TransitionRequest(
                    BuildArtifactLifecycleState.Staged,
                    BuildArtifactLifecycleState.Validated),
                TestContext.Current.CancellationToken);
        }

        BuildArtifactLifecycleState expected = scenario == "backward"
            ? BuildArtifactLifecycleState.Validated
            : BuildArtifactLifecycleState.Staged;
        BuildArtifactLifecycleState target = scenario switch
        {
            "same" => BuildArtifactLifecycleState.Staged,
            "skip" => BuildArtifactLifecycleState.Promoted,
            _ => BuildArtifactLifecycleState.Staged,
        };

        ArtifactStoreOperationResult<ArtifactStoreRecord> result = await store.TransitionAsync(
            workspace.TransitionRequest(expected, target),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ARTIFACT_STORE_TRANSITION_INVALID", Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task LifecycleRejectsStateConflictAndInvalidTimes()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var store = new ArtifactStore();
        _ = await store.StageAsync(
            workspace.StageRequest(workspace.AddSource("asset.bin", "content"u8)),
            TestContext.Current.CancellationToken);

        ArtifactStoreOperationResult<ArtifactStoreRecord> conflict = await store.TransitionAsync(
            workspace.TransitionRequest(BuildArtifactLifecycleState.Validated, BuildArtifactLifecycleState.Promoted),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> nonUtc = await store.TransitionAsync(
            workspace.TransitionRequest(
                BuildArtifactLifecycleState.Staged,
                BuildArtifactLifecycleState.Validated,
                new DateTimeOffset(2026, 8, 3, 14, 0, 0, TimeSpan.FromHours(2))),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> earlier = await store.TransitionAsync(
            workspace.TransitionRequest(
                BuildArtifactLifecycleState.Staged,
                BuildArtifactLifecycleState.Validated,
                new DateTimeOffset(2026, 8, 3, 11, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_STORE_STATE_CONFLICT", Assert.Single(conflict.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_TRANSITION_TIME_INVALID", Assert.Single(nonUtc.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_TRANSITION_TIME_INVALID", Assert.Single(earlier.Failures).Code);
    }

    [Fact]
    public async Task StageRejectsNonStagedArtifactsAndExistingEntries()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        string source = workspace.AddSource("asset.bin", "content"u8);
        var store = new ArtifactStore();

        ArtifactStoreOperationResult<ArtifactStoreRecord> wrongState = await store.StageAsync(
            workspace.StageRequest(source, state: BuildArtifactLifecycleState.Validated),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> first = await store.StageAsync(
            workspace.StageRequest(source),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> collision = await store.StageAsync(
            workspace.StageRequest(source),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_STORE_INITIAL_STATE", Assert.Single(wrongState.Failures).Code);
        Assert.True(first.IsSuccess);
        Assert.Equal("ARTIFACT_STORE_COLLISION", Assert.Single(collision.Failures).Code);
    }

    [Fact]
    public async Task ReadRejectsChangedPayloadAndMismatchedMetadataIdentity()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var store = new ArtifactStore();
        ArtifactStoreRecord staged = (await store.StageAsync(
            workspace.StageRequest(workspace.AddSource("asset.bin", "content"u8)),
            TestContext.Current.CancellationToken)).Value!;
        await File.WriteAllTextAsync(staged.Layout.PayloadPath, "tampered", TestContext.Current.CancellationToken);

        ArtifactStoreOperationResult<ArtifactStoreRecord> changed = await store.ReadAsync(
            workspace.ReadRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_STORE_INTEGRITY_MISMATCH", Assert.Single(changed.Failures).Code);

        File.Copy(workspace.AddSource("replacement.bin", "content"u8), staged.Layout.PayloadPath, overwrite: true);
        JsonNode metadata = JsonNode.Parse(await File.ReadAllTextAsync(
            staged.Layout.MetadataPath,
            TestContext.Current.CancellationToken))!;
        metadata["artifactId"] = "Artifact-Other";
        await File.WriteAllTextAsync(
            staged.Layout.MetadataPath,
            metadata.ToJsonString(),
            TestContext.Current.CancellationToken);

        ArtifactStoreOperationResult<ArtifactStoreRecord> mismatch = await store.ReadAsync(
            workspace.ReadRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal("ARTIFACT_STORE_IDENTITY_MISMATCH", Assert.Single(mismatch.Failures).Code);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("version")]
    [InlineData("payload")]
    [InlineData("malformed")]
    [InlineData("null-root")]
    [InlineData("missing")]
    [InlineData("target")]
    [InlineData("digest")]
    [InlineData("negative-bytes")]
    [InlineData("logical-reference")]
    public async Task ReadRejectsHostileOrUnsupportedMetadata(string scenario)
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var store = new ArtifactStore();
        ArtifactStoreRecord staged = (await store.StageAsync(
            workspace.StageRequest(workspace.AddSource("asset.bin", "content"u8)),
            TestContext.Current.CancellationToken)).Value!;
        string json = await File.ReadAllTextAsync(
            staged.Layout.MetadataPath,
            TestContext.Current.CancellationToken);
        JsonNode? parsed = scenario is "missing" or "target" or "digest" or "negative-bytes" or "logical-reference"
            ? JsonNode.Parse(json)
            : null;
        json = scenario switch
        {
            "unknown" => json.Replace("{", "{\"unexpected\":true,", StringComparison.Ordinal),
            "duplicate" => json.Replace("{", "{\"schemaVersion\":1,", StringComparison.Ordinal),
            "version" => json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99", StringComparison.Ordinal),
            "payload" => json.Replace(staged.Layout.PayloadRelativePath, "Jobs/wrong/payload", StringComparison.Ordinal),
            "malformed" => "{",
            "null-root" => "null",
            "missing" => RemoveProperty(parsed!, "role"),
            "target" => SetProperty(parsed!, "target", "unsupported"),
            "digest" => SetProperty(parsed!, "sha256", "invalid"),
            "negative-bytes" => SetProperty(parsed!, "bytes", -1),
            _ => SetProperty(parsed!, "logicalReference", "../unsafe"),
        };
        await File.WriteAllTextAsync(
            staged.Layout.MetadataPath,
            json,
            TestContext.Current.CancellationToken);

        ArtifactStoreOperationResult<ArtifactStoreRecord> result = await store.ReadAsync(
            workspace.ReadRequest(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains(Assert.Single(result.Failures).Code, _metadataFailureCodes);
    }

    [Fact]
    public async Task CancellationReturnsStructuredFailure()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var cancellation = new CancellationToken(canceled: true);

        ArtifactStoreOperationResult<ArtifactStoreRecord> result = await new ArtifactStore().StageAsync(
            workspace.StageRequest(workspace.AddSource("asset.bin", "content"u8)),
            cancellation);

        Assert.Equal("ARTIFACT_STORE_CANCELLED", Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task HashFailuresArePreservedDuringStageAndRead()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        string source = workspace.AddSource("asset.bin", "content"u8);
        var failingStore = new ArtifactStore(new StubHashService(
            ArtifactHashOperationResult.Failed<ArtifactHashReceipt>(
                new ArtifactHashFailure("HASH_FAILED", "payload", "Hashing failed safely."))));

        ArtifactStoreOperationResult<ArtifactStoreRecord> stageFailure = await failingStore.StageAsync(
            workspace.StageRequest(source),
            TestContext.Current.CancellationToken);

        Assert.Equal("HASH_FAILED", Assert.Single(stageFailure.Failures).Code);

        using var second = new ArtifactStoreTestWorkspace();
        var physicalStore = new ArtifactStore();
        _ = await physicalStore.StageAsync(
            second.StageRequest(second.AddSource("asset.bin", "content"u8)),
            TestContext.Current.CancellationToken);
        ArtifactStoreOperationResult<ArtifactStoreRecord> readFailure = await failingStore.ReadAsync(
            second.ReadRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal("HASH_FAILED", Assert.Single(readFailure.Failures).Code);
    }

    [Fact]
    public async Task ReadAndTransitionCancellationReturnStructuredFailures()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var physicalStore = new ArtifactStore();
        _ = await physicalStore.StageAsync(
            workspace.StageRequest(workspace.AddSource("asset.bin", "content"u8)),
            TestContext.Current.CancellationToken);
        var cancelled = new CancellationToken(canceled: true);

        ArtifactStoreOperationResult<ArtifactStoreRecord> read = await physicalStore.ReadAsync(
            workspace.ReadRequest(),
            cancelled);
        ArtifactStoreOperationResult<ArtifactStoreRecord> transition = await physicalStore.TransitionAsync(
            workspace.TransitionRequest(BuildArtifactLifecycleState.Staged, BuildArtifactLifecycleState.Validated),
            cancelled);

        Assert.Equal("ARTIFACT_STORE_CANCELLED", Assert.Single(read.Failures).Code);
        Assert.Equal("ARTIFACT_STORE_CANCELLED", Assert.Single(transition.Failures).Code);
    }

    [Fact]
    public async Task TransitionCancellationAfterReadIsStructured()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        var physicalStore = new ArtifactStore();
        ArtifactStoreRecord staged = (await physicalStore.StageAsync(
            workspace.StageRequest(workspace.AddSource("asset.bin", "content"u8)),
            TestContext.Current.CancellationToken)).Value!;
        using var cancellation = new CancellationTokenSource();
        var store = new ArtifactStore(new StubHashService(
            ArtifactHashOperationResult.Success(
                new ArtifactHashReceipt(staged.Artifact.Id, staged.ContentIdentity)),
            cancellation));

        ArtifactStoreOperationResult<ArtifactStoreRecord> result = await store.TransitionAsync(
            workspace.TransitionRequest(BuildArtifactLifecycleState.Staged, BuildArtifactLifecycleState.Validated),
            cancellation.Token);

        Assert.Equal("ARTIFACT_STORE_CANCELLED", Assert.Single(result.Failures).Code);
    }

    private sealed class StubHashService(
        ArtifactHashOperationResult<ArtifactHashReceipt> result,
        CancellationTokenSource? cancellation = null) : IArtifactHashService
    {
        public Task<ArtifactHashOperationResult<ArtifactHashReceipt>> HashAsync(
            ArtifactHashRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellation?.Cancel();
            return Task.FromResult(result);
        }
    }

    private static string RemoveProperty(JsonNode node, string name)
    {
        _ = node.AsObject().Remove(name);
        return node.ToJsonString();
    }

    private static string SetProperty(JsonNode node, string name, string value)
    {
        node[name] = value;
        return node.ToJsonString();
    }

    private static string SetProperty(JsonNode node, string name, int value)
    {
        node[name] = value;
        return node.ToJsonString();
    }

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
