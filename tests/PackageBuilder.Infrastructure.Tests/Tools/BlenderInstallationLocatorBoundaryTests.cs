using PackageBuilder.Contracts.Tools;
using PackageBuilder.Infrastructure.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class BlenderInstallationLocatorBoundaryTests
{
    [Theory]
    [InlineData("project-missing")]
    [InlineData("project-reparse")]
    [InlineData("tools-not-descendant")]
    [InlineData("tools-relative")]
    [InlineData("project-noncanonical")]
    public async Task AdditionalInvalidRootBoundariesFailClosed(string scenario)
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string otherTools = workspace.CreateDirectory("other/tools");
        BlenderInstallationDiscoveryRequest baseline = workspace.Request();
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            DirectoryExistsOverride = scenario == "project-missing"
                ? path => path != workspace.ProjectRoot
                : null,
            GetAttributesOverride = scenario == "project-reparse"
                ? path => path == workspace.ProjectRoot
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : File.GetAttributes(path)
                : null,
        };
        var request = new BlenderInstallationDiscoveryRequest(
            baseline.JobId,
            scenario == "project-noncanonical"
                ? workspace.ProjectRoot + "\\."
                : baseline.ProjectRoot,
            scenario switch
            {
                "tools-not-descendant" => otherTools,
                "tools-relative" => "tools",
                _ => baseline.ApprovedToolsRoot,
            },
            baseline.WorkingDirectory,
            baseline.TemporaryDirectory,
            baseline.CacheDirectory,
            baseline.LogDirectory,
            []);
        var locator = new BlenderInstallationLocator(SuccessfulRunner(), fileSystem);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Detections);
        Assert.Equal(
            scenario is "project-missing" ? "BLENDER_DISCOVERY_ROOT_MISSING"
            : scenario is "project-reparse" ? "BLENDER_DISCOVERY_ROOT_REPARSE"
            : "BLENDER_DISCOVERY_ROOT_INVALID",
            Assert.Single(report.Failures).Code);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("unauthorized")]
    [InlineData("argument")]
    [InlineData("unsupported")]
    public async Task ExpectedFilesystemFailuresBecomeSafeDiagnostics(string exceptionKind)
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        Exception expected = ExceptionFor(exceptionKind);
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            GetAttributesOverride = _ => throw expected,
        };
        var locator = new BlenderInstallationLocator(SuccessfulRunner(), fileSystem);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("BLENDER_DISCOVERY_ROOT_UNREADABLE", Assert.Single(report.Failures).Code);
    }

    [Fact]
    public async Task UnexpectedFilesystemFailureIsNotMisreportedAsAnExpectedFailure()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            GetAttributesOverride = _ => throw new InvalidOperationException("Expected test failure."),
        };
        var locator = new BlenderInstallationLocator(SuccessfulRunner(), fileSystem);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => locator.DiscoverAsync(workspace.Request(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("root-reparse", "BLENDER_PORTABLE_ROOT_REPARSE")]
    [InlineData("root-unreadable", "BLENDER_PORTABLE_ROOT_UNREADABLE")]
    public async Task UnsafePortableRootIsNotScanned(string scenario, string expectedCode)
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string portableRoot = workspace.CreateDirectory("project/tools/blender");
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            GetAttributesOverride = path => path == portableRoot
                ? scenario == "root-reparse"
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : throw new IOException("Expected test failure.")
                : File.GetAttributes(path),
        };
        var locator = new BlenderInstallationLocator(SuccessfulRunner(), fileSystem);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, Assert.Single(report.Failures).Code);
        Assert.Empty(report.Detections);
    }

    [Fact]
    public async Task PortableFilesAreFilteredAndUnsafeCandidatesAreReportedWithoutExecution()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string portableRoot = workspace.CreateDirectory("project/tools/blender");
        string reparse = Path.Combine(portableRoot, "reparse", "blender.exe");
        string unreadable = Path.Combine(portableRoot, "unreadable", "blender.exe");
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            EnumerateFilesOverride = path => path == portableRoot
                ?
                [
                    Path.Combine(portableRoot, "ignored.exe"),
                    "relative\\blender.exe",
                    reparse,
                    unreadable,
                ]
                : [],
            EnumerateDirectoriesOverride = _ => [],
            GetAttributesOverride = path => path switch
            {
                _ when path == reparse => FileAttributes.ReparsePoint,
                _ when path == unreadable => throw new IOException("Expected test failure."),
                _ when path == portableRoot => FileAttributes.Directory,
                _ => File.GetAttributes(path),
            },
        };
        RecordingProcessRunner runner = SuccessfulRunner();
        var locator = new BlenderInstallationLocator(runner, fileSystem);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Failures);
        Assert.Empty(runner.Requests);
        Assert.Contains(report.Detections, value =>
            value.ExecutablePath == reparse
            && value.Status == BlenderInstallationDiscoveryStatus.ReparseRejected);
        Assert.Contains(report.Detections, value =>
            value.ExecutablePath == unreadable
            && value.Status == BlenderInstallationDiscoveryStatus.VerificationFailed
            && value.DiagnosticCode == "BLENDER_CANDIDATE_UNREADABLE");
    }

    [Fact]
    public async Task PortableSubdirectoriesSkipReparseAndUnreadableEntries()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string portableRoot = workspace.CreateDirectory("project/tools/blender");
        string reparse = Path.Combine(portableRoot, "reparse");
        string unreadable = Path.Combine(portableRoot, "unreadable");
        string normal = Path.Combine(portableRoot, "normal");
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            EnumerateFilesOverride = _ => [],
            EnumerateDirectoriesOverride = path => path == portableRoot
                ? [reparse, unreadable, normal]
                : [],
            GetAttributesOverride = path => path switch
            {
                _ when path == reparse => FileAttributes.Directory | FileAttributes.ReparsePoint,
                _ when path == unreadable => throw new UnauthorizedAccessException("Expected test failure."),
                _ => FileAttributes.Directory,
            },
        };
        var locator = new BlenderInstallationLocator(SuccessfulRunner(), fileSystem);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Detections);
        Assert.Contains(report.Failures, value => value.Code == "BLENDER_PORTABLE_REPARSE_SKIPPED");
        Assert.Contains(report.Failures, value => value.Code == "BLENDER_PORTABLE_DIRECTORY_UNREADABLE");
    }

    [Fact]
    public async Task PortableDepthLimitIsReportedOnlyOnce()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string portableRoot = workspace.CreateDirectory("project/tools/blender");
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            EnumerateFilesOverride = _ => [],
            EnumerateDirectoriesOverride = path =>
            {
                int depth = RelativeDepth(portableRoot, path);
                return depth < 4
                    ? [Path.Combine(path, "next")]
                    : [Path.Combine(path, "first-too-deep"), Path.Combine(path, "second-too-deep")];
            },
            GetAttributesOverride = _ => FileAttributes.Directory,
        };
        var locator = new BlenderInstallationLocator(SuccessfulRunner(), fileSystem);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        _ = Assert.Single(report.Failures, value => value.Code == "BLENDER_PORTABLE_DEPTH_LIMIT");
    }

    [Fact]
    public async Task PortableDirectoryCountIsBounded()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string portableRoot = workspace.CreateDirectory("project/tools/blender");
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            EnumerateFilesOverride = _ => [],
            EnumerateDirectoriesOverride = path =>
            {
                int depth = RelativeDepth(portableRoot, path);
                return depth >= 4
                    ? []
                    : Enumerable.Range(0, 6).Select(index => Path.Combine(path, $"d{index}"));
            },
            GetAttributesOverride = _ => FileAttributes.Directory,
        };
        var locator = new BlenderInstallationLocator(SuccessfulRunner(), fileSystem);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Contains(report.Failures, value => value.Code == "BLENDER_PORTABLE_DIRECTORY_LIMIT");
    }

    [Fact]
    public async Task ConfiguredCandidateCountIsBoundedAndDuplicateLimitFailuresAreSuppressed()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string[] candidates =
        [
            .. Enumerable.Range(0, 258).Select(index => Path.Combine(
                workspace.ToolsRoot,
                "configured",
                index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
                "blender.exe")),
        ];
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            FileExistsOverride = _ => true,
            GetAttributesOverride = path => path == workspace.ProjectRoot || path == workspace.ToolsRoot
                ? File.GetAttributes(path)
                : FileAttributes.Normal,
        };
        var locator = new BlenderInstallationLocator(SuccessfulRunner(), fileSystem);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(candidates),
            cancellation.Token);

        _ = Assert.Single(report.Failures, value => value.Code == "BLENDER_CANDIDATE_LIMIT");
        _ = Assert.Single(report.Failures, value => value.Code == "BLENDER_DISCOVERY_CANCELLED");
    }

    [Fact]
    public async Task PortableCandidateCountIsBoundedBeforeVerification()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string portableRoot = workspace.CreateDirectory("project/tools/blender");
        string[] candidates =
        [
            .. Enumerable.Range(0, 257).Select(index => Path.Combine(
                portableRoot,
                index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
                "blender.exe")),
        ];
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            EnumerateFilesOverride = path => path == portableRoot ? candidates : [],
            EnumerateDirectoriesOverride = _ => [],
            GetAttributesOverride = path => path == workspace.ProjectRoot
                || path == workspace.ToolsRoot
                || path == portableRoot
                ? File.GetAttributes(path)
                : FileAttributes.Normal,
        };
        var locator = new BlenderInstallationLocator(SuccessfulRunner(), fileSystem);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            cancellation.Token);

        Assert.Contains(report.Failures, value => value.Code == "BLENDER_CANDIDATE_LIMIT");
        Assert.Contains(report.Failures, value => value.Code == "BLENDER_DISCOVERY_CANCELLED");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("C:/Dev/PackageBuilder/tools/blender.exe")]
    [InlineData("relative\\blender.exe")]
    [InlineData("C:\\Dev\\PackageBuilder\\tools\\blender\\")]
    [InlineData("C:\\Dev\\PackageBuilder\\tools\\blender\\..\\blender.exe")]
    public async Task ConfiguredPathCanonicalizationRejectsEveryMalformedShape(string path)
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        var locator = new BlenderInstallationLocator(SuccessfulRunner());

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(path),
            TestContext.Current.CancellationToken);

        Assert.Equal("BLENDER_CONFIGURED_PATH_INVALID", Assert.Single(report.Failures).Code);
    }

    [Fact]
    public async Task ConfiguredPathCanonicalizationHandlesInvalidWindowsCharacters()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        var locator = new BlenderInstallationLocator(SuccessfulRunner());

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request("C:\\Dev\\PackageBuilder\\tools\\blender\0.exe"),
            TestContext.Current.CancellationToken);

        Assert.Equal("BLENDER_CONFIGURED_PATH_INVALID", Assert.Single(report.Failures).Code);
    }

    [Fact]
    public async Task EmptyVersionOutputIsRejected()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string candidate = workspace.CreateFile("project/tools/custom/blender.exe");
        var runner = new RecordingProcessRunner(
            (request, _) => BlenderDiscoveryTestWorkspace.VersionResult(request, "\r\n \n"));
        var locator = new BlenderInstallationLocator(runner);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(candidate),
            TestContext.Current.CancellationToken);

        Assert.Equal("BLENDER_VERSION_OUTPUT_INVALID", Assert.Single(report.Detections).DiagnosticCode);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("unauthorized")]
    [InlineData("argument")]
    [InlineData("unsupported")]
    public async Task ConfiguredCandidateInspectionConvertsExpectedFailures(string exceptionKind)
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string candidate = workspace.CreateFile("project/tools/custom/blender.exe");
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            GetAttributesOverride = path => path == candidate
                ? throw ExceptionFor(exceptionKind)
                : File.GetAttributes(path),
        };
        var locator = new BlenderInstallationLocator(SuccessfulRunner(), fileSystem);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(candidate),
            TestContext.Current.CancellationToken);

        BlenderInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal("BLENDER_CANDIDATE_UNREADABLE", detection.DiagnosticCode);
    }

    private static RecordingProcessRunner SuccessfulRunner() =>
        new((request, _) => BlenderDiscoveryTestWorkspace.VersionResult(request, "Blender 4.5.3"));

    private static Exception ExceptionFor(string kind) => kind switch
    {
        "io" => new IOException("Expected test failure."),
        "unauthorized" => new UnauthorizedAccessException("Expected test failure."),
        "argument" => new ArgumentException("Expected test failure."),
        "unsupported" => new NotSupportedException("Expected test failure."),
        _ => throw new InvalidOperationException("Unknown test exception kind."),
    };

    private static int RelativeDepth(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        return relative == "." ? 0 : relative.Split(Path.DirectorySeparatorChar).Length;
    }
}
