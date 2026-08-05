using PackageBuilder.Application.Tools;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Application.Tests.Tools;

public sealed class LatestApprovedStableSelectionPolicyTests
{
    private static readonly Uri _source = new("https://example.invalid/releases");
    private static readonly DateTimeOffset _timestamp = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SelectsNewestApprovedStableInsteadOfHighestCatalogVersion()
    {
        OfficialReleaseCatalog catalog = Catalog(
            Release("6000.4.0b1"),
            Release("6000.3.3f1"),
            Release("6000.3.2f1"));

        ToolVersionSelectionResult result = Select(
            catalog,
            Approval("6000.3.2f1", ToolVersionApprovalState.ApprovedLatest),
            Approval("6000.3.3f1", ToolVersionApprovalState.Candidate),
            Approval("6000.4.0b1", ToolVersionApprovalState.ApprovedLatest));

        Assert.True(result.IsSuccess);
        Assert.Equal("6000.3.2f1", result.Selection!.Release.Version.Value);
        Assert.Equal(ToolVersionSelectionOrigin.ApprovedLatest, result.Selection.Origin);
    }

    [Fact]
    public void FallsBackToNewestLastKnownGoodWhenNoApprovedLatestIsEligible()
    {
        OfficialReleaseCatalog catalog = Catalog(
            Release("6000.3.3f1"),
            Release("6000.3.2f1"),
            Release("6000.3.1f1"));

        ToolVersionSelectionResult result = Select(
            catalog,
            Approval("6000.3.3f1", ToolVersionApprovalState.Rejected),
            Approval("6000.3.1f1", ToolVersionApprovalState.LastKnownGood),
            Approval("6000.3.2f1", ToolVersionApprovalState.LastKnownGood));

        Assert.True(result.IsSuccess);
        Assert.Equal("6000.3.2f1", result.Selection!.Release.Version.Value);
        Assert.Equal(ToolVersionSelectionOrigin.LastKnownGood, result.Selection.Origin);
    }

    [Fact]
    public void MarketplaceRangeAndLtsRequirementConstrainSelection()
    {
        OfficialReleaseCatalog catalog = Catalog(
            Release("6000.3.3f1"),
            Release("6000.3.2f1", ToolReleaseSupport.LongTermSupport),
            Release("6000.3.1f1", ToolReleaseSupport.LongTermSupport));
        var constraint = new MarketplaceToolVersionConstraint(
            ToolKind.Unity,
            Version("6000.3.1f1"),
            Version("6000.3.2f1"),
            RequireLongTermSupport: true);

        ToolVersionSelectionResult result = Select(
            catalog,
            [Approval("6000.3.3f1", ToolVersionApprovalState.ApprovedLatest),
             Approval("6000.3.2f1", ToolVersionApprovalState.ApprovedLatest)],
            [],
            constraint);

        Assert.True(result.IsSuccess);
        Assert.Equal("6000.3.2f1", result.Selection!.Release.Version.Value);
    }

    [Fact]
    public void RequiredModulesMustExistInApprovalEvidence()
    {
        OfficialReleaseCatalog catalog = Catalog(Release("6000.3.3f1"), Release("6000.3.2f1"));

        ToolVersionSelectionResult result = Select(
            catalog,
            [Approval("6000.3.3f1", ToolVersionApprovalState.ApprovedLatest, "windows-il2cpp"),
             Approval("6000.3.2f1", ToolVersionApprovalState.LastKnownGood, "windows-il2cpp", "android")],
            ["windows-il2cpp", "ANDROID"]);

        Assert.True(result.IsSuccess);
        Assert.Equal("6000.3.2f1", result.Selection!.Release.Version.Value);
        Assert.Equal(ToolVersionSelectionOrigin.LastKnownGood, result.Selection.Origin);
    }

    [Fact]
    public void ReturnsTypedFailureWhenNoEligibleApprovalExists()
    {
        ToolVersionSelectionResult result = Select(
            Catalog(Release("6000.3.3f1")),
            Approval("6000.3.3f1", ToolVersionApprovalState.Candidate));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Selection);
        Assert.Equal("TOOL_VERSION_NO_APPROVED_STABLE", result.Error!.Code);
    }

    [Theory]
    [InlineData("wrong-tool")]
    [InlineData("wrong-bound-tool")]
    [InlineData("inverted-range")]
    public void RejectsInvalidMarketplaceConstraints(string scenario)
    {
        MarketplaceToolVersionConstraint constraint = scenario switch
        {
            "wrong-tool" => new MarketplaceToolVersionConstraint(ToolKind.Unreal),
            "wrong-bound-tool" => new MarketplaceToolVersionConstraint(ToolKind.Unity, UnrealVersion("5.7.0")),
            _ => new MarketplaceToolVersionConstraint(ToolKind.Unity, Version("6000.3.3f1"), Version("6000.3.2f1")),
        };

        ToolVersionSelectionResult result = Select(
            Catalog(Release("6000.3.3f1")),
            [Approval("6000.3.3f1", ToolVersionApprovalState.ApprovedLatest)],
            [],
            constraint);

        Assert.Equal("TOOL_VERSION_MARKETPLACE_CONSTRAINT_INVALID", result.Error!.Code);
    }

    [Theory]
    [InlineData("duplicate-approval")]
    [InlineData("wrong-approval-tool")]
    [InlineData("invalid-state")]
    [InlineData("invalid-module")]
    [InlineData("duplicate-module")]
    public void RejectsInvalidApprovalEvidence(string scenario)
    {
        ToolVersionApproval first = Approval("6000.3.3f1", ToolVersionApprovalState.ApprovedLatest, "android");
        IReadOnlyCollection<ToolVersionApproval> approvals = scenario switch
        {
            "duplicate-approval" => [first, first],
            "wrong-approval-tool" => [new ToolVersionApproval(UnrealVersion("5.7.0"), ToolVersionApprovalState.ApprovedLatest, [])],
            "invalid-state" => [first with { State = (ToolVersionApprovalState)99 }],
            "invalid-module" => [first with { AvailableModules = [" android"] }],
            _ => [first with { AvailableModules = ["android", "ANDROID"] }],
        };

        ToolVersionSelectionResult result = Select(Catalog(Release("6000.3.3f1")), approvals);

        Assert.Equal("TOOL_VERSION_APPROVAL_INVALID", result.Error!.Code);
    }

    [Fact]
    public void RejectsInvalidRequiredModules()
    {
        ToolVersionSelectionResult result = Select(
            Catalog(Release("6000.3.3f1")),
            [Approval("6000.3.3f1", ToolVersionApprovalState.ApprovedLatest)],
            ["android", "ANDROID"]);

        Assert.Equal("TOOL_VERSION_REQUIRED_MODULE_INVALID", result.Error!.Code);
    }

    private static ToolVersionSelectionResult Select(
        OfficialReleaseCatalog catalog,
        params ToolVersionApproval[] approvals) => Select(catalog, approvals, []);

    private static ToolVersionSelectionResult Select(
        OfficialReleaseCatalog catalog,
        IReadOnlyCollection<ToolVersionApproval> approvals,
        IReadOnlyCollection<string>? modules = null,
        MarketplaceToolVersionConstraint? constraint = null) =>
        LatestApprovedStableSelectionPolicy.Select(
            new LatestApprovedStableSelectionRequest(catalog, approvals, modules ?? [], constraint));

    private static OfficialReleaseCatalog Catalog(params OfficialToolRelease[] releases) =>
        OfficialReleaseCatalog.Create(ToolKind.Unity, _source, _timestamp, releases).Value!;

    private static OfficialToolRelease Release(
        string value,
        ToolReleaseSupport support = ToolReleaseSupport.Standard) => new(Version(value), support, _timestamp);

    private static ToolVersionApproval Approval(
        string value,
        ToolVersionApprovalState state,
        params string[] modules) => new(Version(value), state, modules);

    private static ToolVersion Version(string value) => ToolVersion.Create(ToolKind.Unity, value).Value!;

    private static ToolVersion UnrealVersion(string value) => ToolVersion.Create(ToolKind.Unreal, value).Value!;
}
