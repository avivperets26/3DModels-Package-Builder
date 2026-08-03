namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Calculates deterministic artifact content identities through bounded streaming.</summary>
public interface IArtifactHashService
{
    Task<ArtifactHashOperationResult<ArtifactHashReceipt>> HashAsync(
        ArtifactHashRequest request,
        CancellationToken cancellationToken = default);
}
