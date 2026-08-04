namespace PackageBuilder.Infrastructure.Tests.Logging;

internal sealed class StructuredLoggingTestWorkspace : IDisposable
{
    public StructuredLoggingTestWorkspace()
    {
        string projectRoot = FindProjectRoot();
        Root = Path.Combine(projectRoot, "artifacts", "PB-0212", "tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static string FindProjectRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "PackageBuilder.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        return current ?? throw new InvalidOperationException("The repository root could not be located.");
    }
}
