using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Infrastructure.Artifacts;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

public sealed class ArtifactHashServiceFailureTests
{
    [Fact]
    public async Task NullRequestAndNullDependencyAreRejected()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactHashService(null!));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => new ArtifactHashService().HashAsync(null!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("project")]
    [InlineData("file")]
    public async Task RelativePathsAreRejected(string relativePart)
    {
        using var workspace = new ArtifactTestWorkspace();
        string file = workspace.AddFile("Model.fbx", "content"u8);
        ArtifactHashRequest request = new(
            relativePart == "project" ? "relative-project" : workspace.ProjectRoot,
            relativePart == "file" ? "relative-file" : file,
            ArtifactId());

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService().HashAsync(
            request,
            TestContext.Current.CancellationToken);

        AssertFailure(result, "ARTIFACT_HASH_PATH_INVALID");
    }

    [Fact]
    public async Task EmptyAndMalformedQualifiedPathsAreRejected()
    {
        using var workspace = new ArtifactTestWorkspace();
        string file = workspace.AddFile("Model.fbx", "content"u8);

        ArtifactHashOperationResult<ArtifactHashReceipt> empty = await new ArtifactHashService().HashAsync(
            new ArtifactHashRequest(" ", file, ArtifactId()),
            TestContext.Current.CancellationToken);
        AssertFailure(empty, "ARTIFACT_HASH_PATH_INVALID");

        string malformed = $"{workspace.ProjectRoot}{Path.DirectorySeparatorChar}invalid\0name";
        ArtifactHashOperationResult<ArtifactHashReceipt> invalid = await new ArtifactHashService().HashAsync(
            new ArtifactHashRequest(workspace.ProjectRoot, malformed, ArtifactId()),
            TestContext.Current.CancellationToken);
        AssertFailure(invalid, "ARTIFACT_HASH_PATH_INVALID");
    }

    [Theory]
    [InlineData("project")]
    [InlineData("file")]
    public async Task NonCanonicalPathsAreRejected(string part)
    {
        using var workspace = new ArtifactTestWorkspace();
        string file = workspace.AddFile("Model.fbx", "content"u8);
        string project = Path.Combine(workspace.ProjectRoot, ".");
        string nonCanonicalFile = Path.Combine(Path.GetDirectoryName(file)!, ".", Path.GetFileName(file));
        ArtifactHashRequest request = new(
            part == "project" ? project : workspace.ProjectRoot,
            part == "file" ? nonCanonicalFile : file,
            ArtifactId());

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService().HashAsync(
            request,
            TestContext.Current.CancellationToken);

        AssertFailure(result, "ARTIFACT_HASH_PATH_NOT_CANONICAL");
    }

    [Fact]
    public async Task MissingProjectAndFileReturnStructuredFailures()
    {
        using var workspace = new ArtifactTestWorkspace();
        string missingProject = Path.Combine(workspace.Root, "missing-project");
        string missingFile = Path.Combine(missingProject, "missing.bin");

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService().HashAsync(
            new ArtifactHashRequest(missingProject, missingFile, ArtifactId()),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "ARTIFACT_HASH_PROJECT_MISSING");
        AssertFailure(result, "ARTIFACT_HASH_FILE_MISSING");
    }

    [Fact]
    public async Task FileOutsideProjectIsRejected()
    {
        using var workspace = new ArtifactTestWorkspace();
        string outsideRoot = Path.Combine(Path.GetPathRoot(workspace.ProjectRoot)!, "PackageBuilder-PB0204-outside");
        string outsideFile = Path.Combine(outsideRoot, "outside.bin");
        var fileSystem = new DelegatingArtifactHashFileSystem
        {
            FileExistsHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, outsideFile) || File.Exists(path),
        };

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService(fileSystem).HashAsync(
            new ArtifactHashRequest(workspace.ProjectRoot, outsideFile, ArtifactId()),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "ARTIFACT_HASH_OUTSIDE_PROJECT");
    }

    [Fact]
    public async Task ProjectRootItselfIsNotAcceptedAsAnArtifactFile()
    {
        using var workspace = new ArtifactTestWorkspace();
        var fileSystem = new DelegatingArtifactHashFileSystem
        {
            FileExistsHandler = _ => true,
        };

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService(fileSystem).HashAsync(
            new ArtifactHashRequest(workspace.ProjectRoot, workspace.ProjectRoot, ArtifactId()),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "ARTIFACT_HASH_OUTSIDE_PROJECT");
    }

    [Fact]
    public async Task FilesystemRootUsesItsExistingTrailingSeparator()
    {
        string root = Path.GetPathRoot(AppContext.BaseDirectory)!;
        string file = Path.Combine(root, "virtual-artifact.bin");
        byte[] content = "content"u8.ToArray();
        var fileSystem = new DelegatingArtifactHashFileSystem
        {
            DirectoryExistsHandler = _ => true,
            FileExistsHandler = _ => true,
            GetAttributesHandler = _ => FileAttributes.Normal,
            GetFileLengthHandler = _ => content.Length,
            OpenReadLockedHandler = _ => new MemoryStream(content, writable: false),
        };

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService(fileSystem).HashAsync(
            new ArtifactHashRequest(root, file, ArtifactId()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("project")]
    [InlineData("directory")]
    [InlineData("file")]
    public async Task ReparsePointsAreRejectedAcrossTheCompletePath(string reparsePart)
    {
        using var workspace = new ArtifactTestWorkspace();
        string file = workspace.AddFile(Path.Combine("nested", "Model.fbx"), "content"u8);
        string nested = Path.GetDirectoryName(file)!;
        var physical = new PhysicalArtifactHashFileSystem();
        var fileSystem = new DelegatingArtifactHashFileSystem
        {
            GetAttributesHandler = path =>
                reparsePart == "project" && StringComparer.OrdinalIgnoreCase.Equals(path, workspace.ProjectRoot)
                || reparsePart == "directory" && StringComparer.OrdinalIgnoreCase.Equals(path, nested)
                || reparsePart == "file" && StringComparer.OrdinalIgnoreCase.Equals(path, file)
                    ? physical.GetAttributes(path) | FileAttributes.ReparsePoint
                    : physical.GetAttributes(path),
        };

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService(fileSystem).HashAsync(
            Request(workspace, file),
            TestContext.Current.CancellationToken);

        AssertFailure(
            result,
            reparsePart == "file" ? "ARTIFACT_HASH_REPARSE_BOUNDARY" : "ARTIFACT_HASH_REPARSE_BOUNDARY");
    }

    [Fact]
    public async Task FileBecomingReparsePointAfterValidationIsRejected()
    {
        using var workspace = new ArtifactTestWorkspace();
        string file = workspace.AddFile("Model.fbx", "content"u8);
        var physical = new PhysicalArtifactHashFileSystem();
        int fileChecks = 0;
        var fileSystem = new DelegatingArtifactHashFileSystem
        {
            GetAttributesHandler = path =>
                StringComparer.OrdinalIgnoreCase.Equals(path, file) && ++fileChecks == 2
                    ? physical.GetAttributes(path) | FileAttributes.ReparsePoint
                    : physical.GetAttributes(path),
        };

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService(fileSystem).HashAsync(
            Request(workspace, file),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "ARTIFACT_HASH_FILE_REPARSE");
    }

    [Fact]
    public async Task NegativeReportedLengthIsRejected()
    {
        ArtifactHashOperationResult<ArtifactHashReceipt> result = await RunWithStream(
            "content"u8.ToArray(),
            _ => -1,
            static bytes => new MemoryStream(bytes, writable: false));

        AssertFailure(result, "ARTIFACT_HASH_LENGTH_INVALID");
    }

    [Fact]
    public async Task UnreadableOrPrechangedStreamsAreRejected()
    {
        ArtifactHashOperationResult<ArtifactHashReceipt> unreadable = await RunWithStream(
            "content"u8.ToArray(),
            bytes => bytes.Length,
            static _ => new UnreadableStream());
        AssertFailure(unreadable, "ARTIFACT_HASH_SOURCE_CHANGED");

        ArtifactHashOperationResult<ArtifactHashReceipt> changed = await RunWithStream(
            "content"u8.ToArray(),
            bytes => bytes.Length + 1,
            static bytes => new MemoryStream(bytes, writable: false));
        AssertFailure(changed, "ARTIFACT_HASH_SOURCE_CHANGED");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task LengthChangesDuringStreamingAreRejected(int reportedDifference)
    {
        byte[] content = "content"u8.ToArray();
        ArtifactHashOperationResult<ArtifactHashReceipt> result = await RunWithStream(
            content,
            bytes => bytes.Length + reportedDifference,
            static bytes => new NonSeekableReadStream(bytes));

        AssertFailure(result, "ARTIFACT_HASH_SOURCE_CHANGED");
    }

    [Fact]
    public async Task CancellationReturnsStructuredFailure()
    {
        using var workspace = new ArtifactTestWorkspace();
        string file = workspace.AddFile("Model.fbx", "content"u8);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService().HashAsync(
            Request(workspace, file),
            cancellation.Token);

        AssertFailure(result, "ARTIFACT_HASH_CANCELLED");
    }

    [Theory]
    [InlineData("io")]
    [InlineData("access")]
    [InlineData("overflow")]
    public async Task PhysicalFailuresAreSanitized(string failure)
    {
        using var workspace = new ArtifactTestWorkspace();
        string file = workspace.AddFile("Model.fbx", "content"u8);
        var fileSystem = new DelegatingArtifactHashFileSystem
        {
            GetFileLengthHandler = failure == "overflow" ? _ => throw new OverflowException(file) : null,
            OpenReadLockedHandler = failure == "io"
                ? _ => throw new IOException(file)
                : failure == "access"
                    ? _ => throw new UnauthorizedAccessException(file)
                    : null,
        };

        ArtifactHashOperationResult<ArtifactHashReceipt> result = await new ArtifactHashService(fileSystem).HashAsync(
            Request(workspace, file),
            TestContext.Current.CancellationToken);

        AssertFailure(result, failure == "overflow" ? "ARTIFACT_HASH_SIZE_OVERFLOW" : "ARTIFACT_HASH_IO_FAILED");
        Assert.DoesNotContain(workspace.Root, result.Failures[0].Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ArtifactHashOperationResult<ArtifactHashReceipt>> RunWithStream(
        byte[] content,
        Func<byte[], long> length,
        Func<byte[], Stream> stream)
    {
        using var workspace = new ArtifactTestWorkspace();
        string file = workspace.AddFile("Model.fbx", content);
        var fileSystem = new DelegatingArtifactHashFileSystem
        {
            GetFileLengthHandler = _ => length(content),
            OpenReadLockedHandler = _ => stream(content),
        };
        return await new ArtifactHashService(fileSystem).HashAsync(
            Request(workspace, file),
            TestContext.Current.CancellationToken);
    }

    private static ArtifactHashRequest Request(ArtifactTestWorkspace workspace, string file) =>
        new(workspace.ProjectRoot, file, ArtifactId());

    private static BuildArtifactId ArtifactId() => BuildArtifactId.Create("Artifact-01").Value!;

    private static void AssertFailure(ArtifactHashOperationResult<ArtifactHashReceipt> result, string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Failures, failure => failure.Code == code);
    }

    private sealed class UnreadableStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class NonSeekableReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content, writable: false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
