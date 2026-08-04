using PackageBuilder.Contracts.Tools;
using PackageBuilder.Infrastructure.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class UnityInstallationLocatorBoundaryTests
{
    private const int MaximumHubRootCount = 32;
    private const int MaximumHubEditorDirectoryCount = 256;
    private const int MaximumCandidateCount = 256;

    [Theory]
    [InlineData("relative-project", "UNITY_DISCOVERY_ROOT_INVALID")]
    [InlineData("wrong-tools-name", "UNITY_DISCOVERY_ROOT_INVALID")]
    [InlineData("external-tools", "UNITY_DISCOVERY_ROOT_INVALID")]
    [InlineData("missing-project", "UNITY_DISCOVERY_ROOT_MISSING")]
    [InlineData("missing-tools", "UNITY_DISCOVERY_ROOT_MISSING")]
    [InlineData("reparse-project", "UNITY_DISCOVERY_ROOT_REPARSE")]
    [InlineData("reparse-tools", "UNITY_DISCOVERY_ROOT_REPARSE")]
    [InlineData("unreadable-tools", "UNITY_DISCOVERY_ROOT_UNREADABLE")]
    public async Task InvalidDiscoveryRootsFailClosed(string scenario, string expectedCode)
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        UnityInstallationDiscoveryRequest baseline = workspace.Request();
        string externalTools = workspace.CreateDirectory("other/tools");
        var fileSystem = new DelegatingUnityDiscoveryFileSystem
        {
            DirectoryExistsOverride = scenario switch
            {
                "missing-project" => path => path != workspace.ProjectRoot,
                "missing-tools" => path => path != workspace.ToolsRoot,
                _ => null,
            },
            GetAttributesOverride = scenario switch
            {
                "reparse-project" => path => path == workspace.ProjectRoot
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : File.GetAttributes(path),
                "reparse-tools" => path => path == workspace.ToolsRoot
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : File.GetAttributes(path),
                "unreadable-tools" => _ => throw new UnauthorizedAccessException("Expected test failure."),
                _ => null,
            },
        };
        var request = new UnityInstallationDiscoveryRequest(
            baseline.JobId,
            scenario == "relative-project" ? "project" : baseline.ProjectRoot,
            scenario switch
            {
                "wrong-tools-name" => workspace.WorkingDirectory,
                "external-tools" => externalTools,
                _ => baseline.ApprovedToolsRoot,
            },
            baseline.WorkingDirectory,
            baseline.TemporaryDirectory,
            baseline.CacheDirectory,
            baseline.LogDirectory,
            [],
            [],
            []);
        var locator = new UnityInstallationLocator(SuccessfulRunner(), fileSystem);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            request,
            TestContext.Current.CancellationToken);

        UnityInstallationDiscoveryFailure failure = Assert.Single(report.Failures);
        Assert.Equal(expectedCode, failure.Code);
        Assert.Empty(report.Detections);
    }

    [Fact]
    public async Task InvalidConfiguredPathsNeverReachFilesystemOrProcess()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        var fileSystem = new DelegatingUnityDiscoveryFileSystem
        {
            FileExistsOverride = _ => throw new InvalidOperationException("Invalid paths must not reach the filesystem."),
        };
        RecordingProcessRunner runner = SuccessfulRunner();
        var locator = new UnityInstallationLocator(runner, fileSystem);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(["relative/Unity.exe", @"C:\Dev\PackageBuilder\tools\unity\bad" + '\0']),
            TestContext.Current.CancellationToken);

        Assert.Contains(report.Failures, value => value.Code == "UNITY_CONFIGURED_PATH_INVALID");
        Assert.Empty(report.Detections);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task ContainedConfiguredExecutableWithWrongFilenameIsInvalidLayout()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string candidate = workspace.CreateFile("project/tools/unity/6000.3.10f1/Editor/Editor.exe");
        RecordingProcessRunner runner = SuccessfulRunner();
        var locator = new UnityInstallationLocator(runner);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request([candidate]),
            TestContext.Current.CancellationToken);

        Assert.Equal(UnityInstallationDiscoveryStatus.InvalidLayout, Assert.Single(report.Detections).Status);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task ConfiguredReparseAndExpectedInspectionFailureAreRejectedWithoutExecution()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string physical = workspace.CreateEditor("project/tools/unity/physical/6000.3.10f1");
        string linked = workspace.Resolve("project/tools/unity/linked/6000.3.10f1/Editor/Unity.exe");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(linked)!);
        _ = File.CreateSymbolicLink(linked, physical);
        string unreadable = workspace.CreateEditor("project/tools/unity/unreadable/6000.2.9f1");
        var fileSystem = new DelegatingUnityDiscoveryFileSystem
        {
            GetAttributesOverride = path => path == unreadable
                ? throw new IOException("Expected test failure.")
                : File.GetAttributes(path),
        };
        RecordingProcessRunner runner = SuccessfulRunner();
        var locator = new UnityInstallationLocator(runner, fileSystem);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request([linked, unreadable]),
            TestContext.Current.CancellationToken);

        Assert.Contains(report.Detections, value =>
            value.ExecutablePath == linked
            && value.Status == UnityInstallationDiscoveryStatus.ReparseRejected);
        Assert.Contains(report.Detections, value =>
            value.ExecutablePath == unreadable
            && value.DiagnosticCode == "UNITY_CANDIDATE_UNREADABLE");
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task UnexpectedConfiguredInspectionFailureIsNotMisreported()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string candidate = workspace.CreateEditor("project/tools/unity/custom/6000.3.10f1");
        var fileSystem = new DelegatingUnityDiscoveryFileSystem
        {
            FileExistsOverride = _ => throw new InvalidOperationException("Unexpected test failure."),
        };
        var locator = new UnityInstallationLocator(SuccessfulRunner(), fileSystem);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => locator.DiscoverAsync(
            workspace.Request([candidate]),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConfiguredHubRootFailuresAreSafeAndExternalRootsAreNeverEnumerated()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string missing = workspace.Resolve("project/tools/unity/missing-hub");
        string external = workspace.CreateDirectory("external/hub");
        var locator = new UnityInstallationLocator(SuccessfulRunner());

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(hubRoots: ["relative-hub", missing, external, workspace.DefaultHubRoot]),
            TestContext.Current.CancellationToken);

        Assert.Contains(report.Failures, value => value.Code == "UNITY_HUB_ROOT_INVALID");
        Assert.Contains(report.Failures, value => value.Code == "UNITY_HUB_ROOT_MISSING");
        Assert.Contains(report.Failures, value => value.Code == "UNITY_HUB_ROOT_EXTERNAL");
        Assert.Equal(3, report.Failures.Count);
    }

    [Fact]
    public async Task HubRootReparseAndUnreadableEnumerationAreReported()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string target = workspace.CreateDirectory("project/tools/unity/target-hub");
        string linked = workspace.Resolve("project/tools/unity/linked-hub");
        _ = Directory.CreateSymbolicLink(linked, target);
        string unreadable = workspace.CreateDirectory("project/tools/unity/unreadable-hub");
        var fileSystem = new DelegatingUnityDiscoveryFileSystem
        {
            EnumerateDirectoriesOverride = path => path == unreadable
                ? throw new UnauthorizedAccessException("Expected test failure.")
                : Directory.EnumerateDirectories(path),
        };
        var locator = new UnityInstallationLocator(SuccessfulRunner(), fileSystem);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(hubRoots: [linked, unreadable]),
            TestContext.Current.CancellationToken);

        Assert.Contains(report.Failures, value => value.Code == "UNITY_HUB_ROOT_REPARSE");
        Assert.Contains(report.Failures, value => value.Code == "UNITY_HUB_ROOT_UNREADABLE");
        Assert.Empty(report.Detections);
    }

    [Fact]
    public async Task HubChildrenReportMissingReparseAndExecutableReparseOutcomes()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        _ = workspace.CreateDirectory("project/tools/unity/Hub/Editor/incomplete");
        string physicalDirectory = workspace.CreateDirectory("project/tools/unity/physical-version");
        string linkedDirectory = workspace.Resolve("project/tools/unity/Hub/Editor/linked-version");
        _ = Directory.CreateSymbolicLink(linkedDirectory, physicalDirectory);
        string physicalEditor = workspace.CreateEditor("project/tools/unity/physical/6000.3.10f1");
        string linkedEditor = workspace.Resolve("project/tools/unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(linkedEditor)!);
        _ = File.CreateSymbolicLink(linkedEditor, physicalEditor);
        RecordingProcessRunner runner = SuccessfulRunner();
        var locator = new UnityInstallationLocator(runner);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Contains(report.Detections, value =>
            value.Status == UnityInstallationDiscoveryStatus.Missing
            && value.DiagnosticCode == "UNITY_EXECUTABLE_MISSING");
        Assert.Equal(
            2,
            report.Detections.Count(value => value.Status == UnityInstallationDiscoveryStatus.ReparseRejected));
        Assert.Empty(runner.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HubEnumerationRejectsInvalidOrNonchildDirectory(bool useRelativePath)
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        _ = workspace.CreateDirectory("project/tools/unity/Hub/Editor");
        string outside = workspace.CreateDirectory("project/tools/unity/outside-version");
        var fileSystem = new DelegatingUnityDiscoveryFileSystem
        {
            EnumerateDirectoriesOverride = path => path == workspace.DefaultHubRoot
                ? [useRelativePath ? "relative-version" : outside]
                : [],
        };
        var locator = new UnityInstallationLocator(SuccessfulRunner(), fileSystem);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Contains(report.Failures, value => value.Code == "UNITY_HUB_EDITOR_PATH_INVALID");
        Assert.Empty(report.Detections);
    }

    [Fact]
    public async Task RequiredModuleReparseAndUnreadableMarkersRejectBeforeExecution()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string candidate = workspace.CreateEditor("project/tools/unity/custom/6000.3.10f1");
        string physical = workspace.CreateDirectory("project/tools/unity/module-target");
        string linked = workspace.Resolve(
            "project/tools/unity/custom/6000.3.10f1/Editor/Data/PlaybackEngines/LinkedPlayer");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(linked)!);
        _ = Directory.CreateSymbolicLink(linked, physical);
        string unreadable = workspace.CreateDirectory(
            "project/tools/unity/custom/6000.3.10f1/Editor/Data/PlaybackEngines/UnreadablePlayer");
        var linkedRequirement = new UnityEditorModuleRequirement(
            "linked",
            @"Editor\Data\PlaybackEngines\LinkedPlayer");
        var unreadableRequirement = new UnityEditorModuleRequirement(
            "unreadable",
            @"Editor\Data\PlaybackEngines\UnreadablePlayer");
        var fileSystem = new DelegatingUnityDiscoveryFileSystem
        {
            GetAttributesOverride = path => path == unreadable
                ? throw new IOException("Expected test failure.")
                : File.GetAttributes(path),
        };
        RecordingProcessRunner runner = SuccessfulRunner();
        var locator = new UnityInstallationLocator(runner, fileSystem);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request([candidate], requiredModules: [linkedRequirement, unreadableRequirement]),
            TestContext.Current.CancellationToken);

        UnityInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(UnityInstallationDiscoveryStatus.RequiredModulesUnavailable, detection.Status);
        Assert.Contains(detection.Modules, value =>
            value.Status == UnityEditorModuleDiscoveryStatus.ReparseRejected);
        Assert.Contains(detection.Modules, value =>
            value.Status == UnityEditorModuleDiscoveryStatus.Unreadable);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task HubRootCountIsBounded()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string[] roots = [.. Enumerable.Range(0, MaximumHubRootCount).Select(index => workspace.Resolve($"project/tools/unity/hub-{index:D2}"))];
        var locator = new UnityInstallationLocator(SuccessfulRunner());

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(hubRoots: roots),
            TestContext.Current.CancellationToken);

        Assert.Contains(report.Failures, value => value.Code == "UNITY_HUB_ROOT_LIMIT");
    }

    [Fact]
    public async Task HubEditorDirectoryCountIsBoundedBeforeCandidateExecution()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        _ = workspace.CreateDirectory("project/tools/unity/Hub/Editor");
        string[] directories = [.. Enumerable.Range(0, MaximumHubEditorDirectoryCount + 1).Select(index => Path.Combine(workspace.DefaultHubRoot, $"6000.3.{index}f1"))];
        var fileSystem = new DelegatingUnityDiscoveryFileSystem
        {
            DirectoryExistsOverride = _ => true,
            EnumerateDirectoriesOverride = path => path == workspace.DefaultHubRoot ? directories : [],
            FileExistsOverride = _ => false,
            GetAttributesOverride = _ => FileAttributes.Directory,
        };
        var locator = new UnityInstallationLocator(SuccessfulRunner(), fileSystem);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Contains(report.Failures, value => value.Code == "UNITY_HUB_EDITOR_DIRECTORY_LIMIT");
        Assert.Empty(report.Detections);
    }

    [Fact]
    public async Task ConfiguredCandidateCountIsBoundedAndFailureIsReportedOnce()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        _ = workspace.CreateEditor("project/tools/unity/Hub/Editor/6000.3.10f1");
        string[] candidates = [.. Enumerable.Range(0, MaximumCandidateCount + 2).Select(index => workspace.Resolve($"project/tools/unity/{index:D3}/Editor/Unity.exe"))];
        var fileSystem = new DelegatingUnityDiscoveryFileSystem
        {
            FileExistsOverride = _ => true,
            GetAttributesOverride = path => path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? FileAttributes.Normal
                : FileAttributes.Directory,
        };
        RecordingProcessRunner runner = SuccessfulRunner();
        var locator = new UnityInstallationLocator(runner, fileSystem);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(candidates),
            TestContext.Current.CancellationToken);

        Assert.Equal(MaximumCandidateCount, report.Detections.Count);
        Assert.Equal(MaximumCandidateCount, runner.Requests.Count);
        _ = Assert.Single(report.Failures, value => value.Code == "UNITY_CANDIDATE_LIMIT");
    }

    private static RecordingProcessRunner SuccessfulRunner() =>
        new((request, _) => UnityDiscoveryTestWorkspace.VersionResult(request, "6000.3.10f1"));
}
