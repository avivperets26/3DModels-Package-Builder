using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Infrastructure.Artifacts;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

public sealed class ArtifactPromotionContractTests
{
    [Fact]
    public void RequestRejectsNullRequiredValues()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        ArtifactPromotionRequest valid = workspace.Request();

        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactPromotionRequest(
            null!, valid.ArtifactsRoot, valid.BuildsRoot, valid.JobId, valid.ArtifactId, valid.PromotedAtUtc));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactPromotionRequest(
            valid.ProjectRoot, null!, valid.BuildsRoot, valid.JobId, valid.ArtifactId, valid.PromotedAtUtc));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactPromotionRequest(
            valid.ProjectRoot, valid.ArtifactsRoot, null!, valid.JobId, valid.ArtifactId, valid.PromotedAtUtc));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactPromotionRequest(
            valid.ProjectRoot, valid.ArtifactsRoot, valid.BuildsRoot, null!, valid.ArtifactId, valid.PromotedAtUtc));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactPromotionRequest(
            valid.ProjectRoot, valid.ArtifactsRoot, valid.BuildsRoot, valid.JobId, null!, valid.PromotedAtUtc));
    }

    [Fact]
    public async Task ReceiptAndResultExposeImmutablePromotionFacts()
    {
        using var workspace = new ArtifactPromotionTestWorkspace();
        _ = await workspace.StageAndValidateAsync();
        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            workspace.Request(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Failures);
        Assert.NotNull(result.Value!.ArtifactRecord);
        Assert.Equal("packages/Silverwing.zip", result.Value.ReleaseRelativePath);
        Assert.Equal(1, result.Value.CollisionVersion);
        Assert.False(result.Value.Recovered);
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactPromotionReceipt(
            null!, result.Value.ReleasePath, result.Value.ReleaseRelativePath, 1, false));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactPromotionReceipt(
            result.Value.ArtifactRecord, null!, result.Value.ReleaseRelativePath, 1, false));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactPromotionReceipt(
            result.Value.ArtifactRecord, result.Value.ReleasePath, null!, 1, false));
        _ = Assert.Throws<ArgumentNullException>(() => ArtifactPromotionResult.Success(null!));
    }

    [Fact]
    public void FailureFactoryRequiresAtLeastOneNonNullFailure()
    {
        _ = Assert.Throws<ArgumentException>(() => ArtifactPromotionResult.Failed());
        _ = Assert.Throws<ArgumentNullException>(() => ArtifactPromotionResult.Failed(null!));
        _ = Assert.Throws<ArgumentException>(() => ArtifactPromotionResult.Failed([null!]));

        ArtifactPromotionOperationResult result = ArtifactPromotionResult.Failed(
            new ArtifactPromotionFailure("CODE", "location", "diagnostic"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal("CODE", Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task ServiceConstructorsRejectNullDependenciesAndRequests()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new AtomicArtifactPromotionService(null!, new ArtifactHashService()));
        _ = Assert.Throws<ArgumentNullException>(() => new AtomicArtifactPromotionService(new ArtifactStore(), null!));
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await new AtomicArtifactPromotionService().PromoteAsync(
                null!,
                TestContext.Current.CancellationToken));
    }
}
