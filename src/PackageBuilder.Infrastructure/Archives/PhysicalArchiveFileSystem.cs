using PackageBuilder.Contracts.Archives;

namespace PackageBuilder.Infrastructure.Archives;

/// <summary>Provides locked, asynchronous physical filesystem access for archive processing.</summary>
public sealed class PhysicalArchiveFileSystem : IArchiveFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

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
}
