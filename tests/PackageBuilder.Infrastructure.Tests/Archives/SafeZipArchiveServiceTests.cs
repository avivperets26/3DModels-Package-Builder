using System.IO.Compression;
using System.Security.Cryptography;
using PackageBuilder.Contracts.Archives;
using PackageBuilder.Infrastructure.Archives;

namespace PackageBuilder.Infrastructure.Tests.Archives;

public sealed class SafeZipArchiveServiceTests
{
    [Fact]
    public async Task ValidArchiveIsInspectedThenStreamedIntoDedicatedDestination()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive =>
        {
            _ = archive.CreateEntry("Models/");
            _ = archive.CreateEntry("BackslashDirectory\\");
            _ = ArchiveTestWorkspace.AddFile(archive, "Models/Bow.fbx", "model-data");
            _ = ArchiveTestWorkspace.AddFile(archive, "Textures/Bow.png", "texture-data");
        });
        string sourceHashBefore = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(workspace.SourceArchivePath)));
        var service = new SafeZipArchiveService();
        ArchiveOperationRequest request = Request(workspace);

        ArchiveOperationResult<ArchiveInspection> inspection =
            await service.InspectAsync(request, TestContext.Current.CancellationToken);

        Assert.True(inspection.IsSuccess);
        Assert.Equal(4, inspection.Value!.EntryCount);
        Assert.Equal(2, inspection.Value.FileCount);
        Assert.False(Directory.Exists(workspace.DestinationRoot));

        ArchiveOperationResult<ArchiveExtractionReceipt> extraction =
            await service.ExtractAsync(request, TestContext.Current.CancellationToken);

        Assert.True(extraction.IsSuccess);
        Assert.Equal(inspection.Value.TotalUncompressedBytes, extraction.Value!.ExtractedBytes);
        Assert.Equal("model-data", File.ReadAllText(Path.Combine(workspace.DestinationRoot, "Models", "Bow.fbx")));
        Assert.Equal("texture-data", File.ReadAllText(Path.Combine(workspace.DestinationRoot, "Textures", "Bow.png")));
        Assert.Equal(
            sourceHashBefore,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(workspace.SourceArchivePath))));
    }

    [Theory]
    [InlineData("../escape.txt", "ARCHIVE_ENTRY_TRAVERSAL")]
    [InlineData("folder/../../escape.txt", "ARCHIVE_ENTRY_TRAVERSAL")]
    [InlineData("/absolute.txt", "ARCHIVE_ENTRY_ABSOLUTE_PATH")]
    [InlineData("C:/absolute.txt", "ARCHIVE_ENTRY_ABSOLUTE_PATH")]
    [InlineData("folder//file.txt", "ARCHIVE_ENTRY_EMPTY_SEGMENT")]
    [InlineData("folder/./file.txt", "ARCHIVE_ENTRY_TRAVERSAL")]
    [InlineData("folder/file.txt ", "ARCHIVE_ENTRY_WINDOWS_ALIAS")]
    [InlineData("folder/CON.txt", "ARCHIVE_ENTRY_RESERVED_NAME")]
    [InlineData("folder/file:stream.txt", "ARCHIVE_ENTRY_INVALID_CHARACTER")]
    public async Task UnsafeEntryPathsFailBeforeDestinationCreation(string name, string expectedCode)
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, name, "unsafe"));

        ArchiveOperationResult<ArchiveExtractionReceipt> result =
            await new SafeZipArchiveService()
                .ExtractAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, expectedCode);
        Assert.False(Directory.Exists(workspace.DestinationRoot));
        Assert.False(File.Exists(Path.Combine(workspace.Root, "escape.txt")));
    }

    [Fact]
    public async Task CaseAndSeparatorEquivalentTargetsAreRejected()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive =>
        {
            _ = ArchiveTestWorkspace.AddFile(archive, "Folder/File.txt", "one");
            _ = ArchiveTestWorkspace.AddFile(archive, "folder\\file.TXT", "two");
        });

        ArchiveOperationResult<ArchiveInspection> result =
            await new SafeZipArchiveService()
                .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_DUPLICATE_TARGET");
    }

    [Fact]
    public async Task FileDirectoryPrefixCollisionsAreRejectedInEitherOrder()
    {
        foreach (bool childFirst in new[] { false, true })
        {
            using var workspace = new ArchiveTestWorkspace();
            workspace.CreateArchive(archive =>
            {
                if (childFirst)
                {
                    _ = ArchiveTestWorkspace.AddFile(archive, "node/child.txt", "child");
                    _ = ArchiveTestWorkspace.AddFile(archive, "node", "file");
                }
                else
                {
                    _ = ArchiveTestWorkspace.AddFile(archive, "node", "file");
                    _ = ArchiveTestWorkspace.AddFile(archive, "node/child.txt", "child");
                }
            });

            ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService()
                .InspectAsync(
                    Request(workspace, allowExtensionless: true),
                    TestContext.Current.CancellationToken);

            AssertFailure(result, "ARCHIVE_FILE_DIRECTORY_COLLISION");
        }
    }

    [Fact]
    public async Task SymbolicLinkMetadataIsRejected()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive =>
        {
            ZipArchiveEntry entry = ArchiveTestWorkspace.AddFile(archive, "link.txt", "target");
            entry.ExternalAttributes = (0xA000 | 0x1FF) << 16;
        });

        ArchiveOperationResult<ArchiveInspection> result =
            await new SafeZipArchiveService()
                .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_ENTRY_LINK_OR_SPECIAL");
    }

    [Theory]
    [InlineData(0xA000, "link.txt")]
    [InlineData(0x2000, "device.txt")]
    [InlineData(0x8000, "folder/")]
    [InlineData(0x4000, "file.txt")]
    public async Task UnixLinkSpecialAndConflictingTypeMetadataAreRejected(int unixType, string name)
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive =>
        {
            ZipArchiveEntry entry = ArchiveTestWorkspace.AddFile(archive, name, "data");
            entry.ExternalAttributes = (unixType | 0x1FF) << 16;
        });

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService()
            .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(
            result,
            unixType is 0x8000 or 0x4000 ? "ARCHIVE_ENTRY_TYPE_CONFLICT" : "ARCHIVE_ENTRY_LINK_OR_SPECIAL");
    }

    [Fact]
    public async Task WindowsReparseMetadataIsRejected()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive =>
        {
            ZipArchiveEntry entry = ArchiveTestWorkspace.AddFile(archive, "link.txt", "data");
            entry.ExternalAttributes = (int)FileAttributes.ReparsePoint;
        });

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService()
            .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_ENTRY_LINK_OR_SPECIAL");
    }

    [Fact]
    public async Task MatchingUnixDirectoryMetadataIsAccepted()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive =>
        {
            ZipArchiveEntry entry = archive.CreateEntry("folder/");
            entry.ExternalAttributes = (0x4000 | 0x1FF) << 16;
        });

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService()
            .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(Assert.Single(result.Value!.Entries).IsDirectory);
    }

    [Theory]
    [InlineData("folder/file.", "ARCHIVE_ENTRY_WINDOWS_ALIAS")]
    [InlineData("folder/control\u0001.txt", "ARCHIVE_ENTRY_INVALID_CHARACTER")]
    [InlineData("folder/file?.txt", "ARCHIVE_ENTRY_INVALID_CHARACTER")]
    public async Task EmptyAndWindowsUnsafeEntryNamesAreRejected(string name, string expectedCode)
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, name, "data"));

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService()
            .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, expectedCode);
    }

    [Fact]
    public async Task DirectoryEntryWithDataIsRejected()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive =>
        {
            ZipArchiveEntry entry = archive.CreateEntry("folder/");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("not-a-directory");
        });

        ArchiveOperationResult<ArchiveInspection> result =
            await new SafeZipArchiveService()
                .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_DIRECTORY_HAS_DATA");
    }

    [Fact]
    public async Task ExtensionPolicyRejectsUnexpectedContentBeforeWrites()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "script.exe", "binary"));

        ArchiveOperationResult<ArchiveExtractionReceipt> result =
            await new SafeZipArchiveService()
                .ExtractAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_EXTENSION_NOT_ALLOWED");
        Assert.False(Directory.Exists(workspace.DestinationRoot));
    }

    [Fact]
    public async Task EntryCountDepthEntrySizeTotalSizeAndRatioLimitsFailClosed()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive =>
        {
            _ = ArchiveTestWorkspace.AddFile(archive, "a/b/c/deep.txt", "1234567890");
            _ = ArchiveTestWorkspace.AddFile(archive, "second.txt", new string('x', 100));
        });
        var service = new SafeZipArchiveService();

        AssertFailure(
            await service.InspectAsync(
                Request(workspace, maximumEntryCount: 1), TestContext.Current.CancellationToken),
            "ARCHIVE_ENTRY_COUNT_LIMIT");
        AssertFailure(
            await service.InspectAsync(
                Request(workspace, maximumPathDepth: 2), TestContext.Current.CancellationToken),
            "ARCHIVE_PATH_DEPTH_LIMIT");
        AssertFailure(
            await service.InspectAsync(
                Request(workspace, maximumEntryBytes: 20), TestContext.Current.CancellationToken),
            "ARCHIVE_ENTRY_SIZE_LIMIT");
        AssertFailure(
            await service.InspectAsync(
                Request(workspace, maximumEntryBytes: 100, maximumTotalBytes: 105),
                TestContext.Current.CancellationToken),
            "ARCHIVE_TOTAL_SIZE_LIMIT");
        AssertFailure(
            await service.InspectAsync(
                Request(workspace, maximumExpansionRatio: 1), TestContext.Current.CancellationToken),
            "ARCHIVE_EXPANSION_RATIO_LIMIT");
        AssertFailure(
            await service.InspectAsync(
                Request(workspace, maximumArchiveBytes: 1), TestContext.Current.CancellationToken),
            "ARCHIVE_FILE_SIZE_LIMIT");
    }

    [Fact]
    public async Task CorruptArchiveReturnsStructuredFailureWithoutCreatingDestination()
    {
        using var workspace = new ArchiveTestWorkspace();
        File.WriteAllBytes(workspace.SourceArchivePath, [0x50, 0x4B, 0x01]);

        ArchiveOperationResult<ArchiveExtractionReceipt> result =
            await new SafeZipArchiveService()
                .ExtractAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_INVALID");
        Assert.False(Directory.Exists(workspace.DestinationRoot));
    }

    [Fact]
    public async Task CorruptArchiveInspectionReturnsStructuredFailure()
    {
        using var workspace = new ArchiveTestWorkspace();
        File.WriteAllBytes(workspace.SourceArchivePath, [0x50, 0x4B, 0x01]);

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService()
            .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_INVALID");
    }

    [Theory]
    [InlineData(20u, "ARCHIVE_CONTENT_SIZE_MISMATCH")]
    public async Task CentralDirectorySizeTamperingFailsPreflight(uint reportedSize, string expectedCode)
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "file.txt", "safe"));
        workspace.SetFirstCentralDirectoryUncompressedSize(reportedSize);

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService()
            .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, expectedCode);
    }

    [Fact]
    public async Task ImpossibleCompressedSizeMetadataFailsPreflight()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "file.txt", "safe"));
        workspace.SetFirstCentralDirectoryCompressedSize(uint.MaxValue);

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService()
            .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_COMPRESSED_SIZE_INVALID");
    }

    [Fact]
    public async Task InvalidRequestPathsAndDestinationReuseAreRejected()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "file.txt", "safe"));
        var service = new SafeZipArchiveService();

        AssertFailure(
            await service.InspectAsync(
                new ArchiveOperationRequest(
                    workspace.ProjectRoot,
                    Path.Combine(workspace.Root, "missing.zip"),
                    workspace.ContainmentRoot,
                    workspace.DestinationRoot,
                    Policy()),
                TestContext.Current.CancellationToken),
            "ARCHIVE_SOURCE_MISSING");
        AssertFailure(
            await service.InspectAsync(
                new ArchiveOperationRequest(
                    workspace.ProjectRoot,
                    workspace.SourceArchivePath,
                    workspace.ContainmentRoot,
                    Path.Combine(workspace.ProjectRoot, "outside-destination"),
                    Policy()),
                TestContext.Current.CancellationToken),
            "ARCHIVE_DESTINATION_OUTSIDE_CONTAINMENT");

        _ = Directory.CreateDirectory(workspace.DestinationRoot);
        AssertFailure(
            await service.InspectAsync(Request(workspace), TestContext.Current.CancellationToken),
            "ARCHIVE_DESTINATION_EXISTS");
    }

    [Fact]
    public async Task RequestValidationRejectsUnsafeBoundariesAndNonZipSources()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "file.txt", "safe"));
        var service = new SafeZipArchiveService();
        string outsideSource = Path.Combine(Path.GetDirectoryName(workspace.ProjectRoot)!, "outside.zip");
        File.Copy(workspace.SourceArchivePath, outsideSource, overwrite: true);
        string wrongExtension = Path.ChangeExtension(workspace.SourceArchivePath, ".bin");
        File.Copy(workspace.SourceArchivePath, wrongExtension, overwrite: true);

        try
        {
            AssertFailure(
                await service.InspectAsync(
                    RequestWith(workspace, projectRoot: Path.GetPathRoot(workspace.ProjectRoot)!),
                    TestContext.Current.CancellationToken),
                "ARCHIVE_PROJECT_ROOT_INVALID");
            AssertFailure(
                await service.InspectAsync(
                    RequestWith(workspace, sourcePath: outsideSource),
                    TestContext.Current.CancellationToken),
                "ARCHIVE_SOURCE_OUTSIDE_PROJECT");
            AssertFailure(
                await service.InspectAsync(
                    RequestWith(workspace, sourcePath: wrongExtension),
                    TestContext.Current.CancellationToken),
                "ARCHIVE_SOURCE_EXTENSION");
            AssertFailure(
                await service.InspectAsync(
                    RequestWith(workspace, containmentRoot: Path.Combine(workspace.ProjectRoot, "missing")),
                    TestContext.Current.CancellationToken),
                "ARCHIVE_CONTAINMENT_ROOT_INVALID");
            AssertFailure(
                await service.InspectAsync(
                    RequestWith(workspace, containmentRoot: Path.GetDirectoryName(workspace.ProjectRoot)!),
                    TestContext.Current.CancellationToken),
                "ARCHIVE_CONTAINMENT_ROOT_INVALID");
            AssertFailure(
                await service.InspectAsync(
                    RequestWith(workspace, destinationRoot: workspace.ContainmentRoot),
                    TestContext.Current.CancellationToken),
                "ARCHIVE_DESTINATION_OUTSIDE_CONTAINMENT");
        }
        finally
        {
            File.Delete(outsideSource);
            File.Delete(wrongExtension);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" relative")]
    [InlineData("relative")]
    [InlineData("\\\\server\\share")]
    [InlineData("\\\\?\\C:\\unsafe")]
    public async Task RequestValidationRejectsNonCanonicalPaths(string invalidPath)
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "file.txt", "safe"));

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService()
            .InspectAsync(RequestWith(workspace, projectRoot: invalidPath), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Failures,
            failure => failure.Code is "ARCHIVE_PATH_EMPTY_OR_WHITESPACE" or "ARCHIVE_PATH_NOT_LOCAL_ABSOLUTE");
    }

    [Fact]
    public async Task RequestValidationSanitizesCanonicalizationFailures()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "file.txt", "safe"));
        string invalidAbsolutePath = string.Concat(Path.GetPathRoot(workspace.ProjectRoot), "invalid", '\0');

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService()
            .InspectAsync(
                RequestWith(workspace, projectRoot: invalidAbsolutePath),
                TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_PATH_INVALID");
    }

    [Fact]
    public async Task ExistingFileInDestinationChainIsRejected()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "file.txt", "safe"));
        string collision = Path.Combine(workspace.ContainmentRoot, "collision");
        File.WriteAllText(collision, "file");

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService()
            .InspectAsync(
                RequestWith(workspace, destinationRoot: Path.Combine(collision, "child")),
                TestContext.Current.CancellationToken);

        AssertFailure(result, "ARCHIVE_PATH_FILE_COLLISION");
    }

    [Fact]
    public async Task EmptyAndZeroLengthArchivesAreHandledDeterministically()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "empty.txt", ""));

        ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService()
            .InspectAsync(Request(workspace), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.TotalUncompressedBytes);
    }

    [Fact]
    public async Task ExistingDestinationAncestorReparsePointIsRejected()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "file.txt", "safe"));
        string physical = Path.Combine(workspace.Root, "physical");
        string link = Path.Combine(workspace.ContainmentRoot, "link");
        _ = Directory.CreateDirectory(physical);

        try
        {
            _ = Directory.CreateSymbolicLink(link, physical);
            var request = new ArchiveOperationRequest(
                workspace.ProjectRoot,
                workspace.SourceArchivePath,
                workspace.ContainmentRoot,
                Path.Combine(link, "extracted"),
                Policy());

            ArchiveOperationResult<ArchiveInspection> result =
                await new SafeZipArchiveService()
                    .InspectAsync(request, TestContext.Current.CancellationToken);

            AssertFailure(result, "ARCHIVE_PATH_REPARSE_POINT");
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }
        }
    }

    [Fact]
    public async Task PreCancelledInspectionAndExtractionReturnStructuredCancellation()
    {
        using var workspace = new ArchiveTestWorkspace();
        workspace.CreateArchive(archive => _ = ArchiveTestWorkspace.AddFile(archive, "file.txt", "safe"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new SafeZipArchiveService();

        AssertFailure(await service.InspectAsync(Request(workspace), cancellation.Token), "ARCHIVE_CANCELLED");
        AssertFailure(await service.ExtractAsync(Request(workspace), cancellation.Token), "ARCHIVE_CANCELLED");
        Assert.False(Directory.Exists(workspace.DestinationRoot));
    }

    [Fact]
    public void ConstructorsRejectNullDependenciesAndArguments()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new SafeZipArchiveService(null!));
        _ = Assert.Throws<ArgumentNullException>(
            () => new ArchiveOperationRequest(null!, "source", "containment", "destination", Policy()));
        _ = Assert.Throws<ArgumentNullException>(
            () => new ArchiveOperationRequest("project", null!, "containment", "destination", Policy()));
        _ = Assert.Throws<ArgumentNullException>(
            () => new ArchiveOperationRequest("project", "source", null!, "destination", Policy()));
        _ = Assert.Throws<ArgumentNullException>(
            () => new ArchiveOperationRequest("project", "source", "containment", null!, Policy()));
        _ = Assert.Throws<ArgumentNullException>(
            () => new ArchiveOperationRequest("project", "source", "containment", "destination", null!));
    }

    private static ArchiveOperationRequest Request(
        ArchiveTestWorkspace workspace,
        long maximumArchiveBytes = 1_000_000,
        int maximumEntryCount = 100,
        int maximumPathDepth = 10,
        long maximumEntryBytes = 500_000,
        long maximumTotalBytes = 900_000,
        double maximumExpansionRatio = 10_000,
        bool allowExtensionless = false) =>
        new(
            workspace.ProjectRoot,
            workspace.SourceArchivePath,
            workspace.ContainmentRoot,
            workspace.DestinationRoot,
            Policy(
                maximumArchiveBytes,
                maximumEntryCount,
                maximumPathDepth,
                maximumEntryBytes,
                maximumTotalBytes,
                maximumExpansionRatio,
                allowExtensionless));

    private static ArchiveOperationRequest RequestWith(
        ArchiveTestWorkspace workspace,
        string? projectRoot = null,
        string? sourcePath = null,
        string? containmentRoot = null,
        string? destinationRoot = null) =>
        new(
            projectRoot ?? workspace.ProjectRoot,
            sourcePath ?? workspace.SourceArchivePath,
            containmentRoot ?? workspace.ContainmentRoot,
            destinationRoot ?? workspace.DestinationRoot,
            Policy());

    private static ArchiveSafetyPolicy Policy(
        long maximumArchiveBytes = 1_000_000,
        int maximumEntryCount = 100,
        int maximumPathDepth = 10,
        long maximumEntryBytes = 500_000,
        long maximumTotalBytes = 900_000,
        double maximumExpansionRatio = 10_000,
        bool allowExtensionless = false) =>
        ArchiveSafetyPolicy.Create(
            maximumArchiveBytes,
            maximumEntryCount,
            maximumPathDepth,
            maximumEntryBytes,
            maximumTotalBytes,
            maximumExpansionRatio,
            [".txt", ".fbx", ".png"],
            allowExtensionless).Value!;

    private static void AssertFailure<T>(ArchiveOperationResult<T> result, string code)
        where T : class
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        ArchiveFailure failure = Assert.Single(result.Failures);
        Assert.Equal(code, failure.Code);
        Assert.False(string.IsNullOrWhiteSpace(failure.Location));
        Assert.False(string.IsNullOrWhiteSpace(failure.Diagnostic));
    }
}
