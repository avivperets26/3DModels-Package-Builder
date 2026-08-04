using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Contracts.Persistence;

/// <summary>Persists job lifecycle metadata using optimistic expected-state transitions.</summary>
public interface IBuildJobRepository
{
    Task<RepositoryOperationResult> CreateAsync(
        PersistedBuildJob job,
        CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<PersistedBuildJob?>> GetAsync(
        BuildJobId jobId,
        CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<IReadOnlyList<PersistedBuildJob>>> QueryAsync(
        BuildJobState? state = null,
        CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<IReadOnlyList<PersistedBuildJob>>> GetResumableAsync(
        CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<PersistedBuildJob>> TransitionAsync(
        BuildJobId jobId,
        BuildJobState expectedState,
        BuildJobState targetState,
        DateTimeOffset occurredAtUtc,
        string? failureCode = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Stores and queries artifact metadata correlated to a build job.</summary>
public interface IBuildArtifactRepository
{
    Task<RepositoryOperationResult> AddAsync(
        PersistedBuildArtifact artifact,
        CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<IReadOnlyList<PersistedBuildArtifact>>> GetByJobAsync(
        BuildJobId jobId,
        CancellationToken cancellationToken = default);
}

/// <summary>Stores and queries findings correlated to jobs and optional artifacts.</summary>
public interface IValidationFindingRepository
{
    Task<RepositoryOperationResult> AddAsync(
        PersistedValidationFinding finding,
        CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<IReadOnlyList<PersistedValidationFinding>>> GetByJobAsync(
        BuildJobId jobId,
        CancellationToken cancellationToken = default);
}

/// <summary>Maintains deterministic discovery metadata for local tools.</summary>
public interface IToolInstallationRepository
{
    Task<RepositoryOperationResult> UpsertAsync(
        PersistedToolInstallation installation,
        CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<IReadOnlyList<PersistedToolInstallation>>> QueryAsync(
        bool? approved = null,
        CancellationToken cancellationToken = default);
}
