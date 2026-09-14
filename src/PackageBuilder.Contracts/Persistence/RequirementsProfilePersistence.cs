namespace PackageBuilder.Contracts.Persistence;

/// <summary>Marketplace-neutral immutable cache entry. JSON semantics belong to its marketplace adapter.</summary>
public sealed record CachedRequirementsProfile(
    string Marketplace, string Version, DateOnly EffectiveOn, string SourceReference, string Json, string Sha256,
    bool IsApproved);

/// <summary>Explicit reviewed promotion with complete test evidence; compare-and-swap protects the current selection.</summary>
public sealed record RequirementsProfileApproval(
    string Marketplace, string Version, string Sha256, string? ExpectedCurrentSha256,
    string Reviewer, DateTimeOffset ApprovedAtUtc, PersistedCompatibilitySuiteResult Evidence);

/// <summary>Stores immutable versions and atomically promotes a tested version with its approval receipt.</summary>
public interface IRequirementsProfileRepository
{
    Task<RepositoryOperationResult> CacheAsync(CachedRequirementsProfile profile, CancellationToken cancellationToken = default);
    Task<RepositoryOperationResult<CachedRequirementsProfile?>> GetAsync(
        string marketplace, string version, CancellationToken cancellationToken = default);
    Task<RepositoryOperationResult<CachedRequirementsProfile?>> GetCurrentAsync(
        string marketplace, CancellationToken cancellationToken = default);
    Task<RepositoryOperationResult> ApproveAsync(RequirementsProfileApproval approval, CancellationToken cancellationToken = default);
    Task<RepositoryOperationResult<RequirementsProfileApproval?>> GetApprovalAsync(
        string marketplace, string version, CancellationToken cancellationToken = default);
}
