using System.IO.Compression;
using System.Reflection;
using PackageBuilder.Contracts.Archives;
using PackageBuilder.Infrastructure.Archives;

namespace PackageBuilder.Infrastructure.Tests.Archives;

public sealed class ArchiveInternalBoundaryTests
{
    [Fact]
    public void CanonicalDestinationRejectsEscapesAndInvalidPaths()
    {
        string root = Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory)!, "archive-root");
        MethodInfo method = PrivateStatic("CanonicalizeDestination");

        var escaped = (ArchiveOperationResult<string>)method.Invoke(
            null,
            [root, Path.Combine("..", "escape.txt"), "entry"])!;
        var invalid = (ArchiveOperationResult<string>)method.Invoke(
            null,
            [root, "invalid\0.txt", "entry"])!;

        Assert.Equal("ARCHIVE_ENTRY_OUTSIDE_DESTINATION", Assert.Single(escaped.Failures).Code);
        Assert.Equal("ARCHIVE_ENTRY_PATH_INVALID", Assert.Single(invalid.Failures).Code);
    }

    [Fact]
    public void EntryPlanningPropagatesCanonicalDestinationFailure()
    {
        using var stream = new MemoryStream();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        ZipArchiveEntry entry = archive.CreateEntry("file.txt");
        Type requestType = typeof(SafeZipArchiveService).GetNestedType(
            "ValidatedArchiveRequest",
            BindingFlags.NonPublic) ?? throw new InvalidOperationException("Validated request type was not found.");
        object request = Activator.CreateInstance(
            requestType,
            "C:\\project",
            "C:\\project\\source.zip",
            "C:\\project\\staging",
            "C:\\invalid\0root",
            Policy(20, 20))!;
        MethodInfo method = PrivateStatic("PlanEntry");

        var result = (ArchiveOperationResult<ArchiveEntryPlan>)method.Invoke(
            null,
            [entry, 0, request, new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)])!;

        Assert.Equal("ARCHIVE_ENTRY_PATH_INVALID", Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void ExpansionRatioTreatsNonEmptyZeroCompressedContentAsUnbounded()
    {
        MethodInfo method = PrivateStatic("ExpansionRatio");

        double ratio = (double)method.Invoke(null, [1L, 0L])!;

        Assert.Equal(double.PositiveInfinity, ratio);
    }

    [Fact]
    public async Task BoundedReaderEnforcesMetadataEntryAndTotalLimitsIndependently()
    {
        ArchiveSafetyPolicy standard = Policy(entryBytes: 20, totalBytes: 20);
        ArchiveSafetyPolicy smallEntry = Policy(entryBytes: 2, totalBytes: 20);
        var metadataPlan = new ArchiveEntryPlan(0, "file.txt", "destination", false, 4, 2);
        var entryPlan = new ArchiveEntryPlan(0, "file.txt", "destination", false, 4, 4);
        var totalPlan = new ArchiveEntryPlan(0, "file.txt", "destination", false, 4, 4);

        await AssertBoundedFailure(metadataPlan, standard, 0, "data");
        await AssertBoundedFailure(entryPlan, smallEntry, 0, "data");
        await AssertBoundedFailure(totalPlan, standard, 18, "data");
    }

    [Fact]
    public void ContractModelsExposeImmutableInspectionAndReceiptData()
    {
        var plan = new ArchiveEntryPlan(3, "model.fbx", "destination", false, 4, 5);
        var directory = new ArchiveEntryPlan(2, "folder", "folder-destination", true, 0, 0);
        var inspection = new ArchiveInspection([directory, plan], 20, 4, 5);
        var receipt = new ArchiveExtractionReceipt(inspection, "destination-root", 5);

        Assert.Equal(3, plan.Index);
        Assert.Equal("model.fbx", plan.RelativePath);
        Assert.Equal("destination", plan.DestinationPath);
        Assert.False(plan.IsDirectory);
        Assert.Equal(4, plan.CompressedBytes);
        Assert.Equal(5, plan.UncompressedBytes);
        Assert.Equal(2, inspection.EntryCount);
        Assert.Equal(1, inspection.FileCount);
        Assert.Equal(20, inspection.ArchiveBytes);
        Assert.Equal(4, inspection.TotalCompressedBytes);
        Assert.Equal(5, inspection.TotalUncompressedBytes);
        Assert.Same(inspection, receipt.Inspection);
        Assert.Equal("destination-root", receipt.DestinationRoot);
        Assert.Equal(5, receipt.ExtractedBytes);
        _ = Assert.Throws<ArgumentNullException>(() => new ArchiveInspection(null!, 0, 0, 0));
        _ = Assert.Throws<ArgumentNullException>(() => new ArchiveExtractionReceipt(null!, "destination", 0));
        _ = Assert.Throws<ArgumentNullException>(() => new ArchiveExtractionReceipt(inspection, null!, 0));
    }

    private static async Task AssertBoundedFailure(
        ArchiveEntryPlan plan,
        ArchiveSafetyPolicy policy,
        long totalBefore,
        string content)
    {
        MethodInfo method = PrivateStatic("ReadBoundedAsync");
        using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var task = (Task<long>)method.Invoke(
            null,
            [input, plan, policy, totalBefore, null, TestContext.Current.CancellationToken])!;

        _ = await Assert.ThrowsAsync<InvalidDataException>(() => task);
    }

    private static MethodInfo PrivateStatic(string name) =>
        typeof(SafeZipArchiveService).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Private archive method '{name}' was not found.");

    private static ArchiveSafetyPolicy Policy(long entryBytes, long totalBytes) =>
        ArchiveSafetyPolicy.Create(100, 10, 5, entryBytes, totalBytes, 10, [".txt"]).Value!;
}
