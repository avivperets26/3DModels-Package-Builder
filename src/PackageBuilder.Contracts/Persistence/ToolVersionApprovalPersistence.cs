using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Contracts.Persistence;

/// <summary>Outcome recorded by the complete candidate compatibility suite.</summary>
public enum CompatibilitySuiteOutcome
{
    Passed = 0,
    Failed = 1,
}

/// <summary>Immutable test-result evidence required to approve or reject a candidate.</summary>
public sealed record PersistedCompatibilitySuiteResult(
    string RunId,
    CompatibilitySuiteOutcome Outcome,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    string EvidenceReference,
    string EvidenceSha256,
    DateTimeOffset CompletedAtUtc);

/// <summary>Current persisted lifecycle state and installed modules for one engine version.</summary>
public sealed record PersistedToolVersionApproval(
    string Id,
    ToolVersion Version,
    ToolVersionApprovalState State,
    IReadOnlyCollection<string> AvailableModules,
    string? ToolInstallationId,
    DateTimeOffset DiscoveredAtUtc,
    DateTimeOffset UpdatedAtUtc,
    long Revision);

/// <summary>One append-only lifecycle transition with optional compatibility evidence.</summary>
public sealed record PersistedToolVersionApprovalTransition(
    string Id,
    string ApprovalId,
    ToolVersionApprovalState? FromState,
    ToolVersionApprovalState ToState,
    DateTimeOffset OccurredAtUtc,
    PersistedCompatibilitySuiteResult? CompatibilityResult);

/// <summary>Persists approval state with optimistic transitions and append-only test evidence.</summary>
public interface IToolVersionApprovalRepository
{
    Task<RepositoryOperationResult> CreateAsync(
        PersistedToolVersionApproval approval,
        string initialTransitionId,
        CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<PersistedToolVersionApproval?>> GetAsync(
        string approvalId,
        CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<IReadOnlyList<PersistedToolVersionApproval>>> QueryAsync(
        ToolKind? tool = null,
        ToolVersionApprovalState? state = null,
        CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<IReadOnlyList<PersistedToolVersionApprovalTransition>>> GetHistoryAsync(
        string approvalId,
        CancellationToken cancellationToken = default);

    Task<RepositoryOperationResult<PersistedToolVersionApproval>> TransitionAsync(
        string approvalId,
        long expectedRevision,
        ToolVersionApprovalState expectedState,
        ToolVersionApprovalState targetState,
        string transitionId,
        DateTimeOffset occurredAtUtc,
        PersistedCompatibilitySuiteResult? compatibilityResult = null,
        CancellationToken cancellationToken = default);
}
