namespace PackageBuilder.Contracts.Tools;

/// <summary>Discovers Blender candidates and verifies selectable installations through vendor output.</summary>
public interface IBlenderInstallationLocator
{
    Task<BlenderInstallationDiscoveryReport> DiscoverAsync(
        BlenderInstallationDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Provides the minimal filesystem surface required for bounded Blender discovery.</summary>
public interface IBlenderDiscoveryFileSystem
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    FileAttributes GetAttributes(string path);

    IEnumerable<string> EnumerateDirectories(string path);

    IEnumerable<string> EnumerateFiles(string path);
}
