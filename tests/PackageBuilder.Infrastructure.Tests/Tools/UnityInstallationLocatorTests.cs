using PackageBuilder.Contracts.Processes;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;
using PackageBuilder.Infrastructure.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class UnityInstallationLocatorTests
{
    private static readonly UnityEditorModuleRequirement _windowsModule = new(
        "windows-il2cpp",
        @"Editor\Data\PlaybackEngines\WindowsStandaloneSupport");

    private static readonly UnityEditorModuleRequirement _androidModule = new(
        "android-player",
        @"Editor\Data\PlaybackEngines\AndroidPlayer");

    [Fact]
    public async Task DiscoversHubAndConfiguredEditorsWithRequiredModulesAndLiteralVersionOutput()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string hub = workspace.CreateEditor("project/tools/unity/Hub/Editor/6000.3.10f1");
        string configured = workspace.CreateEditor("project/tools/unity/custom/6000.2.9f1");
        _ = workspace.CreateDirectory(
            "project/tools/unity/Hub/Editor/6000.3.10f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport");
        _ = workspace.CreateDirectory(
            "project/tools/unity/custom/6000.2.9f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport");
        var runner = new RecordingProcessRunner(
            (request, _) => UnityDiscoveryTestWorkspace.VersionResult(
                request,
                request.ExecutablePath == configured ? "6000.2.9f1\r\n" : "\n6000.3.10f1\n"));
        var locator = new UnityInstallationLocator(runner);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request([configured], requiredModules: [_windowsModule]),
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Failures);
        Assert.Equal(2, report.Detections.Count);
        Assert.Equal(2, report.VerifiedInstallations.Count);
        Assert.All(report.Detections, detection =>
        {
            Assert.Equal(UnityInstallationDiscoveryStatus.Verified, detection.Status);
            Assert.True(detection.IsVerified);
            Assert.True(detection.CanBeSelected);
            Assert.False(detection.Installation!.IsSelected);
            Assert.Equal(ToolKind.Unity, detection.Installation.Tool);
            Assert.Equal(ToolInstallationContainment.Contained, detection.Installation.Containment);
            Assert.True(Assert.Single(detection.Modules).IsInstalled);
            Assert.Null(detection.DiagnosticCode);
            Assert.Null(detection.Diagnostic);
        });
        Assert.Contains(report.Detections, value =>
            value.ExecutablePath == configured
            && value.Source == UnityInstallationDiscoverySource.Configured
            && value.Installation!.Version.Value == "6000.2.9f1");
        Assert.Contains(report.Detections, value =>
            value.ExecutablePath == hub
            && value.Source == UnityInstallationDiscoverySource.Hub
            && value.Installation!.Version.Value == "6000.3.10f1");

        Assert.Equal(2, runner.Requests.Count);
        Assert.All(runner.Requests, request =>
        {
            Assert.Equal(workspace.ProjectRoot, request.ProjectRoot);
            Assert.Equal(workspace.WorkingDirectory, request.WorkingDirectory);
            Assert.Equal(workspace.TemporaryDirectory, request.TemporaryDirectory);
            Assert.Equal(workspace.CacheDirectory, request.CacheDirectory);
            Assert.Equal(workspace.LogDirectory, request.LogDirectory);
            Assert.Equal(["-version"], request.Arguments);
            Assert.Empty(request.EnvironmentVariables);
            Assert.Equal(4_096, request.MaximumCapturedCharactersPerStream);
            Assert.Equal(TimeSpan.FromSeconds(15), request.ExecutionPolicy.StartupTimeout);
            Assert.Equal(TimeSpan.FromSeconds(15), request.ExecutionPolicy.IdleTimeout);
            Assert.Equal(TimeSpan.FromSeconds(30), request.ExecutionPolicy.TotalTimeout);
            Assert.Equal(TimeSpan.FromSeconds(5), request.ExecutionPolicy.GracefulTerminationTimeout);
        });
    }

    [Fact]
    public async Task DeduplicatesConfiguredAndHubCandidateAndCombinesSources()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string candidate = workspace.CreateEditor("project/tools/unity/Hub/Editor/6000.3.10f1");
        RecordingProcessRunner runner = SuccessfulRunner("6000.3.10f1");
        var locator = new UnityInstallationLocator(runner);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request([candidate, candidate.ToUpperInvariant()]),
            TestContext.Current.CancellationToken);

        UnityInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(
            UnityInstallationDiscoverySource.Configured | UnityInstallationDiscoverySource.Hub,
            detection.Source);
        _ = Assert.Single(runner.Requests);
    }

    [Fact]
    public async Task ExternalConfiguredEditorIsInformationalAndNeverExecutedOrSelectable()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string external = workspace.CreateEditor("external/6000.3.10f1");
        string missing = workspace.Resolve("external/6000.2.9f1/Editor/Unity.exe");
        RecordingProcessRunner runner = SuccessfulRunner("6000.3.10f1");
        var locator = new UnityInstallationLocator(runner);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request([external, missing]),
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Failures);
        Assert.Empty(report.VerifiedInstallations);
        Assert.Empty(runner.Requests);
        UnityInstallationDetection informational = Assert.Single(
            report.Detections,
            value => value.Status == UnityInstallationDiscoveryStatus.ExternalInformational);
        Assert.Equal("UNITY_EXTERNAL_INFORMATIONAL", informational.DiagnosticCode);
        Assert.False(informational.IsVerified);
        Assert.False(informational.CanBeSelected);
        Assert.Null(informational.Installation);
        Assert.Contains(report.Detections, value =>
            value.ExecutablePath == missing
            && value.Status == UnityInstallationDiscoveryStatus.Missing);
    }

    [Fact]
    public async Task MissingRequiredModuleRejectsCandidateBeforeExecution()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string candidate = workspace.CreateEditor("project/tools/unity/custom/6000.3.10f1");
        _ = workspace.CreateDirectory(
            "project/tools/unity/custom/6000.3.10f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport");
        RecordingProcessRunner runner = SuccessfulRunner("6000.3.10f1");
        var locator = new UnityInstallationLocator(runner);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request([candidate], requiredModules: [_windowsModule, _androidModule]),
            TestContext.Current.CancellationToken);

        UnityInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(UnityInstallationDiscoveryStatus.RequiredModulesUnavailable, detection.Status);
        Assert.Equal("UNITY_REQUIRED_MODULES_UNAVAILABLE", detection.DiagnosticCode);
        Assert.Collection(
            detection.Modules,
            installed => Assert.Equal(UnityEditorModuleDiscoveryStatus.Installed, installed.Status),
            missing =>
            {
                Assert.Equal(UnityEditorModuleDiscoveryStatus.Missing, missing.Status);
                Assert.Equal("UNITY_MODULE_MISSING", missing.DiagnosticCode);
            });
        Assert.False(detection.CanBeSelected);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task FileModuleMarkerCanBeVerified()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string candidate = workspace.CreateEditor("project/tools/unity/custom/6000.3.10f1");
        _ = workspace.CreateFile("project/tools/unity/custom/6000.3.10f1/Editor/Data/PlaybackEngines/marker.json");
        var marker = new UnityEditorModuleRequirement(
            "module-marker",
            @"Editor\Data\PlaybackEngines\marker.json",
            UnityEditorModuleEntryKind.File);
        var locator = new UnityInstallationLocator(SuccessfulRunner("6000.3.10f1"));

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request([candidate], requiredModules: [marker]),
            TestContext.Current.CancellationToken);

        Assert.True(Assert.Single(report.Detections).IsVerified);
    }

    [Theory]
    [InlineData("process-failed", UnityInstallationDiscoveryStatus.VerificationFailed, "UNITY_VERSION_EXECUTION_FAILED")]
    [InlineData("timed-out", UnityInstallationDiscoveryStatus.VerificationFailed, "UNITY_VERSION_PROCESS_INCOMPLETE")]
    [InlineData("nonzero", UnityInstallationDiscoveryStatus.VerificationFailed, "UNITY_VERSION_EXIT_CODE")]
    [InlineData("truncated", UnityInstallationDiscoveryStatus.InvalidVersionOutput, "UNITY_VERSION_OUTPUT_TRUNCATED")]
    [InlineData("wrong-format", UnityInstallationDiscoveryStatus.InvalidVersionOutput, "UNITY_VERSION_OUTPUT_INVALID")]
    [InlineData("empty", UnityInstallationDiscoveryStatus.InvalidVersionOutput, "UNITY_VERSION_OUTPUT_INVALID")]
    public async Task RejectsCandidatesWhoseVersionCommandIsNotAuthoritative(
        string scenario,
        UnityInstallationDiscoveryStatus expectedStatus,
        string expectedCode)
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string candidate = workspace.CreateEditor("project/tools/unity/custom/6000.3.10f1");
        var runner = new RecordingProcessRunner((request, _) => scenario switch
        {
            "process-failed" => ProcessExecutionResult.Failed(
                new ProcessExecutionFailure("PROCESS_FAILED", "$", "Expected test failure.")),
            "timed-out" => UnityDiscoveryTestWorkspace.VersionResult(
                request,
                "6000.3.10f1",
                completion: new ProcessCompletionMetadata(
                    ProcessCompletionKind.TotalTimedOut,
                    cancellationSignalCreated: true,
                    gracefulTerminationAcknowledged: false,
                    forcedTermination: true,
                    controlFileCleanupSucceeded: true)),
            "nonzero" => UnityDiscoveryTestWorkspace.VersionResult(request, "6000.3.10f1", exitCode: 2),
            "truncated" => UnityDiscoveryTestWorkspace.VersionResult(request, "6000.3.10f1", truncated: true),
            "wrong-format" => UnityDiscoveryTestWorkspace.VersionResult(request, "Unity 6000.3.10f1"),
            "empty" => UnityDiscoveryTestWorkspace.VersionResult(request, "\r\n"),
            _ => throw new InvalidOperationException("Unknown test scenario."),
        });
        var locator = new UnityInstallationLocator(runner);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request([candidate]),
            TestContext.Current.CancellationToken);

        UnityInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(expectedStatus, detection.Status);
        Assert.Equal(expectedCode, detection.DiagnosticCode);
        Assert.False(detection.IsVerified);
        Assert.False(detection.CanBeSelected);
    }

    [Fact]
    public async Task ContainedConfiguredEditorMustUseVendorLayout()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        string candidate = workspace.CreateFile("project/tools/unity/custom/Unity.exe");
        RecordingProcessRunner runner = SuccessfulRunner("6000.3.10f1");
        var locator = new UnityInstallationLocator(runner);

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request([candidate]),
            TestContext.Current.CancellationToken);

        UnityInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(UnityInstallationDiscoveryStatus.InvalidLayout, detection.Status);
        Assert.Equal("UNITY_EDITOR_LAYOUT_INVALID", detection.DiagnosticCode);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task CancellationStopsVerificationWithoutThrowing()
    {
        using var workspace = new UnityDiscoveryTestWorkspace();
        _ = workspace.CreateEditor("project/tools/unity/Hub/Editor/6000.2.9f1");
        _ = workspace.CreateEditor("project/tools/unity/Hub/Editor/6000.3.10f1");
        RecordingProcessRunner runner = SuccessfulRunner("6000.3.10f1");
        var locator = new UnityInstallationLocator(runner);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        UnityInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            cancellation.Token);

        Assert.Empty(report.Detections);
        Assert.Empty(runner.Requests);
        Assert.Contains(report.Failures, value => value.Code == "UNITY_DISCOVERY_CANCELLED");
    }

    [Fact]
    public async Task ConstructorsRejectNullCollaboratorsAndNullRequest()
    {
        RecordingProcessRunner runner = SuccessfulRunner("6000.3.10f1");
        var fileSystem = new PhysicalUnityDiscoveryFileSystem();

        _ = Assert.Throws<ArgumentNullException>(() => new UnityInstallationLocator(null!));
        _ = Assert.Throws<ArgumentNullException>(() => new UnityInstallationLocator(runner, null!));
        _ = Assert.Throws<ArgumentNullException>(() => new UnityInstallationLocator(null!, fileSystem));
        var locator = new UnityInstallationLocator(runner, fileSystem);
        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => locator.DiscoverAsync(null!, TestContext.Current.CancellationToken));
    }

    private static RecordingProcessRunner SuccessfulRunner(string output) =>
        new((request, _) => UnityDiscoveryTestWorkspace.VersionResult(request, output));
}
