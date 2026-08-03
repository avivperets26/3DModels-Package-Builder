namespace PackageBuilder.Contracts.Snapshots;

/// <summary>Copies accepted sources into a contained, hashed, read-only job snapshot.</summary>
public interface ISourceSnapshotService
{
    Task<SnapshotOperationResult<SourceSnapshotReceipt>> CreateAsync(
        SourceSnapshotRequest request,
        CancellationToken cancellationToken = default);
}
