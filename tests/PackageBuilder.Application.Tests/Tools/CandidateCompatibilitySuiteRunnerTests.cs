using PackageBuilder.Application.Tools;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Application.Tests.Tools;

public sealed class CandidateCompatibilitySuiteRunnerTests
{
    private static readonly DateTimeOffset _completedAt =
        new(2026, 8, 5, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CompletePassingSuiteRecordsEvidenceAndPromotesCandidate()
    {
        var repository = new RecordingRepository(Candidate());
        var executor = new RecordingExecutor();

        CandidateCompatibilitySuiteResult result = await Create(repository, executor).RunAsync(
            Request(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Promoted);
        Assert.Equal(ToolVersionApprovalState.ApprovedLatest, result.Value.FinalState);
        Assert.Equal(Enum.GetValues<CompatibilitySuiteCheckKind>(), executor.ExecutedKinds);
        Assert.Equal(8, result.Value.Checks.Count);
        Assert.All(result.Value.Checks, check => Assert.True(check.Passed));
        Assert.Equal(64, result.Value.EvidenceSha256.Length);
        Assert.All(result.Value.EvidenceSha256, character =>
            Assert.True(character is >= '0' and <= '9' or >= 'a' and <= 'f'));

        TransitionCall transition = Assert.Single(repository.Transitions);
        Assert.Equal(ToolVersionApprovalState.Candidate, transition.ExpectedState);
        Assert.Equal(ToolVersionApprovalState.ApprovedLatest, transition.TargetState);
        Assert.Equal(CompatibilitySuiteOutcome.Passed, transition.Evidence.Outcome);
        Assert.Equal((8, 8, 0),
            (transition.Evidence.TotalTests, transition.Evidence.PassedTests, transition.Evidence.FailedTests));
        Assert.Equal(result.Value.EvidenceSha256, transition.Evidence.EvidenceSha256);
    }

    [Fact]
    public async Task AnyFailureRunsRemainingChecksRejectsCandidateAndPreservesFallbackBoundary()
    {
        var repository = new RecordingRepository(Candidate());
        var executor = new RecordingExecutor(
            failures: new Dictionary<CompatibilitySuiteCheckKind, string>
            {
                [CompatibilitySuiteCheckKind.RiggedAnimatedFixture] = "ANIMATION_IMPORT_FAILED",
            });

        CandidateCompatibilitySuiteResult result = await Create(repository, executor).RunAsync(
            Request(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Promoted);
        Assert.Equal(ToolVersionApprovalState.Rejected, result.Value.FinalState);
        Assert.Equal(8, executor.ExecutedKinds.Count);
        CompatibilitySuiteCheckReceipt failure = Assert.Single(result.Value.Checks, check => !check.Passed);
        Assert.Equal("ANIMATION_IMPORT_FAILED", failure.FailureCode);

        TransitionCall transition = Assert.Single(repository.Transitions);
        Assert.Equal(ToolVersionApprovalState.Rejected, transition.TargetState);
        Assert.Equal(CompatibilitySuiteOutcome.Failed, transition.Evidence.Outcome);
        Assert.Equal((8, 7, 1),
            (transition.Evidence.TotalTests, transition.Evidence.PassedTests, transition.Evidence.FailedTests));
        Assert.DoesNotContain(repository.Transitions,
            call => call.TargetState is ToolVersionApprovalState.ApprovedLatest
                or ToolVersionApprovalState.LastKnownGood);
    }

    [Fact]
    public async Task UnexpectedExecutorFailureBecomesRecordedFailureWithoutLeakingException()
    {
        var repository = new RecordingRepository(Candidate());
        var executor = new RecordingExecutor(throwOn: CompatibilitySuiteCheckKind.StaticModelFixture);

        CandidateCompatibilitySuiteResult result = await Create(repository, executor).RunAsync(
            Request(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ToolVersionApprovalState.Rejected, result.Value!.FinalState);
        Assert.Equal(
            "COMPATIBILITY_STEP_UNEXPECTED_FAILURE",
            result.Value.Checks[0].FailureCode);
        Assert.Equal(CompatibilitySuiteOutcome.Failed, repository.Transitions[0].Evidence.Outcome);
    }

    [Fact]
    public async Task InvalidExecutorResultFailsClosed()
    {
        var repository = new RecordingRepository(Candidate());
        var executor = new RecordingExecutor(invalidOn: CompatibilitySuiteCheckKind.ItemSetFixture);

        CandidateCompatibilitySuiteResult result = await Create(repository, executor).RunAsync(
            Request(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ToolVersionApprovalState.Rejected, result.Value!.FinalState);
        Assert.Equal(
            "COMPATIBILITY_STEP_RESULT_INVALID",
            result.Value.Checks.Single(check => check.Kind == CompatibilitySuiteCheckKind.ItemSetFixture)
                .FailureCode);
    }

    [Fact]
    public async Task MissingRequiredCheckFailsBeforeCandidateReadOrExecution()
    {
        var repository = new RecordingRepository(Candidate());
        var executor = new RecordingExecutor();
        RunCandidateCompatibilitySuiteRequest request = Request() with
        {
            Checks = Checks().Where(check =>
                check.Kind != CompatibilitySuiteCheckKind.MarketplaceStructureValidation).ToArray(),
        };

        CandidateCompatibilitySuiteResult result = await Create(repository, executor).RunAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("COMPATIBILITY_SUITE_INCOMPLETE", result.Error!.Code);
        Assert.Equal(0, repository.GetCallCount);
        Assert.Empty(executor.ExecutedKinds);
        Assert.Empty(repository.Transitions);
    }

    [Theory]
    [InlineData("duplicate-id")]
    [InlineData("unsafe-fixture")]
    [InlineData("unsafe-evidence")]
    [InlineData("undefined-kind")]
    public async Task InvalidConfigurationFailsBeforeAnySideEffect(string scenario)
    {
        CompatibilitySuiteCheck[] checks = Checks();
        RunCandidateCompatibilitySuiteRequest request = Request();
        request = scenario switch
        {
            "duplicate-id" => request with { Checks = checks.Append(checks[0]).ToArray() },
            "unsafe-fixture" => request with
            {
                Checks = checks.Select((check, index) => index == 0
                    ? check with { FixtureReference = "../outside" }
                    : check).ToArray(),
            },
            "unsafe-evidence" => request with { EvidenceReference = "C:\\outside.json" },
            _ => request with
            {
                Checks = checks.Select((check, index) => index == 0
                    ? check with { Kind = (CompatibilitySuiteCheckKind)999 }
                    : check).ToArray(),
            },
        };
        var repository = new RecordingRepository(Candidate());
        var executor = new RecordingExecutor();

        CandidateCompatibilitySuiteResult result = await Create(repository, executor).RunAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("COMPATIBILITY_INPUT_INVALID", result.Error!.Code);
        Assert.Equal(0, repository.GetCallCount);
        Assert.Empty(executor.ExecutedKinds);
    }

    [Fact]
    public async Task StaleOrNonCandidateApprovalNeverRunsOrTransitions()
    {
        var repository = new RecordingRepository(Candidate() with
        {
            State = ToolVersionApprovalState.Installed,
        });
        var executor = new RecordingExecutor();

        CandidateCompatibilitySuiteResult result = await Create(repository, executor).RunAsync(
            Request(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("COMPATIBILITY_CANDIDATE_STALE", result.Error!.Code);
        Assert.Empty(executor.ExecutedKinds);
        Assert.Empty(repository.Transitions);
    }

    [Fact]
    public async Task CancellationPropagatesAndDoesNotRecordPartialDecision()
    {
        var repository = new RecordingRepository(Candidate());
        using var cancellation = new CancellationTokenSource();
        var executor = new RecordingExecutor(cancelAfterFirst: cancellation);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Create(repository, executor).RunAsync(Request(), cancellation.Token));

        _ = Assert.Single(executor.ExecutedKinds);
        Assert.Empty(repository.Transitions);
    }

    [Fact]
    public async Task EvidenceDigestIsDeterministicForSameCandidateConfigurationAndResults()
    {
        CandidateCompatibilitySuiteResult first = await Create(
            new RecordingRepository(Candidate()),
            new RecordingExecutor()).RunAsync(Request(), TestContext.Current.CancellationToken);
        CandidateCompatibilitySuiteResult second = await Create(
            new RecordingRepository(Candidate()),
            new RecordingExecutor()).RunAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(first.Value!.EvidenceSha256, second.Value!.EvidenceSha256);
    }

    [Fact]
    public async Task RepositoryFailureIsSanitizedAndDoesNotReportPromotion()
    {
        var repository = new RecordingRepository(Candidate()) { FailTransition = true };

        CandidateCompatibilitySuiteResult result = await Create(repository).RunAsync(
            Request(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("COMPATIBILITY_PERSISTENCE_FAILED", result.Error!.Code);
        Assert.Equal("The approval transition could not be saved.", result.Error.Message);
        Assert.Null(result.Value);
    }

    private static CandidateCompatibilitySuiteRunner Create(
        RecordingRepository repository,
        RecordingExecutor? executor = null) =>
        new(repository, executor ?? new RecordingExecutor(), new FixedClock(_completedAt));

    private static RunCandidateCompatibilitySuiteRequest Request() => new(
        "unity-6000.3.10f1",
        2,
        "suite-0310",
        "transition-suite-0310",
        "reports/compatibility/suite-0310.json",
        Checks());

    private static CompatibilitySuiteCheck[] Checks() =>
        [.. Enum.GetValues<CompatibilitySuiteCheckKind>()
            .Select(kind => new CompatibilitySuiteCheck(
                "check-" + kind.ToString().ToLowerInvariant(),
                kind,
                "fixtures/" + kind.ToString().ToLowerInvariant()))];

    private static PersistedToolVersionApproval Candidate() => new(
        "unity-6000.3.10f1",
        ToolVersion.Create(ToolKind.Unity, "6000.3.10f1").Value!,
        ToolVersionApprovalState.Candidate,
        ["windows-il2cpp"],
        "unity-installation-1",
        new DateTimeOffset(2026, 8, 5, 15, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 8, 5, 15, 30, 0, TimeSpan.Zero),
        2);

    private sealed class FixedClock(DateTimeOffset value) : ICompatibilitySuiteClock
    {
        public DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RecordingExecutor(
        IReadOnlyDictionary<CompatibilitySuiteCheckKind, string>? failures = null,
        CompatibilitySuiteCheckKind? throwOn = null,
        CompatibilitySuiteCheckKind? invalidOn = null,
        CancellationTokenSource? cancelAfterFirst = null) : ICompatibilitySuiteCheckExecutor
    {
        public List<CompatibilitySuiteCheckKind> ExecutedKinds { get; } = [];

        public Task<CompatibilitySuiteCheckExecutionResult> ExecuteAsync(
            PersistedToolVersionApproval candidate,
            CompatibilitySuiteCheck check,
            string runId,
            CancellationToken cancellationToken = default)
        {
            ExecutedKinds.Add(check.Kind);
            if (check.Kind == throwOn)
            {
                throw new InvalidOperationException("Sensitive engine details.");
            }

            CompatibilitySuiteCheckExecutionResult result = check.Kind == invalidOn
                ? new CompatibilitySuiteCheckExecutionResult(true, "CONTRADICTORY")
                : failures is not null && failures.TryGetValue(check.Kind, out string? failure)
                    ? CompatibilitySuiteCheckExecutionResult.Fail(failure)
                    : CompatibilitySuiteCheckExecutionResult.Pass();
            if (ExecutedKinds.Count == 1)
            {
                cancelAfterFirst?.Cancel();
            }

            return Task.FromResult(result);
        }
    }

    private sealed class RecordingRepository(PersistedToolVersionApproval candidate)
        : IToolVersionApprovalRepository
    {
        public int GetCallCount { get; private set; }

        public bool FailTransition { get; init; }

        public List<TransitionCall> Transitions { get; } = [];

        public Task<RepositoryOperationResult<PersistedToolVersionApproval?>> GetAsync(
            string approvalId,
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            return Task.FromResult(RepositoryOperationResult.Success<PersistedToolVersionApproval?>(candidate));
        }

        public Task<RepositoryOperationResult<PersistedToolVersionApproval>> TransitionAsync(
            string approvalId,
            long expectedRevision,
            ToolVersionApprovalState expectedState,
            ToolVersionApprovalState targetState,
            string transitionId,
            DateTimeOffset occurredAtUtc,
            PersistedCompatibilitySuiteResult? compatibilityResult = null,
            CancellationToken cancellationToken = default)
        {
            Transitions.Add(new TransitionCall(
                expectedState,
                targetState,
                compatibilityResult!));
            return Task.FromResult(FailTransition
                ? RepositoryOperationResult.Failure<PersistedToolVersionApproval>(
                    "REPOSITORY_WRITE_FAILED",
                    "The approval transition could not be saved.")
                : RepositoryOperationResult.Success(candidate with
                {
                    State = targetState,
                    UpdatedAtUtc = occurredAtUtc,
                    Revision = expectedRevision + 1,
                }));
        }

        public Task<RepositoryOperationResult> CreateAsync(
            PersistedToolVersionApproval approval,
            string initialTransitionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RepositoryOperationResult<IReadOnlyList<PersistedToolVersionApproval>>> QueryAsync(
            ToolKind? tool = null,
            ToolVersionApprovalState? state = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RepositoryOperationResult<IReadOnlyList<PersistedToolVersionApprovalTransition>>> GetHistoryAsync(
            string approvalId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed record TransitionCall(
        ToolVersionApprovalState ExpectedState,
        ToolVersionApprovalState TargetState,
        PersistedCompatibilitySuiteResult Evidence);
}
