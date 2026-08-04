using PackageBuilder.Contracts.Processes;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Infrastructure.Tools;

/// <summary>Discovers contained Blender executables and verifies them through shell-free version output.</summary>
public sealed class BlenderInstallationLocator(
    IExternalProcessRunner processRunner,
    IBlenderDiscoveryFileSystem fileSystem) : IBlenderInstallationLocator
{
    internal const int MaximumPortableDepth = 4;
    internal const int MaximumScannedDirectories = 1_024;
    internal const int MaximumCandidateCount = 256;
    internal const int VersionCaptureLimit = 4_096;

    private static readonly ProcessExecutionPolicy _versionPolicy = new(
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(5));

    private readonly IExternalProcessRunner _processRunner = processRunner
        ?? throw new ArgumentNullException(nameof(processRunner));
    private readonly IBlenderDiscoveryFileSystem _fileSystem = fileSystem
        ?? throw new ArgumentNullException(nameof(fileSystem));

    public BlenderInstallationLocator(IExternalProcessRunner processRunner)
        : this(processRunner, new PhysicalBlenderDiscoveryFileSystem())
    {
    }

    public async Task<BlenderInstallationDiscoveryReport> DiscoverAsync(
        BlenderInstallationDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var detections = new List<BlenderInstallationDetection>();
        var failures = new List<BlenderInstallationDiscoveryFailure>();
        if (!TryValidateRequest(request, failures, out string projectRoot, out string toolsRoot))
        {
            return new BlenderInstallationDiscoveryReport(detections, failures);
        }

        var candidates = new Dictionary<string, BlenderInstallationDiscoverySource>(
            StringComparer.OrdinalIgnoreCase);
        DiscoverConfiguredCandidates(request, toolsRoot, candidates, detections, failures);
        DiscoverPortableCandidates(toolsRoot, candidates, detections, failures);

        foreach ((string path, BlenderInstallationDiscoverySource source) in candidates
                     .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                failures.Add(Failure(
                    "BLENDER_DISCOVERY_CANCELLED",
                    "$",
                    "Blender discovery was cancelled before every contained candidate was verified."));
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

        return new BlenderInstallationDiscoveryReport(
            detections.OrderBy(static value => value.ExecutablePath, StringComparer.OrdinalIgnoreCase),
            failures);
    }

    private bool TryValidateRequest(
        BlenderInstallationDiscoveryRequest request,
        List<BlenderInstallationDiscoveryFailure> failures,
        out string projectRoot,
        out string toolsRoot)
    {
        projectRoot = string.Empty;
        toolsRoot = string.Empty;
        if (!TryCanonicalizeDirectory(request.ProjectRoot, out projectRoot)
            || !TryCanonicalizeDirectory(request.ApprovedToolsRoot, out toolsRoot)
            || !string.Equals(Path.GetFileName(toolsRoot), "tools", StringComparison.OrdinalIgnoreCase)
            || !IsStrictDescendant(toolsRoot, projectRoot))
        {
            failures.Add(Failure(
                "BLENDER_DISCOVERY_ROOT_INVALID",
                "approvedToolsRoot",
                "Discovery requires canonical project and tools roots with tools strictly beneath the project root."));
            return false;
        }

        try
        {
            if (!_fileSystem.DirectoryExists(projectRoot) || !_fileSystem.DirectoryExists(toolsRoot))
            {
                failures.Add(Failure(
                    "BLENDER_DISCOVERY_ROOT_MISSING",
                    "approvedToolsRoot",
                    "The canonical project and tools roots must exist before discovery."));
                return false;
            }

            if (IsReparse(_fileSystem.GetAttributes(projectRoot))
                || HasReparseBoundary(projectRoot, toolsRoot))
            {
                failures.Add(Failure(
                    "BLENDER_DISCOVERY_ROOT_REPARSE",
                    "approvedToolsRoot",
                    "Discovery roots cannot contain a reparse-point boundary."));
                return false;
            }
        }
        catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
        {
            failures.Add(Failure(
                "BLENDER_DISCOVERY_ROOT_UNREADABLE",
                "approvedToolsRoot",
                "The discovery roots could not be inspected safely."));
            return false;
        }

        return true;
    }

    private void DiscoverConfiguredCandidates(
        BlenderInstallationDiscoveryRequest request,
        string toolsRoot,
        Dictionary<string, BlenderInstallationDiscoverySource> candidates,
        List<BlenderInstallationDetection> detections,
        List<BlenderInstallationDiscoveryFailure> failures)
    {
        for (int index = 0; index < request.ConfiguredExecutablePaths.Count; index++)
        {
            string? configuredPath = request.ConfiguredExecutablePaths[index];
            if (!TryCanonicalizeFile(configuredPath, out string path))
            {
                failures.Add(Failure(
                    "BLENDER_CONFIGURED_PATH_INVALID",
                    $"configuredExecutablePaths[{index}]",
                    "A configured Blender executable path must be a canonical fully qualified Windows file path."));
                continue;
            }

            try
            {
                if (!_fileSystem.FileExists(path))
                {
                    detections.Add(Rejected(
                        path,
                        BlenderInstallationDiscoverySource.Configured,
                        BlenderInstallationDiscoveryStatus.Missing,
                        "BLENDER_EXECUTABLE_MISSING",
                        "The configured Blender executable does not exist."));
                    continue;
                }

                if (!IsStrictDescendant(path, toolsRoot))
                {
                    detections.Add(Rejected(
                        path,
                        BlenderInstallationDiscoverySource.Configured,
                        BlenderInstallationDiscoveryStatus.ExternalInformational,
                        "BLENDER_EXTERNAL_INFORMATIONAL",
                        "The external Blender detection is informational and cannot be selected or executed."));
                    continue;
                }

                if (HasReparseBoundary(toolsRoot, path))
                {
                    detections.Add(Rejected(
                        path,
                        BlenderInstallationDiscoverySource.Configured,
                        BlenderInstallationDiscoveryStatus.ReparseRejected,
                        "BLENDER_REPARSE_REJECTED",
                        "The contained Blender candidate crosses a reparse-point boundary."));
                    continue;
                }

                _ = AddCandidate(candidates, path, BlenderInstallationDiscoverySource.Configured, failures);
            }
            catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
            {
                detections.Add(Rejected(
                    path,
                    BlenderInstallationDiscoverySource.Configured,
                    BlenderInstallationDiscoveryStatus.VerificationFailed,
                    "BLENDER_CANDIDATE_UNREADABLE",
                    "The configured Blender candidate could not be inspected safely."));
            }
        }
    }

    private void DiscoverPortableCandidates(
        string toolsRoot,
        Dictionary<string, BlenderInstallationDiscoverySource> candidates,
        List<BlenderInstallationDetection> detections,
        List<BlenderInstallationDiscoveryFailure> failures)
    {
        string portableRoot = Path.Combine(toolsRoot, "blender");
        try
        {
            if (!_fileSystem.DirectoryExists(portableRoot))
            {
                return;
            }

            if (IsReparse(_fileSystem.GetAttributes(portableRoot)))
            {
                failures.Add(Failure(
                    "BLENDER_PORTABLE_ROOT_REPARSE",
                    "portableRoot",
                    "The portable Blender root is a reparse point and was not scanned."));
                return;
            }
        }
        catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
        {
            failures.Add(Failure(
                "BLENDER_PORTABLE_ROOT_UNREADABLE",
                "portableRoot",
                "The portable Blender root could not be inspected safely."));
            return;
        }

        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((portableRoot, 0));
        int scannedDirectories = 0;
        bool depthLimitReported = false;
        while (queue.Count > 0)
        {
            (string current, int depth) = queue.Dequeue();
            scannedDirectories++;
            if (scannedDirectories > MaximumScannedDirectories)
            {
                failures.Add(Failure(
                    "BLENDER_PORTABLE_DIRECTORY_LIMIT",
                    "portableRoot",
                    "The bounded portable Blender directory limit was exceeded."));
                return;
            }

            string[] files;
            string[] directories;
            try
            {
                files = [.. _fileSystem.EnumerateFiles(current).Order(StringComparer.OrdinalIgnoreCase)];
                directories =
                [
                    .. _fileSystem.EnumerateDirectories(current).Order(StringComparer.OrdinalIgnoreCase),
                ];
            }
            catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
            {
                failures.Add(Failure(
                    "BLENDER_PORTABLE_DIRECTORY_UNREADABLE",
                    "portableRoot",
                    "A portable Blender directory could not be enumerated safely."));
                continue;
            }

            foreach (string discoveredPath in files)
            {
                if (!string.Equals(Path.GetFileName(discoveredPath), "blender.exe", StringComparison.OrdinalIgnoreCase)
                    || !TryCanonicalizeFile(discoveredPath, out string path))
                {
                    continue;
                }

                try
                {
                    if (IsReparse(_fileSystem.GetAttributes(path)))
                    {
                        detections.Add(Rejected(
                            path,
                            BlenderInstallationDiscoverySource.Portable,
                            BlenderInstallationDiscoveryStatus.ReparseRejected,
                            "BLENDER_REPARSE_REJECTED",
                            "The portable Blender candidate is a reparse point."));
                        continue;
                    }
                }
                catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
                {
                    detections.Add(Rejected(
                        path,
                        BlenderInstallationDiscoverySource.Portable,
                        BlenderInstallationDiscoveryStatus.VerificationFailed,
                        "BLENDER_CANDIDATE_UNREADABLE",
                        "The portable Blender candidate could not be inspected safely."));
                    continue;
                }

                if (!AddCandidate(candidates, path, BlenderInstallationDiscoverySource.Portable, failures))
                {
                    return;
                }
            }

            foreach (string directory in directories)
            {
                try
                {
                    if (IsReparse(_fileSystem.GetAttributes(directory)))
                    {
                        failures.Add(Failure(
                            "BLENDER_PORTABLE_REPARSE_SKIPPED",
                            "portableRoot",
                            "A reparse-point directory beneath the portable Blender root was not scanned."));
                        continue;
                    }
                }
                catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
                {
                    failures.Add(Failure(
                        "BLENDER_PORTABLE_DIRECTORY_UNREADABLE",
                        "portableRoot",
                        "A portable Blender directory could not be inspected safely."));
                    continue;
                }

                if (depth >= MaximumPortableDepth)
                {
                    if (!depthLimitReported)
                    {
                        failures.Add(Failure(
                            "BLENDER_PORTABLE_DEPTH_LIMIT",
                            "portableRoot",
                            "The bounded portable Blender scan depth was reached."));
                        depthLimitReported = true;
                    }

                    continue;
                }

                queue.Enqueue((directory, depth + 1));
            }
        }
    }

    private async Task<BlenderInstallationDetection> VerifyAsync(
        BlenderInstallationDiscoveryRequest request,
        string projectRoot,
        string toolsRoot,
        string path,
        BlenderInstallationDiscoverySource source,
        CancellationToken cancellationToken)
    {
        var processRequest = new ExternalProcessRequest(
            request.JobId,
            projectRoot,
            path,
            request.WorkingDirectory,
            request.TemporaryDirectory,
            request.CacheDirectory,
            request.LogDirectory,
            ["--version"],
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
                BlenderInstallationDiscoveryStatus.VerificationFailed,
                "BLENDER_VERSION_EXECUTION_FAILED",
                "The Blender version command could not be executed safely.");
        }

        ExternalProcessReceipt receipt = processResult.Value!;
        if (receipt.Completion.Kind != ProcessCompletionKind.Exited)
        {
            return Rejected(
                path,
                source,
                BlenderInstallationDiscoveryStatus.VerificationFailed,
                "BLENDER_VERSION_PROCESS_INCOMPLETE",
                "The Blender version command did not exit normally within its bounded policy.");
        }

        if (receipt.ExitCode != 0)
        {
            return Rejected(
                path,
                source,
                BlenderInstallationDiscoveryStatus.VerificationFailed,
                "BLENDER_VERSION_EXIT_CODE",
                "The Blender version command returned a nonzero exit code.");
        }

        if (receipt.StandardOutput.WasTruncated)
        {
            return Rejected(
                path,
                source,
                BlenderInstallationDiscoveryStatus.InvalidVersionOutput,
                "BLENDER_VERSION_OUTPUT_TRUNCATED",
                "The Blender version output exceeded the bounded verification capture.");
        }

        if (!TryParseVersion(receipt.StandardOutput.Text, out ToolVersion? version))
        {
            return Rejected(
                path,
                source,
                BlenderInstallationDiscoveryStatus.InvalidVersionOutput,
                "BLENDER_VERSION_OUTPUT_INVALID",
                "The Blender version output did not contain one canonical supported Blender version.");
        }

        ToolInstallation installation = ToolInstallation.Create(
            ToolKind.Blender,
            version,
            path,
            toolsRoot,
            isSelected: false).Value!;
        return new BlenderInstallationDetection(
            path,
            source,
            BlenderInstallationDiscoveryStatus.Verified,
            installation,
            diagnosticCode: null,
            diagnostic: null);
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

        const string Prefix = "Blender ";
        if (firstContentLine is null || !firstContentLine.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        ToolModelValidationResult<ToolVersion> result = ToolVersion.Create(
            ToolKind.Blender,
            firstContentLine[Prefix.Length..]);
        version = result.Value;
        return result.IsValid;
    }

    private static bool AddCandidate(
        Dictionary<string, BlenderInstallationDiscoverySource> candidates,
        string path,
        BlenderInstallationDiscoverySource source,
        List<BlenderInstallationDiscoveryFailure> failures)
    {
        if (candidates.TryGetValue(path, out BlenderInstallationDiscoverySource existing))
        {
            candidates[path] = existing | source;
            return true;
        }

        if (candidates.Count >= MaximumCandidateCount)
        {
            if (!failures.Any(static failure => failure.Code == "BLENDER_CANDIDATE_LIMIT"))
            {
                failures.Add(Failure(
                    "BLENDER_CANDIDATE_LIMIT",
                    "portableRoot",
                    "The bounded Blender executable candidate limit was exceeded."));
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

    private static bool TryCanonicalizeDirectory(string? value, out string path) =>
        TryCanonicalize(value, out path);

    private static bool TryCanonicalizeFile(string? value, out string path) =>
        TryCanonicalize(value, out path);

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

    private static bool IsStrictDescendant(string path, string root)
    {
        string prefix = root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReparse(FileAttributes attributes) =>
        (attributes & FileAttributes.ReparsePoint) != 0;

    private static bool IsExpectedFileSystemFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static BlenderInstallationDetection Rejected(
        string path,
        BlenderInstallationDiscoverySource source,
        BlenderInstallationDiscoveryStatus status,
        string code,
        string diagnostic) =>
        new(path, source, status, installation: null, code, diagnostic);

    private static BlenderInstallationDiscoveryFailure Failure(
        string code,
        string location,
        string diagnostic) =>
        new(code, location, diagnostic);
}
