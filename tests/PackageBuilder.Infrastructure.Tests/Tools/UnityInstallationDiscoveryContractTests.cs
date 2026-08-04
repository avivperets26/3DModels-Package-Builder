using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class UnityInstallationDiscoveryContractTests
{
    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("slash/name")]
    [InlineData("ümlaut")]
    public void ModuleRequirementRejectsUnsafeIdentifiers(string id) => _ = Assert.Throws<ArgumentException>(() => new UnityEditorModuleRequirement(id, @"Editor\Data"));

    [Theory]
    [InlineData("")]
    [InlineData(@"C:\absolute")]
    [InlineData("forward/slash")]
    [InlineData(@"Editor\..\escape")]
    [InlineData(@"Editor\Data\")]
    [InlineData(@"Editor\\Data")]
    [InlineData(@" Editor\Data")]
    public void ModuleRequirementRejectsUnsafeRelativePaths(string path) => _ = Assert.Throws<ArgumentException>(() => new UnityEditorModuleRequirement("module", path));

    [Fact]
    public void ModuleRequirementRejectsUndefinedEntryKind()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UnityEditorModuleRequirement("module", @"Editor\Data", (UnityEditorModuleEntryKind)99));
    }

    [Fact]
    public void RequestDefensivelyCopiesCollections()
    {
        string[] configured = [@"C:\Dev\PackageBuilder\tools\unity\Editor\Unity.exe"];
        string[] roots = [@"C:\Dev\PackageBuilder\tools\unity\Hub\Editor"];
        UnityEditorModuleRequirement[] modules = [new("windows", @"Editor\Data")];
        UnityInstallationDiscoveryRequest request = Request(configured, roots, modules);

        configured[0] = "changed";
        roots[0] = "changed";
        modules[0] = new UnityEditorModuleRequirement("android", @"Editor\Other");

        Assert.Equal(@"C:\Dev\PackageBuilder\tools\unity\Editor\Unity.exe", request.ConfiguredExecutablePaths[0]);
        Assert.Equal(@"C:\Dev\PackageBuilder\tools\unity\Hub\Editor", request.HubEditorRoots[0]);
        Assert.Equal("windows", request.RequiredModules[0].Id);
    }

    [Fact]
    public void RequestRejectsNullCollectionsEntriesAndDuplicateModules()
    {
        _ = Assert.Throws<ArgumentNullException>(() => Request(null!, [], []));
        _ = Assert.Throws<ArgumentNullException>(() => Request([], null!, []));
        _ = Assert.Throws<ArgumentNullException>(() => Request([], [], null!));
        _ = Assert.Throws<ArgumentException>(() => Request([null!], [], []));
        _ = Assert.Throws<ArgumentException>(() => Request([], [null!], []));
        _ = Assert.Throws<ArgumentException>(() => Request([], [], [null!]));
        _ = Assert.Throws<ArgumentException>(() => Request(
            [],
            [],
            [new("same", @"Editor\A"), new("SAME", @"Editor\B")]));
        _ = Assert.Throws<ArgumentException>(() => Request(
            [],
            [],
            [new("one", @"Editor\A"), new("two", @"editor\a")]));
    }

    [Fact]
    public void RequestRejectsNullRequiredScalarValues()
    {
        BuildJobId jobId = BuildJobId.Create("Job-Contract").Value!;
        string[] values =
        [
            @"C:\Dev\PackageBuilder",
            @"C:\Dev\PackageBuilder\tools",
            @"C:\Dev\PackageBuilder\runtime-data\working",
            @"C:\Dev\PackageBuilder\runtime-data\temp",
            @"C:\Dev\PackageBuilder\runtime-data\cache",
            @"C:\Dev\PackageBuilder\logs",
        ];

        _ = Assert.Throws<ArgumentNullException>(() => new UnityInstallationDiscoveryRequest(
            null!, values[0], values[1], values[2], values[3], values[4], values[5], [], [], []));
        for (int nullIndex = 0; nullIndex < values.Length; nullIndex++)
        {
            string?[] copy = [.. values];
            copy[nullIndex] = null;
            _ = Assert.Throws<ArgumentNullException>(() => new UnityInstallationDiscoveryRequest(
                jobId,
                copy[0]!,
                copy[1]!,
                copy[2]!,
                copy[3]!,
                copy[4]!,
                copy[5]!,
                [],
                [],
                []));
        }
    }

    [Fact]
    public void ModuleDetectionEnforcesStatusAndDiagnostics()
    {
        var requirement = new UnityEditorModuleRequirement("windows", @"Editor\Data");

        _ = Assert.Throws<ArgumentNullException>(() =>
            new UnityEditorModuleDetection(null!, UnityEditorModuleDiscoveryStatus.Installed, null, null));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UnityEditorModuleDetection(requirement, (UnityEditorModuleDiscoveryStatus)99, null, null));
        _ = Assert.Throws<ArgumentException>(() =>
            new UnityEditorModuleDetection(requirement, UnityEditorModuleDiscoveryStatus.Installed, "CODE", "bad"));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new UnityEditorModuleDetection(requirement, UnityEditorModuleDiscoveryStatus.Missing, null, "bad"));

        var installed = new UnityEditorModuleDetection(
            requirement,
            UnityEditorModuleDiscoveryStatus.Installed,
            null,
            null);
        Assert.True(installed.IsInstalled);
    }

    [Fact]
    public void DetectionRequiresVerifiedContainedUnityInstallationAndInstalledModules()
    {
        ToolInstallation unity = Installation(ToolKind.Unity, "6000.3.10f1", isSelected: false);
        ToolInstallation blender = Installation(ToolKind.Blender, "4.5.3", isSelected: false);
        ToolInstallation selectedUnity = Installation(ToolKind.Unity, "6000.3.10f1", isSelected: true);
        var requirement = new UnityEditorModuleRequirement("windows", @"Editor\Data");
        var installedModule = new UnityEditorModuleDetection(
            requirement,
            UnityEditorModuleDiscoveryStatus.Installed,
            null,
            null);
        var missingModule = new UnityEditorModuleDetection(
            requirement,
            UnityEditorModuleDiscoveryStatus.Missing,
            "MISSING",
            "Missing.");

        _ = Assert.Throws<ArgumentException>(() => Detection(UnityInstallationDiscoveryStatus.Verified, null, []));
        _ = Assert.Throws<ArgumentException>(() => Detection(UnityInstallationDiscoveryStatus.Missing, unity, []));
        _ = Assert.Throws<ArgumentException>(() => Detection(UnityInstallationDiscoveryStatus.Verified, blender, []));
        _ = Assert.Throws<ArgumentException>(() => Detection(UnityInstallationDiscoveryStatus.Verified, selectedUnity, []));
        _ = Assert.Throws<ArgumentException>(() => Detection(
            UnityInstallationDiscoveryStatus.Verified,
            unity,
            [missingModule]));

        UnityInstallationDetection detection = Detection(
            UnityInstallationDiscoveryStatus.Verified,
            unity,
            [installedModule]);
        Assert.True(detection.IsVerified);
        Assert.True(detection.CanBeSelected);
        Assert.True(Assert.Single(detection.Modules).IsInstalled);
    }

    [Fact]
    public void DetectionRejectsInvalidSourceStatusModulesAndDiagnostics()
    {
        ToolInstallation unity = Installation(ToolKind.Unity, "6000.3.10f1", isSelected: false);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new UnityInstallationDetection(
            @"C:\Dev\PackageBuilder\tools\unity\6000.3.10f1\Editor\Unity.exe",
            0,
            UnityInstallationDiscoveryStatus.Missing,
            null,
            [],
            "CODE",
            "Diagnostic."));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new UnityInstallationDetection(
            @"C:\Dev\PackageBuilder\tools\unity\6000.3.10f1\Editor\Unity.exe",
            UnityInstallationDiscoverySource.Configured,
            (UnityInstallationDiscoveryStatus)99,
            null,
            [],
            "CODE",
            "Diagnostic."));
        _ = Assert.Throws<ArgumentException>(() => new UnityInstallationDetection(
            @"C:\Dev\PackageBuilder\tools\unity\6000.3.10f1\Editor\Unity.exe",
            UnityInstallationDiscoverySource.Configured,
            UnityInstallationDiscoveryStatus.Verified,
            unity,
            [null!],
            null,
            null));
        _ = Assert.Throws<ArgumentException>(() => new UnityInstallationDetection(
            @"C:\Dev\PackageBuilder\tools\unity\6000.3.10f1\Editor\Unity.exe",
            UnityInstallationDiscoverySource.Configured,
            UnityInstallationDiscoveryStatus.Verified,
            unity,
            [],
            "CODE",
            "Diagnostic."));
        _ = Assert.Throws<ArgumentNullException>(() => new UnityInstallationDetection(
            @"C:\Dev\PackageBuilder\tools\unity\6000.3.10f1\Editor\Unity.exe",
            UnityInstallationDiscoverySource.Configured,
            UnityInstallationDiscoveryStatus.Missing,
            null,
            [],
            null,
            "Diagnostic."));
    }

    [Fact]
    public void FailureAndReportRejectInvalidValuesAndCopyCollections()
    {
        _ = Assert.Throws<ArgumentException>(() => new UnityInstallationDiscoveryFailure("", "$", "bad"));
        _ = Assert.Throws<ArgumentException>(() => new UnityInstallationDiscoveryFailure("CODE", "", "bad"));
        _ = Assert.Throws<ArgumentException>(() => new UnityInstallationDiscoveryFailure("CODE", "$", ""));
        _ = Assert.Throws<ArgumentNullException>(() => new UnityInstallationDiscoveryReport(null!, []));
        _ = Assert.Throws<ArgumentNullException>(() => new UnityInstallationDiscoveryReport([], null!));
        _ = Assert.Throws<ArgumentException>(() => new UnityInstallationDiscoveryReport([null!], []));
        _ = Assert.Throws<ArgumentException>(() => new UnityInstallationDiscoveryReport([], [null!]));

        UnityInstallationDetection[] detections =
        [
            Detection(UnityInstallationDiscoveryStatus.Missing, null, []),
        ];
        UnityInstallationDiscoveryFailure[] failures = [new("CODE", "$", "Diagnostic.")];
        var report = new UnityInstallationDiscoveryReport(detections, failures);
        detections[0] = null!;
        failures[0] = null!;

        _ = Assert.Single(report.Detections);
        _ = Assert.Single(report.Failures);
        Assert.Empty(report.VerifiedInstallations);
    }

    private static UnityInstallationDiscoveryRequest Request(
        IEnumerable<string> configured,
        IEnumerable<string> roots,
        IEnumerable<UnityEditorModuleRequirement> modules) =>
        new(
            BuildJobId.Create("Job-Contract").Value!,
            @"C:\Dev\PackageBuilder",
            @"C:\Dev\PackageBuilder\tools",
            @"C:\Dev\PackageBuilder\runtime-data\working",
            @"C:\Dev\PackageBuilder\runtime-data\temp",
            @"C:\Dev\PackageBuilder\runtime-data\cache",
            @"C:\Dev\PackageBuilder\logs",
            configured,
            roots,
            modules);

    private static UnityInstallationDetection Detection(
        UnityInstallationDiscoveryStatus status,
        ToolInstallation? installation,
        IEnumerable<UnityEditorModuleDetection> modules) =>
        new(
            @"C:\Dev\PackageBuilder\tools\unity\6000.3.10f1\Editor\Unity.exe",
            UnityInstallationDiscoverySource.Configured,
            status,
            installation,
            modules,
            status == UnityInstallationDiscoveryStatus.Verified ? null : "CODE",
            status == UnityInstallationDiscoveryStatus.Verified ? null : "Diagnostic.");

    private static ToolInstallation Installation(ToolKind tool, string version, bool isSelected) =>
        ToolInstallation.Create(
            tool,
            ToolVersion.Create(tool, version).Value!,
            tool == ToolKind.Unity
                ? @"C:\Dev\PackageBuilder\tools\unity\6000.3.10f1\Editor\Unity.exe"
                : @"C:\Dev\PackageBuilder\tools\blender\4.5.3\blender.exe",
            @"C:\Dev\PackageBuilder\tools",
            isSelected).Value!;
}
