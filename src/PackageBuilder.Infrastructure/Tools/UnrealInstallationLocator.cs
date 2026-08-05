using System.Text.Json;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Infrastructure.Tools;

/// <summary>Discovers Unreal Engine roots and verifies contained editor layouts through build metadata.</summary>
public sealed class UnrealInstallationLocator(IUnrealDiscoveryFileSystem fileSystem)
    : IUnrealInstallationLocator
{
    internal const int MaximumLauncherManifestCount = 32;
    internal const int MaximumLauncherManifestBytes = 1_048_576;
    internal const int MaximumLauncherEntryCount = 512;
    internal const int MaximumContainedDirectoryCount = 256;
    internal const int MaximumCandidateCount = 256;
    internal const int MaximumBuildVersionBytes = 65_536;
    internal const int MaximumJsonDepth = 16;

    private const string EditorRelativePath = @"Engine\Binaries\Win64\UnrealEditor-Cmd.exe";
    private const string BuildVersionRelativePath = @"Engine\Build\Build.version";

    private readonly IUnrealDiscoveryFileSystem _fileSystem = fileSystem
        ?? throw new ArgumentNullException(nameof(fileSystem));

    public UnrealInstallationLocator()
        : this(new PhysicalUnrealDiscoveryFileSystem())
    {
    }

    public Task<UnrealInstallationDiscoveryReport> DiscoverAsync(
        UnrealInstallationDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var detections = new List<UnrealInstallationDetection>();
        var failures = new List<UnrealInstallationDiscoveryFailure>();
        if (!TryValidateRequest(request, failures, out string projectRoot, out string toolsRoot))
        {
            return Task.FromResult(new UnrealInstallationDiscoveryReport(detections, failures));
        }

        var candidates = new Dictionary<string, UnrealInstallationDiscoverySource>(
            StringComparer.OrdinalIgnoreCase);
        DiscoverExplicitRoots(
            request.ConfiguredInstallationRoots,
            UnrealInstallationDiscoverySource.Configured,
            "configuredInstallationRoots",
            candidates,
            failures);
        DiscoverExplicitRoots(
            request.SourceBuildRoots,
            UnrealInstallationDiscoverySource.SourceBuild,
            "sourceBuildRoots",
            candidates,
            failures);
        DiscoverExplicitRoots(
            request.RegisteredInstallationRoots,
            UnrealInstallationDiscoverySource.Registered,
            "registeredInstallationRoots",
            candidates,
            failures);
        DiscoverExplicitRoots(
            request.StandardInstallationRoots,
            UnrealInstallationDiscoverySource.Standard,
            "standardInstallationRoots",
            candidates,
            failures);
        DiscoverLauncherManifestCandidates(request, projectRoot, candidates, failures);
        DiscoverContainedCandidates(toolsRoot, candidates, failures);

        foreach ((string root, UnrealInstallationDiscoverySource source) in candidates
                     .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                failures.Add(Failure(
                    "UNREAL_DISCOVERY_CANCELLED",
                    "$",
                    "Unreal discovery was cancelled before every candidate was inspected."));
                break;
            }

            detections.Add(InspectCandidate(root, source, toolsRoot));
        }

        return Task.FromResult(new UnrealInstallationDiscoveryReport(
            detections.OrderBy(static value => value.InstallationRoot, StringComparer.OrdinalIgnoreCase),
            failures));
    }

    private bool TryValidateRequest(
        UnrealInstallationDiscoveryRequest request,
        List<UnrealInstallationDiscoveryFailure> failures,
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
                "UNREAL_DISCOVERY_ROOT_INVALID",
                "approvedToolsRoot",
                "Discovery requires canonical project and tools roots with tools strictly beneath the project root."));
            return false;
        }

        try
        {
            if (!_fileSystem.DirectoryExists(projectRoot) || !_fileSystem.DirectoryExists(toolsRoot))
            {
                failures.Add(Failure(
                    "UNREAL_DISCOVERY_ROOT_MISSING",
                    "approvedToolsRoot",
                    "The canonical project and tools roots must exist before discovery."));
                return false;
            }

            if (IsReparse(_fileSystem.GetAttributes(projectRoot))
                || HasReparseBoundary(projectRoot, toolsRoot))
            {
                failures.Add(Failure(
                    "UNREAL_DISCOVERY_ROOT_REPARSE",
                    "approvedToolsRoot",
                    "Discovery roots cannot contain a reparse-point boundary."));
                return false;
            }
        }
        catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
        {
            failures.Add(Failure(
                "UNREAL_DISCOVERY_ROOT_UNREADABLE",
                "approvedToolsRoot",
                "The discovery roots could not be inspected safely."));
            return false;
        }

        return true;
    }

    private static void DiscoverExplicitRoots(
        IReadOnlyList<string> values,
        UnrealInstallationDiscoverySource source,
        string propertyName,
        Dictionary<string, UnrealInstallationDiscoverySource> candidates,
        List<UnrealInstallationDiscoveryFailure> failures)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (!TryCanonicalize(values[index], out string root))
            {
                failures.Add(Failure(
                    "UNREAL_INSTALLATION_ROOT_INVALID",
                    $"{propertyName}[{index}]",
                    "An Unreal installation root must be a canonical fully qualified Windows directory path."));
                continue;
            }

            if (!AddCandidate(candidates, root, source, failures, propertyName))
            {
                return;
            }
        }
    }

    private void DiscoverLauncherManifestCandidates(
        UnrealInstallationDiscoveryRequest request,
        string projectRoot,
        Dictionary<string, UnrealInstallationDiscoverySource> candidates,
        List<UnrealInstallationDiscoveryFailure> failures)
    {
        var uniqueManifests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int acceptedManifestCount = 0;
        int launcherEntryCount = 0;
        for (int index = 0; index < request.LauncherManifestPaths.Count; index++)
        {
            string location = $"launcherManifestPaths[{index}]";
            if (!TryCanonicalize(request.LauncherManifestPaths[index], out string manifestPath))
            {
                failures.Add(Failure(
                    "UNREAL_LAUNCHER_MANIFEST_PATH_INVALID",
                    location,
                    "A launcher manifest path must be a canonical fully qualified Windows file path."));
                continue;
            }

            if (!uniqueManifests.Add(manifestPath))
            {
                continue;
            }

            acceptedManifestCount++;
            if (acceptedManifestCount > MaximumLauncherManifestCount)
            {
                failures.Add(Failure(
                    "UNREAL_LAUNCHER_MANIFEST_LIMIT",
                    "launcherManifestPaths",
                    "The bounded launcher-manifest limit was exceeded."));
                return;
            }

            try
            {
                if (!_fileSystem.FileExists(manifestPath))
                {
                    failures.Add(Failure(
                        "UNREAL_LAUNCHER_MANIFEST_MISSING",
                        location,
                        "A configured launcher manifest does not exist."));
                    continue;
                }

                if (!IsStrictDescendant(manifestPath, projectRoot)
                    || HasReparseBoundary(projectRoot, manifestPath))
                {
                    failures.Add(Failure(
                        "UNREAL_LAUNCHER_MANIFEST_UNSAFE",
                        location,
                        "Launcher manifests must be contained beneath the project root without reparse boundaries."));
                    continue;
                }

                using Stream stream = _fileSystem.OpenRead(manifestPath);
                byte[] bytes = ReadBounded(stream, MaximumLauncherManifestBytes, out bool tooLarge);
                if (tooLarge)
                {
                    failures.Add(Failure(
                        "UNREAL_LAUNCHER_MANIFEST_TOO_LARGE",
                        location,
                        "The launcher manifest exceeds the bounded input size."));
                    continue;
                }

                using var document = JsonDocument.Parse(
                    bytes,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = MaximumJsonDepth,
                    });
                if (!HasUniqueProperties(document.RootElement)
                    || document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("InstallationList", out JsonElement installations)
                    || installations.ValueKind != JsonValueKind.Array)
                {
                    failures.Add(Failure(
                        "UNREAL_LAUNCHER_MANIFEST_INVALID",
                        location,
                        "The launcher manifest must contain one unambiguous InstallationList array."));
                    continue;
                }

                int entryIndex = 0;
                foreach (JsonElement entry in installations.EnumerateArray())
                {
                    launcherEntryCount++;
                    if (launcherEntryCount > MaximumLauncherEntryCount)
                    {
                        failures.Add(Failure(
                            "UNREAL_LAUNCHER_ENTRY_LIMIT",
                            "launcherManifestPaths",
                            "The bounded launcher installation-entry limit was exceeded."));
                        return;
                    }

                    string entryLocation = $"{location}.InstallationList[{entryIndex}]";
                    entryIndex++;
                    if (!IsUnrealLauncherEntry(entry))
                    {
                        continue;
                    }

                    if (!entry.TryGetProperty("InstallLocation", out JsonElement installLocation)
                        || installLocation.ValueKind != JsonValueKind.String
                        || !TryCanonicalize(installLocation.GetString(), out string root))
                    {
                        failures.Add(Failure(
                            "UNREAL_LAUNCHER_ENTRY_INVALID",
                            entryLocation,
                            "An Unreal launcher entry requires a canonical InstallLocation."));
                        continue;
                    }

                    if (!AddCandidate(
                            candidates,
                            root,
                            UnrealInstallationDiscoverySource.LauncherManifest,
                            failures,
                            "launcherManifestPaths"))
                    {
                        return;
                    }
                }
            }
            catch (JsonException)
            {
                failures.Add(Failure(
                    "UNREAL_LAUNCHER_MANIFEST_INVALID",
                    location,
                    "The launcher manifest is not valid bounded JSON."));
            }
            catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
            {
                failures.Add(Failure(
                    "UNREAL_LAUNCHER_MANIFEST_UNREADABLE",
                    location,
                    "The launcher manifest could not be inspected safely."));
            }
        }
    }

    private void DiscoverContainedCandidates(
        string toolsRoot,
        Dictionary<string, UnrealInstallationDiscoverySource> candidates,
        List<UnrealInstallationDiscoveryFailure> failures)
    {
        string containedRoot = Path.Combine(toolsRoot, "unreal");
        try
        {
            if (!_fileSystem.DirectoryExists(containedRoot))
            {
                return;
            }

            if (HasReparseBoundary(toolsRoot, containedRoot))
            {
                failures.Add(Failure(
                    "UNREAL_CONTAINED_ROOT_REPARSE",
                    "approvedToolsRoot",
                    "The conventional contained Unreal root crosses a reparse-point boundary."));
                return;
            }

            if (_fileSystem.DirectoryExists(Path.Combine(containedRoot, "Engine")))
            {
                _ = AddCandidate(
                    candidates,
                    containedRoot,
                    UnrealInstallationDiscoverySource.ContainedRoot,
                    failures,
                    "approvedToolsRoot");
                return;
            }

            string[] directories =
            [
                .. _fileSystem.EnumerateDirectories(containedRoot)
                    .Take(MaximumContainedDirectoryCount + 1),
            ];
            if (directories.Length > MaximumContainedDirectoryCount)
            {
                failures.Add(Failure(
                    "UNREAL_CONTAINED_DIRECTORY_LIMIT",
                    "approvedToolsRoot",
                    "The bounded contained Unreal directory limit was exceeded."));
                return;
            }

            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
            foreach (string discoveredDirectory in directories)
            {
                if (!TryCanonicalize(discoveredDirectory, out string directory)
                    || !IsStrictDescendant(directory, containedRoot))
                {
                    failures.Add(Failure(
                        "UNREAL_CONTAINED_DIRECTORY_INVALID",
                        "approvedToolsRoot",
                        "A contained Unreal directory was not a canonical direct child of its root."));
                    continue;
                }

                if (!AddCandidate(
                        candidates,
                        directory,
                        UnrealInstallationDiscoverySource.ContainedRoot,
                        failures,
                        "approvedToolsRoot"))
                {
                    return;
                }
            }
        }
        catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
        {
            failures.Add(Failure(
                "UNREAL_CONTAINED_ROOT_UNREADABLE",
                "approvedToolsRoot",
                "The conventional contained Unreal root could not be inspected safely."));
        }
    }

    private UnrealInstallationDetection InspectCandidate(
        string root,
        UnrealInstallationDiscoverySource source,
        string toolsRoot)
    {
        string executablePath = Path.Combine(root, EditorRelativePath);
        string buildVersionPath = Path.Combine(root, BuildVersionRelativePath);
        try
        {
            if (!_fileSystem.DirectoryExists(root))
            {
                return Rejected(
                    root,
                    executablePath,
                    buildVersionPath,
                    source,
                    UnrealInstallationDiscoveryStatus.Missing,
                    "UNREAL_INSTALLATION_MISSING",
                    "The Unreal installation root does not exist.");
            }

            if (!IsStrictDescendant(root, toolsRoot))
            {
                return Rejected(
                    root,
                    executablePath,
                    buildVersionPath,
                    source,
                    UnrealInstallationDiscoveryStatus.ExternalInformational,
                    "UNREAL_EXTERNAL_INFORMATIONAL",
                    "The external Unreal detection is informational and cannot be selected or executed.");
            }

            if (HasReparseBoundary(toolsRoot, root))
            {
                return Rejected(
                    root,
                    executablePath,
                    buildVersionPath,
                    source,
                    UnrealInstallationDiscoveryStatus.ReparseRejected,
                    "UNREAL_REPARSE_REJECTED",
                    "The Unreal installation root crosses a reparse-point boundary.");
            }

            if (!_fileSystem.DirectoryExists(Path.Combine(root, "Engine"))
                || !_fileSystem.FileExists(executablePath)
                || !_fileSystem.FileExists(buildVersionPath))
            {
                return Rejected(
                    root,
                    executablePath,
                    buildVersionPath,
                    source,
                    UnrealInstallationDiscoveryStatus.InvalidLayout,
                    "UNREAL_ENGINE_LAYOUT_INVALID",
                    "An Unreal installation requires Engine, UnrealEditor-Cmd.exe, and Engine\\Build\\Build.version.");
            }

            if (HasReparseBoundary(root, executablePath) || HasReparseBoundary(root, buildVersionPath))
            {
                return Rejected(
                    root,
                    executablePath,
                    buildVersionPath,
                    source,
                    UnrealInstallationDiscoveryStatus.ReparseRejected,
                    "UNREAL_REPARSE_REJECTED",
                    "The Unreal editor or build-version metadata crosses a reparse-point boundary.");
            }

            using Stream stream = _fileSystem.OpenRead(buildVersionPath);
            byte[] bytes = ReadBounded(stream, MaximumBuildVersionBytes, out bool tooLarge);
            if (tooLarge || !TryParseBuildVersion(bytes, out ToolVersion? version))
            {
                return Rejected(
                    root,
                    executablePath,
                    buildVersionPath,
                    source,
                    UnrealInstallationDiscoveryStatus.InvalidBuildVersion,
                    tooLarge ? "UNREAL_BUILD_VERSION_TOO_LARGE" : "UNREAL_BUILD_VERSION_INVALID",
                    tooLarge
                        ? "The Unreal build-version metadata exceeds the bounded input size."
                        : "The Unreal build-version metadata is not canonical supported JSON.");
            }

            ToolInstallation installation = ToolInstallation.Create(
                ToolKind.Unreal,
                version,
                executablePath,
                toolsRoot,
                isSelected: false).Value!;
            return new UnrealInstallationDetection(
                root,
                executablePath,
                buildVersionPath,
                source,
                UnrealInstallationDiscoveryStatus.Verified,
                installation,
                diagnosticCode: null,
                diagnostic: null);
        }
        catch (JsonException)
        {
            return Rejected(
                root,
                executablePath,
                buildVersionPath,
                source,
                UnrealInstallationDiscoveryStatus.InvalidBuildVersion,
                "UNREAL_BUILD_VERSION_INVALID",
                "The Unreal build-version metadata is not valid bounded JSON.");
        }
        catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
        {
            return Rejected(
                root,
                executablePath,
                buildVersionPath,
                source,
                UnrealInstallationDiscoveryStatus.Unreadable,
                "UNREAL_INSTALLATION_UNREADABLE",
                "The Unreal installation could not be inspected safely.");
        }
    }

    private static bool TryParseBuildVersion(byte[] bytes, out ToolVersion? version)
    {
        version = null;
        using var document = JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumJsonDepth,
            });
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !HasUniqueProperties(root)
            || !TryGetNonnegativeInt32(root, "MajorVersion", out int major)
            || !TryGetNonnegativeInt32(root, "MinorVersion", out int minor)
            || !TryGetNonnegativeInt32(root, "PatchVersion", out int patch))
        {
            return false;
        }

        ToolModelValidationResult<ToolVersion> result = ToolVersion.Create(
            ToolKind.Unreal,
            $"{major}.{minor}.{patch}");
        version = result.Value;
        return result.IsValid;
    }

    private static bool TryGetNonnegativeInt32(
        JsonElement value,
        string propertyName,
        out int result)
    {
        result = 0;
        return value.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out result)
            && result >= 0;
    }

    private static bool IsUnrealLauncherEntry(JsonElement entry) => entry.ValueKind == JsonValueKind.Object && (HasUnrealPrefix(entry, "AppName") || HasUnrealPrefix(entry, "ArtifactId"));

    private static bool HasUnrealPrefix(JsonElement entry, string propertyName) =>
        entry.TryGetProperty(propertyName, out JsonElement property)
        && property.ValueKind == JsonValueKind.String
        && property.GetString() is { } value
        && value.StartsWith("UE_", StringComparison.OrdinalIgnoreCase);

    private static bool HasUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name) || !HasUniqueProperties(property.Value))
                {
                    return false;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement element in value.EnumerateArray())
            {
                if (!HasUniqueProperties(element))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static byte[] ReadBounded(Stream stream, int maximumBytes, out bool tooLarge)
    {
        using var destination = new MemoryStream(Math.Min(maximumBytes, 81_920));
        byte[] buffer = new byte[81_920];
        int remaining = maximumBytes + 1;
        while (remaining > 0)
        {
            int read = stream.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                break;
            }

            destination.Write(buffer, 0, read);
            remaining -= read;
        }

        tooLarge = destination.Length > maximumBytes;
        return destination.ToArray();
    }

    private static bool AddCandidate(
        Dictionary<string, UnrealInstallationDiscoverySource> candidates,
        string root,
        UnrealInstallationDiscoverySource source,
        List<UnrealInstallationDiscoveryFailure> failures,
        string location)
    {
        if (candidates.TryGetValue(root, out UnrealInstallationDiscoverySource existing))
        {
            candidates[root] = existing | source;
            return true;
        }

        if (candidates.Count >= MaximumCandidateCount)
        {
            if (!failures.Any(static failure => failure.Code == "UNREAL_CANDIDATE_LIMIT"))
            {
                failures.Add(Failure(
                    "UNREAL_CANDIDATE_LIMIT",
                    location,
                    "The bounded Unreal installation candidate limit was exceeded."));
            }

            return false;
        }

        candidates.Add(root, source);
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

    private static UnrealInstallationDetection Rejected(
        string root,
        string executablePath,
        string buildVersionPath,
        UnrealInstallationDiscoverySource source,
        UnrealInstallationDiscoveryStatus status,
        string code,
        string diagnostic) =>
        new(root, executablePath, buildVersionPath, source, status, installation: null, code, diagnostic);

    private static UnrealInstallationDiscoveryFailure Failure(
        string code,
        string location,
        string diagnostic) =>
        new(code, location, diagnostic);
}
