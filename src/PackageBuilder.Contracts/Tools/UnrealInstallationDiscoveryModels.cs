using System.Collections.ObjectModel;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Contracts.Tools;

/// <summary>Identifies how an Unreal Engine installation root was found.</summary>
[Flags]
public enum UnrealInstallationDiscoverySource
{
    Configured = 1,
    LauncherManifest = 2,
    SourceBuild = 4,
    Registered = 8,
    Standard = 16,
    ContainedRoot = 32,
}

/// <summary>Identifies the verification outcome for one Unreal Engine installation root.</summary>
public enum UnrealInstallationDiscoveryStatus
{
    Verified = 0,
    ExternalInformational = 1,
    Missing = 2,
    InvalidLayout = 3,
    ReparseRejected = 4,
    InvalidBuildVersion = 5,
    Unreadable = 6,
}

/// <summary>Names explicit Unreal discovery inputs plus the canonical project and tools roots.</summary>
public sealed class UnrealInstallationDiscoveryRequest
{
    private readonly ReadOnlyCollection<string> _configuredInstallationRoots;
    private readonly ReadOnlyCollection<string> _launcherManifestPaths;
    private readonly ReadOnlyCollection<string> _sourceBuildRoots;
    private readonly ReadOnlyCollection<string> _registeredInstallationRoots;
    private readonly ReadOnlyCollection<string> _standardInstallationRoots;

    public UnrealInstallationDiscoveryRequest(
        BuildJobId jobId,
        string projectRoot,
        string approvedToolsRoot,
        IEnumerable<string> configuredInstallationRoots,
        IEnumerable<string> launcherManifestPaths,
        IEnumerable<string> sourceBuildRoots,
        IEnumerable<string> registeredInstallationRoots,
        IEnumerable<string> standardInstallationRoots)
    {
        ArgumentNullException.ThrowIfNull(configuredInstallationRoots);
        ArgumentNullException.ThrowIfNull(launcherManifestPaths);
        ArgumentNullException.ThrowIfNull(sourceBuildRoots);
        ArgumentNullException.ThrowIfNull(registeredInstallationRoots);
        ArgumentNullException.ThrowIfNull(standardInstallationRoots);

        string[] configured = [.. configuredInstallationRoots];
        string[] manifests = [.. launcherManifestPaths];
        string[] sourceBuilds = [.. sourceBuildRoots];
        string[] registered = [.. registeredInstallationRoots];
        string[] standard = [.. standardInstallationRoots];
        if (configured.Any(static value => value is null)
            || manifests.Any(static value => value is null)
            || sourceBuilds.Any(static value => value is null)
            || registered.Any(static value => value is null)
            || standard.Any(static value => value is null))
        {
            throw new ArgumentException("Unreal discovery request collections cannot contain null entries.");
        }

        JobId = jobId ?? throw new ArgumentNullException(nameof(jobId));
        ProjectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
        ApprovedToolsRoot = approvedToolsRoot ?? throw new ArgumentNullException(nameof(approvedToolsRoot));
        _configuredInstallationRoots = Array.AsReadOnly(configured);
        _launcherManifestPaths = Array.AsReadOnly(manifests);
        _sourceBuildRoots = Array.AsReadOnly(sourceBuilds);
        _registeredInstallationRoots = Array.AsReadOnly(registered);
        _standardInstallationRoots = Array.AsReadOnly(standard);
    }

    public BuildJobId JobId { get; }

    public string ProjectRoot { get; }

    public string ApprovedToolsRoot { get; }

    public IReadOnlyList<string> ConfiguredInstallationRoots => _configuredInstallationRoots;

    public IReadOnlyList<string> LauncherManifestPaths => _launcherManifestPaths;

    public IReadOnlyList<string> SourceBuildRoots => _sourceBuildRoots;

    public IReadOnlyList<string> RegisteredInstallationRoots => _registeredInstallationRoots;

    public IReadOnlyList<string> StandardInstallationRoots => _standardInstallationRoots;
}

/// <summary>Records one verified, informational, or rejected Unreal Engine installation root.</summary>
public sealed class UnrealInstallationDetection
{
    private const UnrealInstallationDiscoverySource AllSources =
        UnrealInstallationDiscoverySource.Configured
        | UnrealInstallationDiscoverySource.LauncherManifest
        | UnrealInstallationDiscoverySource.SourceBuild
        | UnrealInstallationDiscoverySource.Registered
        | UnrealInstallationDiscoverySource.Standard
        | UnrealInstallationDiscoverySource.ContainedRoot;

    public UnrealInstallationDetection(
        string installationRoot,
        string editorExecutablePath,
        string buildVersionPath,
        UnrealInstallationDiscoverySource source,
        UnrealInstallationDiscoveryStatus status,
        ToolInstallation? installation,
        string? diagnosticCode,
        string? diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installationRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(editorExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildVersionPath);
        if (!Enum.IsDefined(status) || source == 0 || (source & ~AllSources) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        bool verified = status == UnrealInstallationDiscoveryStatus.Verified;
        if (verified != (installation is not null))
        {
            throw new ArgumentException("Only a verified detection may contain an installation.", nameof(installation));
        }

        if (installation is not null
            && (installation.Tool != ToolKind.Unreal
                || !installation.CanBeSelected
                || installation.IsSelected
                || !string.Equals(
                    installation.ExecutablePath,
                    editorExecutablePath,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "A verified detection requires an unselected contained Unreal installation for its editor path.",
                nameof(installation));
        }

        if (verified)
        {
            if (diagnosticCode is not null || diagnostic is not null)
            {
                throw new ArgumentException("A verified detection cannot contain a failure diagnostic.");
            }
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
        }

        InstallationRoot = installationRoot;
        EditorExecutablePath = editorExecutablePath;
        BuildVersionPath = buildVersionPath;
        Source = source;
        Status = status;
        Installation = installation;
        DiagnosticCode = diagnosticCode;
        Diagnostic = diagnostic;
    }

    public string InstallationRoot { get; }

    public string EditorExecutablePath { get; }

    public string BuildVersionPath { get; }

    public UnrealInstallationDiscoverySource Source { get; }

    public UnrealInstallationDiscoveryStatus Status { get; }

    public ToolInstallation? Installation { get; }

    public string? DiagnosticCode { get; }

    public string? Diagnostic { get; }

    public bool IsVerified => Installation is not null;

    public bool CanBeSelected => Installation?.CanBeSelected == true;
}

/// <summary>Describes a discovery-wide Unreal failure without embedding an untrusted physical path.</summary>
public sealed class UnrealInstallationDiscoveryFailure(string code, string location, string diagnostic)
{
    public string Code { get; } = string.IsNullOrWhiteSpace(code)
        ? throw new ArgumentException("A failure code is required.", nameof(code))
        : code;

    public string Location { get; } = string.IsNullOrWhiteSpace(location)
        ? throw new ArgumentException("A safe failure location is required.", nameof(location))
        : location;

    public string Diagnostic { get; } = string.IsNullOrWhiteSpace(diagnostic)
        ? throw new ArgumentException("A safe diagnostic is required.", nameof(diagnostic))
        : diagnostic;
}

/// <summary>Returns deterministic Unreal Engine outcomes plus discovery-wide failures.</summary>
public sealed class UnrealInstallationDiscoveryReport
{
    private readonly ReadOnlyCollection<UnrealInstallationDetection> _detections;
    private readonly ReadOnlyCollection<UnrealInstallationDiscoveryFailure> _failures;

    public UnrealInstallationDiscoveryReport(
        IEnumerable<UnrealInstallationDetection> detections,
        IEnumerable<UnrealInstallationDiscoveryFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(detections);
        ArgumentNullException.ThrowIfNull(failures);
        UnrealInstallationDetection[] detectionValues = [.. detections];
        UnrealInstallationDiscoveryFailure[] failureValues = [.. failures];
        if (detectionValues.Any(static value => value is null)
            || failureValues.Any(static value => value is null))
        {
            throw new ArgumentException("Discovery report values cannot contain null entries.");
        }

        _detections = Array.AsReadOnly(detectionValues);
        _failures = Array.AsReadOnly(failureValues);
    }

    public IReadOnlyList<UnrealInstallationDetection> Detections => _detections;

    public IReadOnlyList<UnrealInstallationDiscoveryFailure> Failures => _failures;

    public IReadOnlyList<ToolInstallation> VerifiedInstallations =>
        _detections.Where(static value => value.Installation is not null)
            .Select(static value => value.Installation!)
            .ToArray();
}
