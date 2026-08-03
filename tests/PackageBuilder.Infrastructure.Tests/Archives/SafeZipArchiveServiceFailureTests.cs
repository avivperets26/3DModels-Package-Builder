using PackageBuilder.Contracts.Archives;
using PackageBuilder.Infrastructure.Archives;

namespace PackageBuilder.Infrastructure.Tests.Archives;

public sealed class SafeZipArchiveServiceFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceOpenFailuresAreSanitized(bool unauthorized)
    {
        using ArchiveTestWorkspace workspace = ValidWorkspace();
        var fileSystem = new DelegatingArchiveFileSystem
        {
            OpenReadLockedHandler = _ => unauthorized
                ? throw new UnauthorizedAccessException("private path")
                : throw new IOException("private path"),
        };
        var service = new SafeZipArchiveService(fileSystem);

        AssertFailure(
            await service.InspectAsync(Request(workspace), TestContext.Current.CancellationToken),
            "ARCHIVE_READ_FAILED");
        AssertFailure(
            await service.ExtractAsync(Request(workspace), TestContext.Current.CancellationToken),
            "ARCHIVE_EXTRACTION_IO_FAILED");
    }

    [Fact]
    public async Task NonSeekableSourceIsRejectedForInspectionAndExtraction()
    {
        using ArchiveTestWorkspace workspace = ValidWorkspace();
        byte[] bytes = File.ReadAllBytes(workspace.SourceArchivePath);
        var fileSystem = new DelegatingArchiveFileSystem
        {
            OpenReadLockedHandler = _ => new NonSeekableReadStream(bytes),
        };
        var service = new SafeZipArchiveService(fileSystem);

        AssertFailure(
            await service.InspectAsync(Request(workspace), TestContext.Current.CancellationToken),
            "ARCHIVE_STREAM_NOT_SEEKABLE");
        AssertFailure(
            await service.ExtractAsync(Request(workspace), TestContext.Current.CancellationToken),
            "ARCHIVE_STREAM_NOT_SEEKABLE");
    }

    [Fact]
    public async Task ExtractionChecksArchiveSizeBeforeOpeningZip()
    {
        using ArchiveTestWorkspace workspace = ValidWorkspace();
        byte[] bytes = File.ReadAllBytes(workspace.SourceArchivePath);
        var fileSystem = new DelegatingArchiveFileSystem
        {
            OpenReadLockedHandler = _ => new ReportedLengthStream(bytes, 2_000_000),
        };

        ArchiveOperationResult<ArchiveExtractionReceipt> result = await new SafeZipArchiveService(fileSystem)
            .ExtractAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_FILE_SIZE_LIMIT");
    }

    [Fact]
    public async Task DestinationAppearingAfterPreflightIsRejectedBeforeWrites()
    {
        using ArchiveTestWorkspace workspace = ValidWorkspace();
        int destinationFileChecks = 0;
        var fileSystem = new DelegatingArchiveFileSystem
        {
            FileExistsHandler = path =>
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(path, workspace.DestinationRoot))
                {
                    destinationFileChecks++;
                    return destinationFileChecks >= 3;
                }

                return File.Exists(path);
            },
        };

        ArchiveOperationResult<ArchiveExtractionReceipt> result = await new SafeZipArchiveService(fileSystem)
            .ExtractAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_DESTINATION_EXISTS");
        Assert.False(Directory.Exists(workspace.DestinationRoot));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DestinationCreationFailuresAreStructured(bool unauthorized)
    {
        using ArchiveTestWorkspace workspace = ValidWorkspace();
        var fileSystem = new DelegatingArchiveFileSystem
        {
            CreateDirectoryHandler = _ =>
            {
                if (unauthorized)
                {
                    throw new UnauthorizedAccessException("private path");
                }

                throw new IOException("private path");
            },
        };

        ArchiveOperationResult<ArchiveExtractionReceipt> result = await new SafeZipArchiveService(fileSystem)
            .ExtractAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_DESTINATION_CREATE_FAILED");
    }

    [Fact]
    public async Task DestinationCreationRejectsFileCollisionAndReparsePoint()
    {
        using ArchiveTestWorkspace workspace = ValidWorkspace();
        int fileChecks = 0;
        var collisionFileSystem = new DelegatingArchiveFileSystem
        {
            FileExistsHandler = path =>
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(path, workspace.DestinationRoot))
                {
                    fileChecks++;
                    return fileChecks >= 4;
                }

                return File.Exists(path);
            },
        };
        AssertFailure(
            await new SafeZipArchiveService(collisionFileSystem)
                .ExtractAsync(Request(workspace), TestContext.Current.CancellationToken),
            "ARCHIVE_DESTINATION_COLLISION");

        var reparseFileSystem = new DelegatingArchiveFileSystem
        {
            GetAttributesHandler = path =>
                StringComparer.OrdinalIgnoreCase.Equals(path, workspace.DestinationRoot)
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : File.GetAttributes(path),
        };
        AssertFailure(
            await new SafeZipArchiveService(reparseFileSystem)
                .ExtractAsync(Request(workspace), TestContext.Current.CancellationToken),
            "ARCHIVE_DESTINATION_REPARSE_POINT");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputOpenFailuresAreStructured(bool unauthorized)
    {
        using ArchiveTestWorkspace workspace = ValidWorkspace();
        var fileSystem = new DelegatingArchiveFileSystem
        {
            CreateNewFileHandler = _ => unauthorized
                ? throw new UnauthorizedAccessException("private path")
                : throw new IOException("private path"),
        };

        ArchiveOperationResult<ArchiveExtractionReceipt> result = await new SafeZipArchiveService(fileSystem)
            .ExtractAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_ENTRY_WRITE_FAILED");
    }

    [Fact]
    public async Task NestedDirectoryCreationAndExistingTargetFailuresStopExtraction()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "folder/file.txt", "safe"));
        var directoryFailureFileSystem = new DelegatingArchiveFileSystem
        {
            CreateDirectoryHandler = path =>
            {
                if (path.EndsWith($"{Path.DirectorySeparatorChar}folder", StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("private path");
                }

                _ = Directory.CreateDirectory(path);
            },
        };
        AssertFailure(
            await new SafeZipArchiveService(directoryFailureFileSystem)
                .ExtractAsync(Request(workspace), TestContext.Current.CancellationToken),
            "ARCHIVE_DESTINATION_CREATE_FAILED");

        if (Directory.Exists(workspace.DestinationRoot))
        {
            Directory.Delete(workspace.DestinationRoot, recursive: true);
        }

        string target = Path.Combine(workspace.DestinationRoot, "folder", "file.txt");
        var targetFailureFileSystem = new DelegatingArchiveFileSystem
        {
            FileExistsHandler = path => StringComparer.OrdinalIgnoreCase.Equals(path, target) || File.Exists(path),
        };
        AssertFailure(
            await new SafeZipArchiveService(targetFailureFileSystem)
                .ExtractAsync(Request(workspace), TestContext.Current.CancellationToken),
            "ARCHIVE_PATH_FILE_COLLISION");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingPathInspectionFailuresAreStructured(bool unauthorized)
    {
        using ArchiveTestWorkspace workspace = ValidWorkspace();
        var fileSystem = new DelegatingArchiveFileSystem
        {
            GetAttributesHandler = path =>
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(path, workspace.SourceArchivePath))
                {
                    if (unauthorized)
                    {
                        throw new UnauthorizedAccessException("private path");
                    }

                    throw new IOException("private path");
                }

                return File.GetAttributes(path);
            },
        };

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService(fileSystem)
            .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_PATH_INSPECTION_FAILED");
    }

    [Fact]
    public async Task InconsistentFilesystemViewFailsClosedWhenRequiredLeafDisappears()
    {
        using ArchiveTestWorkspace workspace = ValidWorkspace();
        int sourceFileChecks = 0;
        var fileSystem = new DelegatingArchiveFileSystem
        {
            FileExistsHandler = path =>
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(path, workspace.SourceArchivePath))
                {
                    sourceFileChecks++;
                    return sourceFileChecks == 1;
                }

                return File.Exists(path);
            },
        };

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService(fileSystem)
            .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_PATH_MISSING");
    }

    [Fact]
    public async Task InvalidRequestIsRejectedByExtractionAndNullRequestsThrow()
    {
        using ArchiveTestWorkspace workspace = ValidWorkspace();
        var service = new SafeZipArchiveService();
        ArchiveOperationRequest invalid = new(
            "relative",
            workspace.SourceArchivePath,
            workspace.ContainmentRoot,
            workspace.DestinationRoot,
            Policy());

        AssertFailure(
            await service.ExtractAsync(invalid, TestContext.Current.CancellationToken),
            "ARCHIVE_PATH_NOT_LOCAL_ABSOLUTE");
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.InspectAsync(null!, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ExtractAsync(null!, TestContext.Current.CancellationToken));
    }

    private static ArchiveTestWorkspace ValidWorkspace()
    {
        var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "file.txt", "safe"));
        return workspace;
    }

    private static ArchiveOperationRequest Request(ArchiveTestWorkspace workspace) =>
        new(
            workspace.ProjectRoot,
            workspace.SourceArchivePath,
            workspace.ContainmentRoot,
            workspace.DestinationRoot,
            Policy());

    private static ArchiveSafetyPolicy Policy() =>
        ArchiveSafetyPolicy.Create(1_000_000, 100, 10, 500_000, 900_000, 10_000, [".txt"]).Value!;

    private static void AssertFailure<T>(ArchiveOperationResult<T> result, string code)
        where T : class
    {
        Assert.False(result.IsSuccess);
        ArchiveFailure failure = Assert.Single(result.Failures);
        Assert.Equal(code, failure.Code);
        Assert.DoesNotContain("private path", failure.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NonSeekableReadStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public override bool CanSeek => false;
    }

    private sealed class ReportedLengthStream(byte[] bytes, long reportedLength) : MemoryStream(bytes, writable: false)
    {
        public override long Length => reportedLength;
    }
}
