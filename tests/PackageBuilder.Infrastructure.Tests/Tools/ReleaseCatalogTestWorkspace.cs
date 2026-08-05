using System.Text;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

internal sealed class ReleaseCatalogTestWorkspace : IDisposable
{
    public ReleaseCatalogTestWorkspace()
    {
        RepositoryRoot = FindRepositoryRoot();
        Root = Path.Combine(
            RepositoryRoot,
            "artifacts",
            "PB-0305",
            "tests",
            Guid.NewGuid().ToString("N"));
        ProjectRoot = CreateDirectory("project");
        DownloadsRoot = CreateDirectory("project/downloads");
        CacheRoot = CreateDirectory("project/runtime-data/cache");
    }

    public string RepositoryRoot { get; }

    public string Root { get; }

    public string ProjectRoot { get; }

    public string DownloadsRoot { get; }

    public string CacheRoot { get; }

    public OfficialReleaseCatalogRequest Request(ToolKind tool, bool allowNetworkRefresh = false) =>
        new(tool, ProjectRoot, DownloadsRoot, CacheRoot, allowNetworkRefresh);

    public string CreateDirectory(string relativePath)
    {
        string path = Resolve(relativePath);
        _ = Directory.CreateDirectory(path);
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

internal sealed class StubReleaseMetadataTransport : IOfficialReleaseMetadataTransport
{
    public int CallCount { get; private set; }

    public Func<Uri, int, CancellationToken, ReleaseCatalogResult<OfficialReleaseMetadataPayload>>? Fetch { get; init; }

    public Task<ReleaseCatalogResult<OfficialReleaseMetadataPayload>> FetchAsync(
        Uri source,
        int maximumBytes,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        ReleaseCatalogResult<OfficialReleaseMetadataPayload> result = Fetch?.Invoke(
            source,
            maximumBytes,
            cancellationToken)
            ?? ReleaseCatalogResult.Failure<OfficialReleaseMetadataPayload>(
                "TEST_TRANSPORT_NOT_CONFIGURED",
                "The test transport was not configured.");
        return Task.FromResult(result);
    }

    public static ReleaseCatalogResult<OfficialReleaseMetadataPayload> Payload(Uri source, string contents) =>
        ReleaseCatalogResult.Success(
            new OfficialReleaseMetadataPayload(source, Encoding.UTF8.GetBytes(contents)));
}

internal sealed class FixedReleaseCatalogTimeProvider(DateTimeOffset value) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => value;
}
