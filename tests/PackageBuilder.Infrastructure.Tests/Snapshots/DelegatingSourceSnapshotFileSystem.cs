using PackageBuilder.Contracts.Snapshots;
using PackageBuilder.Infrastructure.Snapshots;

namespace PackageBuilder.Infrastructure.Tests.Snapshots;

internal sealed class DelegatingSourceSnapshotFileSystem : ISourceSnapshotFileSystem
{
    private readonly PhysicalSourceSnapshotFileSystem _physical = new();

    public Func<string, bool>? FileExistsHandler { get; init; }
    public Func<string, bool>? DirectoryExistsHandler { get; init; }
    public Func<string, FileAttributes>? GetAttributesHandler { get; init; }
    public Func<string, IEnumerable<string>>? EnumerateEntriesHandler { get; init; }
    public Func<string, long>? GetFileLengthHandler { get; init; }
    public Func<string, Stream>? OpenReadLockedHandler { get; init; }
    public Func<string, Stream>? CreateNewFileHandler { get; init; }
    public Action<string>? CreateDirectoryHandler { get; init; }
    public Action<string>? MarkFileReadOnlyHandler { get; init; }

    public bool FileExists(string path) => FileExistsHandler?.Invoke(path) ?? _physical.FileExists(path);
    public bool DirectoryExists(string path) => DirectoryExistsHandler?.Invoke(path) ?? _physical.DirectoryExists(path);
    public FileAttributes GetAttributes(string path) => GetAttributesHandler?.Invoke(path) ?? _physical.GetAttributes(path);
    public IEnumerable<string> EnumerateEntries(string directoryPath) =>
        EnumerateEntriesHandler?.Invoke(directoryPath) ?? _physical.EnumerateEntries(directoryPath);
    public long GetFileLength(string path) => GetFileLengthHandler?.Invoke(path) ?? _physical.GetFileLength(path);
    public Stream OpenReadLocked(string path) => OpenReadLockedHandler?.Invoke(path) ?? _physical.OpenReadLocked(path);
    public Stream CreateNewFile(string path) => CreateNewFileHandler?.Invoke(path) ?? _physical.CreateNewFile(path);
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
    public void MarkFileReadOnly(string path)
    {
        if (MarkFileReadOnlyHandler is null)
        {
            _physical.MarkFileReadOnly(path);
        }
        else
        {
            MarkFileReadOnlyHandler(path);
        }
    }
}
