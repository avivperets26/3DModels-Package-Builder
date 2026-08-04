using PackageBuilder.Contracts.Processes;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Infrastructure.Tools;

/// <summary>Discovers contained Unity Editors and verifies versions and required modules.</summary>
public sealed class UnityInstallationLocator(
    IExternalProcessRunner processRunner,
    IUnityDiscoveryFileSystem fileSystem) : IUnityInstallationLocator
{
    internal const int MaximumHubRootCount = 32;
    internal const int MaximumHubEditorDirectoryCount = 256;
    internal const int MaximumCandidateCount = 256;
    internal const int VersionCaptureLimit = 4_096;

    private static readonly ProcessExecutionPolicy _versionPolicy = new(
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(5));

    private readonly IExternalProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));
    private readonly IUnityDiscoveryFileSystem _fileSystem = fileSystem
        ?? throw new ArgumentNullException(nameof(fileSystem));

    public UnityInstallationLocator(IExternalProcessRunner processRunner)
        : this(processRunner, new PhysicalUnityDiscoveryFileSystem())
    {
    }

    public async Task<UnityInstallationDiscoveryReport> DiscoverAsync(
        UnityInstallationDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var detections = new List<UnityInstallationDetection>();
        var failures = new List<UnityInstallationDiscoveryFailure>();
        if (!TryValidateRequest(request, failures, out string projectRoot, out string toolsRoot))
        {
            return new UnityInstallationDiscoveryReport(detections, failures);
        }

        var candidates = new Dictionary<string, UnityInstallationDiscoverySource>(
            StringComparer.OrdinalIgnoreCase);
        DiscoverConfiguredCandidates(request, toolsRoot, candidates, detections, failures);
        DiscoverHubCandidates(request, toolsRoot, candidates, detections, failures);

        foreach ((string path, UnityInstallationDiscoverySource source) in candidates
                     .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                failures.Add(Failure(
                    "UNITY_DISCOVERY_CANCELLED",
                    "$",
                    "Unity discovery was cancelled before every contained candidate was verified."));
                break;
            }

            detections.Add(await VerifyAsync(
                    request,
                    projectRoot,
                    toolsRoot,
                    path,
                    source,
                    cancellationToken)
                .ConfigureAwait(false));
        }

        return new UnityInstallationDiscoveryReport(
            detections.OrderBy(static value => value.ExecutablePath, StringComparer.OrdinalIgnoreCase),
            failures);
    }

    private bool TryValidateRequest(
        UnityInstallationDiscoveryRequest request,
        List<UnityInstallationDiscoveryFailure> failures,
        out string projectRoot,
        out string toolsRoot)
    {
        projectRoot = string.Empty;
        toolsRoot = string.Empty;
        if (!TryCanonicalize(request.ProjectRoot, out projectRoot)
            || !TryCanonicalize(request.ApprovedToolsRoot, out toolsRoot)
            || !string.Equals(Path.GetFileName(toolsRoot), "tools", StringComparison.OrdinalIgnoreCase)
            || !IsStrictDescendant(toolsRoot, projectRoot))
        {
            failures.Add(Failure(
                "UNITY_DISCOVERY_ROOT_INVALID",
                "approvedToolsRoot",
                "Discovery requires canonical project and tools roots with tools strictly beneath the project root."));
            return false;
        }

        try
        {
            if (!_fileSystem.DirectoryExists(projectRoot) || !_fileSystem.DirectoryExists(toolsRoot))
            {
                failures.Add(Failure(
                    "UNITY_DISCOVERY_ROOT_MISSING",
                    "approvedToolsRoot",
                    "The canonical project and tools roots must exist before discovery."));
                return false;
            }

            if (IsReparse(_fileSystem.GetAttributes(projectRoot))
                || HasReparseBoundary(projectRoot, toolsRoot))
            {
                failures.Add(Failure(
                    "UNITY_DISCOVERY_ROOT_REPARSE",
                    "approvedToolsRoot",
                    "Discovery roots cannot contain a reparse-point boundary."));
                return false;
            }
        }
        catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
        {
            failures.Add(Failure(
                "UNITY_DISCOVERY_ROOT_UNREADABLE",
                "approvedToolsRoot",
                "The discovery roots could not be inspected safely."));
            return false;
        }

        return true;
    }

    private void DiscoverConfiguredCandidates(
        UnityInstallationDiscoveryRequest request,
        string toolsRoot,
        Dictionary<string, UnityInstallationDiscoverySource> candidates,
        List<UnityInstallationDetection> detections,
        List<UnityInstallationDiscoveryFailure> failures)
    {
        for (int index = 0; index < request.ConfiguredExecutablePaths.Count; index++)
        {
            string configuredPath = request.ConfiguredExecutablePaths[index];
            if (!TryCanonicalize(configuredPath, out string path))
            {
                failures.Add(Failure(
                    "UNITY_CONFIGURED_PATH_INVALID",
                    $"configuredExecutablePaths[{index}]",
                    "A configured Unity executable path must be a canonical fully qualified Windows file path."));
                continue;
            }

            try
            {
                if (!_fileSystem.FileExists(path))
                {
                    detections.Add(Rejected(
                        path,
                        UnityInstallationDiscoverySource.Configured,
                        UnityInstallationDiscoveryStatus.Missing,
                        [],
                        "UNITY_EXECUTABLE_MISSING",
                        "The configured Unity executable does not exist."));
                    continue;
                }

                if (!IsStrictDescendant(path, toolsRoot))
                {
                    detections.Add(Rejected(
                        path,
                        UnityInstallationDiscoverySource.Configured,
                        UnityInstallationDiscoveryStatus.ExternalInformational,
                        [],
                        "UNITY_EXTERNAL_INFORMATIONAL",
                        "The external Unity detection is informational and cannot be selected or executed."));
                    continue;
                }

                if (!TryGetInstallationRoot(path, out _))
                {
                    detections.Add(Rejected(
                        path,
                        UnityInstallationDiscoverySource.Configured,
                        UnityInstallationDiscoveryStatus.InvalidLayout,
                        [],
                        "UNITY_EDITOR_LAYOUT_INVALID",
                        "A Unity Editor executable must use the <version>\\Editor\\Unity.exe layout."));
                    continue;
                }

                if (HasReparseBoundary(toolsRoot, path))
                {
                    detections.Add(Rejected(
                        path,
                        UnityInstallationDiscoverySource.Configured,
                        UnityInstallationDiscoveryStatus.ReparseRejected,
                        [],
                        "UNITY_REPARSE_REJECTED",
                        "The contained Unity candidate crosses a reparse-point boundary."));
                    continue;
                }

                if (!AddCandidate(candidates, path, UnityInstallationDiscoverySource.Configured, failures))
                {
                    return;
                }
            }
            catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
            {
                detections.Add(Rejected(
                    path,
                    UnityInstallationDiscoverySource.Configured,
                    UnityInstallationDiscoveryStatus.VerificationFailed,
                    [],
                    "UNITY_CANDIDATE_UNREADABLE",
                    "The configured Unity candidate could not be inspected safely."));
            }
        }
    }

    private void DiscoverHubCandidates(
        UnityInstallationDiscoveryRequest request,
        string toolsRoot,
        Dictionary<string, UnityInstallationDiscoverySource> candidates,
        List<UnityInstallationDetection> detections,
        List<UnityInstallationDiscoveryFailure> failures)
    {
        var uniqueRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int acceptedRootCount = 0;
        int scannedEditorDirectories = 0;
        for (int rootIndex = -1; rootIndex < request.HubEditorRoots.Count; rootIndex++)
        {
            bool isDefault = rootIndex < 0;
            string value = isDefault
                ? Path.Combine(toolsRoot, "unity", "Hub", "Editor")
                : request.HubEditorRoots[rootIndex];
            string location = isDefault ? "defaultHubEditorRoot" : $"hubEditorRoots[{rootIndex}]";
            if (!TryCanonicalize(value, out string root))
            {
                failures.Add(Failure(
                    "UNITY_HUB_ROOT_INVALID",
                    location,
                    "A Unity Hub editor root must be a canonical fully qualified Windows directory path."));
                continue;
            }

            if (!uniqueRoots.Add(root))
            {
                continue;
            }

            acceptedRootCount++;
            if (acceptedRootCount > MaximumHubRootCount)
            {
                failures.Add(Failure(
                    "UNITY_HUB_ROOT_LIMIT",
                    "hubEditorRoots",
                    "The bounded Unity Hub editor-root limit was exceeded."));
                return;
            }

            if (!IsStrictDescendant(root, toolsRoot))
            {
                failures.Add(Failure(
                    "UNITY_HUB_ROOT_EXTERNAL",
                    location,
                    "An external Unity Hub root is informational only and was not enumerated."));
                continue;
            }

            try
            {
                if (!_fileSystem.DirectoryExists(root))
                {
                    if (!isDefault)
                    {
                        failures.Add(Failure(
                            "UNITY_HUB_ROOT_MISSING",
                            location,
                            "The configured Unity Hub editor root does not exist."));
                    }

                    continue;
                }

                if (HasReparseBoundary(toolsRoot, root))
                {
                    failures.Add(Failure(
                        "UNITY_HUB_ROOT_REPARSE",
                        location,
                        "The Unity Hub editor root crosses a reparse-point boundary and was not scanned."));
                    continue;
                }

                int remainingDirectoryCapacity = MaximumHubEditorDirectoryCount - scannedEditorDirectories;
                string[] editorDirectories =
                [
                    .. _fileSystem.EnumerateDirectories(root).Take(remainingDirectoryCapacity + 1),
                ];
                if (editorDirectories.Length > remainingDirectoryCapacity)
                {
                    failures.Add(Failure(
                        "UNITY_HUB_EDITOR_DIRECTORY_LIMIT",
                        "hubEditorRoots",
                        "The bounded Unity Hub editor-directory limit was exceeded."));
                    return;
                }

                Array.Sort(editorDirectories, StringComparer.OrdinalIgnoreCase);
                foreach (string discoveredDirectory in editorDirectories)
                {
                    scannedEditorDirectories++;
                    if (!TryCanonicalize(discoveredDirectory, out string editorDirectory)
                        || !IsStrictDescendant(editorDirectory, root))
                    {
                        failures.Add(Failure(
                            "UNITY_HUB_EDITOR_PATH_INVALID",
                            location,
                            "A Unity Hub editor directory was not a canonical child of its configured root."));
                        continue;
                    }

                    string path = Path.Combine(editorDirectory, "Editor", "Unity.exe");
                    if (HasReparseBoundary(root, editorDirectory))
                    {
                        detections.Add(Rejected(
                            path,
                            UnityInstallationDiscoverySource.Hub,
                            UnityInstallationDiscoveryStatus.ReparseRejected,
                            [],
                            "UNITY_REPARSE_REJECTED",
                            "A Unity Hub editor directory crosses a reparse-point boundary."));
                        continue;
                    }

                    if (!_fileSystem.FileExists(path))
                    {
                        detections.Add(Rejected(
                            path,
                            UnityInstallationDiscoverySource.Hub,
                            UnityInstallationDiscoveryStatus.Missing,
                            [],
                            "UNITY_EXECUTABLE_MISSING",
                            "A Unity Hub editor directory does not contain Editor\\Unity.exe."));
                        continue;
                    }

                    if (HasReparseBoundary(editorDirectory, path))
                    {
                        detections.Add(Rejected(
                            path,
                            UnityInstallationDiscoverySource.Hub,
                            UnityInstallationDiscoveryStatus.ReparseRejected,
                            [],
                            "UNITY_REPARSE_REJECTED",
                            "The Unity Hub executable crosses a reparse-point boundary."));
                        continue;
                    }

                    if (!AddCandidate(candidates, path, UnityInstallationDiscoverySource.Hub, failures))
                    {
                        return;
                    }
                }
            }
            catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
            {
                failures.Add(Failure(
                    "UNITY_HUB_ROOT_UNREADABLE",
                    location,
                    "A Unity Hub editor root could not be inspected safely."));
            }
        }
    }

    private async Task<UnityInstallationDetection> VerifyAsync(
        UnityInstallationDiscoveryRequest request,
        string projectRoot,
        string toolsRoot,
        string path,
        UnityInstallationDiscoverySource source,
        CancellationToken cancellationToken)
    {
        _ = TryGetInstallationRoot(path, out string installationRoot);

        List<UnityEditorModuleDetection> modules = InspectRequiredModules(
            installationRoot,
            request.RequiredModules);
        if (modules.Any(static module => !module.IsInstalled))
        {
            return Rejected(
                path,
                source,
                UnityInstallationDiscoveryStatus.RequiredModulesUnavailable,
                modules,
                "UNITY_REQUIRED_MODULES_UNAVAILABLE",
                "One or more required Unity module markers are missing or unsafe.");
        }

        var processRequest = new ExternalProcessRequest(
            request.JobId,
            projectRoot,
            path,
            request.WorkingDirectory,
            request.TemporaryDirectory,
            request.CacheDirectory,
            request.LogDirectory,
            ["-version"],
            [],
            VersionCaptureLimit,
            _versionPolicy);
        ProcessExecutionOperationResult processResult = await _processRunner.RunAsync(
                processRequest,
                cancellationToken)
            .ConfigureAwait(false);
        if (!processResult.IsSuccess)
        {
            return Rejected(
                path,
                source,
                UnityInstallationDiscoveryStatus.VerificationFailed,
                modules,
                "UNITY_VERSION_EXECUTION_FAILED",
                "The Unity version command could not be executed safely.");
        }

        ExternalProcessReceipt receipt = processResult.Value!;
        if (receipt.Completion.Kind != ProcessCompletionKind.Exited)
        {
            return Rejected(
                path,
                source,
                UnityInstallationDiscoveryStatus.VerificationFailed,
                modules,
                "UNITY_VERSION_PROCESS_INCOMPLETE",
                "The Unity version command did not exit normally within its bounded policy.");
        }

        if (receipt.ExitCode != 0)
        {
            return Rejected(
                path,
                source,
                UnityInstallationDiscoveryStatus.VerificationFailed,
                modules,
                "UNITY_VERSION_EXIT_CODE",
                "The Unity version command returned a nonzero exit code.");
        }

        if (receipt.StandardOutput.WasTruncated)
        {
            return Rejected(
                path,
                source,
                UnityInstallationDiscoveryStatus.InvalidVersionOutput,
                modules,
                "UNITY_VERSION_OUTPUT_TRUNCATED",
                "The Unity version output exceeded the bounded verification capture.");
        }

        if (!TryParseVersion(receipt.StandardOutput.Text, out ToolVersion? version))
        {
            return Rejected(
                path,
                source,
                UnityInstallationDiscoveryStatus.InvalidVersionOutput,
                modules,
                "UNITY_VERSION_OUTPUT_INVALID",
                "The Unity version output did not contain one canonical supported Unity version.");
        }

        ToolInstallation installation = ToolInstallation.Create(
            ToolKind.Unity,
            version,
            path,
            toolsRoot,
            isSelected: false).Value!;
        return new UnityInstallationDetection(
            path,
            source,
            UnityInstallationDiscoveryStatus.Verified,
            installation,
            modules,
            diagnosticCode: null,
            diagnostic: null);
    }

    private List<UnityEditorModuleDetection> InspectRequiredModules(
        string installationRoot,
        IReadOnlyList<UnityEditorModuleRequirement> requirements)
    {
        var results = new List<UnityEditorModuleDetection>(requirements.Count);
        foreach (UnityEditorModuleRequirement requirement in requirements)
        {
            string path = Path.GetFullPath(Path.Combine(installationRoot, requirement.RelativePath));
            try
            {
                bool exists = requirement.EntryKind == UnityEditorModuleEntryKind.Directory
                    ? _fileSystem.DirectoryExists(path)
                    : _fileSystem.FileExists(path);
                if (!exists)
                {
                    results.Add(ModuleRejected(
                        requirement,
                        UnityEditorModuleDiscoveryStatus.Missing,
                        "UNITY_MODULE_MISSING",
                        "The required Unity module marker is missing."));
                    continue;
                }

                if (HasReparseBoundary(installationRoot, path))
                {
                    results.Add(ModuleRejected(
                        requirement,
                        UnityEditorModuleDiscoveryStatus.ReparseRejected,
                        "UNITY_MODULE_REPARSE_REJECTED",
                        "The required Unity module marker crosses a reparse-point boundary."));
                    continue;
                }

                results.Add(new UnityEditorModuleDetection(
                    requirement,
                    UnityEditorModuleDiscoveryStatus.Installed,
                    diagnosticCode: null,
                    diagnostic: null));
            }
            catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
            {
                results.Add(ModuleRejected(
                    requirement,
                    UnityEditorModuleDiscoveryStatus.Unreadable,
                    "UNITY_MODULE_UNREADABLE",
                    "The required Unity module marker could not be inspected safely."));
            }
        }

        return results;
    }

    private static bool TryParseVersion(string output, out ToolVersion? version)
    {
        version = null;
        using var reader = new StringReader(output);
        string? firstContentLine = null;
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                firstContentLine = line;
                break;
            }
        }

        if (firstContentLine is null)
        {
            return false;
        }

        ToolModelValidationResult<ToolVersion> result = ToolVersion.Create(ToolKind.Unity, firstContentLine);
        version = result.Value;
        return result.IsValid;
    }

    private static bool AddCandidate(
        Dictionary<string, UnityInstallationDiscoverySource> candidates,
        string path,
        UnityInstallationDiscoverySource source,
        List<UnityInstallationDiscoveryFailure> failures)
    {
        if (candidates.TryGetValue(path, out UnityInstallationDiscoverySource existing))
        {
            candidates[path] = existing | source;
            return true;
        }

        if (candidates.Count >= MaximumCandidateCount)
        {
            if (!failures.Any(static failure => failure.Code == "UNITY_CANDIDATE_LIMIT"))
            {
                failures.Add(Failure(
                    "UNITY_CANDIDATE_LIMIT",
                    "configuredExecutablePaths",
                    "The bounded Unity executable candidate limit was exceeded."));
            }

            return false;
        }

        candidates.Add(path, source);
        return true;
    }

    private bool HasReparseBoundary(string trustedRoot, string path)
    {
        string relative = Path.GetRelativePath(trustedRoot, path);
        string current = trustedRoot;
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (IsReparse(_fileSystem.GetAttributes(current)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetInstallationRoot(string executablePath, out string installationRoot)
    {
        installationRoot = string.Empty;
        if (!string.Equals(Path.GetFileName(executablePath), "Unity.exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        DirectoryInfo editorDirectory = Directory.GetParent(executablePath)!;
        if (!string.Equals(editorDirectory.Name, "Editor", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        installationRoot = editorDirectory.Parent!.FullName;
        return true;
    }

    private static bool TryCanonicalize(string? value, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains('/')
            || !Path.IsPathFullyQualified(value)
            || value.EndsWith(Path.DirectorySeparatorChar))
        {
            return false;
        }

        try
        {
            path = Path.GetFullPath(value);
            return string.Equals(path, value, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            path = string.Empty;
            return false;
        }
    }

    private static bool IsStrictDescendant(string path, string root) =>
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool IsReparse(FileAttributes attributes) =>
        (attributes & FileAttributes.ReparsePoint) != 0;

    private static bool IsExpectedFileSystemFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static UnityInstallationDetection Rejected(
        string path,
        UnityInstallationDiscoverySource source,
        UnityInstallationDiscoveryStatus status,
        IEnumerable<UnityEditorModuleDetection> modules,
        string code,
        string diagnostic) =>
        new(path, source, status, installation: null, modules, code, diagnostic);

    private static UnityEditorModuleDetection ModuleRejected(
        UnityEditorModuleRequirement requirement,
        UnityEditorModuleDiscoveryStatus status,
        string code,
        string diagnostic) =>
        new(requirement, status, code, diagnostic);

    private static UnityInstallationDiscoveryFailure Failure(
        string code,
        string location,
        string diagnostic) =>
        new(code, location, diagnostic);
}
