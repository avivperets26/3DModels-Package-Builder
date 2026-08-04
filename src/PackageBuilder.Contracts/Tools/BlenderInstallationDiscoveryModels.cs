using System.Collections.ObjectModel;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Contracts.Tools;

/// <summary>Identifies how a Blender executable candidate was found.</summary>
[Flags]
public enum BlenderInstallationDiscoverySource
{
    Configured = 1,
    Portable = 2,
}

/// <summary>Identifies the verification outcome for one Blender executable candidate.</summary>
public enum BlenderInstallationDiscoveryStatus
{
    Verified = 0,
    ExternalInformational = 1,
    Missing = 2,
    ReparseRejected = 3,
    VerificationFailed = 4,
    InvalidVersionOutput = 5,
}

/// <summary>Names the contained roots and explicit candidates used by one discovery pass.</summary>
public sealed class BlenderInstallationDiscoveryRequest
{
    private readonly ReadOnlyCollection<string> _configuredExecutablePaths;

    public BlenderInstallationDiscoveryRequest(
        BuildJobId jobId,
        string projectRoot,
        string approvedToolsRoot,
        string workingDirectory,
        string temporaryDirectory,
        string cacheDirectory,
        string logDirectory,
        IEnumerable<string> configuredExecutablePaths)
    {
        ArgumentNullException.ThrowIfNull(configuredExecutablePaths);

        JobId = jobId ?? throw new ArgumentNullException(nameof(jobId));
        ProjectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
        ApprovedToolsRoot = approvedToolsRoot ?? throw new ArgumentNullException(nameof(approvedToolsRoot));
        WorkingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
        TemporaryDirectory = temporaryDirectory ?? throw new ArgumentNullException(nameof(temporaryDirectory));
        CacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        LogDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
        _configuredExecutablePaths = Array.AsReadOnly(configuredExecutablePaths.ToArray());
    }

    public BuildJobId JobId { get; }

    public string ProjectRoot { get; }

    public string ApprovedToolsRoot { get; }

    public string WorkingDirectory { get; }

    public string TemporaryDirectory { get; }

    public string CacheDirectory { get; }

    public string LogDirectory { get; }

    public IReadOnlyList<string> ConfiguredExecutablePaths => _configuredExecutablePaths;
}

/// <summary>Records one verified, informational, or rejected Blender executable candidate.</summary>
public sealed class BlenderInstallationDetection
{
    public BlenderInstallationDetection(
        string executablePath,
        BlenderInstallationDiscoverySource source,
        BlenderInstallationDiscoveryStatus status,
        ToolInstallation? installation,
        string? diagnosticCode,
        string? diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Enum.IsDefined(status)
            || source == 0
            || (source & ~(BlenderInstallationDiscoverySource.Configured | BlenderInstallationDiscoverySource.Portable)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        bool verified = status == BlenderInstallationDiscoveryStatus.Verified;
        if (verified != (installation is not null))
        {
            throw new ArgumentException("Only a verified detection may contain an installation.", nameof(installation));
        }

        if (installation is not null
            && (installation.Tool != ToolKind.Blender
                || !installation.CanBeSelected
                || installation.IsSelected))
        {
            throw new ArgumentException(
                "A verified detection requires an unselected contained Blender installation.",
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

        ExecutablePath = executablePath;
        Source = source;
        Status = status;
        Installation = installation;
        DiagnosticCode = diagnosticCode;
        Diagnostic = diagnostic;
    }

    public string ExecutablePath { get; }

    public BlenderInstallationDiscoverySource Source { get; }

    public BlenderInstallationDiscoveryStatus Status { get; }

    public ToolInstallation? Installation { get; }

    public string? DiagnosticCode { get; }

    public string? Diagnostic { get; }

    public bool IsVerified => Installation is not null;

    public bool CanBeSelected => Installation?.CanBeSelected == true;
}

/// <summary>Describes a discovery-wide failure without embedding an untrusted physical path.</summary>
public sealed class BlenderInstallationDiscoveryFailure(string code, string location, string diagnostic)
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

/// <summary>Returns deterministic candidate outcomes plus any discovery-wide failures.</summary>
public sealed class BlenderInstallationDiscoveryReport
{
    private readonly ReadOnlyCollection<BlenderInstallationDetection> _detections;
    private readonly ReadOnlyCollection<BlenderInstallationDiscoveryFailure> _failures;

    public BlenderInstallationDiscoveryReport(
        IEnumerable<BlenderInstallationDetection> detections,
        IEnumerable<BlenderInstallationDiscoveryFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(detections);
        ArgumentNullException.ThrowIfNull(failures);
        BlenderInstallationDetection[] detectionValues = [.. detections];
        BlenderInstallationDiscoveryFailure[] failureValues = [.. failures];
        if (detectionValues.Any(static value => value is null)
            || failureValues.Any(static value => value is null))
        {
            throw new ArgumentException("Discovery report values cannot contain null entries.");
        }

        _detections = Array.AsReadOnly(detectionValues);
        _failures = Array.AsReadOnly(failureValues);
    }

    public IReadOnlyList<BlenderInstallationDetection> Detections => _detections;

    public IReadOnlyList<BlenderInstallationDiscoveryFailure> Failures => _failures;

    public IReadOnlyList<ToolInstallation> VerifiedInstallations =>
        _detections.Where(static value => value.Installation is not null)
            .Select(static value => value.Installation!)
            .ToArray();
}
