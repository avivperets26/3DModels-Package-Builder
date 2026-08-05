using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class UnrealDiscoveryTestWorkspace : IDisposable
{
    public UnrealDiscoveryTestWorkspace()
    {
        RepositoryRoot = FindRepositoryRoot();
        Root = Path.Combine(
            RepositoryRoot,
            "artifacts",
            "PB-0304",
            "tests",
            Guid.NewGuid().ToString("N"));
        ProjectRoot = CreateDirectory("project");
        ToolsRoot = CreateDirectory("project/tools");
    }

    public string RepositoryRoot { get; }

    public string Root { get; }

    public string ProjectRoot { get; }

    public string ToolsRoot { get; }

    public UnrealInstallationDiscoveryRequest Request(
        IEnumerable<string>? configuredRoots = null,
        IEnumerable<string>? launcherManifests = null,
        IEnumerable<string>? sourceBuildRoots = null,
        IEnumerable<string>? registeredRoots = null,
        IEnumerable<string>? standardRoots = null) =>
        new(
            BuildJobId.Create("Job-PB0304").Value!,
            ProjectRoot,
            ToolsRoot,
            configuredRoots ?? [],
            launcherManifests ?? [],
            sourceBuildRoots ?? [],
            registeredRoots ?? [],
            standardRoots ?? []);

    public string CreateEngine(string relativeRoot, int major = 5, int minor = 8, int patch = 0)
    {
        string root = CreateDirectory(relativeRoot);
        _ = CreateFile(Path.Combine(relativeRoot, "Engine", "Binaries", "Win64", "UnrealEditor-Cmd.exe"));
        _ = CreateFile(
            Path.Combine(relativeRoot, "Engine", "Build", "Build.version"),
            $$"""
            {
              "MajorVersion": {{major}},
              "MinorVersion": {{minor}},
              "PatchVersion": {{patch}},
              "Changelist": 1,
              "CompatibleChangelist": 1,
              "IsLicenseeVersion": 0,
              "IsPromotedBuild": 1,
              "BranchName": "++UE5+Release-{{major}}.{{minor}}"
            }
            """);
        return root;
    }

    public string CreateLauncherManifest(string relativePath, params (string AppName, string Root)[] entries)
    {
        string installations = string.Join(
            ",",
            entries.Select(static entry =>
                $$"""{"AppName":"{{entry.AppName}}","ArtifactId":"{{entry.AppName}}","InstallLocation":{{System.Text.Json.JsonSerializer.Serialize(entry.Root)}}}"""));
        return CreateFile(relativePath, $$"""{"InstallationList":[{{installations}}]}""");
    }

    public string CreateDirectory(string relativePath)
    {
        string path = Resolve(relativePath);
        _ = Directory.CreateDirectory(path);
        return path;
    }

    public string CreateFile(string relativePath, string contents = "probe")
    {
        string path = Resolve(relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    public string Resolve(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PackageBuilder.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Package Builder project root could not be located.");
    }
}

public sealed class DelegatingUnrealDiscoveryFileSystem : IUnrealDiscoveryFileSystem
{
    private readonly PackageBuilder.Infrastructure.Tools.PhysicalUnrealDiscoveryFileSystem _physical = new();

    public Func<string, bool>? DirectoryExistsOverride { get; init; }

    public Func<string, bool>? FileExistsOverride { get; init; }

    public Func<string, FileAttributes>? GetAttributesOverride { get; init; }

    public Func<string, IEnumerable<string>>? EnumerateDirectoriesOverride { get; init; }

    public Func<string, Stream>? OpenReadOverride { get; init; }

    public bool DirectoryExists(string path) => DirectoryExistsOverride?.Invoke(path) ?? _physical.DirectoryExists(path);

    public bool FileExists(string path) => FileExistsOverride?.Invoke(path) ?? _physical.FileExists(path);

    public FileAttributes GetAttributes(string path) =>
        GetAttributesOverride?.Invoke(path) ?? _physical.GetAttributes(path);

    public IEnumerable<string> EnumerateDirectories(string path) =>
        EnumerateDirectoriesOverride?.Invoke(path) ?? _physical.EnumerateDirectories(path);

    public Stream OpenRead(string path) => OpenReadOverride?.Invoke(path) ?? _physical.OpenRead(path);
}
