using System.Security.Cryptography;
using PackageBuilder.Contracts.Snapshots;
using PackageBuilder.Infrastructure.Snapshots;

namespace PackageBuilder.Infrastructure.Tests.Snapshots;

public sealed class SourceSnapshotServiceTests
{
    [Fact]
    public async Task CopiesNestedSourcesRecordsHashesAndLeavesOriginalsUnchanged()
    {
        using var workspace = new SnapshotTestWorkspace();
        string model = workspace.AddSource("Model.fbx", "model-bytes");
        string texture = workspace.AddSource(Path.Combine("Textures", "T_Model_Albedo.png"), "texture-bytes");
        byte[] modelBefore = await File.ReadAllBytesAsync(model, TestContext.Current.CancellationToken);
        byte[] textureBefore = await File.ReadAllBytesAsync(texture, TestContext.Current.CancellationToken);

        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            Request(workspace),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.FileCount);
        Assert.Equal(modelBefore.Length + textureBefore.Length, result.Value.TotalBytes);
        Assert.Equal(["Model.fbx", "Textures/T_Model_Albedo.png"], result.Value.Files.Select(static file => file.RelativePath));
        Assert.Equal(Sha256(modelBefore), result.Value.Files[0].Sha256);
        Assert.Equal(Sha256(textureBefore), result.Value.Files[1].Sha256);
        Assert.Equal(modelBefore, await File.ReadAllBytesAsync(model, TestContext.Current.CancellationToken));
        Assert.Equal(textureBefore, await File.ReadAllBytesAsync(texture, TestContext.Current.CancellationToken));
        Assert.Equal(
            modelBefore,
            await File.ReadAllBytesAsync(
                Path.Combine(workspace.SnapshotRoot, "Model.fbx"),
                TestContext.Current.CancellationToken));
        Assert.NotEqual(
            FileAttributes.None,
            File.GetAttributes(Path.Combine(workspace.SnapshotRoot, "Model.fbx")) & FileAttributes.ReadOnly);
    }

    [Fact]
    public async Task PhysicalSnapshotCannotBeOverwrittenAfterSuccess()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.glb", "immutable");
        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            Request(workspace),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        _ = Assert.Throws<UnauthorizedAccessException>(() => File.WriteAllText(Path.Combine(workspace.SnapshotRoot, "Model.glb"), "changed"));
    }

    [Fact]
    public async Task ExistingDestinationIsRejectedBeforeWrites()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        _ = Directory.CreateDirectory(workspace.SnapshotRoot);

        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            Request(workspace),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_DESTINATION_IN_USE");
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace.SnapshotRoot));
    }

    [Fact]
    public async Task EmptySourceIsRejectedWithoutDestinationCreation()
    {
        using var workspace = new SnapshotTestWorkspace();

        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            Request(workspace),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_SOURCE_EMPTY");
        Assert.False(Directory.Exists(workspace.SnapshotRoot));
    }

    [Theory]
    [InlineData("project", "SNAPSHOT_PROJECT_MISSING")]
    [InlineData("source", "SNAPSHOT_SOURCE_MISSING")]
    [InlineData("job", "SNAPSHOT_JOB_MISSING")]
    public async Task MissingRequiredRootsFailClosed(string missing, string expectedCode)
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        string absent = Path.Combine(workspace.Root, "absent", missing);
        SourceSnapshotRequest request = new(
            missing == "project" ? absent : workspace.ProjectRoot,
            missing == "source" ? absent : workspace.SourceRoot,
            missing == "job" ? absent : workspace.JobRoot,
            workspace.SnapshotRoot,
            Policy());

        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            request,
            TestContext.Current.CancellationToken);

        AssertFailure(result, expectedCode);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("job")]
    [InlineData("snapshot")]
    public async Task RootsOutsideApprovedBoundariesAreRejected(string root)
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        string outside = Path.Combine(Path.GetPathRoot(workspace.ProjectRoot)!, "outside-package-builder", Guid.NewGuid().ToString("N"));
        SourceSnapshotRequest request = new(
            workspace.ProjectRoot,
            root == "source" ? outside : workspace.SourceRoot,
            root == "job" ? outside : workspace.JobRoot,
            root == "snapshot" ? outside : workspace.SnapshotRoot,
            Policy());

        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Failures);
    }

    [Fact]
    public async Task SourceAndJobOverlapIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        string jobInsideSource = Path.Combine(workspace.SourceRoot, "job");
        _ = Directory.CreateDirectory(jobInsideSource);

        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            new SourceSnapshotRequest(
                workspace.ProjectRoot,
                workspace.SourceRoot,
                jobInsideSource,
                Path.Combine(jobInsideSource, "snapshot"),
                Policy()),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_SOURCE_DESTINATION_OVERLAP");
    }

    [Theory]
    [InlineData(1, 100, 100, "SNAPSHOT_FILE_COUNT_LIMIT")]
    [InlineData(10, 3, 100, "SNAPSHOT_FILE_LIMIT")]
    [InlineData(10, 4, 7, "SNAPSHOT_TOTAL_LIMIT")]
    public async Task ExplicitLimitsAreEnforcedBeforeDestinationCreation(int count, long fileBytes, long totalBytes, string code)
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("One.fbx", "1234");
        _ = workspace.AddSource("Two.png", "5678");
        SourceSnapshotPolicy policy = SourceSnapshotPolicy.Create(count, fileBytes, Math.Max(totalBytes, fileBytes)).Value!;

        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            Request(workspace, policy),
            TestContext.Current.CancellationToken);

        AssertFailure(result, code);
        Assert.False(Directory.Exists(workspace.SnapshotRoot));
    }

    [Fact]
    public async Task CancellationReturnsStructuredFailure()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(Request(workspace), cancellation.Token);

        AssertFailure(result, "SNAPSHOT_CANCELLED");
    }

    [Fact]
    public async Task DestinationAppearingAfterPreflightIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        var physical = new PhysicalSourceSnapshotFileSystem();
        int checks = 0;
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            DirectoryExistsHandler = path =>
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(path, workspace.SnapshotRoot) && ++checks == 2)
                {
                    _ = Directory.CreateDirectory(path);
                }
                return physical.DirectoryExists(path);
            },
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_DESTINATION_IN_USE");
    }

    [Fact]
    public async Task SourceLengthChangeAfterPreflightIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        string source = workspace.AddSource("Model.fbx", "model");
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            OpenReadLockedHandler = path =>
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(path, source))
                {
                    File.AppendAllText(path, "changed");
                }
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            },
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_SOURCE_CHANGED");
    }

    [Fact]
    public async Task IoFailureReturnsSanitizedFailure()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            OpenReadLockedHandler = _ => throw new IOException(workspace.SourceRoot),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_IO_FAILED");
        Assert.DoesNotContain(workspace.SourceRoot, result.Failures[0].Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static SourceSnapshotService CreateService() => new();

    private static SourceSnapshotRequest Request(SnapshotTestWorkspace workspace, SourceSnapshotPolicy? policy = null) =>
        new(workspace.ProjectRoot, workspace.SourceRoot, workspace.JobRoot, workspace.SnapshotRoot, policy ?? Policy());

    private static SourceSnapshotPolicy Policy() => SourceSnapshotPolicy.Create(100, 1_000_000, 10_000_000).Value!;

    private static string Sha256(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static void AssertFailure(SnapshotOperationResult<SourceSnapshotReceipt> result, string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Failures, failure => failure.Code == code);
    }
}
