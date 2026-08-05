using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Application.Tools;

/// <summary>Approval evidence and installed modules for one canonical tool version.</summary>
public sealed record ToolVersionApproval(
    ToolVersion Version,
    ToolVersionApprovalState State,
    IReadOnlyCollection<string> AvailableModules);

/// <summary>Marketplace-owned engine bounds applied without coupling core policy to one adapter.</summary>
public sealed record MarketplaceToolVersionConstraint(
    ToolKind Tool,
    ToolVersion? MinimumVersionInclusive = null,
    ToolVersion? MaximumVersionInclusive = null,
    bool RequireLongTermSupport = false);

/// <summary>All deterministic inputs required to select a production tool version.</summary>
public sealed record LatestApprovedStableSelectionRequest(
    OfficialReleaseCatalog Catalog,
    IReadOnlyCollection<ToolVersionApproval> Approvals,
    IReadOnlyCollection<string> RequiredModules,
    MarketplaceToolVersionConstraint? MarketplaceConstraint = null);

/// <summary>Explains whether selection used the newest approval or its retained fallback.</summary>
public enum ToolVersionSelectionOrigin
{
    ApprovedLatest = 0,
    LastKnownGood = 1,
}

/// <summary>An exact selected release plus the approval state that authorized it.</summary>
public sealed record LatestApprovedStableSelection(
    OfficialToolRelease Release,
    ToolVersionSelectionOrigin Origin);

/// <summary>Sanitized expected failure from validating or evaluating selection inputs.</summary>
public sealed record ToolVersionSelectionError(string Code, string Message);

/// <summary>Non-throwing result for expected policy failures.</summary>
public sealed record ToolVersionSelectionResult(
    LatestApprovedStableSelection? Selection,
    ToolVersionSelectionError? Error)
{
    public bool IsSuccess => Error is null;
}

/// <summary>Selects the newest compatible approved stable version, then Last Known Good.</summary>
public static class LatestApprovedStableSelectionPolicy
{
    public static ToolVersionSelectionResult Select(LatestApprovedStableSelectionRequest? request)
    {
        ToolVersionSelectionError? validationError = Validate(request);
        if (validationError is not null)
        {
            return Failure(validationError.Code, validationError.Message);
        }

        LatestApprovedStableSelectionRequest validRequest = request!;
        var releasesByVersion = validRequest.Catalog.Releases
            .Where(static release => release.Version.Channel == ToolReleaseChannel.Stable)
            .Where(release => MarketplaceAllows(release, validRequest.MarketplaceConstraint))
            .ToDictionary(static release => release.Version.Value, StringComparer.Ordinal);

        ToolVersionApproval? approved = FindNewestEligibleApproval(
            validRequest,
            releasesByVersion,
            ToolVersionApprovalState.ApprovedLatest);
        if (approved is not null)
        {
            return Success(releasesByVersion[approved.Version.Value], ToolVersionSelectionOrigin.ApprovedLatest);
        }

        ToolVersionApproval? fallback = FindNewestEligibleApproval(
            validRequest,
            releasesByVersion,
            ToolVersionApprovalState.LastKnownGood);
        return fallback is not null
            ? Success(releasesByVersion[fallback.Version.Value], ToolVersionSelectionOrigin.LastKnownGood)
            : Failure(
                "TOOL_VERSION_NO_APPROVED_STABLE",
                "No approved stable tool version satisfies the required modules and marketplace constraints.");
    }

    private static ToolVersionSelectionError? Validate(LatestApprovedStableSelectionRequest? request)
    {
        if (request?.Catalog is null || request.Approvals is null || request.RequiredModules is null)
        {
            return Error("TOOL_VERSION_SELECTION_INPUT_INVALID", "The catalog, approvals, and required modules are required.");
        }

        MarketplaceToolVersionConstraint? constraint = request.MarketplaceConstraint;
        if (constraint is not null
            && (!Enum.IsDefined(constraint.Tool)
                || constraint.Tool != request.Catalog.Tool
                || constraint.MinimumVersionInclusive is { } minimum && minimum.Tool != constraint.Tool
                || constraint.MaximumVersionInclusive is { } maximum && maximum.Tool != constraint.Tool
                || constraint.MinimumVersionInclusive is { } lower
                    && constraint.MaximumVersionInclusive is { } upper
                    && lower > upper))
        {
            return Error("TOOL_VERSION_MARKETPLACE_CONSTRAINT_INVALID", "Marketplace version constraints must match the catalog tool and form a valid inclusive range.");
        }

        if (!TryNormalizeModules(request.RequiredModules, out _))
        {
            return Error("TOOL_VERSION_REQUIRED_MODULE_INVALID", "Required module identifiers must be unique non-empty trimmed ordinal values.");
        }

        var versions = new HashSet<string>(StringComparer.Ordinal);
        foreach (ToolVersionApproval? approval in request.Approvals)
        {
            if (approval?.Version is null
                || approval.Version.Tool != request.Catalog.Tool
                || !Enum.IsDefined(approval.State)
                || approval.AvailableModules is null
                || !TryNormalizeModules(approval.AvailableModules, out _)
                || !versions.Add(approval.Version.Value))
            {
                return Error("TOOL_VERSION_APPROVAL_INVALID", "Approvals must be unique, tool-matched, valid lifecycle records with valid module identifiers.");
            }
        }

        return null;
    }

    private static ToolVersionApproval? FindNewestEligibleApproval(
        LatestApprovedStableSelectionRequest request,
        Dictionary<string, OfficialToolRelease> releasesByVersion,
        ToolVersionApprovalState state)
    {
        _ = TryNormalizeModules(request.RequiredModules, out HashSet<string> requiredModules);
        return request.Approvals
            .Where(approval => approval.State == state && releasesByVersion.ContainsKey(approval.Version.Value))
            .Where(approval => requiredModules.IsSubsetOf(approval.AvailableModules))
            .OrderByDescending(static approval => approval.Version)
            .FirstOrDefault();
    }

    private static bool MarketplaceAllows(
        OfficialToolRelease release,
        MarketplaceToolVersionConstraint? constraint) =>
        constraint is null
        || (!constraint.RequireLongTermSupport || release.Support == ToolReleaseSupport.LongTermSupport)
        && (constraint.MinimumVersionInclusive is null || release.Version >= constraint.MinimumVersionInclusive)
        && (constraint.MaximumVersionInclusive is null || release.Version <= constraint.MaximumVersionInclusive);

    private static bool TryNormalizeModules(
        IEnumerable<string?> modules,
        out HashSet<string> normalized)
    {
        normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? module in modules)
        {
            if (string.IsNullOrWhiteSpace(module)
                || !string.Equals(module, module.Trim(), StringComparison.Ordinal)
                || !normalized.Add(module))
            {
                return false;
            }
        }

        return true;
    }

    private static ToolVersionSelectionResult Success(
        OfficialToolRelease release,
        ToolVersionSelectionOrigin origin) => new(new LatestApprovedStableSelection(release, origin), null);

    private static ToolVersionSelectionResult Failure(string code, string message) =>
        new(null, Error(code, message));

    private static ToolVersionSelectionError Error(string code, string message) => new(code, message);
}
