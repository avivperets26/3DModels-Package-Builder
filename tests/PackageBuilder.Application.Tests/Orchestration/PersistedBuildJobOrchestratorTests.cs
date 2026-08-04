using PackageBuilder.Application.Orchestration;
using PackageBuilder.Contracts.Logging;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Application.Tests.Orchestration;

public sealed class PersistedBuildJobOrchestratorTests
{
    private static readonly BuildJobState[] _executionStages =
    [
        BuildJobState.Preflight,
        BuildJobState.Inspecting,
        BuildJobState.Normalizing,
        BuildJobState.BuildingTargets,
        BuildJobState.RenderingPreviews,
        BuildJobState.Validating,
        BuildJobState.PackagingMarketplace,
        BuildJobState.CleanReimport,
    ];

    [Fact]
    public async Task FakeWorkerJobCompletesEntirePersistedStateMachine()
    {
        var repository = new InMemoryBuildJobRepository();
        var promoter = new RecordingPromoter();
        var logger = new RecordingLogWriter();
        PersistedBuildJobOrchestrator orchestrator = Create(
            repository,
            new DeterministicFakeBuildJobWorker(),
            promoter,
            logger);

        BuildJobOrchestrationResult result = await orchestrator.StartAsync(
            Request(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(BuildJobState.Completed, result.Value!.State);
        Assert.Equal(_executionStages, result.Value.ExecutedStages);
        Assert.True(result.Value.ReleasePromoted);
        Assert.False(result.Value.Resumed);
        Assert.Equal(
            [
                "queued->preflight",
                "preflight->inspecting",
                "inspecting->normalizing",
                "normalizing->building-targets",
                "building-targets->rendering-previews",
                "rendering-previews->validating",
                "validating->packaging-marketplace",
                "packaging-marketplace->clean-reimport",
                "clean-reimport->completed",
            ],
            repository.Transitions);
        Assert.Equal(8, logger.Events.Count);
        Assert.All(logger.Events, entry => Assert.Equal("Correlation-0213", entry.CorrelationId));
        Assert.Equal(1, promoter.CallCount);
    }

    [Fact]
    public async Task NewOrchestratorResumesFromExactPersistedStageAfterInterruption()
    {
        var repository = new InMemoryBuildJobRepository();
        BuildJobOrchestrationResult interrupted = await Create(
            repository,
            new DeterministicFakeBuildJobWorker(interruptionStage: BuildJobState.Normalizing))
            .StartAsync(Request(), TestContext.Current.CancellationToken);
        var promoter = new RecordingPromoter();

        BuildJobOrchestrationResult resumed = await Create(
            repository,
            new DeterministicFakeBuildJobWorker(),
            promoter)
            .ResumeAsync(JobId(), "Correlation-Restart", TestContext.Current.CancellationToken);

        Assert.True(interrupted.IsSuccess);
        Assert.Equal(BuildJobState.Normalizing, interrupted.Value!.State);
        Assert.False(interrupted.Value.ReleasePromoted);
        Assert.True(resumed.IsSuccess);
        Assert.True(resumed.Value!.Resumed);
        Assert.Equal(BuildJobState.Completed, resumed.Value.State);
        Assert.Equal(_executionStages.Skip(2), resumed.Value.ExecutedStages);
        Assert.Equal(1, promoter.CallCount);
    }

    [Theory]
    [MemberData(nameof(ExecutionStages))]
    public async Task FailureAtEveryStagePersistsFailedAndNeverPromotes(string failureStageIdentifier)
    {
        var repository = new InMemoryBuildJobRepository();
        var promoter = new RecordingPromoter();
        BuildJobState failureStage = BuildJobState.TryParse(failureStageIdentifier).Value!;

        BuildJobOrchestrationResult result = await Create(
            repository,
            new DeterministicFakeBuildJobWorker(failureStage),
            promoter)
            .StartAsync(Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(BuildJobState.Failed, result.Value!.State);
        Assert.False(result.Value.ReleasePromoted);
        Assert.Equal(0, promoter.CallCount);
        Assert.Equal("FAKE_WORKER_FAILED", repository.Current!.FailureCode);
        Assert.DoesNotContain("clean-reimport->completed", repository.Transitions);
    }

    [Fact]
    public async Task PromotionFailurePersistsFailedAndDoesNotClaimPromotion()
    {
        var repository = new InMemoryBuildJobRepository();
        var promoter = new RecordingPromoter(BuildJobPromotionResult.Failure("PROMOTION_COLLISION"));

        BuildJobOrchestrationResult result = await Create(
            repository,
            new DeterministicFakeBuildJobWorker(),
            promoter)
            .StartAsync(Request(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(BuildJobState.Failed, result.Value!.State);
        Assert.False(result.Value.ReleasePromoted);
        Assert.Equal("PROMOTION_COLLISION", repository.Current!.FailureCode);
        Assert.Equal(1, promoter.CallCount);
    }

    [Fact]
    public async Task UnexpectedWorkerAndPromoterExceptionsFailClosedWithoutDiagnosticsLeak()
    {
        var workerRepository = new InMemoryBuildJobRepository();
        BuildJobOrchestrationResult workerResult = await Create(
            workerRepository,
            new ThrowingWorker())
            .StartAsync(Request(), TestContext.Current.CancellationToken);
        var promotionRepository = new InMemoryBuildJobRepository();
        BuildJobOrchestrationResult promotionResult = await Create(
            promotionRepository,
            new DeterministicFakeBuildJobWorker(),
            new ThrowingPromoter())
            .StartAsync(Request("Job-Promotion"), TestContext.Current.CancellationToken);

        Assert.True(workerResult.IsSuccess);
        Assert.Equal(BuildJobState.Failed, workerResult.Value!.State);
        Assert.Equal("WORKER_UNEXPECTED_FAILURE", workerRepository.Current!.FailureCode);
        Assert.True(promotionResult.IsSuccess);
        Assert.Equal(BuildJobState.Failed, promotionResult.Value!.State);
        Assert.Equal("PROMOTION_UNEXPECTED_FAILURE", promotionRepository.Current!.FailureCode);
    }

    [Fact]
    public async Task ReviewRequiresExplicitContinuationAndThenCompletes()
    {
        var repository = new InMemoryBuildJobRepository();
        var worker = new ReviewOnceWorker();
        PersistedBuildJobOrchestrator orchestrator = Create(repository, worker);

        BuildJobOrchestrationResult review = await orchestrator.StartAsync(
            Request(),
            TestContext.Current.CancellationToken);
        BuildJobOrchestrationResult ordinaryResume = await orchestrator.ResumeAsync(
            JobId(),
            "Correlation-Resume",
            TestContext.Current.CancellationToken);
        BuildJobOrchestrationResult continued = await orchestrator.ContinueAfterReviewAsync(
            JobId(),
            "Correlation-Review",
            TestContext.Current.CancellationToken);

        Assert.Equal(BuildJobState.AwaitingReview, review.Value!.State);
        Assert.Equal(BuildJobState.AwaitingReview, ordinaryResume.Value!.State);
        Assert.Empty(ordinaryResume.Value.ExecutedStages);
        Assert.Equal(BuildJobState.Completed, continued.Value!.State);
        Assert.Contains("awaiting-review->inspecting", repository.Transitions);
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("failed")]
    [InlineData("cancelled")]
    public async Task TerminalResumeIsIdempotentAndPerformsNoWork(string terminalState)
    {
        BuildJobState state = BuildJobState.TryParse(terminalState).Value!;
        var repository = new InMemoryBuildJobRepository();
        repository.Seed(state, state.Equals(BuildJobState.Failed) ? "SEEDED_FAILURE" : null);
        var worker = new RecordingWorker();
        var promoter = new RecordingPromoter();

        BuildJobOrchestrationResult result = await Create(repository, worker, promoter)
            .ResumeAsync(JobId(), "Correlation-Terminal", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(state, result.Value!.State);
        Assert.Empty(result.Value.ExecutedStages);
        Assert.Equal(0, worker.CallCount);
        Assert.Equal(0, promoter.CallCount);
    }

    [Fact]
    public async Task MissingJobAndRepositoryFailuresReturnSanitizedErrors()
    {
        var missingRepository = new InMemoryBuildJobRepository();
        BuildJobOrchestrationResult missing = await Create(
            missingRepository,
            new DeterministicFakeBuildJobWorker())
            .ResumeAsync(JobId(), "Correlation-Missing", TestContext.Current.CancellationToken);
        var failingRepository = new InMemoryBuildJobRepository { FailReads = true };
        BuildJobOrchestrationResult failed = await Create(
            failingRepository,
            new DeterministicFakeBuildJobWorker())
            .ResumeAsync(JobId(), "Correlation-Failed", TestContext.Current.CancellationToken);

        Assert.False(missing.IsSuccess);
        Assert.Equal("ORCHESTRATION_JOB_NOT_FOUND", missing.Error!.Code);
        Assert.False(failed.IsSuccess);
        Assert.Equal("ORCHESTRATION_PERSISTENCE_FAILED", failed.Error!.Code);
        Assert.DoesNotContain("C:\\", failed.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreationAndTransitionFailuresStopBeforeUnauthorizedWork()
    {
        var creationRepository = new InMemoryBuildJobRepository { FailCreates = true };
        var creationWorker = new RecordingWorker();
        BuildJobOrchestrationResult creation = await Create(creationRepository, creationWorker)
            .StartAsync(Request(), TestContext.Current.CancellationToken);
        var transitionRepository = new InMemoryBuildJobRepository { FailTransitions = true };
        var transitionWorker = new RecordingWorker();
        BuildJobOrchestrationResult transition = await Create(transitionRepository, transitionWorker)
            .StartAsync(Request("Job-Transition"), TestContext.Current.CancellationToken);

        Assert.False(creation.IsSuccess);
        Assert.False(transition.IsSuccess);
        Assert.Equal(0, creationWorker.CallCount);
        Assert.Equal(0, transitionWorker.CallCount);
    }

    [Fact]
    public async Task LoggingFailureStopsBeforeWorkerAndLeavesResumableState()
    {
        var repository = new InMemoryBuildJobRepository();
        var worker = new RecordingWorker();
        var logger = new RecordingLogWriter { FailWrites = true };

        BuildJobOrchestrationResult result = await Create(repository, worker, logger: logger)
            .StartAsync(Request(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ORCHESTRATION_LOG_FAILED", result.Error!.Code);
        Assert.Equal(BuildJobState.Preflight, repository.Current!.State);
        Assert.Equal(0, worker.CallCount);
    }

    [Fact]
    public async Task CancellationAtPreflightPersistsCancelledAndNeverPromotes()
    {
        var repository = new InMemoryBuildJobRepository();
        using var cancellation = new CancellationTokenSource();
        var worker = new CancellingWorker(cancellation);
        var promoter = new RecordingPromoter();

        BuildJobOrchestrationResult result = await Create(repository, worker, promoter)
            .StartAsync(Request(), cancellation.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(BuildJobState.Cancelled, result.Value!.State);
        Assert.False(result.Value.ReleasePromoted);
        Assert.Equal(0, promoter.CallCount);
    }

    [Fact]
    public async Task CancellationAtNonCancellableStageLeavesExactStateForResume()
    {
        var repository = new InMemoryBuildJobRepository();
        repository.Seed(BuildJobState.Normalizing);
        using var cancellation = new CancellationTokenSource();
        var worker = new CancellingWorker(cancellation);

        BuildJobOrchestrationResult interrupted = await Create(repository, worker)
            .ResumeAsync(JobId(), "Correlation-Cancel", cancellation.Token);
        BuildJobOrchestrationResult resumed = await Create(
            repository,
            new DeterministicFakeBuildJobWorker())
            .ResumeAsync(JobId(), "Correlation-Resume", TestContext.Current.CancellationToken);

        Assert.Equal(BuildJobState.Normalizing, interrupted.Value!.State);
        Assert.Equal(BuildJobState.Completed, resumed.Value!.State);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("unknown-disposition")]
    [InlineData("missing-failure-code")]
    [InlineData("unexpected-failure-code")]
    [InlineData("review-from-wrong-stage")]
    public async Task ContradictoryWorkerResultsFailClosedWithoutTransition(string scenario)
    {
        var repository = new InMemoryBuildJobRepository();
        if (scenario == "review-from-wrong-stage")
        {
            repository.Seed(BuildJobState.Preflight);
        }

        var worker = new InvalidResultWorker(scenario);

        BuildJobOrchestrationResult result = scenario == "review-from-wrong-stage"
            ? await Create(repository, worker).ResumeAsync(
                JobId(),
                "Correlation-Invalid",
                TestContext.Current.CancellationToken)
            : await Create(repository, worker).StartAsync(
                Request(),
                TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("ORCHESTRATION_WORKER_RESULT_INVALID", result.Error!.Code);
        Assert.NotEqual(BuildJobState.Completed, repository.Current!.State);
    }

    [Fact]
    public async Task InvalidInputsAreRejectedBeforePersistence()
    {
        var repository = new InMemoryBuildJobRepository();
        PersistedBuildJobOrchestrator orchestrator = Create(
            repository,
            new DeterministicFakeBuildJobWorker());

        BuildJobOrchestrationResult missingCorrelation = await orchestrator.StartAsync(
            new StartBuildJobRequest(JobId(), " "),
            TestContext.Current.CancellationToken);
        BuildJobOrchestrationResult missingJob = await orchestrator.ResumeAsync(
            null!,
            "Correlation",
            TestContext.Current.CancellationToken);

        Assert.False(missingCorrelation.IsSuccess);
        Assert.False(missingJob.IsSuccess);
        Assert.Equal("ORCHESTRATION_INPUT_INVALID", missingCorrelation.Error!.Code);
        Assert.Null(repository.Current);
    }

    [Fact]
    public void PublicOrchestrationBoundariesRejectNullDependenciesAndReceiptValues()
    {
        var repository = new InMemoryBuildJobRepository();
        var worker = new DeterministicFakeBuildJobWorker();
        var promoter = new RecordingPromoter();
        var logger = new RecordingLogWriter();
        var clock = new IncrementingClock();

        _ = Assert.Throws<ArgumentNullException>(() =>
            new PersistedBuildJobOrchestrator(null!, worker, promoter, logger, clock));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new PersistedBuildJobOrchestrator(repository, null!, promoter, logger, clock));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new PersistedBuildJobOrchestrator(repository, worker, null!, logger, clock));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new PersistedBuildJobOrchestrator(repository, worker, promoter, null!, clock));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new PersistedBuildJobOrchestrator(repository, worker, promoter, logger, null!));

        _ = Assert.Throws<ArgumentNullException>(() =>
            new BuildJobOrchestrationReceipt(
                null!, BuildJobState.Queued, "Correlation", [], false, false));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new BuildJobOrchestrationReceipt(JobId(), null!, "Correlation", [], false, false));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new BuildJobOrchestrationReceipt(JobId(), BuildJobState.Queued, null!, [], false, false));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new BuildJobOrchestrationReceipt(
                JobId(), BuildJobState.Queued, "Correlation", null!, false, false));
    }

    [Fact]
    public async Task NonUtcClockIsRejectedBeforeAStageCanRun()
    {
        var repository = new InMemoryBuildJobRepository();
        repository.Seed(BuildJobState.Preflight);
        var worker = new RecordingWorker();
        var localOffset = new DateTimeOffset(2026, 8, 4, 14, 0, 0, TimeSpan.FromHours(2));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create(repository, worker, clock: new FixedClock(localOffset))
                .ResumeAsync(JobId(), "Correlation-Clock", TestContext.Current.CancellationToken));

        Assert.Equal(0, worker.CallCount);
    }

    [Fact]
    public async Task BackwardsClockIsClampedToPersistedTimestamp()
    {
        var repository = new InMemoryBuildJobRepository();
        DateTimeOffset persisted = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);
        repository.Seed(BuildJobState.Normalizing, updatedAtUtc: persisted);
        var clock = new FixedClock(persisted.AddHours(-1));

        BuildJobOrchestrationResult result = await Create(
            repository,
            new DeterministicFakeBuildJobWorker(),
            clock: clock)
            .ResumeAsync(JobId(), "Correlation-Clock", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.All(repository.TransitionTimes, value => Assert.True(value >= persisted));
    }

    [Fact]
    public async Task FakeWorkerValidatesCancellationAndConfiguredBehaviors()
    {
        var failure = new DeterministicFakeBuildJobWorker(BuildJobState.Preflight);
        var interruption = new DeterministicFakeBuildJobWorker(interruptionStage: BuildJobState.Preflight);
        var review = new DeterministicFakeBuildJobWorker(requestInspectionReview: true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Equal(
            BuildStageExecutionDisposition.Failed,
            (await failure.ExecuteAsync(
                JobId(),
                BuildJobState.Preflight,
                "Correlation",
                TestContext.Current.CancellationToken)).Disposition);
        Assert.Equal(
            BuildStageExecutionDisposition.Interrupted,
            (await interruption.ExecuteAsync(
                JobId(),
                BuildJobState.Preflight,
                "Correlation",
                TestContext.Current.CancellationToken)).Disposition);
        Assert.Equal(
            BuildStageExecutionDisposition.AwaitingReview,
            (await review.ExecuteAsync(
                JobId(),
                BuildJobState.Inspecting,
                "Correlation",
                TestContext.Current.CancellationToken)).Disposition);
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            failure.ExecuteAsync(JobId(), BuildJobState.Preflight, "Correlation", cancellation.Token));
    }

    public static TheoryData<string> ExecutionStages() =>
        [.. _executionStages.Select(stage => stage.CanonicalIdentifier)];

    private static PersistedBuildJobOrchestrator Create(
        InMemoryBuildJobRepository repository,
        IBuildJobStageWorker worker,
        IBuildJobReleasePromoter? promoter = null,
        RecordingLogWriter? logger = null,
        IBuildJobClock? clock = null) =>
        new(
            repository,
            worker,
            promoter ?? new RecordingPromoter(),
            logger ?? new RecordingLogWriter(),
            clock ?? new IncrementingClock());

    private static StartBuildJobRequest Request(string id = "Job-PB0213") =>
        new(BuildJobId.Create(id).Value!, "Correlation-0213", "Product-Version-01");

    private static BuildJobId JobId() => BuildJobId.Create("Job-PB0213").Value!;

    private sealed class InMemoryBuildJobRepository : IBuildJobRepository
    {
        public PersistedBuildJob? Current { get; private set; }

        public List<string> Transitions { get; } = [];

        public List<DateTimeOffset> TransitionTimes { get; } = [];

        public bool FailCreates { get; init; }

        public bool FailReads { get; init; }

        public bool FailTransitions { get; init; }

        public Task<RepositoryOperationResult> CreateAsync(
            PersistedBuildJob job,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailCreates)
            {
                return Task.FromResult(RepositoryOperationResult.Failure(
                    "REPOSITORY_STORAGE_FAILED",
                    "The persisted job could not be created."));
            }

            if (Current is not null)
            {
                return Task.FromResult(RepositoryOperationResult.Failure(
                    "REPOSITORY_JOB_CONFLICT",
                    "The persisted job already exists."));
            }

            Current = job;
            return Task.FromResult(RepositoryOperationResult.Success());
        }

        public Task<RepositoryOperationResult<PersistedBuildJob?>> GetAsync(
            BuildJobId jobId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(FailReads
                ? RepositoryOperationResult.Failure<PersistedBuildJob?>(
                    "REPOSITORY_STORAGE_FAILED",
                    "The persisted job could not be read.")
                : RepositoryOperationResult.Success<PersistedBuildJob?>(Current));
        }

        public Task<RepositoryOperationResult<IReadOnlyList<PersistedBuildJob>>> QueryAsync(
            BuildJobState? state = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RepositoryOperationResult.Success<IReadOnlyList<PersistedBuildJob>>(
                Current is not null && (state is null || Current.State.Equals(state)) ? [Current] : []));

        public Task<RepositoryOperationResult<IReadOnlyList<PersistedBuildJob>>> GetResumableAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RepositoryOperationResult.Success<IReadOnlyList<PersistedBuildJob>>(
                Current is { State.IsTerminal: false } ? [Current] : []));

        public Task<RepositoryOperationResult<PersistedBuildJob>> TransitionAsync(
            BuildJobId jobId,
            BuildJobState expectedState,
            BuildJobState targetState,
            DateTimeOffset occurredAtUtc,
            string? failureCode = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailTransitions || Current is null || !Current.State.Equals(expectedState))
            {
                return Task.FromResult(RepositoryOperationResult.Failure<PersistedBuildJob>(
                    "REPOSITORY_STATE_CONFLICT",
                    "The persisted transition was rejected."));
            }

            Assert.True(BuildJobTransitionPolicy.IsApproved(expectedState, targetState));
            Current = Current with
            {
                State = targetState,
                UpdatedAtUtc = occurredAtUtc,
                FailureCode = failureCode,
            };
            Transitions.Add($"{expectedState}->{targetState}");
            TransitionTimes.Add(occurredAtUtc);
            return Task.FromResult(RepositoryOperationResult.Success(Current));
        }

        public void Seed(
            BuildJobState state,
            string? failureCode = null,
            DateTimeOffset? updatedAtUtc = null)
        {
            DateTimeOffset timestamp = updatedAtUtc ??
                new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);
            Current = new PersistedBuildJob(JobId(), null, state, timestamp, timestamp, failureCode);
        }
    }

    private sealed class RecordingLogWriter : IStructuredLogWriter
    {
        public List<StructuredLogEvent> Events { get; } = [];

        public bool FailWrites { get; init; }

        public Task<StructuredLogResult> WriteApplicationAsync(
            StructuredLogEvent logEvent,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(StructuredLogResult.Success());

        public Task<StructuredLogResult> WriteJobAsync(
            BuildJobId jobId,
            StructuredLogEvent logEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(logEvent);
            return Task.FromResult(FailWrites
                ? StructuredLogResult.Failure("LOG_WRITE_FAILED", "The log write failed.")
                : StructuredLogResult.Success());
        }
    }

    private sealed class RecordingPromoter(BuildJobPromotionResult? result = null) : IBuildJobReleasePromoter
    {
        private readonly BuildJobPromotionResult _result = result ?? BuildJobPromotionResult.Success();

        public int CallCount { get; private set; }

        public Task<BuildJobPromotionResult> PromoteAsync(
            BuildJobId jobId,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class IncrementingClock : IBuildJobClock
    {
        private DateTimeOffset _current = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset GetUtcNow()
        {
            _current = _current.AddSeconds(1);
            return _current;
        }
    }

    private sealed class FixedClock(DateTimeOffset value) : IBuildJobClock
    {
        public DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RecordingWorker : IBuildJobStageWorker
    {
        public int CallCount { get; private set; }

        public Task<BuildStageExecutionResult> ExecuteAsync(
            BuildJobId jobId,
            BuildJobState stage,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(BuildStageExecutionResult.Success());
        }
    }

    private sealed class ReviewOnceWorker : IBuildJobStageWorker
    {
        private bool _reviewRequested;

        public Task<BuildStageExecutionResult> ExecuteAsync(
            BuildJobId jobId,
            BuildJobState stage,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            if (!_reviewRequested && stage.Equals(BuildJobState.Inspecting))
            {
                _reviewRequested = true;
                return Task.FromResult(BuildStageExecutionResult.ReviewRequired());
            }

            return Task.FromResult(BuildStageExecutionResult.Success());
        }
    }

    private sealed class CancellingWorker(CancellationTokenSource cancellation) : IBuildJobStageWorker
    {
        public Task<BuildStageExecutionResult> ExecuteAsync(
            BuildJobId jobId,
            BuildJobState stage,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(BuildStageExecutionResult.Success());
        }
    }

    private sealed class ThrowingWorker : IBuildJobStageWorker
    {
        public Task<BuildStageExecutionResult> ExecuteAsync(
            BuildJobId jobId,
            BuildJobState stage,
            string correlationId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("private worker detail");
    }

    private sealed class ThrowingPromoter : IBuildJobReleasePromoter
    {
        public Task<BuildJobPromotionResult> PromoteAsync(
            BuildJobId jobId,
            string correlationId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("private promotion detail");
    }

    private sealed class InvalidResultWorker(string scenario) : IBuildJobStageWorker
    {
        public Task<BuildStageExecutionResult> ExecuteAsync(
            BuildJobId jobId,
            BuildJobState stage,
            string correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(scenario switch
            {
                "null" => null!,
                "unknown-disposition" => new BuildStageExecutionResult((BuildStageExecutionDisposition)999),
                "missing-failure-code" => new BuildStageExecutionResult(BuildStageExecutionDisposition.Failed),
                "unexpected-failure-code" => new BuildStageExecutionResult(
                    BuildStageExecutionDisposition.Succeeded,
                    "UNEXPECTED"),
                "review-from-wrong-stage" => BuildStageExecutionResult.ReviewRequired(),
                _ => throw new InvalidOperationException("Unknown test scenario."),
            });
    }
}
