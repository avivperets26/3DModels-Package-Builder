using PackageBuilder.Contracts.Snapshots;

namespace PackageBuilder.Infrastructure.Snapshots;

/// <summary>Provides locked, streaming physical filesystem access for source snapshots.</summary>
public sealed class PhysicalSourceSnapshotFileSystem : ISourceSnapshotFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

    public IEnumerable<string> EnumerateEntries(string directoryPath) =>
        Directory.EnumerateFileSystemEntries(directoryPath);

    public long GetFileLength(string path) => new FileInfo(path).Length;

    public Stream OpenReadLocked(string path) => new FileStream(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 65_536,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    public Stream CreateNewFile(string path) => new FileStream(
        path,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.None,
        bufferSize: 65_536,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    public void CreateDirectory(string path) => _ = Directory.CreateDirectory(path);

    public void MarkFileReadOnly(string path) =>
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
}
