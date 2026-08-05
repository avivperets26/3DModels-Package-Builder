using System.Text;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;
using PackageBuilder.Infrastructure.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class UnrealInstallationLocatorTests
{
    private const int MaximumLauncherManifestBytes = 1_048_576;
    private const int MaximumBuildVersionBytes = 65_536;

    [Fact]
    public void ConstructorRejectsNullFileSystem() => _ = Assert.Throws<ArgumentNullException>(() => new UnrealInstallationLocator(null!));

    [Fact]
    public async Task DiscoversConfiguredSourceLauncherAndContainedInstallationsDeterministically()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string configured = workspace.CreateEngine("project/tools/custom/Configured", 5, 6, 1);
        string source = workspace.CreateEngine("project/tools/source/UE-main", 5, 8, 0);
        string launcher = workspace.CreateEngine("project/tools/launcher/UE_5.7", 5, 7, 2);
        string contained = workspace.CreateEngine("project/tools/unreal/UE_5.5", 5, 5, 4);
        string manifest = workspace.CreateLauncherManifest(
            "project/downloads/LauncherInstalled.dat",
            ("NotUnreal", workspace.Resolve("external/ignored")),
            ("UE_5.7", launcher));
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(
                configuredRoots: [configured],
                launcherManifests: [manifest],
                sourceBuildRoots: [source]),
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Failures);
        Assert.Equal([configured, launcher, source, contained], report.Detections.Select(static value => value.InstallationRoot));
        Assert.All(report.Detections, static detection =>
        {
            Assert.Equal(UnrealInstallationDiscoveryStatus.Verified, detection.Status);
            Assert.True(detection.CanBeSelected);
            Assert.Equal(ToolInstallationContainment.Contained, detection.Installation!.Containment);
            Assert.False(detection.Installation.IsSelected);
        });
        Assert.Equal("5.6.1", report.Detections[0].Installation!.Version.Value);
        Assert.Equal("5.7.2", report.Detections[1].Installation!.Version.Value);
        Assert.Equal("5.8.0", report.Detections[2].Installation!.Version.Value);
        Assert.Equal("5.5.4", report.Detections[3].Installation!.Version.Value);
    }

    [Fact]
    public async Task DeduplicatesCandidateAndCombinesEveryDiscoverySource()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string root = workspace.CreateEngine("project/tools/unreal/UE_5.8");
        string manifest = workspace.CreateLauncherManifest(
            "project/downloads/LauncherInstalled.dat",
            ("UE_5.8", root));
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(
                configuredRoots: [root],
                launcherManifests: [manifest, manifest],
                sourceBuildRoots: [root],
                registeredRoots: [root],
                standardRoots: [root]),
            TestContext.Current.CancellationToken);

        UnrealInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(
            UnrealInstallationDiscoverySource.Configured
            | UnrealInstallationDiscoverySource.LauncherManifest
            | UnrealInstallationDiscoverySource.SourceBuild
            | UnrealInstallationDiscoverySource.Registered
            | UnrealInstallationDiscoverySource.Standard
            | UnrealInstallationDiscoverySource.ContainedRoot,
            detection.Source);
        Assert.Empty(report.Failures);
    }

    [Fact]
    public async Task ConventionalRootMayBeTheEngineInstallationItself()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string root = workspace.CreateEngine("project/tools/unreal", 5, 8, 1);
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        UnrealInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(root, detection.InstallationRoot);
        Assert.Equal(UnrealInstallationDiscoveryStatus.Verified, detection.Status);
        Assert.Empty(report.Failures);
    }

    [Fact]
    public async Task ExternalRegisteredStandardAndConfiguredRootsAreInformationalOnly()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string registered = workspace.CreateEngine("external/registered/UE-source");
        string standard = workspace.CreateEngine("external/standard/UE_5.8");
        var fileSystem = new DelegatingUnrealDiscoveryFileSystem
        {
            OpenReadOverride = _ => throw new InvalidOperationException("External installations must not be opened."),
        };
        var locator = new UnrealInstallationLocator(fileSystem);

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(
                configuredRoots: [registered],
                registeredRoots: [registered],
                standardRoots: [standard]),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, report.Detections.Count);
        Assert.All(report.Detections, static detection =>
        {
            Assert.Equal(UnrealInstallationDiscoveryStatus.ExternalInformational, detection.Status);
            Assert.False(detection.CanBeSelected);
            Assert.Null(detection.Installation);
        });
        Assert.Equal(
            UnrealInstallationDiscoverySource.Configured | UnrealInstallationDiscoverySource.Registered,
            report.Detections.Single(value => value.InstallationRoot == registered).Source);
    }

    [Fact]
    public async Task MissingAndIncompleteRootsProduceExplicitOutcomes()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string missing = workspace.Resolve("project/tools/missing");
        string incomplete = workspace.CreateDirectory("project/tools/incomplete");
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(configuredRoots: [missing, incomplete]),
            TestContext.Current.CancellationToken);

        Assert.Contains(report.Detections, value =>
            value.InstallationRoot == missing
            && value.Status == UnrealInstallationDiscoveryStatus.Missing
            && value.DiagnosticCode == "UNREAL_INSTALLATION_MISSING");
        Assert.Contains(report.Detections, value =>
            value.InstallationRoot == incomplete
            && value.Status == UnrealInstallationDiscoveryStatus.InvalidLayout
            && value.DiagnosticCode == "UNREAL_ENGINE_LAYOUT_INVALID");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"MajorVersion\":5,\"MinorVersion\":8,\"PatchVersion\":-1}")]
    [InlineData("{\"MajorVersion\":5,\"MajorVersion\":6,\"MinorVersion\":8,\"PatchVersion\":0}")]
    [InlineData("{\"MajorVersion\":5,\"MinorVersion\":8,\"PatchVersion\":0,}")]
    public async Task InvalidBuildVersionMetadataFailsClosed(string contents)
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string root = workspace.CreateEngine("project/tools/custom/UE-invalid");
        _ = workspace.CreateFile("project/tools/custom/UE-invalid/Engine/Build/Build.version", contents);
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(configuredRoots: [root]),
            TestContext.Current.CancellationToken);

        UnrealInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(UnrealInstallationDiscoveryStatus.InvalidBuildVersion, detection.Status);
        Assert.Equal("UNREAL_BUILD_VERSION_INVALID", detection.DiagnosticCode);
    }

    [Fact]
    public async Task OversizedBuildVersionIsRejectedBeforeUnboundedRead()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string root = workspace.CreateEngine("project/tools/custom/UE-large");
        _ = workspace.CreateFile(
            "project/tools/custom/UE-large/Engine/Build/Build.version",
            new string(' ', MaximumBuildVersionBytes + 1));
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(configuredRoots: [root]),
            TestContext.Current.CancellationToken);

        UnrealInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(UnrealInstallationDiscoveryStatus.InvalidBuildVersion, detection.Status);
        Assert.Equal("UNREAL_BUILD_VERSION_TOO_LARGE", detection.DiagnosticCode);
    }

    [Fact]
    public async Task LauncherManifestSkipsOtherProductsAndReportsInvalidUnrealEntries()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string manifest = workspace.CreateFile(
            "project/downloads/LauncherInstalled.dat",
            """
            {
              "InstallationList": [
                42,
                {"AppName":"Fortnite","InstallLocation":"relative"},
                {"AppName":"UE_5.8","InstallLocation":"relative"},
                {"ArtifactId":"UE_5.7"}
              ]
            }
            """);
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(launcherManifests: [manifest]),
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Detections);
        Assert.Equal(2, report.Failures.Count(value => value.Code == "UNREAL_LAUNCHER_ENTRY_INVALID"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"InstallationList\":{},\"InstallationList\":[]}")]
    [InlineData("{\"InstallationList\":{}}")]
    [InlineData("{\"InstallationList\":[{\"AppName\":\"UE_5.8\",\"AppName\":\"UE_5.7\"}]}")]
    public async Task InvalidLauncherManifestFailsClosed(string contents)
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string manifest = workspace.CreateFile("project/downloads/LauncherInstalled.dat", contents);
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(launcherManifests: [manifest]),
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Detections);
        Assert.Equal("UNREAL_LAUNCHER_MANIFEST_INVALID", Assert.Single(report.Failures).Code);
    }

    [Fact]
    public async Task MissingExternalAndOversizedLauncherManifestsAreRejectedWithoutParsing()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string missing = workspace.Resolve("project/downloads/missing.dat");
        string external = workspace.CreateFile("external/LauncherInstalled.dat", "{}");
        string oversized = workspace.CreateFile(
            "project/downloads/large.dat",
            new string(' ', MaximumLauncherManifestBytes + 1));
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(launcherManifests: [missing, external, oversized]),
            TestContext.Current.CancellationToken);

        Assert.Contains(report.Failures, value => value.Code == "UNREAL_LAUNCHER_MANIFEST_MISSING");
        Assert.Contains(report.Failures, value => value.Code == "UNREAL_LAUNCHER_MANIFEST_UNSAFE");
        Assert.Contains(report.Failures, value => value.Code == "UNREAL_LAUNCHER_MANIFEST_TOO_LARGE");
    }

    [Fact]
    public async Task CancellationStopsCandidateInspectionWithExplicitFailure()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string root = workspace.CreateEngine("project/tools/custom/UE_5.8");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var locator = new UnrealInstallationLocator();

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(configuredRoots: [root]),
            cancellation.Token);

        Assert.Empty(report.Detections);
        Assert.Equal("UNREAL_DISCOVERY_CANCELLED", Assert.Single(report.Failures).Code);
    }

    [Fact]
    public async Task UnexpectedFilesystemFailureIsNotMisreported()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string root = workspace.CreateEngine("project/tools/custom/UE_5.8");
        var fileSystem = new DelegatingUnrealDiscoveryFileSystem
        {
            DirectoryExistsOverride = path => path == root
                ? throw new InvalidOperationException("Unexpected test failure.")
                : Directory.Exists(path),
        };
        var locator = new UnrealInstallationLocator(fileSystem);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => locator.DiscoverAsync(
            workspace.Request(configuredRoots: [root]),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExpectedBuildVersionReadFailureBecomesSafeUnreadableOutcome()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string root = workspace.CreateEngine("project/tools/custom/UE_5.8");
        var fileSystem = new DelegatingUnrealDiscoveryFileSystem
        {
            OpenReadOverride = _ => throw new IOException("Sensitive physical detail."),
        };
        var locator = new UnrealInstallationLocator(fileSystem);

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(configuredRoots: [root]),
            TestContext.Current.CancellationToken);

        UnrealInstallationDetection detection = Assert.Single(report.Detections);
        Assert.Equal(UnrealInstallationDiscoveryStatus.Unreadable, detection.Status);
        Assert.Equal("UNREAL_INSTALLATION_UNREADABLE", detection.DiagnosticCode);
        Assert.DoesNotContain("Sensitive", detection.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonseekableStreamsAreReadWithinTheSameBounds()
    {
        using var workspace = new UnrealDiscoveryTestWorkspace();
        string root = workspace.CreateEngine("project/tools/custom/UE_5.8");
        byte[] metadata = Encoding.UTF8.GetBytes(
            "{\"MajorVersion\":5,\"MinorVersion\":8,\"PatchVersion\":1}");
        var fileSystem = new DelegatingUnrealDiscoveryFileSystem
        {
            OpenReadOverride = _ => new NonseekableReadStream(metadata),
        };
        var locator = new UnrealInstallationLocator(fileSystem);

        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(
            workspace.Request(configuredRoots: [root]),
            TestContext.Current.CancellationToken);

        Assert.Equal("5.8.1", Assert.Single(report.VerifiedInstallations).Version.Value);
    }

    private sealed class NonseekableReadStream(byte[] contents) : Stream
    {
        private readonly MemoryStream _inner = new(contents);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
