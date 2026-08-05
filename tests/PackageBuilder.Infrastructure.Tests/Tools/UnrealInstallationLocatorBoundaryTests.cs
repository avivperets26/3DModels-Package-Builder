using PackageBuilder.Contracts.Tools;
using PackageBuilder.Infrastructure.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class UnrealInstallationLocatorBoundaryTests
{
    private const int MaximumLauncherManifestCount = 32;
    private const int MaximumLauncherEntryCount = 512;
    private const int MaximumContainedDirectoryCount = 256;
    private const int MaximumCandidateCount = 256;

    [Theory]
    [InlineData("projectRoot", "UNREAL_DISCOVERY_ROOT_INVALID")]
    [InlineData("toolsName", "UNREAL_DISCOVERY_ROOT_INVALID")]
    [InlineData("toolsOutside", "UNREAL_DISCOVERY_ROOT_INVALID")]
    [InlineData("missingProject", "UNREAL_DISCOVERY_ROOT_MISSING")]
    [InlineData("missingTools", "UNREAL_DISCOVERY_ROOT_MISSING")]
    public async Task InvalidOrMissingDiscoveryRootsFailClosed(string scenario, string expectedCode)
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        UnrealInstallationDiscoveryRequest baseline = workspace.Request();
        string projectRoot = scenario == "projectRoot" ? "relative" : baseline.ProjectRoot;
        string toolsRoot = scenario switch
        {
            "toolsName" => workspace.CreateDirectory("project/tooling"),
            "toolsOutside" => workspace.CreateDirectory("external/tools"),
            "missingTools" => workspace.Resolve("project/missing/tools"),
            _ => baseline.ApprovedToolsRoot,
        };
        if (scenario == "missingProject")
        {
            projectRoot = workspace.Resolve("missing-project");
            toolsRoot = Path.Combine(projectRoot, "tools");
        }

        var request = new UnrealInstallationDiscoveryRequest(
            baseline.JobId,
            projectRoot,
            toolsRoot,
            [],
            [],
            [],
            [],
            []);
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, Assert.Single(report.Failures).Code);
        Assert.Empty(report.Detections);
    }

    [Fact]
    public async Task RootReparseAndExpectedInspectionFailureAreReportedSafely()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        var reparseFileSystem = new DelegatingUnrealDiscoveryFileSystem
        {
            GetAttributesOverride = path => path == workspace.ProjectRoot
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : File.GetAttributes(path),
        };
        var unreadableFileSystem = new DelegatingUnrealDiscoveryFileSystem
        {
            GetAttributesOverride = _ => throw new UnauthorizedAccessException("Sensitive detail."),
        };

        UnrealInstallationDiscoveryReport reparse = await new UnrealInstallationLocator(reparseFileSystem)
            .DiscoverAsync(workspace.Request(), TestContext.Current.CancellationToken);
        UnrealInstallationDiscoveryReport unreadable = await new UnrealInstallationLocator(unreadableFileSystem)
            .DiscoverAsync(workspace.Request(), TestContext.Current.CancellationToken);

        Assert.Equal("UNREAL_DISCOVERY_ROOT_REPARSE", Assert.Single(reparse.Failures).Code);
        Assert.Equal("UNREAL_DISCOVERY_ROOT_UNREADABLE", Assert.Single(unreadable.Failures).Code);
        Assert.DoesNotContain("Sensitive", unreadable.Failures[0].Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidExplicitRootsNeverReachFilesystem()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(
                configuredRoots: ["relative", @"C:\invalid" + '\0'],
                sourceBuildRoots: ["relative/source"],
                registeredRoots: ["relative/registered"],
                standardRoots: ["relative/standard"]),
            TestContext.Current.CancellationToken);

        Assert.Equal(5, report.Failures.Count(value => value.Code == "UNREAL_INSTALLATION_ROOT_INVALID"));
        Assert.Empty(report.Detections);
    }

    [Fact]
    public async Task CandidateRootAndEngineFileReparseBoundariesFailClosed()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string root = workspace.CreateEngine("project/tools/physical/UE_5.8");
        string linkedRoot = workspace.Resolve("project/tools/linked/UE_5.8");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(linkedRoot)!);
        _ = Directory.CreateSymbolicLink(linkedRoot, root);
        string fileRoot = workspace.CreateEngine("project/tools/file-link/UE_5.7");
        string physicalVersion = workspace.Resolve("project/tools/file-link/UE_5.7/Engine/Build/Build.version");
        string linkedVersion = physicalVersion + ".linked";
        File.Move(physicalVersion, linkedVersion);
        _ = File.CreateSymbolicLink(physicalVersion, linkedVersion);
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(configuredRoots: [linkedRoot, fileRoot]),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, report.Detections.Count);
        Assert.All(report.Detections, static value =>
            Assert.Equal(UnrealInstallationDiscoveryStatus.ReparseRejected, value.Status));
    }

    [Fact]
    public async Task EditorExecutableReparseBoundaryFailsBeforeBuildVersionInspection()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string root = workspace.CreateEngine("project/tools/executable-link/UE_5.8");
        string executable = Path.Combine(root, "Engine", "Binaries", "Win64", "UnrealEditor-Cmd.exe");
        string physical = executable + ".physical";
        File.Move(executable, physical);
        _ = File.CreateSymbolicLink(executable, physical);
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(configuredRoots: [root]),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            UnrealInstallationDiscoveryStatus.ReparseRejected,
            Assert.Single(report.Detections).Status);
    }

    [Fact]
    public async Task ContainedRootReparseAndUnreadableEnumerationAreReported()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string target = workspace.CreateDirectory("project/tools/unreal-target");
        string containedRoot = workspace.Resolve("project/tools/unreal");
        _ = Directory.CreateSymbolicLink(containedRoot, target);
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport reparse = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("UNREAL_CONTAINED_ROOT_REPARSE", Assert.Single(reparse.Failures).Code);

        Directory.Delete(containedRoot);
        _ = workspace.CreateDirectory("project/tools/unreal");
        var fileSystem = new DelegatingUnrealDiscoveryFileSystem
        {
            EnumerateDirectoriesOverride = path => path == containedRoot
                ? throw new IOException("Sensitive detail.")
                : Directory.EnumerateDirectories(path),
        };
        UnrealInstallationDiscoveryReport unreadable = await new UnrealInstallationLocator(fileSystem)
            .DiscoverAsync(workspace.Request(), TestContext.Current.CancellationToken);

        Assert.Equal("UNREAL_CONTAINED_ROOT_UNREADABLE", Assert.Single(unreadable.Failures).Code);
        Assert.DoesNotContain("Sensitive", unreadable.Failures[0].Diagnostic, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ContainedEnumerationRejectsInvalidOrNonchildDirectory(bool relative)
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string containedRoot = workspace.CreateDirectory("project/tools/unreal");
        string outside = workspace.CreateDirectory("project/tools/outside");
        var fileSystem = new DelegatingUnrealDiscoveryFileSystem
        {
            EnumerateDirectoriesOverride = path => path == containedRoot
                ? [relative ? "relative" : outside]
                : Directory.EnumerateDirectories(path),
        };
        var locator = new UnrealInstallationLocator(fileSystem);

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("UNREAL_CONTAINED_DIRECTORY_INVALID", Assert.Single(report.Failures).Code);
        Assert.Empty(report.Detections);
    }

    [Fact]
    public async Task ContainedDirectoryCountIsBoundedBeforeInspection()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string containedRoot = workspace.CreateDirectory("project/tools/unreal");
        string[] roots =
        [
            .. Enumerable.Range(0, MaximumContainedDirectoryCount + 1)
                .Select(index => Path.Combine(containedRoot, $"UE_{index:D3}")),
        ];
        var fileSystem = new DelegatingUnrealDiscoveryFileSystem
        {
            EnumerateDirectoriesOverride = path => path == containedRoot ? roots : [],
        };
        var locator = new UnrealInstallationLocator(fileSystem);

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("UNREAL_CONTAINED_DIRECTORY_LIMIT", Assert.Single(report.Failures).Code);
        Assert.Empty(report.Detections);
    }

    [Fact]
    public async Task CandidateCountIsBoundedAndReportedOnce()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string[] roots =
        [
            .. Enumerable.Range(0, MaximumCandidateCount + 2)
                .Select(index => workspace.Resolve($"project/tools/custom/UE_{index:D3}")),
        ];
        _ = workspace.CreateDirectory("project/tools/unreal/extra");
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(configuredRoots: roots),
            TestContext.Current.CancellationToken);

        Assert.Equal(MaximumCandidateCount, report.Detections.Count);
        _ = Assert.Single(report.Failures, value => value.Code == "UNREAL_CANDIDATE_LIMIT");
    }

    [Fact]
    public async Task LauncherCandidatesShareTheGlobalCandidateBound()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string entries = string.Join(
            ",",
            Enumerable.Range(0, MaximumCandidateCount + 1).Select(index =>
                $"{{\"AppName\":\"UE_{index}\",\"InstallLocation\":{System.Text.Json.JsonSerializer.Serialize(workspace.Resolve($"project/tools/manifest/UE_{index:D3}"))}}}"));
        string manifest = workspace.CreateFile(
            "project/downloads/candidates.dat",
            $"{{\"InstallationList\":[{entries}]}}");

        UnrealInstallationDiscoveryReport report = await new UnrealInstallationLocator()
            .DiscoverAsync(
                workspace.Request(launcherManifests: [manifest]),
                TestContext.Current.CancellationToken);

        Assert.Equal(MaximumCandidateCount, report.Detections.Count);
        _ = Assert.Single(report.Failures, value => value.Code == "UNREAL_CANDIDATE_LIMIT");
    }

    [Fact]
    public async Task LauncherManifestCountAndEntryCountAreBounded()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string[] manifests =
        [
            .. Enumerable.Range(0, MaximumLauncherManifestCount + 1)
                .Select(index => workspace.Resolve($"project/downloads/{index:D2}.dat")),
        ];
        UnrealInstallationDiscoveryReport manifestLimit = await new UnrealInstallationLocator()
            .DiscoverAsync(
                workspace.Request(launcherManifests: manifests),
                TestContext.Current.CancellationToken);
        Assert.Contains(manifestLimit.Failures, value => value.Code == "UNREAL_LAUNCHER_MANIFEST_LIMIT");

        string entries = string.Join(
            ",",
            Enumerable.Range(0, MaximumLauncherEntryCount + 1)
                .Select(static index => $"{{\"AppName\":\"Other_{index}\"}}"));
        string largeManifest = workspace.CreateFile(
            "project/downloads/entries.dat",
            $"{{\"InstallationList\":[{entries}]}}");
        UnrealInstallationDiscoveryReport entryLimit = await new UnrealInstallationLocator()
            .DiscoverAsync(
                workspace.Request(launcherManifests: [largeManifest]),
                TestContext.Current.CancellationToken);
        Assert.Equal("UNREAL_LAUNCHER_ENTRY_LIMIT", Assert.Single(entryLimit.Failures).Code);
    }

    [Fact]
    public async Task ManifestPathAndReadFailuresAreSafe()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string manifest = workspace.CreateFile(
            "project/downloads/LauncherInstalled.dat",
            "{\"InstallationList\":[]}");
        var unreadable = new DelegatingUnrealDiscoveryFileSystem
        {
            OpenReadOverride = _ => throw new UnauthorizedAccessException("Sensitive detail."),
        };
        UnrealInstallationDiscoveryReport unreadableReport = await new UnrealInstallationLocator(unreadable)
            .DiscoverAsync(
                workspace.Request(launcherManifests: [manifest]),
                TestContext.Current.CancellationToken);

        Assert.Equal("UNREAL_LAUNCHER_MANIFEST_UNREADABLE", Assert.Single(unreadableReport.Failures).Code);
        Assert.DoesNotContain("Sensitive", unreadableReport.Failures[0].Diagnostic, StringComparison.Ordinal);

        UnrealInstallationDiscoveryReport invalidPath = await new UnrealInstallationLocator()
            .DiscoverAsync(
                workspace.Request(launcherManifests: ["relative", @"C:\bad" + '\0']),
                TestContext.Current.CancellationToken);
        Assert.Equal(2, invalidPath.Failures.Count(value => value.Code == "UNREAL_LAUNCHER_MANIFEST_PATH_INVALID"));
    }

    [Fact]
    public async Task LauncherManifestReparseBoundaryIsRejected()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string physical = workspace.CreateFile(
            "project/downloads/physical.dat",
            "{\"InstallationList\":[]}");
        string linked = workspace.Resolve("project/downloads/linked.dat");
        _ = File.CreateSymbolicLink(linked, physical);
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(launcherManifests: [linked]),
            TestContext.Current.CancellationToken);

        Assert.Equal("UNREAL_LAUNCHER_MANIFEST_UNSAFE", Assert.Single(report.Failures).Code);
    }
}
