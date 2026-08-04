using System.Collections.ObjectModel;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Contracts.Tools;

/// <summary>Identifies how a Unity Editor candidate was found.</summary>
[Flags]
public enum UnityInstallationDiscoverySource
{
    Configured = 1,
    Hub = 2,
}

/// <summary>Identifies the verification outcome for one Unity Editor candidate.</summary>
public enum UnityInstallationDiscoveryStatus
{
    Verified = 0,
    ExternalInformational = 1,
    Missing = 2,
    InvalidLayout = 3,
    ReparseRejected = 4,
    RequiredModulesUnavailable = 5,
    VerificationFailed = 6,
    InvalidVersionOutput = 7,
}

/// <summary>Identifies the filesystem entry used to prove that a Unity module is installed.</summary>
public enum UnityEditorModuleEntryKind
{
    Directory = 0,
    File = 1,
}

/// <summary>Identifies the result of checking one required Unity Editor module marker.</summary>
public enum UnityEditorModuleDiscoveryStatus
{
    Installed = 0,
    Missing = 1,
    ReparseRejected = 2,
    Unreadable = 3,
}

/// <summary>Defines one safe marker beneath a Unity Editor installation root.</summary>
public sealed class UnityEditorModuleRequirement
{
    public UnityEditorModuleRequirement(
        string id,
        string relativePath,
        UnityEditorModuleEntryKind entryKind = UnityEditorModuleEntryKind.Directory)
    {
        if (!IsValidId(id))
        {
            throw new ArgumentException(
                "A module ID must contain 1-64 ASCII letters, digits, periods, underscores, or hyphens.",
                nameof(id));
        }

        if (!Enum.IsDefined(entryKind))
        {
            throw new ArgumentOutOfRangeException(nameof(entryKind));
        }

        if (!TryNormalizeRelativePath(relativePath, out string normalizedPath))
        {
            throw new ArgumentException(
                "A module marker must be a canonical relative Windows path without traversal.",
                nameof(relativePath));
        }

        Id = id;
        RelativePath = normalizedPath;
        EntryKind = entryKind;
    }

    public string Id { get; }

    public string RelativePath { get; }

    public UnityEditorModuleEntryKind EntryKind { get; }

    private static bool IsValidId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 64
        && value.All(static character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    private static bool TryNormalizeRelativePath(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains('/')
            || value.EndsWith(Path.DirectorySeparatorChar)
            || Path.IsPathFullyQualified(value))
        {
            return false;
        }

        string[] segments = value.Split(Path.DirectorySeparatorChar);
        if (segments.Any(static segment =>
                segment.Length == 0
                || segment is "." or ".."
                || segment.Contains(':')
                || segment != segment.Trim()))
        {
            return false;
        }

        normalized = string.Join(Path.DirectorySeparatorChar, segments);
        return string.Equals(normalized, value, StringComparison.Ordinal);
    }
}

/// <summary>Names contained roots, Hub roots, explicit candidates, and required module markers.</summary>
public sealed class UnityInstallationDiscoveryRequest
{
    private readonly ReadOnlyCollection<string> _configuredExecutablePaths;
    private readonly ReadOnlyCollection<string> _hubEditorRoots;
    private readonly ReadOnlyCollection<UnityEditorModuleRequirement> _requiredModules;

    public UnityInstallationDiscoveryRequest(
        BuildJobId jobId,
        string projectRoot,
        string approvedToolsRoot,
        string workingDirectory,
        string temporaryDirectory,
        string cacheDirectory,
        string logDirectory,
        IEnumerable<string> configuredExecutablePaths,
        IEnumerable<string> hubEditorRoots,
        IEnumerable<UnityEditorModuleRequirement> requiredModules)
    {
        ArgumentNullException.ThrowIfNull(configuredExecutablePaths);
        ArgumentNullException.ThrowIfNull(hubEditorRoots);
        ArgumentNullException.ThrowIfNull(requiredModules);

        string[] executablePaths = [.. configuredExecutablePaths];
        string[] hubRoots = [.. hubEditorRoots];
        UnityEditorModuleRequirement[] modules = [.. requiredModules];
        if (executablePaths.Any(static value => value is null)
            || hubRoots.Any(static value => value is null)
            || modules.Any(static value => value is null))
        {
            throw new ArgumentException("Unity discovery request collections cannot contain null entries.");
        }

        if (modules.Select(static value => value.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != modules.Length
            || modules.Select(static value => value.RelativePath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count()
                != modules.Length)
        {
            throw new ArgumentException("Required Unity module IDs and marker paths must be unique.");
        }

        JobId = jobId ?? throw new ArgumentNullException(nameof(jobId));
        ProjectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
        ApprovedToolsRoot = approvedToolsRoot ?? throw new ArgumentNullException(nameof(approvedToolsRoot));
        WorkingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
        TemporaryDirectory = temporaryDirectory ?? throw new ArgumentNullException(nameof(temporaryDirectory));
        CacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        LogDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
        _configuredExecutablePaths = Array.AsReadOnly(executablePaths);
        _hubEditorRoots = Array.AsReadOnly(hubRoots);
        _requiredModules = Array.AsReadOnly(modules);
    }

    public BuildJobId JobId { get; }

    public string ProjectRoot { get; }

    public string ApprovedToolsRoot { get; }

    public string WorkingDirectory { get; }

    public string TemporaryDirectory { get; }

    public string CacheDirectory { get; }

    public string LogDirectory { get; }

    public IReadOnlyList<string> ConfiguredExecutablePaths => _configuredExecutablePaths;

    public IReadOnlyList<string> HubEditorRoots => _hubEditorRoots;

    public IReadOnlyList<UnityEditorModuleRequirement> RequiredModules => _requiredModules;
}

/// <summary>Records whether one required Unity module marker is installed and safe.</summary>
public sealed class UnityEditorModuleDetection
{
    public UnityEditorModuleDetection(
        UnityEditorModuleRequirement requirement,
        UnityEditorModuleDiscoveryStatus status,
        string? diagnosticCode,
        string? diagnostic)
    {
        Requirement = requirement ?? throw new ArgumentNullException(nameof(requirement));
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (status == UnityEditorModuleDiscoveryStatus.Installed)
        {
            if (diagnosticCode is not null || diagnostic is not null)
            {
                throw new ArgumentException("An installed module cannot contain a failure diagnostic.");
            }
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
        }

        Status = status;
        DiagnosticCode = diagnosticCode;
        Diagnostic = diagnostic;
    }

    public UnityEditorModuleRequirement Requirement { get; }

    public UnityEditorModuleDiscoveryStatus Status { get; }

    public string? DiagnosticCode { get; }

    public string? Diagnostic { get; }

    public bool IsInstalled => Status == UnityEditorModuleDiscoveryStatus.Installed;
}

/// <summary>Records one verified, informational, or rejected Unity Editor candidate.</summary>
public sealed class UnityInstallationDetection
{
    private readonly ReadOnlyCollection<UnityEditorModuleDetection> _modules;

    public UnityInstallationDetection(
        string executablePath,
        UnityInstallationDiscoverySource source,
        UnityInstallationDiscoveryStatus status,
        ToolInstallation? installation,
        IEnumerable<UnityEditorModuleDetection> modules,
        string? diagnosticCode,
        string? diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(modules);
        if (!Enum.IsDefined(status)
            || source == 0
            || (source & ~(UnityInstallationDiscoverySource.Configured | UnityInstallationDiscoverySource.Hub)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        UnityEditorModuleDetection[] moduleValues = [.. modules];
        if (moduleValues.Any(static value => value is null))
        {
            throw new ArgumentException("Module detections cannot contain null entries.", nameof(modules));
        }

        bool verified = status == UnityInstallationDiscoveryStatus.Verified;
        if (verified != (installation is not null))
        {
            throw new ArgumentException("Only a verified detection may contain an installation.", nameof(installation));
        }

        if (installation is not null
            && (installation.Tool != ToolKind.Unity
                || !installation.CanBeSelected
                || installation.IsSelected
                || moduleValues.Any(static module => !module.IsInstalled)))
        {
            throw new ArgumentException(
                "A verified detection requires all modules and an unselected contained Unity installation.",
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
        _modules = Array.AsReadOnly(moduleValues);
        DiagnosticCode = diagnosticCode;
        Diagnostic = diagnostic;
    }

    public string ExecutablePath { get; }

    public UnityInstallationDiscoverySource Source { get; }

    public UnityInstallationDiscoveryStatus Status { get; }

    public ToolInstallation? Installation { get; }

    public IReadOnlyList<UnityEditorModuleDetection> Modules => _modules;

    public string? DiagnosticCode { get; }

    public string? Diagnostic { get; }

    public bool IsVerified => Installation is not null;

    public bool CanBeSelected => Installation?.CanBeSelected == true;
}

/// <summary>Describes a discovery-wide failure without embedding an untrusted physical path.</summary>
public sealed class UnityInstallationDiscoveryFailure(string code, string location, string diagnostic)
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

/// <summary>Returns deterministic Unity Editor outcomes plus discovery-wide failures.</summary>
public sealed class UnityInstallationDiscoveryReport
{
    private readonly ReadOnlyCollection<UnityInstallationDetection> _detections;
    private readonly ReadOnlyCollection<UnityInstallationDiscoveryFailure> _failures;

    public UnityInstallationDiscoveryReport(
        IEnumerable<UnityInstallationDetection> detections,
        IEnumerable<UnityInstallationDiscoveryFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(detections);
        ArgumentNullException.ThrowIfNull(failures);
        UnityInstallationDetection[] detectionValues = [.. detections];
        UnityInstallationDiscoveryFailure[] failureValues = [.. failures];
        if (detectionValues.Any(static value => value is null)
            || failureValues.Any(static value => value is null))
        {
            throw new ArgumentException("Discovery report values cannot contain null entries.");
        }

        _detections = Array.AsReadOnly(detectionValues);
        _failures = Array.AsReadOnly(failureValues);
    }

    public IReadOnlyList<UnityInstallationDetection> Detections => _detections;

    public IReadOnlyList<UnityInstallationDiscoveryFailure> Failures => _failures;

    public IReadOnlyList<ToolInstallation> VerifiedInstallations =>
        _detections.Where(static value => value.Installation is not null)
            .Select(static value => value.Installation!)
            .ToArray();
}
