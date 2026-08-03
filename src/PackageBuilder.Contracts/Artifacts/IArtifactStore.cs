namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Stages, reads, and advances contained job artifacts with verified metadata.</summary>
public interface IArtifactStore
{
    Task<ArtifactStoreOperationResult<ArtifactStoreRecord>> StageAsync(
        ArtifactStoreStageRequest request,
        CancellationToken cancellationToken = default);

    Task<ArtifactStoreOperationResult<ArtifactStoreRecord>> ReadAsync(
        ArtifactStoreReadRequest request,
        CancellationToken cancellationToken = default);

    Task<ArtifactStoreOperationResult<ArtifactStoreRecord>> TransitionAsync(
        ArtifactStoreTransitionRequest request,
        CancellationToken cancellationToken = default);
}
