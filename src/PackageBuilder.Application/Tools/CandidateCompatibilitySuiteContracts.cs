using System.Collections.ObjectModel;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Application.Tools;

/// <summary>The required compatibility checks that gate every stable candidate promotion.</summary>
public enum CompatibilitySuiteCheckKind
{
    StaticModelFixture = 0,
    RiggedFixture = 1,
    RiggedAnimatedFixture = 2,
    ItemSetFixture = 3,
    ItemCollectionFixture = 4,
    MaterialPreviewComparison = 5,
    CleanExportReimport = 6,
    MarketplaceStructureValidation = 7,
}

/// <summary>One configured and independently observable compatibility-suite check.</summary>
public sealed record CompatibilitySuiteCheck(
    string Id,
    CompatibilitySuiteCheckKind Kind,
    string FixtureReference);

/// <summary>Runs one configured fixture or validator without owning approval state.</summary>
public interface ICompatibilitySuiteCheckExecutor
{
    Task<CompatibilitySuiteCheckExecutionResult> ExecuteAsync(
        PersistedToolVersionApproval candidate,
        CompatibilitySuiteCheck check,
        string runId,
        CancellationToken cancellationToken = default);
}

/// <summary>Provides UTC completion timestamps for deterministic orchestration and tests.</summary>
public interface ICompatibilitySuiteClock
{
    DateTimeOffset GetUtcNow();
}

/// <summary>Stable result returned by one configured compatibility check.</summary>
public sealed record CompatibilitySuiteCheckExecutionResult(bool Passed, string? FailureCode = null)
{
    public static CompatibilitySuiteCheckExecutionResult Pass() => new(true);

    public static CompatibilitySuiteCheckExecutionResult Fail(string failureCode) =>
        new(false, failureCode);
}

/// <summary>Input for one complete candidate promotion decision.</summary>
public sealed record RunCandidateCompatibilitySuiteRequest(
    string CandidateApprovalId,
    long ExpectedRevision,
    string RunId,
    string TransitionId,
    string EvidenceReference,
    IReadOnlyCollection<CompatibilitySuiteCheck> Checks);

/// <summary>Sanitized expected runner failure.</summary>
public sealed record CandidateCompatibilitySuiteError(string Code, string Message);

/// <summary>Recorded outcome for one configured check.</summary>
public sealed record CompatibilitySuiteCheckReceipt(
    string Id,
    CompatibilitySuiteCheckKind Kind,
    string FixtureReference,
    bool Passed,
    string? FailureCode);

/// <summary>Observable promotion decision safe for future CLI and WPF consumers.</summary>
public sealed class CandidateCompatibilitySuiteReceipt
{
    public CandidateCompatibilitySuiteReceipt(
        string candidateApprovalId,
        ToolVersion candidateVersion,
        ToolVersionApprovalState finalState,
        string runId,
        string evidenceReference,
        string evidenceSha256,
        DateTimeOffset completedAtUtc,
        IEnumerable<CompatibilitySuiteCheckReceipt> checks)
    {
        CandidateApprovalId = candidateApprovalId;
        CandidateVersion = candidateVersion;
        FinalState = finalState;
        RunId = runId;
        EvidenceReference = evidenceReference;
        EvidenceSha256 = evidenceSha256;
        CompletedAtUtc = completedAtUtc;
        ArgumentNullException.ThrowIfNull(checks);
        Checks = new ReadOnlyCollection<CompatibilitySuiteCheckReceipt>(checks.ToArray());
    }

    public string CandidateApprovalId { get; }

    public ToolVersion CandidateVersion { get; }

    public ToolVersionApprovalState FinalState { get; }

    public string RunId { get; }

    public string EvidenceReference { get; }

    public string EvidenceSha256 { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public IReadOnlyList<CompatibilitySuiteCheckReceipt> Checks { get; }

    public bool Promoted => FinalState == ToolVersionApprovalState.ApprovedLatest;
}

/// <summary>Non-throwing expected result for a complete candidate suite request.</summary>
public sealed class CandidateCompatibilitySuiteResult
{
    private CandidateCompatibilitySuiteResult(
        CandidateCompatibilitySuiteReceipt? value,
        CandidateCompatibilitySuiteError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public CandidateCompatibilitySuiteReceipt? Value { get; }

    public CandidateCompatibilitySuiteError? Error { get; }

    public static CandidateCompatibilitySuiteResult Success(CandidateCompatibilitySuiteReceipt value) =>
        new(value, null);

    public static CandidateCompatibilitySuiteResult Failure(string code, string message) =>
        new(null, new CandidateCompatibilitySuiteError(code, message));
}
