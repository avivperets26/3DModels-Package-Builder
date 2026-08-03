using System.Security.Cryptography;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Infrastructure.Artifacts;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

public sealed class ArtifactHashServiceTests
{
    [Fact]
    public async Task PhysicalHashMatchesSha256AndDoesNotModifySource()
    {
        using var workspace = new ArtifactTestWorkspace();
        byte[] content = "artifact-content"u8.ToArray();
        string file = workspace.AddFile(Path.Combine("nested", "Model.fbx"), content);
        DateTime writeBefore = File.GetLastWriteTimeUtc(file);

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService().HashAsync(
            Request(workspace, file),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Artifact-01", result.Value!.ArtifactId.Value);
        Assert.Equal(content.Length, result.Value.ContentIdentity.Bytes);
        Assert.Equal(Sha256(content), result.Value.ContentIdentity.Sha256.Value);
        Assert.Equal(content, await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken));
        Assert.Equal(writeBefore, File.GetLastWriteTimeUtc(file));
    }

    [Fact]
    public async Task EmptyFileHasCanonicalEmptyDigest()
    {
        using var workspace = new ArtifactTestWorkspace();
        string file = workspace.AddFile("Empty.glb", []);

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService().HashAsync(
            Request(workspace, file),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.ContentIdentity.Bytes);
        Assert.Equal(Sha256([]), result.Value.ContentIdentity.Sha256.Value);
    }

    [Fact]
    public async Task LargeInputUsesBoundedStreamingReads()
    {
        using var workspace = new ArtifactTestWorkspace();
        byte[] content = new byte[(5 * 1024 * 1024) + 17];
        for (int index = 0; index < content.Length; index++)
        {
            content[index] = (byte)(index % 251);
        }
        string file = workspace.AddFile("Large.bin", content);
        var stream = new TrackingReadStream(content);
        var fileSystem = new DelegatingArtifactHashFileSystem
        {
            GetFileLengthHandler = _ => content.Length,
            OpenReadLockedHandler = _ => stream,
        };

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService(fileSystem).HashAsync(
            Request(workspace, file),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(content.Length, result.Value!.ContentIdentity.Bytes);
        Assert.Equal(Sha256(content), result.Value.ContentIdentity.Sha256.Value);
        Assert.InRange(stream.MaximumRequestedRead, 1, 65_536);
        Assert.True(stream.ReadCount > 2);
    }

    [Fact]
    public async Task SameBytesAtDifferentPathsProduceDuplicateIdentity()
    {
        using var workspace = new ArtifactTestWorkspace();
        byte[] content = "same-content"u8.ToArray();
        string first = workspace.AddFile("First.bin", content);
        string second = workspace.AddFile("Second.bin", content);
        ArtifactHashService service = new();

        ArtifactHashReceipt firstReceipt = (await service.HashAsync(
            Request(workspace, first, "Artifact-First"),
            TestContext.Current.CancellationToken)).Value!;
        ArtifactHashReceipt secondReceipt = (await service.HashAsync(
            Request(workspace, second, "Artifact-Second"),
            TestContext.Current.CancellationToken)).Value!;

        ArtifactDuplicateGroup group = Assert.Single(
            ArtifactDuplicateDetector.Detect([firstReceipt, secondReceipt]).Value!);
        Assert.Equal(["Artifact-First", "Artifact-Second"], group.ArtifactIds.Select(static id => id.Value));
    }

    private static ArtifactHashRequest Request(
        ArtifactTestWorkspace workspace,
        string file,
        string id = "Artifact-01") =>
        new(workspace.ProjectRoot, file, BuildArtifactId.Create(id).Value!);

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private sealed class TrackingReadStream(byte[] content) : MemoryStream(content, writable: false)
    {
        public int MaximumRequestedRead { get; private set; }

        public int ReadCount { get; private set; }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            MaximumRequestedRead = Math.Max(MaximumRequestedRead, buffer.Length);
            ReadCount++;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
