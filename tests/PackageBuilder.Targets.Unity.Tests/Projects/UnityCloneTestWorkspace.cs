using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Targets.Unity.Tests.Projects;

internal sealed class UnityCloneTestWorkspace : IDisposable
{
    public UnityCloneTestWorkspace()
    {
        string repositoryRoot = FindRepositoryRoot();
        Root = Path.Combine(
            repositoryRoot,
            "artifacts",
            "test-workspaces",
            "PB-0604",
            Guid.NewGuid().ToString("N"));
        Template = Path.Combine(Root, "template");
        Job = Path.Combine(Root, "job");
        _ = Directory.CreateDirectory(Path.Combine(Template, "Assets"));
        _ = Directory.CreateDirectory(Path.Combine(Template, "Packages"));
        _ = Directory.CreateDirectory(Path.Combine(Template, "ProjectSettings"));
        _ = Directory.CreateDirectory(Job);
        File.WriteAllText(Path.Combine(Template, "Assets", "marker.txt"), "template-content\n");
        File.WriteAllText(Path.Combine(Template, "Packages", "manifest.json"), "{}\n");
        File.WriteAllText(Path.Combine(Template, "ProjectSettings", "ProjectVersion.txt"), "version\n");
        JobId = BuildJobId.Create("Job-Unity-01").Value!;
    }

    public string Root { get; }

    public string Template { get; }

    public string Job { get; }

    public string Clone => Path.Combine(Job, "unity-project");

    public string Lock => Path.Combine(Job, ".packagebuilder-unity-clone.lock");

    public BuildJobId JobId { get; }

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

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
