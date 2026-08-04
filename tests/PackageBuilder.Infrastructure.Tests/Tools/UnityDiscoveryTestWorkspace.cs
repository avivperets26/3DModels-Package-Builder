using PackageBuilder.Contracts.Processes;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class UnityDiscoveryTestWorkspace : IDisposable
{
    public UnityDiscoveryTestWorkspace()
    {
        RepositoryRoot = FindRepositoryRoot();
        Root = Path.Combine(
            RepositoryRoot,
            "artifacts",
            "PB-0303",
            "tests",
            Guid.NewGuid().ToString("N"));
        ProjectRoot = CreateDirectory("project");
        ToolsRoot = CreateDirectory("project/tools");
        WorkingDirectory = CreateDirectory("project/runtime-data/working");
        TemporaryDirectory = CreateDirectory("project/runtime-data/temp");
        CacheDirectory = CreateDirectory("project/runtime-data/cache");
        LogDirectory = CreateDirectory("project/logs");
    }

    public string RepositoryRoot { get; }

    public string Root { get; }

    public string ProjectRoot { get; }

    public string ToolsRoot { get; }

    public string DefaultHubRoot => Resolve("project/tools/unity/Hub/Editor");

    public string WorkingDirectory { get; }

    public string TemporaryDirectory { get; }

    public string CacheDirectory { get; }

    public string LogDirectory { get; }

    public UnityInstallationDiscoveryRequest Request(
        IEnumerable<string>? configuredPaths = null,
        IEnumerable<string>? hubRoots = null,
        IEnumerable<UnityEditorModuleRequirement>? requiredModules = null) =>
        new(
            BuildJobId.Create("Job-PB0303").Value!,
            ProjectRoot,
            ToolsRoot,
            WorkingDirectory,
            TemporaryDirectory,
            CacheDirectory,
            LogDirectory,
            configuredPaths ?? [],
            hubRoots ?? [],
            requiredModules ?? []);

    public string CreateEditor(string relativeInstallationRoot)
    {
        _ = CreateDirectory(relativeInstallationRoot);
        return CreateFile(Path.Combine(relativeInstallationRoot, "Editor", "Unity.exe"));
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

    public static ProcessExecutionOperationResult VersionResult(
        ExternalProcessRequest request,
        string output,
        int exitCode = 0,
        bool truncated = false,
        ProcessCompletionMetadata? completion = null) =>
        ProcessExecutionResult.Success(
            new ExternalProcessReceipt(
                request.JobId,
                new ExecutableMetadata(
                    "tools/unity/test/Editor/Unity.exe",
                    5,
                    new string('0', 64),
                    fileVersion: null,
                    productVersion: null),
                exitCode,
                new ProcessStreamCapture(output, truncated),
                new ProcessStreamCapture(string.Empty, wasTruncated: false),
                completion));

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

public sealed class DelegatingUnityDiscoveryFileSystem : IUnityDiscoveryFileSystem
{
    private readonly PackageBuilder.Infrastructure.Tools.PhysicalUnityDiscoveryFileSystem _physical = new();

    public Func<string, bool>? DirectoryExistsOverride { get; init; }

    public Func<string, bool>? FileExistsOverride { get; init; }

    public Func<string, FileAttributes>? GetAttributesOverride { get; init; }

    public Func<string, IEnumerable<string>>? EnumerateDirectoriesOverride { get; init; }

    public bool DirectoryExists(string path) => DirectoryExistsOverride?.Invoke(path) ?? _physical.DirectoryExists(path);

    public bool FileExists(string path) => FileExistsOverride?.Invoke(path) ?? _physical.FileExists(path);

    public FileAttributes GetAttributes(string path) =>
        GetAttributesOverride?.Invoke(path) ?? _physical.GetAttributes(path);

    public IEnumerable<string> EnumerateDirectories(string path) =>
        EnumerateDirectoriesOverride?.Invoke(path) ?? _physical.EnumerateDirectories(path);
}
