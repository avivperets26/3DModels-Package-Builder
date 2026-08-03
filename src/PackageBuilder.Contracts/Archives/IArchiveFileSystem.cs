namespace PackageBuilder.Contracts.Archives;

/// <summary>Abstracts only the filesystem operations required by safe archive processing.</summary>
public interface IArchiveFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    FileAttributes GetAttributes(string path);

    Stream OpenReadLocked(string path);

    Stream CreateNewFile(string path);

    void CreateDirectory(string path);
}
