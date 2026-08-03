using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Infrastructure.Artifacts;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

internal sealed class DelegatingArtifactHashFileSystem : IArtifactHashFileSystem
{
    private readonly PhysicalArtifactHashFileSystem _physical = new();

    public Func<string, bool>? FileExistsHandler { get; init; }

    public Func<string, bool>? DirectoryExistsHandler { get; init; }

    public Func<string, FileAttributes>? GetAttributesHandler { get; init; }

    public Func<string, long>? GetFileLengthHandler { get; init; }

    public Func<string, Stream>? OpenReadLockedHandler { get; init; }

    public bool FileExists(string path) => FileExistsHandler?.Invoke(path) ?? _physical.FileExists(path);

    public bool DirectoryExists(string path) => DirectoryExistsHandler?.Invoke(path) ?? _physical.DirectoryExists(path);

    public FileAttributes GetAttributes(string path) =>
        GetAttributesHandler?.Invoke(path) ?? _physical.GetAttributes(path);

    public long GetFileLength(string path) => GetFileLengthHandler?.Invoke(path) ?? _physical.GetFileLength(path);

    public Stream OpenReadLocked(string path) => OpenReadLockedHandler?.Invoke(path) ?? _physical.OpenReadLocked(path);
}
