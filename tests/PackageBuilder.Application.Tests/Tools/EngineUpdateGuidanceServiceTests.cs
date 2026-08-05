using PackageBuilder.Application.Tools;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Application.Tests.Tools;

[Trait("Task", "PB-0309")]
public sealed class EngineUpdateGuidanceServiceTests
{
    private const string Root = @"C:\Dev\PackageBuilder";
    private const string Tools = @"C:\Dev\PackageBuilder\tools";
    private const string Downloads = @"C:\Dev\PackageBuilder\downloads";
    private const string Cache = @"C:\Dev\PackageBuilder\runtime-data\cache";

    [Fact]
    public async Task ExplicitCheckReportsMissingAndNewerStableCandidatesForAllEngines()
    {
        var provider = new FakeProvider(
            Catalog(ToolKind.Blender, "5.1.0", "5.2.0-alpha.1"),
            Catalog(ToolKind.Unity, "6000.3.11f1"),
            Catalog(ToolKind.Unreal, "5.8.0"));
        var service = new EngineUpdateGuidanceService(provider, new FakeLauncher());
        EngineUpdateCheckRequest request = Request(
            [Installation(ToolKind.Blender, "5.0.0"), Installation(ToolKind.Unreal, "5.8.0")],
            [Approval(ToolKind.Blender, "5.1.0", ToolVersionApprovalState.Candidate)],
            network: true);

        EngineUpdateCheckResult result = await service.CheckAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Engines.Count);
        Assert.Equal(EngineUpdateAvailability.NewerStableCandidate, Status(result, ToolKind.Blender).Availability);
        Assert.Equal("5.1.0", Status(result, ToolKind.Blender).LatestStableVersion!.Value);
        Assert.Equal(ToolVersionApprovalState.Candidate, Status(result, ToolKind.Blender).LatestVersionApprovalState);
        Assert.Equal(EngineUpdateAvailability.MissingInstallation, Status(result, ToolKind.Unity).Availability);
        Assert.Equal(EngineUpdateAvailability.UpToDate, Status(result, ToolKind.Unreal).Availability);
        Assert.All(provider.Requests, requestValue => Assert.True(requestValue.AllowNetworkRefresh));
    }

    [Fact]
    public async Task DefaultCheckIsCacheOnlyAndReportsPerEngineCatalogFailure()
    {
        var provider = new FakeProvider(Catalog(ToolKind.Blender, "5.0.0"), Catalog(ToolKind.Unity, "6000.3.10f1"));
        var service = new EngineUpdateGuidanceService(provider, new FakeLauncher());

        EngineUpdateCheckResult result = await service.CheckAsync(Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.All(provider.Requests, requestValue => Assert.False(requestValue.AllowNetworkRefresh));
        Assert.Equal(EngineUpdateAvailability.CatalogUnavailable, Status(result, ToolKind.Unreal).Availability);
        Assert.Equal("RELEASE_CATALOG_CACHE_UNAVAILABLE", Status(result, ToolKind.Unreal).ErrorCode);
    }

    [Fact]
    public async Task PreviewOnlyCatalogCannotBecomeAnUpdateCandidate()
    {
        var provider = new FakeProvider(
            Catalog(ToolKind.Blender, "5.2.0-alpha.1"),
            Catalog(ToolKind.Unity, "6000.3.10f1"),
            Catalog(ToolKind.Unreal, "5.8.0"));
        var service = new EngineUpdateGuidanceService(provider, new FakeLauncher());

        EngineUpdateCheckResult result = await service.CheckAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(EngineUpdateAvailability.CatalogUnavailable, Status(result, ToolKind.Blender).Availability);
        Assert.Equal("RELEASE_CATALOG_NO_STABLE", Status(result, ToolKind.Blender).ErrorCode);
    }

    [Fact]
    public void GuidanceUsesOfficialLinksContainedDestinationAndNoSilentActions()
    {
        var service = new EngineUpdateGuidanceService(new FakeProvider(), new FakeLauncher());

        foreach (ToolKind tool in new[] { ToolKind.Blender, ToolKind.Unity, ToolKind.Unreal })
        {
            ToolVersion version = Version(tool, tool == ToolKind.Unity ? "6000.3.10f1" : tool == ToolKind.Unreal ? "5.8.0" : "5.0.0");
            EngineGuidanceLaunchResult result = EngineUpdateGuidanceService.CreateGuidance(new(tool, version, Root, Tools, false));

            Assert.True(result.IsSuccess);
            Assert.StartsWith(Tools + Path.DirectorySeparatorChar, result.Guidance!.ContainedDestination, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Uri.UriSchemeHttps, result.Guidance.OfficialInstallationGuide.Scheme);
            Assert.Equal(Uri.UriSchemeHttps, result.Guidance.OfficialLicenceTerms.Scheme);
            Assert.False(result.Guidance.DownloadsAutomatically);
            Assert.False(result.Guidance.RunsInstaller);
            Assert.False(result.Guidance.AcceptsLicence);
            Assert.False(result.Guidance.AssumesPaidTier);
            Assert.Contains(result.Guidance.UserSteps, step => step.Contains("tools", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task LaunchRequiresConfirmationAndOnlyOpensApprovedOfficialGuide()
    {
        var launcher = new FakeLauncher();
        var service = new EngineUpdateGuidanceService(new FakeProvider(), launcher);
        var denied = new EngineGuidanceLaunchRequest(ToolKind.Blender, Version(ToolKind.Blender, "5.0.0"), Root, Tools, false);

        Assert.Equal("ENGINE_GUIDANCE_CONFIRMATION_REQUIRED", (await service.LaunchGuidanceAsync(denied, TestContext.Current.CancellationToken)).ErrorCode);
        Assert.Empty(launcher.Opened);

        EngineGuidanceLaunchResult launched = await service.LaunchGuidanceAsync(denied with { UserConfirmedExternalNavigation = true }, TestContext.Current.CancellationToken);
        Assert.True(launched.IsSuccess);
        Assert.True(launched.WasLaunched);
        Assert.Equal(new Uri("https://www.blender.org/download/"), Assert.Single(launcher.Opened));
    }

    [Fact]
    public async Task LauncherFailureReturnsSanitizedTypedError()
    {
        var service = new EngineUpdateGuidanceService(new FakeProvider(), new FakeLauncher(result: false));
        var request = new EngineGuidanceLaunchRequest(
            ToolKind.Unreal,
            Version(ToolKind.Unreal, "5.8.0"),
            Root,
            Tools,
            UserConfirmedExternalNavigation: true);

        EngineGuidanceLaunchResult result = await service.LaunchGuidanceAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("ENGINE_GUIDANCE_LAUNCH_FAILED", result.ErrorCode);
        Assert.False(result.WasLaunched);
    }

    [Theory]
    [InlineData("outside-tools")]
    [InlineData("preview")]
    [InlineData("wrong-tool")]
    [InlineData("dotnet")]
    public void UnsafeOrUnsupportedGuidanceFailsClosed(string scenario)
    {
        _ = new EngineUpdateGuidanceService(new FakeProvider(), new FakeLauncher());
        EngineGuidanceLaunchRequest request = scenario switch
        {
            "outside-tools" => new(ToolKind.Blender, Version(ToolKind.Blender, "5.0.0"), Root, @"C:\Tools", false),
            "preview" => new(ToolKind.Blender, Version(ToolKind.Blender, "5.1.0-alpha.1"), Root, Tools, false),
            "wrong-tool" => new(ToolKind.Blender, Version(ToolKind.Unity, "6000.3.10f1"), Root, Tools, false),
            _ => new(ToolKind.DotNet, Version(ToolKind.DotNet, "10.0.302"), Root, Tools, false),
        };

        Assert.Equal("ENGINE_GUIDANCE_INPUT_INVALID", EngineUpdateGuidanceService.CreateGuidance(request).ErrorCode);
    }

    [Fact]
    public async Task InvalidCollectionsFailBeforeAnyCatalogLookup()
    {
        var provider = new FakeProvider();
        var service = new EngineUpdateGuidanceService(provider, new FakeLauncher());
        ToolInstallation duplicate = Installation(ToolKind.Blender, "5.0.0");

        EngineUpdateCheckResult result = await service.CheckAsync(Request([duplicate, duplicate]), TestContext.Current.CancellationToken);

        Assert.Equal("ENGINE_UPDATE_INSTALLATION_INVALID", result.ErrorCode);
        Assert.Empty(provider.Requests);
    }

    [Theory]
    [InlineData(@"C:\Tools", Downloads, Cache)]
    [InlineData(Root, @"C:\Downloads", Cache)]
    [InlineData(Root, Downloads, @"C:\Cache")]
    public async Task UnsafeCheckRootsFailBeforeAnyCatalogLookup(
        string tools,
        string downloads,
        string cache)
    {
        var provider = new FakeProvider();
        var service = new EngineUpdateGuidanceService(provider, new FakeLauncher());
        EngineUpdateCheckRequest request = Request() with
        {
            ToolsRoot = tools,
            DownloadsRoot = downloads,
            CacheRoot = cache,
        };

        EngineUpdateCheckResult result = await service.CheckAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("ENGINE_UPDATE_INPUT_INVALID", result.ErrorCode);
        Assert.Empty(provider.Requests);
    }

    private static EngineUpdateStatus Status(EngineUpdateCheckResult result, ToolKind tool) =>
        Assert.Single(result.Engines, value => value.Tool == tool);

    private static EngineUpdateCheckRequest Request(
        IReadOnlyCollection<ToolInstallation>? installations = null,
        IReadOnlyCollection<ToolVersionApproval>? approvals = null,
        bool network = false) => new(Root, Tools, Downloads, Cache, installations ?? [], approvals ?? [], network);

    private static ToolInstallation Installation(ToolKind tool, string value) =>
        ToolInstallation.Create(tool, Version(tool, value), Path.Combine(Tools, tool.ToString(), value, "editor.exe"), Tools, false).Value!;

    private static ToolVersionApproval Approval(ToolKind tool, string value, ToolVersionApprovalState state) =>
        new(Version(tool, value), state, []);

    private static ToolVersion Version(ToolKind tool, string value) => ToolVersion.Create(tool, value).Value!;

    private static OfficialReleaseCatalogSnapshot Catalog(ToolKind tool, params string[] values)
    {
        OfficialReleaseCatalog catalog = OfficialReleaseCatalog.Create(
            tool,
            new Uri("https://example.invalid/releases"),
            new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero),
            values.Select(value => new OfficialToolRelease(Version(tool, value), ToolReleaseSupport.Standard, null))).Value!;
        return new(catalog, ReleaseCatalogOrigin.FreshNetwork, "raw", "cache", null);
    }

    private sealed class FakeProvider(params OfficialReleaseCatalogSnapshot[] catalogs) : IOfficialReleaseCatalogProvider
    {
        private readonly Dictionary<ToolKind, OfficialReleaseCatalogSnapshot> _catalogs = catalogs.ToDictionary(value => value.Catalog.Tool);
        public List<OfficialReleaseCatalogRequest> Requests { get; } = [];

        public Task<ReleaseCatalogResult<OfficialReleaseCatalogSnapshot>> GetAsync(OfficialReleaseCatalogRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_catalogs.TryGetValue(request.Tool, out OfficialReleaseCatalogSnapshot? value)
                ? ReleaseCatalogResult.Success(value)
                : ReleaseCatalogResult.Failure<OfficialReleaseCatalogSnapshot>("RELEASE_CATALOG_CACHE_UNAVAILABLE", "Unavailable."));
        }
    }

    private sealed class FakeLauncher(bool result = true) : IEngineInstallationGuidanceLauncher
    {
        private readonly bool _result = result;
        public List<Uri> Opened { get; } = [];

        public Task<bool> OpenAsync(Uri officialGuidanceUri, CancellationToken cancellationToken = default)
        {
            Opened.Add(officialGuidanceUri);
            return Task.FromResult(_result);
        }
    }
}
