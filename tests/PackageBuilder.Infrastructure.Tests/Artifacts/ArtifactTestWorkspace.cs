namespace PackageBuilder.Infrastructure.Tests.Artifacts;

internal sealed class ArtifactTestWorkspace : IDisposable
{
    public ArtifactTestWorkspace()
    {
        ProjectRoot = FindProjectRoot();
        Root = Path.Combine(ProjectRoot, "artifacts", "PB-0204", "tests", Guid.NewGuid().ToString("N"));
        FilesRoot = Path.Combine(Root, "files");
        _ = Directory.CreateDirectory(FilesRoot);
    }

    public string ProjectRoot { get; }

    public string Root { get; }

    public string FilesRoot { get; }

    public string AddFile(string relativePath, ReadOnlySpan<byte> content)
    {
        string path = Path.Combine(FilesRoot, relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content.ToArray());
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
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
