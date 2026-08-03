using PackageBuilder.Contracts.Snapshots;
using PackageBuilder.Infrastructure.Snapshots;

namespace PackageBuilder.Infrastructure.Tests.Snapshots;

public sealed class SourceSnapshotServiceFailureTests
{
    [Theory]
    [InlineData("relative")]
    [InlineData("noncanonical")]
    [InlineData("invalid")]
    public async Task InvalidOrNoncanonicalPathsAreRejected(string mode)
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        string project = mode switch
        {
            "relative" => "relative",
            "noncanonical" => Path.Combine(workspace.ProjectRoot, "folder", ".."),
            _ => workspace.ProjectRoot + '\0',
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            new SourceSnapshotRequest(project, workspace.SourceRoot, workspace.JobRoot, workspace.SnapshotRoot, Policy()),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Failures, failure => failure.Code is "SNAPSHOT_PATH_INVALID" or "SNAPSHOT_PATH_NOT_CANONICAL");
    }

    [Fact]
    public async Task WhitespacePathIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            new SourceSnapshotRequest(" ", workspace.SourceRoot, workspace.JobRoot, workspace.SnapshotRoot, Policy()),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_PATH_INVALID");
    }

    [Fact]
    public async Task ReparseProjectBoundaryIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        var physical = new PhysicalSourceSnapshotFileSystem();
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            GetAttributesHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, workspace.ProjectRoot)
                ? physical.GetAttributes(path) | FileAttributes.ReparsePoint
                : physical.GetAttributes(path),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_REPARSE_BOUNDARY");
    }

    [Fact]
    public async Task ReparseSourceBoundaryIsRejectedDuringValidation()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        var physical = new PhysicalSourceSnapshotFileSystem();
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            GetAttributesHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, workspace.SourceRoot)
                ? physical.GetAttributes(path) | FileAttributes.ReparsePoint
                : physical.GetAttributes(path),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_REPARSE_BOUNDARY");
    }

    [Fact]
    public async Task ReparseSourceDirectoryAppearingAfterValidationIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        var physical = new PhysicalSourceSnapshotFileSystem();
        int sourceChecks = 0;
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            GetAttributesHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, workspace.SourceRoot)
                && ++sourceChecks >= 2
                    ? physical.GetAttributes(path) | FileAttributes.ReparsePoint
                    : physical.GetAttributes(path),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_SOURCE_REPARSE");
    }

    [Fact]
    public async Task ReparseSourceFileIsRejectedDuringPreflight()
    {
        using var workspace = new SnapshotTestWorkspace();
        string source = workspace.AddSource("Model.fbx", "model");
        var physical = new PhysicalSourceSnapshotFileSystem();
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            GetAttributesHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, source)
                ? physical.GetAttributes(path) | FileAttributes.ReparsePoint
                : physical.GetAttributes(path),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_SOURCE_REPARSE");
    }

    [Fact]
    public async Task EscapingEnumeratedEntryIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            EnumerateEntriesHandler = _ => [Path.Combine(workspace.Root, "outside.fbx")],
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_SOURCE_ESCAPE");
    }

    [Fact]
    public async Task UnsafePortableNameIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        string unsafePath = Path.Combine(workspace.SourceRoot, "bad<name.fbx");
        var physical = new PhysicalSourceSnapshotFileSystem();
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            EnumerateEntriesHandler = _ => [unsafePath],
            GetAttributesHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, unsafePath)
                ? FileAttributes.Normal
                : physical.GetAttributes(path),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_NAME_UNSAFE");
    }

    [Theory]
    [InlineData("trailing .fbx ")]
    [InlineData("trailing.")]
    [InlineData("bad|name.fbx")]
    [InlineData("bad\u0001name.fbx")]
    [InlineData("CON.fbx")]
    public void EveryUnsafePortableNameClassIsRejected(string unsafeName)
    {
        System.Reflection.MethodInfo validator = typeof(SourceSnapshotService).GetMethod(
            "ValidateRelativePath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        SnapshotFailure failure = Assert.IsType<SnapshotFailure>(validator.Invoke(null, [unsafeName]));
        Assert.Equal("SNAPSHOT_NAME_UNSAFE", failure.Code);
    }

    [Fact]
    public async Task ExistingDestinationFileIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        File.WriteAllText(workspace.SnapshotRoot, "occupied");

        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_DESTINATION_IN_USE");
    }

    [Fact]
    public async Task DestinationFileAppearingAfterPreflightIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        int snapshotFileChecks = 0;
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            FileExistsHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, workspace.SnapshotRoot)
                && ++snapshotFileChecks >= 3,
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_DESTINATION_IN_USE");
    }

    [Fact]
    public async Task SnapshotRootBecomingReparsePointIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        var physical = new PhysicalSourceSnapshotFileSystem();
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            GetAttributesHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, workspace.SnapshotRoot)
                ? physical.GetAttributes(path) | FileAttributes.ReparsePoint
                : physical.GetAttributes(path),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_DESTINATION_REPARSE");
    }

    [Fact]
    public async Task NestedDestinationBecomingReparsePointFailsClosed()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource(Path.Combine("Nested", "Model.fbx"), "model");
        string nestedDestination = Path.Combine(workspace.SnapshotRoot, "Nested");
        var physical = new PhysicalSourceSnapshotFileSystem();
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            GetAttributesHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, nestedDestination)
                ? physical.GetAttributes(path) | FileAttributes.ReparsePoint
                : physical.GetAttributes(path),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_IO_FAILED");
    }

    [Fact]
    public async Task ExistingSnapshotAncestorReparsePointIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        string ancestor = Path.Combine(workspace.JobRoot, "container");
        _ = Directory.CreateDirectory(ancestor);
        string snapshot = Path.Combine(ancestor, "snapshot");
        var physical = new PhysicalSourceSnapshotFileSystem();
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            GetAttributesHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, ancestor)
                ? physical.GetAttributes(path) | FileAttributes.ReparsePoint
                : physical.GetAttributes(path),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            new SourceSnapshotRequest(workspace.ProjectRoot, workspace.SourceRoot, workspace.JobRoot, snapshot, Policy()),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_REPARSE_BOUNDARY");
    }

    [Fact]
    public async Task ExistingFileInSnapshotAncestorChainFailsClosed()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        string fileAncestor = Path.Combine(workspace.JobRoot, "occupied");
        File.WriteAllText(fileAncestor, "file");
        string snapshot = Path.Combine(fileAncestor, "snapshot");

        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            new SourceSnapshotRequest(workspace.ProjectRoot, workspace.SourceRoot, workspace.JobRoot, snapshot, Policy()),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_IO_FAILED");
    }

    [Theory]
    [InlineData("equal")]
    [InlineData("source-inside-job")]
    [InlineData("job-inside-source")]
    public async Task EverySourceJobOverlapRelationshipIsRejected(string relationship)
    {
        using var workspace = new SnapshotTestWorkspace();
        string source = workspace.SourceRoot;
        string job = workspace.JobRoot;
        if (relationship == "equal")
        {
            source = job;
        }
        else if (relationship == "source-inside-job")
        {
            source = Path.Combine(job, "source");
            _ = Directory.CreateDirectory(source);
        }
        else
        {
            job = Path.Combine(source, "job");
            _ = Directory.CreateDirectory(job);
        }

        string snapshot = Path.Combine(job, "snapshot");
        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            new SourceSnapshotRequest(workspace.ProjectRoot, source, job, snapshot, Policy()),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_SOURCE_DESTINATION_OVERLAP");
    }

    [Fact]
    public async Task SourceMayNotEqualProjectBoundary()
    {
        using var workspace = new SnapshotTestWorkspace();
        SnapshotOperationResult<SourceSnapshotReceipt> result = await CreateService().CreateAsync(
            new SourceSnapshotRequest(
                workspace.ProjectRoot,
                workspace.ProjectRoot,
                workspace.JobRoot,
                workspace.SnapshotRoot,
                Policy()),
            TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_SOURCE_OUTSIDE_PROJECT");
    }

    [Fact]
    public void TrailingSeparatorHelperPreservesDriveRoot()
    {
        System.Reflection.MethodInfo helper = typeof(SourceSnapshotService).GetMethod(
            "EnsureTrailingSeparator",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        string root = Path.GetPathRoot(AppContext.BaseDirectory)!;

        Assert.Equal(root, helper.Invoke(null, [root]));
    }

    [Fact]
    public async Task SourceFileBecomingReparsePointAfterPreflightIsRejected()
    {
        using var workspace = new SnapshotTestWorkspace();
        string source = workspace.AddSource("Model.fbx", "model");
        var physical = new PhysicalSourceSnapshotFileSystem();
        int sourceChecks = 0;
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            GetAttributesHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, source)
                && ++sourceChecks >= 2
                    ? physical.GetAttributes(path) | FileAttributes.ReparsePoint
                    : physical.GetAttributes(path),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_SOURCE_CHANGED");
    }

    [Fact]
    public async Task UnauthorizedWriteReturnsSanitizedIoFailure()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            CreateNewFileHandler = _ => throw new UnauthorizedAccessException(workspace.SnapshotRoot),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_IO_FAILED");
        Assert.DoesNotContain(workspace.SnapshotRoot, result.Failures[0].Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SizeAccountingOverflowReturnsStructuredFailure()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("One.fbx", "1");
        _ = workspace.AddSource("Two.fbx", "2");
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            GetFileLengthHandler = _ => (long.MaxValue / 2) + 1,
        };
        SourceSnapshotPolicy policy = SourceSnapshotPolicy.Create(2, long.MaxValue, long.MaxValue).Value!;

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace, policy), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_SIZE_OVERFLOW");
    }

    [Fact]
    public async Task NegativeReportedFileLengthFailsClosed()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        var fileSystem = new DelegatingSourceSnapshotFileSystem { GetFileLengthHandler = _ => -1 };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_FILE_LIMIT");
    }

    [Fact]
    public async Task UnreadableSourceStreamFailsClosed()
    {
        using var workspace = new SnapshotTestWorkspace();
        _ = workspace.AddSource("Model.fbx", "model");
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            OpenReadLockedHandler = _ => new UnreadableStream(),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_SOURCE_CHANGED");
    }

    [Theory]
    [InlineData(3, "1234")]
    [InlineData(5, "1234")]
    public async Task NonseekableSourceSizeMismatchIsRejected(long plannedBytes, string actual)
    {
        using var workspace = new SnapshotTestWorkspace();
        string source = workspace.AddSource("Model.fbx", "model");
        var fileSystem = new DelegatingSourceSnapshotFileSystem
        {
            GetFileLengthHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, source) ? plannedBytes : 0,
            OpenReadLockedHandler = _ => new NonSeekableReadStream(actual),
        };

        SnapshotOperationResult<SourceSnapshotReceipt> result = await new SourceSnapshotService(fileSystem).CreateAsync(
            Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "SNAPSHOT_SOURCE_CHANGED");
    }

    private static SourceSnapshotService CreateService() => new();

    private static SourceSnapshotRequest Request(SnapshotTestWorkspace workspace, SourceSnapshotPolicy? policy = null) =>
        new(workspace.ProjectRoot, workspace.SourceRoot, workspace.JobRoot, workspace.SnapshotRoot, policy ?? Policy());

    private static SourceSnapshotPolicy Policy() => SourceSnapshotPolicy.Create(100, 1_000_000, 10_000_000).Value!;

    private static void AssertFailure(SnapshotOperationResult<SourceSnapshotReceipt> result, string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Failures, failure => failure.Code == code);
    }

    private sealed class NonSeekableReadStream(string content) : Stream
    {
        private readonly MemoryStream _inner = new(System.Text.Encoding.UTF8.GetBytes(content));
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
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
}
