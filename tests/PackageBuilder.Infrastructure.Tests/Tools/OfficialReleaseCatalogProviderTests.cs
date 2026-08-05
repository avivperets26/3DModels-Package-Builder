using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;
using PackageBuilder.Infrastructure.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class OfficialReleaseCatalogProviderTests
{
    private static readonly DateTimeOffset _refreshTime = new(2026, 8, 5, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ConstructorRejectsNullTransport() =>
        _ = Assert.Throws<ArgumentNullException>(() => new OfficialReleaseCatalogProvider(null!));

    [Fact]
    public async Task CacheOnlyIsOfflineByDefault()
    {
        using var workspace = new ReleaseCatalogTestWorkspace();
        var transport = new StubReleaseMetadataTransport();
        OfficialReleaseCatalogProvider provider = Provider(transport);

        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> result = await provider.GetAsync(
            workspace.Request(ToolKind.DotNet),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("RELEASE_CATALOG_CACHE_UNAVAILABLE", result.Error!.Code);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task RefreshNormalizesAndAtomicallyStoresOfficialDotNetMetadata()
    {
        using var workspace = new ReleaseCatalogTestWorkspace();
        var transport = new StubReleaseMetadataTransport
        {
            Fetch = static (source, _, _) => StubReleaseMetadataTransport.Payload(source, DotNetMetadata),
        };
        OfficialReleaseCatalogProvider provider = Provider(transport);

        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> result = await provider.GetAsync(
            workspace.Request(ToolKind.DotNet, allowNetworkRefresh: true),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        OfficialReleaseCatalogSnapshot snapshot = result.Value!;
        Assert.Equal(ReleaseCatalogOrigin.FreshNetwork, snapshot.Origin);
        Assert.Equal(_refreshTime, snapshot.Catalog.RefreshedAtUtc);
        Assert.Equal(
            ["11.0.100-preview.6.26359.118", "10.0.302", "9.0.316"],
            snapshot.Catalog.Releases.Select(static release => release.Version.Value));
        Assert.Equal(
            ToolReleaseSupport.LongTermSupport,
            snapshot.Catalog.Releases.Single(static release => release.Version.Value == "10.0.302").Support);
        Assert.DoesNotContain(snapshot.Catalog.Releases, static release => release.Version.Value == "8.0.423");
        Assert.True(File.Exists(snapshot.RawMetadataPath));
        Assert.True(File.Exists(snapshot.CacheMetadataPath));
        Assert.StartsWith(workspace.DownloadsRoot + Path.DirectorySeparatorChar, snapshot.RawMetadataPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(workspace.CacheRoot + Path.DirectorySeparatorChar, snapshot.CacheMetadataPath, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(snapshot.CacheMetadataPath)!, "*.tmp"));
    }

    [Fact]
    public async Task CacheOnlyReturnsPersistedMetadataWithoutTransportAccess()
    {
        using var workspace = new ReleaseCatalogTestWorkspace();
        var refreshTransport = new StubReleaseMetadataTransport
        {
            Fetch = static (source, _, _) => StubReleaseMetadataTransport.Payload(source, DotNetMetadata),
        };
        OfficialReleaseCatalogProvider provider = Provider(refreshTransport);
        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> refreshed = await provider.GetAsync(
            workspace.Request(ToolKind.DotNet, allowNetworkRefresh: true),
            TestContext.Current.CancellationToken);
        Assert.True(refreshed.IsSuccess);

        var offlineTransport = new StubReleaseMetadataTransport();
        OfficialReleaseCatalogProvider offlineProvider = Provider(offlineTransport);
        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> cached = await offlineProvider.GetAsync(
            workspace.Request(ToolKind.DotNet),
            TestContext.Current.CancellationToken);

        Assert.True(cached.IsSuccess);
        Assert.Equal(ReleaseCatalogOrigin.Cache, cached.Value!.Origin);
        Assert.Equal(_refreshTime, cached.Value.Catalog.RefreshedAtUtc);
        Assert.Null(cached.Value.RefreshFailureCode);
        Assert.Equal(0, offlineTransport.CallCount);
    }

    [Fact]
    public async Task ConcurrentRefreshesSerializeWritesAndLeaveAReadableCache()
    {
        using var workspace = new ReleaseCatalogTestWorkspace();
        var transport = new StubReleaseMetadataTransport
        {
            Fetch = static (source, _, _) => StubReleaseMetadataTransport.Payload(source, DotNetMetadata),
        };
        OfficialReleaseCatalogProvider provider = Provider(transport);

        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot>[] refreshes = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => provider.GetAsync(
                workspace.Request(ToolKind.DotNet, allowNetworkRefresh: true),
                TestContext.Current.CancellationToken)));
        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> cached = await Provider(
            new StubReleaseMetadataTransport()).GetAsync(
                workspace.Request(ToolKind.DotNet),
                TestContext.Current.CancellationToken);

        Assert.All(refreshes, static result => Assert.True(result.IsSuccess));
        Assert.True(cached.IsSuccess);
        Assert.Equal(ReleaseCatalogOrigin.Cache, cached.Value!.Origin);
        Assert.Equal(3, cached.Value.Catalog.Releases.Count);
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(cached.Value.CacheMetadataPath)!, "*.tmp"));
    }

    [Fact]
    public async Task NetworkFailureReturnsLastKnownValidMetadataWithFailureProvenance()
    {
        using var workspace = new ReleaseCatalogTestWorkspace();
        _ = await SeedDotNetCache(workspace);
        var unavailable = new StubReleaseMetadataTransport
        {
            Fetch = static (_, _, _) => ReleaseCatalogResult.Failure<OfficialReleaseMetadataPayload>(
                "RELEASE_TRANSPORT_UNAVAILABLE",
                "Unavailable."),
        };

        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> result = await Provider(unavailable).GetAsync(
            workspace.Request(ToolKind.DotNet, allowNetworkRefresh: true),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReleaseCatalogOrigin.LastKnownCache, result.Value!.Origin);
        Assert.Equal("RELEASE_TRANSPORT_UNAVAILABLE", result.Value.RefreshFailureCode);
        Assert.Equal(_refreshTime, result.Value.Catalog.RefreshedAtUtc);
    }

    [Fact]
    public async Task InvalidFreshMetadataDoesNotReplaceLastKnownCache()
    {
        using var workspace = new ReleaseCatalogTestWorkspace();
        OfficialReleaseCatalogSnapshot seeded = await SeedDotNetCache(workspace);
        byte[] before = await File.ReadAllBytesAsync(
            seeded.CacheMetadataPath,
            TestContext.Current.CancellationToken);
        var invalid = new StubReleaseMetadataTransport
        {
            Fetch = static (source, _, _) => StubReleaseMetadataTransport.Payload(source, "{\"releases-index\":{}}"),
        };

        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> result = await Provider(invalid).GetAsync(
            workspace.Request(ToolKind.DotNet, allowNetworkRefresh: true),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReleaseCatalogOrigin.LastKnownCache, result.Value!.Origin);
        Assert.Equal("RELEASE_SOURCE_METADATA_INVALID", result.Value.RefreshFailureCode);
        Assert.Equal(before, await File.ReadAllBytesAsync(
            seeded.CacheMetadataPath,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(ToolKind.Blender, BlenderMetadata, "4.5.0-beta.2", "4.2.0")]
    [InlineData(ToolKind.Unity, UnityMetadata, "6000.4.0b2", "6000.3.5f1")]
    [InlineData(ToolKind.Unreal, UnrealMetadata, "5.9.0-preview.2", "5.8.0")]
    public async Task VendorAdaptersReturnStableLtsAndPreviewMetadata(
        ToolKind tool,
        string metadata,
        string expectedPreview,
        string expectedStable)
    {
        using var workspace = new ReleaseCatalogTestWorkspace();
        var transport = new StubReleaseMetadataTransport
        {
            Fetch = (source, _, _) => StubReleaseMetadataTransport.Payload(source, metadata),
        };

        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> result = await Provider(transport).GetAsync(
            workspace.Request(tool, allowNetworkRefresh: true),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value!.Catalog.Releases, release =>
            release.Version.Value == expectedPreview
            && release.Version.Channel == ToolReleaseChannel.Preview);
        Assert.Contains(result.Value.Catalog.Releases, release =>
            release.Version.Value == expectedStable
            && release.Version.Channel == ToolReleaseChannel.Stable);
    }

    [Fact]
    public async Task UnityAdapterPreservesLtsClassificationAndReleaseTimestamp()
    {
        using var workspace = new ReleaseCatalogTestWorkspace();
        var transport = new StubReleaseMetadataTransport
        {
            Fetch = static (source, _, _) => StubReleaseMetadataTransport.Payload(source, UnityMetadata),
        };

        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> result = await Provider(transport).GetAsync(
            workspace.Request(ToolKind.Unity, allowNetworkRefresh: true),
            TestContext.Current.CancellationToken);

        OfficialToolRelease lts = result.Value!.Catalog.Releases.Single(
            static release => release.Version.Value == "6000.3.5f1");
        Assert.Equal(ToolReleaseSupport.LongTermSupport, lts.Support);
        Assert.Equal(new DateTimeOffset(2026, 7, 14, 9, 10, 11, TimeSpan.Zero), lts.ReleasedAtUtc);
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("external-downloads")]
    [InlineData("wrong-leaf")]
    public async Task RejectsUnsafeOrMisconfiguredRootsBeforeTransport(string scenario)
    {
        using var workspace = new ReleaseCatalogTestWorkspace();
        var transport = new StubReleaseMetadataTransport();
        OfficialReleaseCatalogRequest valid = workspace.Request(ToolKind.DotNet, allowNetworkRefresh: true);
        OfficialReleaseCatalogRequest request = scenario switch
        {
            "relative" => valid with { ProjectRoot = "relative" },
            "external-downloads" => valid with { DownloadsRoot = workspace.CreateDirectory("external/downloads") },
            _ => valid with { CacheRoot = workspace.CreateDirectory("project/runtime-data/not-cache") },
        };

        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> result = await Provider(transport).GetAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("RELEASE_CATALOG_PATHS_UNSAFE", result.Error!.Code);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task CorruptOrVersionIncompatibleCacheFailsClosed()
    {
        using var workspace = new ReleaseCatalogTestWorkspace();
        OfficialReleaseCatalogSnapshot seeded = await SeedDotNetCache(workspace);
        await File.WriteAllTextAsync(
            seeded.CacheMetadataPath,
            "{\"schemaVersion\":999,\"tool\":\"DotNet\"}",
            TestContext.Current.CancellationToken);

        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> result = await Provider(
            new StubReleaseMetadataTransport()).GetAsync(
                workspace.Request(ToolKind.DotNet),
                TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("RELEASE_CATALOG_CACHE_UNAVAILABLE", result.Error!.Code);
    }

    [Fact]
    public async Task CancellationStopsBeforeNetworkOrCacheMutation()
    {
        using var workspace = new ReleaseCatalogTestWorkspace();
        var transport = new StubReleaseMetadataTransport();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> result = await Provider(transport).GetAsync(
            workspace.Request(ToolKind.DotNet, allowNetworkRefresh: true),
            cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal("RELEASE_CATALOG_CANCELLED", result.Error!.Code);
        Assert.Equal(0, transport.CallCount);
        Assert.False(Directory.Exists(Path.Combine(workspace.DownloadsRoot, "release-catalogs")));
    }

    [Fact]
    public async Task OversizedOrCrossAuthorityPayloadCannotPopulateCache()
    {
        using var workspace = new ReleaseCatalogTestWorkspace();
        var transport = new StubReleaseMetadataTransport
        {
            Fetch = static (_, maximumBytes, _) => ReleaseCatalogResult.Success(
                new OfficialReleaseMetadataPayload(
                    new Uri("https://attacker.invalid/releases"),
                    new byte[maximumBytes + 1])),
        };

        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> result = await Provider(transport).GetAsync(
            workspace.Request(ToolKind.DotNet, allowNetworkRefresh: true),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("RELEASE_CATALOG_REFRESH_AND_CACHE_UNAVAILABLE", result.Error!.Code);
    }

    private static OfficialReleaseCatalogProvider Provider(IOfficialReleaseMetadataTransport transport) =>
        new(transport, new FixedReleaseCatalogTimeProvider(_refreshTime));

    private static async Task<OfficialReleaseCatalogSnapshot> SeedDotNetCache(
        ReleaseCatalogTestWorkspace workspace)
    {
        var transport = new StubReleaseMetadataTransport
        {
            Fetch = static (source, _, _) => StubReleaseMetadataTransport.Payload(source, DotNetMetadata),
        };
        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> result = await Provider(transport).GetAsync(
            workspace.Request(ToolKind.DotNet, allowNetworkRefresh: true),
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private const string DotNetMetadata = """
        {
          "releases-index": [
            {"latest-sdk":"11.0.100-preview.6.26359.118","latest-release-date":"2026-07-14","support-phase":"preview","release-type":"sts"},
            {"latest-sdk":"10.0.302","latest-release-date":"2026-07-14","support-phase":"active","release-type":"lts"},
            {"latest-sdk":"9.0.316","latest-release-date":"2026-07-14","support-phase":"maintenance","release-type":"sts"},
            {"latest-sdk":"8.0.423","latest-release-date":"2026-07-14","support-phase":"eol","release-type":"lts"}
          ]
        }
        """;

    private const string UnityMetadata = """
        {
          "offset": 0,
          "limit": 100,
          "total": 3,
          "results": [
            {"version":"6000.4.0b2","releaseDate":"2026-08-01T10:11:12Z","stream":"BETA"},
            {"version":"6000.3.5f1","releaseDate":"2026-07-14T09:10:11Z","stream":"LTS"},
            {"version":"6000.2.10f1","releaseDate":"2026-06-01T00:00:00Z","stream":"TECH"}
          ]
        }
        """;

    private const string BlenderMetadata = """
        <html><body>
          <a>Blender 4.5 LTS Beta 2</a>
          <a>Blender 4.4</a>
          <a>Blender 4.2 LTS</a>
        </body></html>
        """;

    private const string UnrealMetadata = """
        <html><body>
          <a>Unreal Engine 5.9 Preview 2</a>
          <a>Unreal Engine 5.8 Release Notes</a>
        </body></html>
        """;
}
