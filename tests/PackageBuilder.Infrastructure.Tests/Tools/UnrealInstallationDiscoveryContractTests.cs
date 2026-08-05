using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class UnrealInstallationDiscoveryContractTests
{
    [Fact]
    public void RequestCopiesEveryInputCollection()
    {
        string[] configured = [@"C:\Dev\PackageBuilder\tools\unreal\configured"];
        string[] manifests = [@"C:\Dev\PackageBuilder\downloads\LauncherInstalled.dat"];
        string[] sources = [@"C:\Dev\PackageBuilder\tools\unreal\source"];
        string[] registered = [@"C:\External\Registered"];
        string[] standard = [@"C:\Program Files\Epic Games\UE_5.8"];

        var request = new UnrealInstallationDiscoveryRequest(
            BuildJobId.Create("Job-PB0304").Value!,
            @"C:\Dev\PackageBuilder",
            @"C:\Dev\PackageBuilder\tools",
            configured,
            manifests,
            sources,
            registered,
            standard);
        configured[0] = "changed";
        manifests[0] = "changed";
        sources[0] = "changed";
        registered[0] = "changed";
        standard[0] = "changed";

        Assert.Equal(@"C:\Dev\PackageBuilder\tools\unreal\configured", Assert.Single(request.ConfiguredInstallationRoots));
        Assert.Equal(@"C:\Dev\PackageBuilder\downloads\LauncherInstalled.dat", Assert.Single(request.LauncherManifestPaths));
        Assert.Equal(@"C:\Dev\PackageBuilder\tools\unreal\source", Assert.Single(request.SourceBuildRoots));
        Assert.Equal(@"C:\External\Registered", Assert.Single(request.RegisteredInstallationRoots));
        Assert.Equal(@"C:\Program Files\Epic Games\UE_5.8", Assert.Single(request.StandardInstallationRoots));
    }

    [Fact]
    public void RequestRejectsNullRequiredValuesAndNullEntries()
    {
        BuildJobId jobId = BuildJobId.Create("Job-PB0304").Value!;
        _ = Assert.Throws<ArgumentNullException>(() => new UnrealInstallationDiscoveryRequest(
            null!, "project", "tools", [], [], [], [], []));
        _ = Assert.Throws<ArgumentNullException>(() => new UnrealInstallationDiscoveryRequest(
            jobId, null!, "tools", [], [], [], [], []));
        _ = Assert.Throws<ArgumentNullException>(() => new UnrealInstallationDiscoveryRequest(
            jobId, "project", null!, [], [], [], [], []));
        _ = Assert.Throws<ArgumentNullException>(() => new UnrealInstallationDiscoveryRequest(
            jobId, "project", "tools", null!, [], [], [], []));
        _ = Assert.Throws<ArgumentNullException>(() => new UnrealInstallationDiscoveryRequest(
            jobId, "project", "tools", [], null!, [], [], []));
        _ = Assert.Throws<ArgumentNullException>(() => new UnrealInstallationDiscoveryRequest(
            jobId, "project", "tools", [], [], null!, [], []));
        _ = Assert.Throws<ArgumentNullException>(() => new UnrealInstallationDiscoveryRequest(
            jobId, "project", "tools", [], [], [], null!, []));
        _ = Assert.Throws<ArgumentNullException>(() => new UnrealInstallationDiscoveryRequest(
            jobId, "project", "tools", [], [], [], [], null!));
        _ = Assert.Throws<ArgumentException>(() => new UnrealInstallationDiscoveryRequest(
            jobId, "project", "tools", [null!], [], [], [], []));
        _ = Assert.Throws<ArgumentException>(() => new UnrealInstallationDiscoveryRequest(
            jobId, "project", "tools", [], [null!], [], [], []));
        _ = Assert.Throws<ArgumentException>(() => new UnrealInstallationDiscoveryRequest(
            jobId, "project", "tools", [], [], [null!], [], []));
        _ = Assert.Throws<ArgumentException>(() => new UnrealInstallationDiscoveryRequest(
            jobId, "project", "tools", [], [], [], [null!], []));
        _ = Assert.Throws<ArgumentException>(() => new UnrealInstallationDiscoveryRequest(
            jobId, "project", "tools", [], [], [], [], [null!]));
    }

    [Fact]
    public void VerifiedDetectionRequiresMatchingContainedUnrealInstallation()
    {
        ToolInstallation unreal = Installation(ToolKind.Unreal, isSelected: false);
        var detection = new UnrealInstallationDetection(
            @"C:\Dev\PackageBuilder\tools\unreal\UE_5.8",
            unreal.ExecutablePath,
            @"C:\Dev\PackageBuilder\tools\unreal\UE_5.8\Engine\Build\Build.version",
            UnrealInstallationDiscoverySource.Configured | UnrealInstallationDiscoverySource.SourceBuild,
            UnrealInstallationDiscoveryStatus.Verified,
            unreal,
            null,
            null);

        Assert.True(detection.IsVerified);
        Assert.True(detection.CanBeSelected);
        Assert.Equal(unreal, detection.Installation);
        Assert.EndsWith(@"\Engine\Binaries\Win64\UnrealEditor-Cmd.exe", detection.EditorExecutablePath, StringComparison.Ordinal);
        Assert.EndsWith(@"\Engine\Build\Build.version", detection.BuildVersionPath, StringComparison.Ordinal);
        Assert.Equal(
            UnrealInstallationDiscoverySource.Configured | UnrealInstallationDiscoverySource.SourceBuild,
            detection.Source);
    }

    [Fact]
    public void DetectionRejectsInvalidStateCombinations()
    {
        ToolInstallation unreal = Installation(ToolKind.Unreal, isSelected: false);
        ToolInstallation blender = Installation(ToolKind.Blender, isSelected: false);
        ToolInstallation selected = Installation(ToolKind.Unreal, isSelected: true);
        ToolInstallation external = ToolInstallation.Create(
            ToolKind.Unreal,
            ToolVersion.Create(ToolKind.Unreal, "5.8.0").Value!,
            @"C:\External\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe",
            @"C:\Dev\PackageBuilder\tools",
            isSelected: false).Value!;

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Detection((UnrealInstallationDiscoverySource)0, UnrealInstallationDiscoveryStatus.Verified, unreal, null, null));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Detection((UnrealInstallationDiscoverySource)64, UnrealInstallationDiscoveryStatus.Verified, unreal, null, null));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Detection(UnrealInstallationDiscoverySource.Configured, (UnrealInstallationDiscoveryStatus)99, null, "code", "diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => Detection(UnrealInstallationDiscoverySource.Configured, UnrealInstallationDiscoveryStatus.Verified, null, null, null));
        _ = Assert.Throws<ArgumentException>(() => Detection(UnrealInstallationDiscoverySource.Configured, UnrealInstallationDiscoveryStatus.Missing, unreal, "code", "diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => Detection(UnrealInstallationDiscoverySource.Configured, UnrealInstallationDiscoveryStatus.Verified, blender, null, null));
        _ = Assert.Throws<ArgumentException>(() => Detection(UnrealInstallationDiscoverySource.Configured, UnrealInstallationDiscoveryStatus.Verified, selected, null, null));
        _ = Assert.Throws<ArgumentException>(() => Detection(UnrealInstallationDiscoverySource.Configured, UnrealInstallationDiscoveryStatus.Verified, external, null, null));
        _ = Assert.Throws<ArgumentException>(() => Detection(UnrealInstallationDiscoverySource.Configured, UnrealInstallationDiscoveryStatus.Verified, unreal, "code", null));
        _ = Assert.Throws<ArgumentNullException>(() => Detection(UnrealInstallationDiscoverySource.Configured, UnrealInstallationDiscoveryStatus.Missing, null, null, "diagnostic"));
    }

    [Fact]
    public void ReportCopiesValuesAndReturnsVerifiedInstallations()
    {
        ToolInstallation installation = Installation(ToolKind.Unreal, isSelected: false);
        UnrealInstallationDetection verified = Detection(
            UnrealInstallationDiscoverySource.Configured,
            UnrealInstallationDiscoveryStatus.Verified,
            installation,
            null,
            null);
        UnrealInstallationDetection rejected = Detection(
            UnrealInstallationDiscoverySource.Standard,
            UnrealInstallationDiscoveryStatus.ExternalInformational,
            null,
            "code",
            "diagnostic");
        UnrealInstallationDetection[] detections = [verified, rejected];
        UnrealInstallationDiscoveryFailure[] failures = [new("failure", "$", "diagnostic")];
        var report = new UnrealInstallationDiscoveryReport(detections, failures);
        detections[0] = rejected;
        failures[0] = new("changed", "$", "changed");

        Assert.Equal(2, report.Detections.Count);
        Assert.Equal(installation, Assert.Single(report.VerifiedInstallations));
        Assert.Equal("failure", Assert.Single(report.Failures).Code);
    }

    [Fact]
    public void FailureAndReportRejectInvalidValues()
    {
        _ = Assert.Throws<ArgumentException>(() => new UnrealInstallationDiscoveryFailure("", "$", "diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => new UnrealInstallationDiscoveryFailure("code", "", "diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => new UnrealInstallationDiscoveryFailure("code", "$", ""));
        _ = Assert.Throws<ArgumentNullException>(() => new UnrealInstallationDiscoveryReport(null!, []));
        _ = Assert.Throws<ArgumentNullException>(() => new UnrealInstallationDiscoveryReport([], null!));
        _ = Assert.Throws<ArgumentException>(() => new UnrealInstallationDiscoveryReport([null!], []));
        _ = Assert.Throws<ArgumentException>(() => new UnrealInstallationDiscoveryReport([], [null!]));
    }

    private static UnrealInstallationDetection Detection(
        UnrealInstallationDiscoverySource source,
        UnrealInstallationDiscoveryStatus status,
        ToolInstallation? installation,
        string? code,
        string? diagnostic) =>
        new(
            @"C:\Dev\PackageBuilder\tools\unreal\UE_5.8",
            @"C:\Dev\PackageBuilder\tools\unreal\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe",
            @"C:\Dev\PackageBuilder\tools\unreal\UE_5.8\Engine\Build\Build.version",
            source,
            status,
            installation,
            code,
            diagnostic);

    private static ToolInstallation Installation(ToolKind tool, bool isSelected) =>
        ToolInstallation.Create(
            tool,
            ToolVersion.Create(tool, tool == ToolKind.Blender ? "4.5.3" : "5.8.0").Value!,
            tool == ToolKind.Blender
                ? @"C:\Dev\PackageBuilder\tools\blender\blender.exe"
                : @"C:\Dev\PackageBuilder\tools\unreal\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe",
            @"C:\Dev\PackageBuilder\tools",
            isSelected).Value!;
}
