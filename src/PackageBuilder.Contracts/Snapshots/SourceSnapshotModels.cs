using System.Collections.ObjectModel;

namespace PackageBuilder.Contracts.Snapshots;

/// <summary>Names the trusted project boundary, accepted source, job boundary, and new snapshot root.</summary>
public sealed class SourceSnapshotRequest(
    string projectRoot,
    string sourceRoot,
    string jobRoot,
    string snapshotRoot,
    SourceSnapshotPolicy policy)
{
    public string ProjectRoot { get; } = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));

    public string SourceRoot { get; } = sourceRoot ?? throw new ArgumentNullException(nameof(sourceRoot));

    public string JobRoot { get; } = jobRoot ?? throw new ArgumentNullException(nameof(jobRoot));

    public string SnapshotRoot { get; } = snapshotRoot ?? throw new ArgumentNullException(nameof(snapshotRoot));

    public SourceSnapshotPolicy Policy { get; } = policy ?? throw new ArgumentNullException(nameof(policy));
}

/// <summary>Records one copied file using a portable logical path, byte count, and lowercase SHA-256.</summary>
public sealed class SourceSnapshotFile(string relativePath, long bytes, string sha256)
{
    public string RelativePath { get; } = relativePath ?? throw new ArgumentNullException(nameof(relativePath));

    public long Bytes { get; } = bytes;

    public string Sha256 { get; } = sha256 ?? throw new ArgumentNullException(nameof(sha256));
}

/// <summary>Confirms the immutable files written into one dedicated job snapshot.</summary>
public sealed class SourceSnapshotReceipt
{
    private readonly ReadOnlyCollection<SourceSnapshotFile> _files;

    public SourceSnapshotReceipt(string snapshotRoot, IEnumerable<SourceSnapshotFile> files, long totalBytes)
    {
        ArgumentNullException.ThrowIfNull(files);
        SnapshotRoot = snapshotRoot ?? throw new ArgumentNullException(nameof(snapshotRoot));
        _files = new ReadOnlyCollection<SourceSnapshotFile>(files.ToArray());
        TotalBytes = totalBytes;
    }

    public string SnapshotRoot { get; }

    public IReadOnlyList<SourceSnapshotFile> Files => _files;

    public int FileCount => _files.Count;

    public long TotalBytes { get; }
}
