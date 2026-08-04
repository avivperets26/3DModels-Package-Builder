using PackageBuilder.Contracts.Processes;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;
using PackageBuilder.Infrastructure.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class BlenderInstallationLocatorTests
{
    [Fact]
    public async Task DiscoversConfiguredAndPortableInstallationsAndVerifiesLiteralVersionOutput()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string configured = workspace.CreateFile("project/tools/custom/blender.exe");
        string portable = workspace.CreateFile("project/tools/blender/blender-4.5.3-windows-x64/blender.exe");
        var runner = new RecordingProcessRunner(
            (request, _) => BlenderDiscoveryTestWorkspace.VersionResult(
                request,
                request.ExecutablePath == configured
                    ? "Blender 4.4.2\r\n  build date: test"
                    : "\nBlender 4.5.3\n  build date: test"));
        var locator = new BlenderInstallationLocator(runner);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(configured),
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Failures);
        Assert.Equal(2, report.Detections.Count);
        Assert.Equal(2, report.VerifiedInstallations.Count);
        Assert.All(report.Detections, detection =>
        {
            Assert.Equal(BlenderInstallationDiscoveryStatus.Verified, detection.Status);
            Assert.True(detection.IsVerified);
            Assert.True(detection.CanBeSelected);
            Assert.False(detection.Installation!.IsSelected);
            Assert.Equal(ToolKind.Blender, detection.Installation.Tool);
            Assert.Equal(ToolInstallationContainment.Contained, detection.Installation.Containment);
            Assert.Null(detection.DiagnosticCode);
            Assert.Null(detection.Diagnostic);
        });
        Assert.Contains(report.Detections, value =>
            value.ExecutablePath == configured
            && value.Source == BlenderInstallationDiscoverySource.Configured
            && value.Installation!.Version.Value == "4.4.2");
        Assert.Contains(report.Detections, value =>
            value.ExecutablePath == portable
            && value.Source == BlenderInstallationDiscoverySource.Portable
            && value.Installation!.Version.Value == "4.5.3");

        Assert.Equal(2, runner.Requests.Count);
        Assert.All(runner.Requests, request =>
        {
            Assert.Equal(workspace.ProjectRoot, request.ProjectRoot);
            Assert.Equal(workspace.WorkingDirectory, request.WorkingDirectory);
            Assert.Equal(workspace.TemporaryDirectory, request.TemporaryDirectory);
            Assert.Equal(workspace.CacheDirectory, request.CacheDirectory);
            Assert.Equal(workspace.LogDirectory, request.LogDirectory);
            Assert.Equal(["--version"], request.Arguments);
            Assert.Empty(request.EnvironmentVariables);
            Assert.Equal(4_096, request.MaximumCapturedCharactersPerStream);
            Assert.Equal(TimeSpan.FromSeconds(15), request.ExecutionPolicy.StartupTimeout);
            Assert.Equal(TimeSpan.FromSeconds(15), request.ExecutionPolicy.IdleTimeout);
            Assert.Equal(TimeSpan.FromSeconds(30), request.ExecutionPolicy.TotalTimeout);
            Assert.Equal(TimeSpan.FromSeconds(5), request.ExecutionPolicy.GracefulTerminationTimeout);
        });
    }

    [Fact]
    public async Task DeduplicatesConfiguredPortableCandidateAndCombinesItsSources()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string candidate = workspace.CreateFile("project/tools/blender/4.5.3/blender.exe");
        RecordingProcessRunner runner = SuccessfulRunner("Blender 4.5.3");
        var locator = new BlenderInstallationLocator(runner);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(candidate, candidate.ToUpperInvariant()),
            TestContext.Current.CancellationToken);

        BlenderInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(
            BlenderInstallationDiscoverySource.Configured | BlenderInstallationDiscoverySource.Portable,
            detection.Source);
        _ = Assert.Single(runner.Requests);
    }

    [Fact]
    public async Task ExternalConfiguredDetectionIsInformationalAndIsNeverExecutedOrSelectable()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string external = workspace.CreateFile("external/blender.exe");
        string missing = workspace.Resolve("external/missing-blender.exe");
        RecordingProcessRunner runner = SuccessfulRunner("Blender 4.5.3");
        var locator = new BlenderInstallationLocator(runner);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(external, missing),
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Failures);
        Assert.Empty(report.VerifiedInstallations);
        Assert.Empty(runner.Requests);
        BlenderInstallationDetection informational = Assert.Single(
            report.Detections,
            value => value.Status == BlenderInstallationDiscoveryStatus.ExternalInformational);
        Assert.Equal("BLENDER_EXTERNAL_INFORMATIONAL", informational.DiagnosticCode);
        Assert.False(informational.IsVerified);
        Assert.False(informational.CanBeSelected);
        Assert.Null(informational.Installation);
        Assert.Contains(
            report.Detections,
            value => value.ExecutablePath == missing
                     && value.Status == BlenderInstallationDiscoveryStatus.Missing
                     && value.DiagnosticCode == "BLENDER_EXECUTABLE_MISSING");
    }

    [Theory]
    [InlineData("process-failed", BlenderInstallationDiscoveryStatus.VerificationFailed, "BLENDER_VERSION_EXECUTION_FAILED")]
    [InlineData("timed-out", BlenderInstallationDiscoveryStatus.VerificationFailed, "BLENDER_VERSION_PROCESS_INCOMPLETE")]
    [InlineData("nonzero", BlenderInstallationDiscoveryStatus.VerificationFailed, "BLENDER_VERSION_EXIT_CODE")]
    [InlineData("truncated", BlenderInstallationDiscoveryStatus.InvalidVersionOutput, "BLENDER_VERSION_OUTPUT_TRUNCATED")]
    [InlineData("wrong-prefix", BlenderInstallationDiscoveryStatus.InvalidVersionOutput, "BLENDER_VERSION_OUTPUT_INVALID")]
    [InlineData("invalid-version", BlenderInstallationDiscoveryStatus.InvalidVersionOutput, "BLENDER_VERSION_OUTPUT_INVALID")]
    public async Task RejectsCandidatesWhoseVersionCommandIsNotAuthoritative(
        string scenario,
        BlenderInstallationDiscoveryStatus expectedStatus,
        string expectedCode)
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string candidate = workspace.CreateFile("project/tools/custom/blender.exe");
        var runner = new RecordingProcessRunner((request, _) => scenario switch
        {
            "process-failed" => ProcessExecutionResult.Failed(
                new ProcessExecutionFailure("PROCESS_FAILED", "$", "Expected test failure.")),
            "timed-out" => BlenderDiscoveryTestWorkspace.VersionResult(
                request,
                "Blender 4.5.3",
                completion: new ProcessCompletionMetadata(
                    ProcessCompletionKind.TotalTimedOut,
                    cancellationSignalCreated: true,
                    gracefulTerminationAcknowledged: false,
                    forcedTermination: true,
                    controlFileCleanupSucceeded: true)),
            "nonzero" => BlenderDiscoveryTestWorkspace.VersionResult(request, "Blender 4.5.3", exitCode: 2),
            "truncated" => BlenderDiscoveryTestWorkspace.VersionResult(request, "Blender 4.5.3", truncated: true),
            "wrong-prefix" => BlenderDiscoveryTestWorkspace.VersionResult(request, "Not Blender 4.5.3"),
            "invalid-version" => BlenderDiscoveryTestWorkspace.VersionResult(request, "Blender 4.05.3"),
            _ => throw new InvalidOperationException("Unknown test scenario."),
        });
        var locator = new BlenderInstallationLocator(runner);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(candidate),
            TestContext.Current.CancellationToken);

        BlenderInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(expectedStatus, detection.Status);
        Assert.Equal(expectedCode, detection.DiagnosticCode);
        Assert.False(detection.IsVerified);
        Assert.False(detection.CanBeSelected);
    }

    [Fact]
    public async Task InvalidConfiguredPathIsReportedWithoutFilesystemOrProcessUse()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        RecordingProcessRunner runner = SuccessfulRunner("Blender 4.5.3");
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            FileExistsOverride = _ => throw new InvalidOperationException("Malformed paths must not reach the filesystem."),
        };
        var locator = new BlenderInstallationLocator(runner, fileSystem);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request("relative/blender.exe"),
            TestContext.Current.CancellationToken);

        BlenderInstallationDiscoveryFailure failure = Assert.Single(report.Failures);
        Assert.Equal("BLENDER_CONFIGURED_PATH_INVALID", failure.Code);
        Assert.Empty(report.Detections);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task ContainedConfiguredReparsePointIsRejectedBeforeExecution()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        string physical = workspace.CreateFile("project/tools/physical/blender.exe");
        string linked = workspace.Resolve("project/tools/linked-blender.exe");
        _ = File.CreateSymbolicLink(linked, physical);
        RecordingProcessRunner runner = SuccessfulRunner("Blender 4.5.3");
        var locator = new BlenderInstallationLocator(runner);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(linked),
            TestContext.Current.CancellationToken);

        BlenderInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(BlenderInstallationDiscoveryStatus.ReparseRejected, detection.Status);
        Assert.Equal("BLENDER_REPARSE_REJECTED", detection.DiagnosticCode);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task CancellationStopsVerificationWithoutThrowing()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        _ = workspace.CreateFile("project/tools/blender/4.4.2/blender.exe");
        _ = workspace.CreateFile("project/tools/blender/4.5.3/blender.exe");
        RecordingProcessRunner runner = SuccessfulRunner("Blender 4.5.3");
        var locator = new BlenderInstallationLocator(runner);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            cancellation.Token);

        Assert.Empty(report.Detections);
        Assert.Empty(runner.Requests);
        Assert.Contains(report.Failures, value => value.Code == "BLENDER_DISCOVERY_CANCELLED");
    }

    [Theory]
    [InlineData("relative-project", "BLENDER_DISCOVERY_ROOT_INVALID")]
    [InlineData("wrong-tools-name", "BLENDER_DISCOVERY_ROOT_INVALID")]
    [InlineData("missing-tools", "BLENDER_DISCOVERY_ROOT_MISSING")]
    [InlineData("reparse-tools", "BLENDER_DISCOVERY_ROOT_REPARSE")]
    [InlineData("unreadable-tools", "BLENDER_DISCOVERY_ROOT_UNREADABLE")]
    public async Task InvalidDiscoveryRootsFailClosed(string scenario, string expectedCode)
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        BlenderInstallationDiscoveryRequest baseline = workspace.Request();
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            DirectoryExistsOverride = scenario == "missing-tools"
                ? path => path != workspace.ToolsRoot
                : null,
            GetAttributesOverride = scenario switch
            {
                "reparse-tools" => path => path == workspace.ToolsRoot
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : File.GetAttributes(path),
                "unreadable-tools" => _ => throw new UnauthorizedAccessException("Expected test failure."),
                _ => null,
            },
        };
        var request = new BlenderInstallationDiscoveryRequest(
            baseline.JobId,
            scenario == "relative-project" ? "project" : baseline.ProjectRoot,
            scenario == "wrong-tools-name" ? workspace.WorkingDirectory : baseline.ApprovedToolsRoot,
            baseline.WorkingDirectory,
            baseline.TemporaryDirectory,
            baseline.CacheDirectory,
            baseline.LogDirectory,
            []);
        var locator = new BlenderInstallationLocator(SuccessfulRunner("Blender 4.5.3"), fileSystem);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            request,
            TestContext.Current.CancellationToken);

        BlenderInstallationDiscoveryFailure failure = Assert.Single(report.Failures);
        Assert.Equal(expectedCode, failure.Code);
        Assert.Empty(report.Detections);
    }

    [Fact]
    public async Task UnreadablePortableDirectoryIsReportedAndOtherDiscoveryRemainsSafe()
    {
        using var workspace = new BlenderDiscoveryTestWorkspace();
        _ = workspace.CreateDirectory("project/tools/blender");
        var fileSystem = new DelegatingBlenderDiscoveryFileSystem
        {
            EnumerateFilesOverride = path => path.EndsWith("blender", StringComparison.OrdinalIgnoreCase)
                ? throw new IOException("Expected test failure.")
                : Directory.EnumerateFiles(path),
        };
        var locator = new BlenderInstallationLocator(
            SuccessfulRunner("Blender 4.5.3"),
            fileSystem);

        BlenderInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Detections);
        Assert.Contains(report.Failures, value => value.Code == "BLENDER_PORTABLE_DIRECTORY_UNREADABLE");
    }

    [Fact]
    public async Task ConstructorsRejectNullCollaboratorsAndNullRequest()
    {
        RecordingProcessRunner runner = SuccessfulRunner("Blender 4.5.3");
        var fileSystem = new PhysicalBlenderDiscoveryFileSystem();

        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationLocator(null!));
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationLocator(runner, null!));
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationLocator(null!, fileSystem));
        var locator = new BlenderInstallationLocator(runner, fileSystem);
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => locator.DiscoverAsync(null!, TestContext.Current.CancellationToken));
    }

    private static RecordingProcessRunner SuccessfulRunner(string output) =>
        new((request, _) => BlenderDiscoveryTestWorkspace.VersionResult(request, output));
}
