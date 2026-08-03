namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Publishes validated artifacts into Builds without exposing partial releases.</summary>
public interface IArtifactPromotionService
{
    Task<ArtifactPromotionOperationResult> PromoteAsync(
        ArtifactPromotionRequest request,
        CancellationToken cancellationToken = default);
}
