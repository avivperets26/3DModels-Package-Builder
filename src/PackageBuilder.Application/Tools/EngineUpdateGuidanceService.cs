using System.Collections.ObjectModel;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Application.Tools;

public enum EngineUpdateAvailability
{
    UpToDate = 0,
    MissingInstallation = 1,
    NewerStableCandidate = 2,
    CatalogUnavailable = 3,
}

public sealed record EngineUpdateCheckRequest(
    string ProjectRoot,
    string ToolsRoot,
    string DownloadsRoot,
    string CacheRoot,
    IReadOnlyCollection<ToolInstallation> Installations,
    IReadOnlyCollection<ToolVersionApproval> Approvals,
    bool UserInitiatedNetworkRefresh = false);

public sealed record EngineUpdateStatus(
    ToolKind Tool,
    EngineUpdateAvailability Availability,
    ToolVersion? LatestStableVersion,
    ToolVersion? NewestContainedInstalledVersion,
    ToolVersionApprovalState? LatestVersionApprovalState,
    ReleaseCatalogOrigin? CatalogOrigin,
    string? RefreshFailureCode,
    string? ErrorCode);

public sealed record EngineUpdateCheckResult(
    IReadOnlyList<EngineUpdateStatus> Engines,
    string? ErrorCode,
    string? ErrorMessage)
{
    public bool IsSuccess => ErrorCode is null;
}

public sealed record EngineInstallationGuidance(
    ToolKind Tool,
    ToolVersion Version,
    string ContainedDestination,
    Uri OfficialInstallationGuide,
    Uri OfficialLicenceTerms,
    string LicenceNotice,
    IReadOnlyList<string> UserSteps,
    bool DownloadsAutomatically,
    bool RunsInstaller,
    bool AcceptsLicence,
    bool AssumesPaidTier);

public sealed record EngineGuidanceLaunchRequest(
    ToolKind Tool,
    ToolVersion Version,
    string ProjectRoot,
    string ToolsRoot,
    bool UserConfirmedExternalNavigation);

public sealed record EngineGuidanceLaunchResult(
    EngineInstallationGuidance? Guidance,
    bool WasLaunched,
    string? ErrorCode,
    string? ErrorMessage)
{
    public bool IsSuccess => ErrorCode is null;
}

/// <summary>Checks official stable releases and creates non-installing, contained guidance.</summary>
public sealed class EngineUpdateGuidanceService(
    IOfficialReleaseCatalogProvider catalogProvider,
    IEngineInstallationGuidanceLauncher guidanceLauncher)
{
    private static readonly ToolKind[] _engines = [ToolKind.Blender, ToolKind.Unity, ToolKind.Unreal];
    private static readonly ReadOnlyDictionary<ToolKind, GuidanceDefinition> _guidance =
        new(new Dictionary<ToolKind, GuidanceDefinition>
        {
            [ToolKind.Blender] = new(
                new Uri("https://www.blender.org/download/"),
                new Uri("https://www.blender.org/about/license/"),
                "Blender is distributed under the GNU GPL. Review the official licence before downloading or redistributing it."),
            [ToolKind.Unity] = new(
                new Uri("https://docs.unity3d.com/Manual/GettingStartedInstallingUnity.html"),
                new Uri("https://unity.com/legal/editor-terms-of-service/software"),
                "Unity use is subject to current terms, plan eligibility, authorized-user, and seat conditions. Package Builder cannot determine eligibility."),
            [ToolKind.Unreal] = new(
                new Uri("https://dev.epicgames.com/documentation/en-us/unreal-engine/install-unreal-engine"),
                new Uri("https://www.unrealengine.com/eula/unreal"),
                "Unreal Engine use is subject to the current EULA, eligibility, seat-subscription, and royalty conditions. Package Builder cannot determine eligibility."),
        });

    private readonly IOfficialReleaseCatalogProvider _catalogProvider =
        catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
    private readonly IEngineInstallationGuidanceLauncher _guidanceLauncher =
        guidanceLauncher ?? throw new ArgumentNullException(nameof(guidanceLauncher));

    public async Task<EngineUpdateCheckResult> CheckAsync(
        EngineUpdateCheckRequest? request,
        CancellationToken cancellationToken = default)
    {
        string? validationCode = ValidateCheckRequest(request);
        if (validationCode is not null)
        {
            return CheckFailure(validationCode, "Engine update inputs must be canonical, contained, unique, and tool-matched.");
        }

        EngineUpdateCheckRequest valid = request!;
        var statuses = new List<EngineUpdateStatus>(_engines.Length);
        foreach (ToolKind tool in _engines)
        {
            ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> catalog =
                await _catalogProvider.GetAsync(
                    new OfficialReleaseCatalogRequest(
                        tool,
                        valid.ProjectRoot,
                        valid.DownloadsRoot,
                        valid.CacheRoot,
                        AllowNetworkRefresh: valid.UserInitiatedNetworkRefresh),
                    cancellationToken).ConfigureAwait(false);
            statuses.Add(CreateStatus(tool, valid, catalog));
        }

        return new(statuses.AsReadOnly(), null, null);
    }

    public static EngineGuidanceLaunchResult CreateGuidance(EngineGuidanceLaunchRequest? request)
    {
        if (!TryValidateGuidanceRequest(request, out string destination))
        {
            return LaunchFailure("ENGINE_GUIDANCE_INPUT_INVALID", "Guidance requires a stable engine version and a canonical destination beneath the project tools root.");
        }

        GuidanceDefinition definition = _guidance[request!.Tool];
        string toolDirectory = request.Tool.ToString().ToLowerInvariant();
        var guidance = new EngineInstallationGuidance(
            request.Tool,
            request.Version,
            destination,
            definition.InstallationGuide,
            definition.LicenceTerms,
            definition.LicenceNotice,
            Array.AsReadOnly(
            [
                "Review the official licence, plan, seat, eligibility, and royalty conditions that apply to you.",
                $"In the vendor installer or launcher, explicitly choose: {Path.Combine("tools", toolDirectory, request.Version.Value)}",
                "Select only the modules required by your target workflow and review the download size before continuing.",
                "Complete all vendor prompts yourself, then run Package Builder discovery again to verify the contained installation.",
            ]),
            DownloadsAutomatically: false,
            RunsInstaller: false,
            AcceptsLicence: false,
            AssumesPaidTier: false);
        return new(guidance, WasLaunched: false, null, null);
    }

    public async Task<EngineGuidanceLaunchResult> LaunchGuidanceAsync(
        EngineGuidanceLaunchRequest? request,
        CancellationToken cancellationToken = default)
    {
        EngineGuidanceLaunchResult created = CreateGuidance(request);
        if (!created.IsSuccess)
        {
            return created;
        }

        if (!request!.UserConfirmedExternalNavigation)
        {
            return LaunchFailure("ENGINE_GUIDANCE_CONFIRMATION_REQUIRED", "Opening official vendor guidance requires explicit user confirmation.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return LaunchFailure("ENGINE_GUIDANCE_CANCELLED", "Opening official vendor guidance was cancelled.");
        }

        bool opened;
        try
        {
            opened = await _guidanceLauncher.OpenAsync(
                created.Guidance!.OfficialInstallationGuide,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return LaunchFailure("ENGINE_GUIDANCE_CANCELLED", "Opening official vendor guidance was cancelled.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            return LaunchFailure("ENGINE_GUIDANCE_LAUNCH_FAILED", "Official vendor guidance could not be opened.");
        }

        return opened
            ? created with { WasLaunched = true }
            : LaunchFailure("ENGINE_GUIDANCE_LAUNCH_FAILED", "Official vendor guidance could not be opened.");
    }

    private static EngineUpdateStatus CreateStatus(
        ToolKind tool,
        EngineUpdateCheckRequest request,
        ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> catalog)
    {
        ToolVersion? installed = request.Installations
            .Where(value => value.Tool == tool && value.CanBeSelected)
            .Select(value => value.Version)
            .OrderByDescending(static value => value)
            .FirstOrDefault();
        if (!catalog.IsSuccess)
        {
            return new(tool, EngineUpdateAvailability.CatalogUnavailable, null, installed, null, null, null, catalog.Error!.Code);
        }

        OfficialReleaseCatalogSnapshot snapshot = catalog.Value!;
        ToolVersion? latest = snapshot.Catalog.Releases
            .Where(static release => release.Version.Channel == ToolReleaseChannel.Stable)
            .Select(static release => release.Version)
            .OrderByDescending(static version => version)
            .FirstOrDefault();
        if (latest is null)
        {
            return new(tool, EngineUpdateAvailability.CatalogUnavailable, null, installed, null, snapshot.Origin, snapshot.RefreshFailureCode, "RELEASE_CATALOG_NO_STABLE");
        }
        ToolVersionApprovalState? approval = request.Approvals
            .Where(value => value.Version.Tool == tool && value.Version == latest)
            .Select(static value => (ToolVersionApprovalState?)value.State)
            .SingleOrDefault();
        EngineUpdateAvailability availability = installed is null
            ? EngineUpdateAvailability.MissingInstallation
            : latest > installed
                ? EngineUpdateAvailability.NewerStableCandidate
                : EngineUpdateAvailability.UpToDate;
        return new(
            tool,
            availability,
            latest,
            installed,
            approval,
            snapshot.Origin,
            snapshot.RefreshFailureCode,
            null);
    }

    private static string? ValidateCheckRequest(EngineUpdateCheckRequest? request)
    {
        if (request is null
            || !TryValidateRoots(request.ProjectRoot, request.ToolsRoot, out _)
            || !TryValidateNamedContainedRoot(request.ProjectRoot, request.DownloadsRoot, "downloads")
            || !TryValidateNamedContainedRoot(request.ProjectRoot, request.CacheRoot, "cache")
            || request.Installations is null
            || request.Approvals is null)
        {
            return "ENGINE_UPDATE_INPUT_INVALID";
        }

        var installations = new HashSet<(ToolKind Tool, string Version)>();
        foreach (ToolInstallation? installation in request.Installations)
        {
            if (installation is null
                || installation.Tool is ToolKind.DotNet
                || !installations.Add((installation.Tool, installation.Version.Value)))
            {
                return "ENGINE_UPDATE_INSTALLATION_INVALID";
            }
        }

        var approvals = new HashSet<(ToolKind Tool, string Version)>();
        foreach (ToolVersionApproval? approval in request.Approvals)
        {
            if (approval?.Version is null
                || approval.Version.Tool is ToolKind.DotNet
                || !Enum.IsDefined(approval.State)
                || !approvals.Add((approval.Version.Tool, approval.Version.Value)))
            {
                return "ENGINE_UPDATE_APPROVAL_INVALID";
            }
        }

        return null;
    }

    private static bool TryValidateGuidanceRequest(
        EngineGuidanceLaunchRequest? request,
        out string destination)
    {
        destination = string.Empty;
        if (request?.Version is null
            || request.Tool is ToolKind.DotNet
            || !_guidance.ContainsKey(request.Tool)
            || request.Version.Tool != request.Tool
            || request.Version.Channel != ToolReleaseChannel.Stable
            || !TryValidateRoots(request.ProjectRoot, request.ToolsRoot, out string toolsRoot))
        {
            return false;
        }

        destination = Path.Combine(
            toolsRoot,
            request.Tool.ToString().ToLowerInvariant(),
            request.Version.Value);
        return IsStrictDescendant(destination, toolsRoot);
    }

    private static bool TryValidateRoots(string? projectValue, string? toolsValue, out string toolsRoot)
    {
        toolsRoot = string.Empty;
        return TryCanonicalize(projectValue, out string projectRoot)
            && TryCanonicalize(toolsValue, out toolsRoot)
            && IsStrictDescendant(toolsRoot, projectRoot)
            && string.Equals(Path.GetFileName(toolsRoot), "tools", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryValidateNamedContainedRoot(
        string? projectValue,
        string? candidateValue,
        string expectedName) =>
        TryCanonicalize(projectValue, out string projectRoot)
        && TryCanonicalize(candidateValue, out string candidateRoot)
        && IsStrictDescendant(candidateRoot, projectRoot)
        && string.Equals(Path.GetFileName(candidateRoot), expectedName, StringComparison.OrdinalIgnoreCase);

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
            return false;
        }
    }

    private static bool IsStrictDescendant(string candidate, string root) =>
        candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static EngineUpdateCheckResult CheckFailure(string code, string message) =>
        new([], code, message);

    private static EngineGuidanceLaunchResult LaunchFailure(string code, string message) =>
        new(null, WasLaunched: false, code, message);

    private sealed record GuidanceDefinition(
        Uri InstallationGuide,
        Uri LicenceTerms,
        string LicenceNotice);
}
