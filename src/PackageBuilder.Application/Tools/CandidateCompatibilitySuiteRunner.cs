using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Application.Tools;

/// <summary>
/// Executes every configured compatibility check and atomically approves or rejects the candidate.
/// The repository remains the authority that demotes the previous Approved Latest only on success.
/// </summary>
public sealed class CandidateCompatibilitySuiteRunner(
    IToolVersionApprovalRepository repository,
    ICompatibilitySuiteCheckExecutor executor,
    ICompatibilitySuiteClock clock)
{
    private const int MaximumIdentityLength = 128;
    private const int MaximumTextLength = 2048;
    private const string UnexpectedFailure = "COMPATIBILITY_STEP_UNEXPECTED_FAILURE";
    private const string InvalidResultFailure = "COMPATIBILITY_STEP_RESULT_INVALID";
    private static readonly CompatibilitySuiteCheckKind[] _requiredChecks =
        Enum.GetValues<CompatibilitySuiteCheckKind>();

    private readonly IToolVersionApprovalRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly ICompatibilitySuiteCheckExecutor _executor =
        executor ?? throw new ArgumentNullException(nameof(executor));
    private readonly ICompatibilitySuiteClock _clock =
        clock ?? throw new ArgumentNullException(nameof(clock));

    public async Task<CandidateCompatibilitySuiteResult> RunAsync(
        RunCandidateCompatibilitySuiteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CandidateCompatibilitySuiteResult? invalid = Validate(request);
        if (invalid is not null)
        {
            return invalid;
        }

        RepositoryOperationResult<PersistedToolVersionApproval?> read = await _repository.GetAsync(
            request.CandidateApprovalId,
            cancellationToken).ConfigureAwait(false);
        if (!read.IsSuccess)
        {
            return PersistenceFailure(read.Error);
        }

        PersistedToolVersionApproval? candidate = read.Value;
        if (candidate is null)
        {
            return CandidateCompatibilitySuiteResult.Failure(
                "COMPATIBILITY_CANDIDATE_NOT_FOUND",
                "The requested candidate approval does not exist.");
        }

        if (candidate.State != ToolVersionApprovalState.Candidate
            || candidate.Revision != request.ExpectedRevision)
        {
            return CandidateCompatibilitySuiteResult.Failure(
                "COMPATIBILITY_CANDIDATE_STALE",
                "The candidate state or revision changed before the suite could begin.");
        }

        var receipts = new List<CompatibilitySuiteCheckReceipt>(request.Checks.Count);
        foreach (CompatibilitySuiteCheck check in request.Checks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompatibilitySuiteCheckExecutionResult result;
            try
            {
                result = await _executor.ExecuteAsync(
                    candidate,
                    check,
                    request.RunId,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                result = CompatibilitySuiteCheckExecutionResult.Fail(UnexpectedFailure);
            }

            result = Normalize(result);
            receipts.Add(new CompatibilitySuiteCheckReceipt(
                check.Id,
                check.Kind,
                check.FixtureReference,
                result.Passed,
                result.FailureCode));
        }

        DateTimeOffset completedAtUtc = _clock.GetUtcNow();
        if (completedAtUtc.Offset != TimeSpan.Zero)
        {
            return CandidateCompatibilitySuiteResult.Failure(
                "COMPATIBILITY_CLOCK_INVALID",
                "The compatibility-suite clock must return a UTC timestamp.");
        }

        int passed = receipts.Count(receipt => receipt.Passed);
        int failed = receipts.Count - passed;
        CompatibilitySuiteOutcome outcome = failed == 0
            ? CompatibilitySuiteOutcome.Passed
            : CompatibilitySuiteOutcome.Failed;
        string evidenceSha256 = ComputeEvidenceSha256(request, candidate, completedAtUtc, receipts);
        var evidence = new PersistedCompatibilitySuiteResult(
            request.RunId,
            outcome,
            receipts.Count,
            passed,
            failed,
            request.EvidenceReference,
            evidenceSha256,
            completedAtUtc);
        ToolVersionApprovalState target = outcome == CompatibilitySuiteOutcome.Passed
            ? ToolVersionApprovalState.ApprovedLatest
            : ToolVersionApprovalState.Rejected;

        RepositoryOperationResult<PersistedToolVersionApproval> transition =
            await _repository.TransitionAsync(
                candidate.Id,
                request.ExpectedRevision,
                ToolVersionApprovalState.Candidate,
                target,
                request.TransitionId,
                completedAtUtc,
                evidence,
                cancellationToken).ConfigureAwait(false);
        return !transition.IsSuccess
            ? PersistenceFailure(transition.Error)
            : CandidateCompatibilitySuiteResult.Success(
            new CandidateCompatibilitySuiteReceipt(
                candidate.Id,
                candidate.Version,
                transition.Value!.State,
                request.RunId,
                request.EvidenceReference,
                evidenceSha256,
                completedAtUtc,
                receipts));
    }

    private static CandidateCompatibilitySuiteResult? Validate(
        RunCandidateCompatibilitySuiteRequest request)
    {
        if (!IsIdentity(request.CandidateApprovalId) || request.ExpectedRevision < 0
            || !IsIdentity(request.RunId) || !IsIdentity(request.TransitionId)
            || !IsLogicalReference(request.EvidenceReference) || request.Checks is null)
        {
            return InvalidInput();
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var kinds = new HashSet<CompatibilitySuiteCheckKind>();
        foreach (CompatibilitySuiteCheck? check in request.Checks)
        {
            if (check is null || !IsIdentity(check.Id) || !Enum.IsDefined(check.Kind)
                || !IsLogicalReference(check.FixtureReference) || !ids.Add(check.Id))
            {
                return InvalidInput();
            }

            _ = kinds.Add(check.Kind);
        }

        return _requiredChecks.All(kinds.Contains)
            ? null
            : CandidateCompatibilitySuiteResult.Failure(
                "COMPATIBILITY_SUITE_INCOMPLETE",
                "The configuration must include every required compatibility check kind.");
    }

    private static CompatibilitySuiteCheckExecutionResult Normalize(
        CompatibilitySuiteCheckExecutionResult? result) =>
        result is null || result.Passed && result.FailureCode is not null
            || !result.Passed && !IsFailureCode(result.FailureCode)
            ? CompatibilitySuiteCheckExecutionResult.Fail(InvalidResultFailure)
            : result;

    private static string ComputeEvidenceSha256(
        RunCandidateCompatibilitySuiteRequest request,
        PersistedToolVersionApproval candidate,
        DateTimeOffset completedAtUtc,
        IEnumerable<CompatibilitySuiteCheckReceipt> receipts)
    {
        var canonical = new StringBuilder();
        Append(canonical, "schema", "1");
        Append(canonical, "candidate", candidate.Id);
        Append(canonical, "version", candidate.Version.Value);
        Append(canonical, "run", request.RunId);
        Append(canonical, "completed", completedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        foreach (CompatibilitySuiteCheckReceipt receipt in receipts)
        {
            Append(canonical, "id", receipt.Id);
            Append(canonical, "kind", ((int)receipt.Kind).ToString(CultureInfo.InvariantCulture));
            Append(canonical, "fixture", receipt.FixtureReference);
            Append(canonical, "passed", receipt.Passed ? "1" : "0");
            Append(canonical, "failure", receipt.FailureCode ?? string.Empty);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Append(StringBuilder builder, string name, string value) =>
        _ = builder.Append(name).Append(':').Append(value.Length).Append(':').Append(value).Append('\n');

    private static CandidateCompatibilitySuiteResult InvalidInput() =>
        CandidateCompatibilitySuiteResult.Failure(
            "COMPATIBILITY_INPUT_INVALID",
            "Candidate suite identifiers, evidence references, and configured checks must be valid.");

    private static CandidateCompatibilitySuiteResult PersistenceFailure(RepositoryOperationError? error) =>
        CandidateCompatibilitySuiteResult.Failure(
            "COMPATIBILITY_PERSISTENCE_FAILED",
            error?.Message ?? "The candidate approval operation failed.");

    private static bool IsIdentity(string? value) => IsText(value, MaximumIdentityLength);

    private static bool IsLogicalReference(string? value) =>
        IsText(value, MaximumTextLength) && value![0] != '/' && !value.Contains('\\', StringComparison.Ordinal)
        && !value.Contains(':', StringComparison.Ordinal) && value.Split('/').All(segment =>
            segment.Length > 0 && segment is not "." and not ".."
            && !char.IsWhiteSpace(segment[0]) && !char.IsWhiteSpace(segment[^1]));

    private static bool IsText(string? value, int maximumLength) =>
        value is not null && value.Length is > 0 && value.Length <= maximumLength
        && !string.IsNullOrWhiteSpace(value) && !char.IsWhiteSpace(value[0])
        && !char.IsWhiteSpace(value[^1]) && !value.Any(char.IsControl);

    private static bool IsFailureCode(string? value) =>
        value is { Length: > 0 and <= MaximumIdentityLength }
        && value[0] is >= 'A' and <= 'Z'
        && value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');
}
