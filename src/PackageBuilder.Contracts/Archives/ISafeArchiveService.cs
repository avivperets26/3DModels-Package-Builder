namespace PackageBuilder.Contracts.Archives;

/// <summary>Preflights and extracts untrusted ZIP archives without shell execution.</summary>
public interface ISafeArchiveService
{
    Task<ArchiveOperationResult<ArchiveInspection>> InspectAsync(
        ArchiveOperationRequest request,
        CancellationToken cancellationToken = default);

    Task<ArchiveOperationResult<ArchiveExtractionReceipt>> ExtractAsync(
        ArchiveOperationRequest request,
        CancellationToken cancellationToken = default);
}
