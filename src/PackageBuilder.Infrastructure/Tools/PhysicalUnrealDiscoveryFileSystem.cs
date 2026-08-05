using PackageBuilder.Contracts.Tools;

namespace PackageBuilder.Infrastructure.Tools;

/// <summary>Reads only the filesystem facts required for bounded Unreal Engine discovery.</summary>
public sealed class PhysicalUnrealDiscoveryFileSystem : IUnrealDiscoveryFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

    public IEnumerable<string> EnumerateDirectories(string path) => Directory.EnumerateDirectories(path);

    public Stream OpenRead(string path) => File.OpenRead(path);
}
