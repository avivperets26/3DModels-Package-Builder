namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Abstracts the read-only filesystem operations required for streamed artifact hashing.</summary>
public interface IArtifactHashFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    FileAttributes GetAttributes(string path);

    long GetFileLength(string path);

    Stream OpenReadLocked(string path);
}
