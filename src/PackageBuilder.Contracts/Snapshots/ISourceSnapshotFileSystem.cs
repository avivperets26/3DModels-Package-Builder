namespace PackageBuilder.Contracts.Snapshots;

/// <summary>Abstracts only filesystem operations required to create immutable source snapshots.</summary>
public interface ISourceSnapshotFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    FileAttributes GetAttributes(string path);

    IEnumerable<string> EnumerateEntries(string directoryPath);

    long GetFileLength(string path);

    Stream OpenReadLocked(string path);

    Stream CreateNewFile(string path);

    void CreateDirectory(string path);

    void MarkFileReadOnly(string path);
}
