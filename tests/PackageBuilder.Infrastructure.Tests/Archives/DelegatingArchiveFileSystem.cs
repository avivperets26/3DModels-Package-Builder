using PackageBuilder.Contracts.Archives;
using PackageBuilder.Infrastructure.Archives;

namespace PackageBuilder.Infrastructure.Tests.Archives;

internal sealed class DelegatingArchiveFileSystem : IArchiveFileSystem
{
    private readonly PhysicalArchiveFileSystem _physical = new();

    public Func<string, bool>? FileExistsHandler { get; init; }

    public Func<string, bool>? DirectoryExistsHandler { get; init; }

    public Func<string, FileAttributes>? GetAttributesHandler { get; init; }

    public Func<string, Stream>? OpenReadLockedHandler { get; init; }

    public Func<string, Stream>? CreateNewFileHandler { get; init; }

    public Action<string>? CreateDirectoryHandler { get; init; }

    public bool FileExists(string path) => FileExistsHandler?.Invoke(path) ?? _physical.FileExists(path);

    public bool DirectoryExists(string path) => DirectoryExistsHandler?.Invoke(path) ?? _physical.DirectoryExists(path);

    public FileAttributes GetAttributes(string path) =>
        GetAttributesHandler?.Invoke(path) ?? _physical.GetAttributes(path);

    public Stream OpenReadLocked(string path) =>
        OpenReadLockedHandler?.Invoke(path) ?? _physical.OpenReadLocked(path);

    public Stream CreateNewFile(string path) =>
        CreateNewFileHandler?.Invoke(path) ?? _physical.CreateNewFile(path);

    public void CreateDirectory(string path)
    {
        if (CreateDirectoryHandler is null)
        {
            _physical.CreateDirectory(path);
        }
        else
        {
            CreateDirectoryHandler(path);
        }
    }
}
