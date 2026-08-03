namespace PackageBuilder.Infrastructure.Tests.Snapshots;

internal sealed class SnapshotTestWorkspace : IDisposable
{
    public SnapshotTestWorkspace()
    {
        ProjectRoot = FindProjectRoot();
        Root = Path.Combine(ProjectRoot, "artifacts", "PB-0203", "tests", Guid.NewGuid().ToString("N"));
        SourceRoot = Path.Combine(Root, "source-assets", "Product");
        JobRoot = Path.Combine(Root, "jobs", "Job-01");
        SnapshotRoot = Path.Combine(JobRoot, "source-snapshot");
        _ = Directory.CreateDirectory(SourceRoot);
        _ = Directory.CreateDirectory(JobRoot);
    }

    public string ProjectRoot { get; }

    public string Root { get; }

    public string SourceRoot { get; }

    public string JobRoot { get; }

    public string SnapshotRoot { get; }

    public string AddSource(string relativePath, string content)
    {
        string path = Path.Combine(SourceRoot, relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(Root, recursive: true);
    }

    private static string FindProjectRoot()
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
