namespace PackageBuilder.Contracts.Tools;

/// <summary>Discovers Unreal Engine candidates and verifies their contained editor layout and build metadata.</summary>
public interface IUnrealInstallationLocator
{
    Task<UnrealInstallationDiscoveryReport> DiscoverAsync(
        UnrealInstallationDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Provides the minimal filesystem surface required for bounded Unreal Engine discovery.</summary>
public interface IUnrealDiscoveryFileSystem
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    FileAttributes GetAttributes(string path);

    IEnumerable<string> EnumerateDirectories(string path);

    Stream OpenRead(string path);
}
