namespace PackageBuilder.Domain.Tools;

/// <summary>Lifecycle state for a discovered tool version and its compatibility approval.</summary>
public enum ToolVersionApprovalState
{
    Discovered = 0,
    Installed = 1,
    Candidate = 2,
    ApprovedLatest = 3,
    Rejected = 4,
    LastKnownGood = 5,
}

/// <summary>Defines the only approved lifecycle transitions for tool-version evidence.</summary>
public static class ToolVersionApprovalTransitionPolicy
{
    public static bool CanTransition(
        ToolVersionApprovalState current,
        ToolVersionApprovalState target) =>
        Enum.IsDefined(current)
        && Enum.IsDefined(target)
        && (current, target) is
            (ToolVersionApprovalState.Discovered, ToolVersionApprovalState.Installed)
            or (ToolVersionApprovalState.Installed, ToolVersionApprovalState.Candidate)
            or (ToolVersionApprovalState.Candidate, ToolVersionApprovalState.ApprovedLatest)
            or (ToolVersionApprovalState.Candidate, ToolVersionApprovalState.Rejected)
            or (ToolVersionApprovalState.ApprovedLatest, ToolVersionApprovalState.LastKnownGood)
            or (ToolVersionApprovalState.Rejected, ToolVersionApprovalState.Candidate);
}
