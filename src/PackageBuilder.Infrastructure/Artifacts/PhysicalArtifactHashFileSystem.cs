using PackageBuilder.Contracts.Artifacts;

namespace PackageBuilder.Infrastructure.Artifacts;

/// <summary>Opens contained artifacts for locked, sequential, asynchronous reads only.</summary>
public sealed class PhysicalArtifactHashFileSystem : IArtifactHashFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

    public long GetFileLength(string path) => new FileInfo(path).Length;

    public Stream OpenReadLocked(string path) => new FileStream(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 65_536,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
}
