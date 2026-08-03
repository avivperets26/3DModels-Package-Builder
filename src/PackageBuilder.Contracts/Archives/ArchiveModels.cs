using System.Collections.ObjectModel;

namespace PackageBuilder.Contracts.Archives;

/// <summary>Names the trusted project boundary, source ZIP, contained destination boundary, and dedicated destination.</summary>
public sealed class ArchiveOperationRequest(
    string projectRoot,
    string sourceArchivePath,
    string destinationContainmentRoot,
    string destinationRoot,
    ArchiveSafetyPolicy policy)
{
    public string ProjectRoot { get; } = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));

    public string SourceArchivePath { get; } = sourceArchivePath ?? throw new ArgumentNullException(nameof(sourceArchivePath));

    public string DestinationContainmentRoot { get; } = destinationContainmentRoot
            ?? throw new ArgumentNullException(nameof(destinationContainmentRoot));

    public string DestinationRoot { get; } = destinationRoot ?? throw new ArgumentNullException(nameof(destinationRoot));

    public ArchiveSafetyPolicy Policy { get; } = policy ?? throw new ArgumentNullException(nameof(policy));
}

/// <summary>Describes one validated ZIP entry and its canonical extraction target.</summary>
public sealed class ArchiveEntryPlan(
    int index,
    string relativePath,
    string destinationPath,
    bool isDirectory,
    long compressedBytes,
    long uncompressedBytes)
{
    public int Index { get; } = index;

    public string RelativePath { get; } = relativePath;

    public string DestinationPath { get; } = destinationPath;

    public bool IsDirectory { get; } = isDirectory;

    public long CompressedBytes { get; } = compressedBytes;

    public long UncompressedBytes { get; } = uncompressedBytes;
}

/// <summary>Contains the complete immutable preflight plan and measured archive totals.</summary>
public sealed class ArchiveInspection
{
    private readonly ReadOnlyCollection<ArchiveEntryPlan> _entries;

    public ArchiveInspection(
        IEnumerable<ArchiveEntryPlan> entries,
        long archiveBytes,
        long totalCompressedBytes,
        long totalUncompressedBytes)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = new ReadOnlyCollection<ArchiveEntryPlan>(entries.ToArray());
        ArchiveBytes = archiveBytes;
        TotalCompressedBytes = totalCompressedBytes;
        TotalUncompressedBytes = totalUncompressedBytes;
    }

    public IReadOnlyList<ArchiveEntryPlan> Entries => _entries;

    public int EntryCount => _entries.Count;

    public int FileCount => _entries.Count(static entry => !entry.IsDirectory);

    public long ArchiveBytes { get; }

    public long TotalCompressedBytes { get; }

    public long TotalUncompressedBytes { get; }
}

/// <summary>Confirms a successful extraction and the exact preflight plan that authorized it.</summary>
public sealed class ArchiveExtractionReceipt(ArchiveInspection inspection, string destinationRoot, long extractedBytes)
{
    public ArchiveInspection Inspection { get; } = inspection ?? throw new ArgumentNullException(nameof(inspection));

    public string DestinationRoot { get; } = destinationRoot ?? throw new ArgumentNullException(nameof(destinationRoot));

    public long ExtractedBytes { get; } = extractedBytes;
}
