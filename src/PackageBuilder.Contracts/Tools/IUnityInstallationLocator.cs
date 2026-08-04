namespace PackageBuilder.Contracts.Tools;

/// <summary>Discovers Unity Editor candidates and verifies their versions and required modules.</summary>
public interface IUnityInstallationLocator
{
    Task<UnityInstallationDiscoveryReport> DiscoverAsync(
        UnityInstallationDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Provides the minimal filesystem surface required for bounded Unity discovery.</summary>
public interface IUnityDiscoveryFileSystem
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    FileAttributes GetAttributes(string path);

    IEnumerable<string> EnumerateDirectories(string path);
}
