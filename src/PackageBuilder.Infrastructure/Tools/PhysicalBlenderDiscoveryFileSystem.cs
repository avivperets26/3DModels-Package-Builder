using PackageBuilder.Contracts.Tools;

namespace PackageBuilder.Infrastructure.Tools;

/// <summary>Reads only the filesystem facts required for bounded Blender candidate discovery.</summary>
public sealed class PhysicalBlenderDiscoveryFileSystem : IBlenderDiscoveryFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

    public IEnumerable<string> EnumerateDirectories(string path) => Directory.EnumerateDirectories(path);

    public IEnumerable<string> EnumerateFiles(string path) => Directory.EnumerateFiles(path);
}
