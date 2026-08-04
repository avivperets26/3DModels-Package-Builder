using PackageBuilder.Contracts.Tools;

namespace PackageBuilder.Infrastructure.Tools;

/// <summary>Reads only the filesystem facts required for bounded Unity Editor discovery.</summary>
public sealed class PhysicalUnityDiscoveryFileSystem : IUnityDiscoveryFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

    public IEnumerable<string> EnumerateDirectories(string path) => Directory.EnumerateDirectories(path);
}
