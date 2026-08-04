using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class BlenderInstallationDiscoveryContractTests
{
    [Fact]
    public void RequestCopiesConfiguredPathsAndExposesItsRoots()
    {
        string[] configured = [@"C:\Dev\PackageBuilder\tools\blender\blender.exe"];
        BuildJobId jobId = BuildJobId.Create("Job-PB0302").Value!;
        var request = new BlenderInstallationDiscoveryRequest(
            jobId,
            @"C:\Dev\PackageBuilder",
            @"C:\Dev\PackageBuilder\tools",
            @"C:\Dev\PackageBuilder\runtime-data\working",
            @"C:\Dev\PackageBuilder\runtime-data\temp",
            @"C:\Dev\PackageBuilder\runtime-data\cache",
            @"C:\Dev\PackageBuilder\logs",
            configured);
        configured[0] = "changed";

        Assert.Same(jobId, request.JobId);
        Assert.Equal(@"C:\Dev\PackageBuilder", request.ProjectRoot);
        Assert.Equal(@"C:\Dev\PackageBuilder\tools", request.ApprovedToolsRoot);
        Assert.Equal(@"C:\Dev\PackageBuilder\runtime-data\working", request.WorkingDirectory);
        Assert.Equal(@"C:\Dev\PackageBuilder\runtime-data\temp", request.TemporaryDirectory);
        Assert.Equal(@"C:\Dev\PackageBuilder\runtime-data\cache", request.CacheDirectory);
        Assert.Equal(@"C:\Dev\PackageBuilder\logs", request.LogDirectory);
        Assert.Equal(@"C:\Dev\PackageBuilder\tools\blender\blender.exe", Assert.Single(request.ConfiguredExecutablePaths));
    }

    [Fact]
    public void RequestRejectsNullRequiredValues()
    {
        BuildJobId jobId = BuildJobId.Create("Job-PB0302").Value!;
        string root = @"C:\Dev\PackageBuilder";
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationDiscoveryRequest(
            null!, root, root, root, root, root, root, []));
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationDiscoveryRequest(
            jobId, null!, root, root, root, root, root, []));
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationDiscoveryRequest(
            jobId, root, null!, root, root, root, root, []));
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationDiscoveryRequest(
            jobId, root, root, null!, root, root, root, []));
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationDiscoveryRequest(
            jobId, root, root, root, null!, root, root, []));
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationDiscoveryRequest(
            jobId, root, root, root, root, null!, root, []));
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationDiscoveryRequest(
            jobId, root, root, root, root, root, null!, []));
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationDiscoveryRequest(
            jobId, root, root, root, root, root, root, null!));
    }

    [Fact]
    public void DetectionEnforcesVerifiedContainedBlenderInvariant()
    {
        ToolVersion version = ToolVersion.Create(ToolKind.Blender, "4.5.3").Value!;
        ToolInstallation contained = ToolInstallation.Create(
            ToolKind.Blender,
            version,
            @"C:\Dev\PackageBuilder\tools\blender\blender.exe",
            @"C:\Dev\PackageBuilder\tools",
            isSelected: false).Value!;
        var verified = new BlenderInstallationDetection(
            contained.ExecutablePath,
            BlenderInstallationDiscoverySource.Configured,
            BlenderInstallationDiscoveryStatus.Verified,
            contained,
            diagnosticCode: null,
            diagnostic: null);

        Assert.True(verified.IsVerified);
        Assert.True(verified.CanBeSelected);
        Assert.Same(contained, verified.Installation);
        _ = Assert.Throws<ArgumentException>(() => new BlenderInstallationDetection(
            contained.ExecutablePath,
            BlenderInstallationDiscoverySource.Configured,
            BlenderInstallationDiscoveryStatus.Verified,
            installation: null,
            diagnosticCode: null,
            diagnostic: null));
        _ = Assert.Throws<ArgumentException>(() => new BlenderInstallationDetection(
            contained.ExecutablePath,
            BlenderInstallationDiscoverySource.Configured,
            BlenderInstallationDiscoveryStatus.Missing,
            contained,
            "CODE",
            "Diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => new BlenderInstallationDetection(
            contained.ExecutablePath,
            BlenderInstallationDiscoverySource.Configured,
            BlenderInstallationDiscoveryStatus.Verified,
            contained,
            "CODE",
            "Diagnostic"));
    }

    [Fact]
    public void DetectionRejectsInvalidSourcesStatusesDiagnosticsAndInstallations()
    {
        const string Path = @"C:\Dev\PackageBuilder\tools\blender\blender.exe";
        ToolInstallation external = ToolInstallation.Create(
            ToolKind.Blender,
            ToolVersion.Create(ToolKind.Blender, "4.5.3").Value!,
            @"C:\External\blender.exe",
            @"C:\Dev\PackageBuilder\tools",
            isSelected: false).Value!;

        _ = Assert.Throws<ArgumentException>(() => new BlenderInstallationDetection(
            " ", BlenderInstallationDiscoverySource.Configured, BlenderInstallationDiscoveryStatus.Missing,
            null, "CODE", "Diagnostic"));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BlenderInstallationDetection(
            Path, 0, BlenderInstallationDiscoveryStatus.Missing, null, "CODE", "Diagnostic"));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BlenderInstallationDetection(
            Path, (BlenderInstallationDiscoverySource)8, BlenderInstallationDiscoveryStatus.Missing,
            null, "CODE", "Diagnostic"));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new BlenderInstallationDetection(
            Path, BlenderInstallationDiscoverySource.Configured, (BlenderInstallationDiscoveryStatus)99,
            null, "CODE", "Diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => new BlenderInstallationDetection(
            Path, BlenderInstallationDiscoverySource.Configured, BlenderInstallationDiscoveryStatus.Verified,
            external, null, null));
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationDetection(
            Path, BlenderInstallationDiscoverySource.Configured, BlenderInstallationDiscoveryStatus.Missing,
            null, null, "Diagnostic"));
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationDetection(
            Path, BlenderInstallationDiscoverySource.Configured, BlenderInstallationDiscoveryStatus.Missing,
            null, "CODE", null));
    }

    [Fact]
    public void ReportCopiesValuesAndReturnsOnlyVerifiedInstallations()
    {
        ToolInstallation installation = ToolInstallation.Create(
            ToolKind.Blender,
            ToolVersion.Create(ToolKind.Blender, "4.5.3").Value!,
            @"C:\Dev\PackageBuilder\tools\blender\blender.exe",
            @"C:\Dev\PackageBuilder\tools",
            isSelected: false).Value!;
        var verified = new BlenderInstallationDetection(
            installation.ExecutablePath,
            BlenderInstallationDiscoverySource.Portable,
            BlenderInstallationDiscoveryStatus.Verified,
            installation,
            null,
            null);
        var missing = new BlenderInstallationDetection(
            @"C:\Dev\PackageBuilder\tools\blender\missing.exe",
            BlenderInstallationDiscoverySource.Configured,
            BlenderInstallationDiscoveryStatus.Missing,
            null,
            "MISSING",
            "Missing.");
        var failure = new BlenderInstallationDiscoveryFailure("FAILURE", "$", "Failure.");
        BlenderInstallationDetection[] detections = [verified, missing];
        BlenderInstallationDiscoveryFailure[] failures = [failure];
        var report = new BlenderInstallationDiscoveryReport(detections, failures);
        detections[0] = missing;
        failures[0] = new BlenderInstallationDiscoveryFailure("OTHER", "$", "Other.");

        Assert.Equal(2, report.Detections.Count);
        Assert.Same(verified, report.Detections[0]);
        Assert.Same(failure, Assert.Single(report.Failures));
        Assert.Same(installation, Assert.Single(report.VerifiedInstallations));
    }

    [Fact]
    public void ReportAndFailureRejectInvalidValues()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationDiscoveryReport(null!, []));
        _ = Assert.Throws<ArgumentNullException>(() => new BlenderInstallationDiscoveryReport([], null!));
        _ = Assert.Throws<ArgumentException>(() => new BlenderInstallationDiscoveryReport(
            new BlenderInstallationDetection[] { null! }, []));
        _ = Assert.Throws<ArgumentException>(() => new BlenderInstallationDiscoveryReport(
            [], new BlenderInstallationDiscoveryFailure[] { null! }));
        _ = Assert.Throws<ArgumentException>(() => new BlenderInstallationDiscoveryFailure("", "$", "Diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => new BlenderInstallationDiscoveryFailure("CODE", "", "Diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => new BlenderInstallationDiscoveryFailure("CODE", "$", ""));
    }
}
