using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Domain.Tests.Tools;

public sealed class ToolVersionApprovalTransitionPolicyTests
{
    public static TheoryData<ToolVersionApprovalState, ToolVersionApprovalState> AllowedTransitions => new()
    {
        { ToolVersionApprovalState.Discovered, ToolVersionApprovalState.Installed },
        { ToolVersionApprovalState.Installed, ToolVersionApprovalState.Candidate },
        { ToolVersionApprovalState.Candidate, ToolVersionApprovalState.ApprovedLatest },
        { ToolVersionApprovalState.Candidate, ToolVersionApprovalState.Rejected },
        { ToolVersionApprovalState.ApprovedLatest, ToolVersionApprovalState.LastKnownGood },
        { ToolVersionApprovalState.Rejected, ToolVersionApprovalState.Candidate },
    };

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void AllowsOnlyDocumentedLifecycleEdges(
        ToolVersionApprovalState current,
        ToolVersionApprovalState target)
        => Assert.True(ToolVersionApprovalTransitionPolicy.CanTransition(current, target));

    [Theory]
    [InlineData(ToolVersionApprovalState.Discovered, ToolVersionApprovalState.Candidate)]
    [InlineData(ToolVersionApprovalState.Candidate, ToolVersionApprovalState.LastKnownGood)]
    [InlineData(ToolVersionApprovalState.LastKnownGood, ToolVersionApprovalState.ApprovedLatest)]
    [InlineData(ToolVersionApprovalState.Rejected, ToolVersionApprovalState.ApprovedLatest)]
    [InlineData((ToolVersionApprovalState)99, ToolVersionApprovalState.Discovered)]
    public void RejectsSkippedTerminalAndUndefinedEdges(
        ToolVersionApprovalState current,
        ToolVersionApprovalState target)
        => Assert.False(ToolVersionApprovalTransitionPolicy.CanTransition(current, target));
}
