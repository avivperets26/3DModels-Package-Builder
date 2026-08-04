using PackageBuilder.Contracts.Logging;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Application.Orchestration;

/// <summary>
/// Drives the authoritative build-job graph through optimistic persisted transitions. Workers
/// execute stages but cannot transition jobs or promote releases directly.
/// </summary>
public sealed class PersistedBuildJobOrchestrator(
    IBuildJobRepository jobRepository,
    IBuildJobStageWorker worker,
    IBuildJobReleasePromoter promoter,
    IStructuredLogWriter logWriter,
    IBuildJobClock clock)
{
    private readonly IBuildJobRepository _jobRepository =
        jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
    private readonly IBuildJobStageWorker _worker =
        worker ?? throw new ArgumentNullException(nameof(worker));
    private readonly IBuildJobReleasePromoter _promoter =
        promoter ?? throw new ArgumentNullException(nameof(promoter));
    private readonly IStructuredLogWriter _logWriter =
        logWriter ?? throw new ArgumentNullException(nameof(logWriter));
    private readonly IBuildJobClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>Persists a queued job before any worker stage begins, then executes it.</summary>
    public async Task<BuildJobOrchestrationResult> StartAsync(
        StartBuildJobRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        BuildJobOrchestrationResult? invalid = ValidateIdentity(request.JobId, request.CorrelationId);
        if (invalid is not null)
        {
            return invalid;
        }

        DateTimeOffset createdAtUtc = GetUtcNow();
        RepositoryOperationResult creation = await _jobRepository.CreateAsync(
            new PersistedBuildJob(
                request.JobId,
                request.ProductVersionId,
                BuildJobState.Queued,
                createdAtUtc,
                createdAtUtc,
                null),
            cancellationToken).ConfigureAwait(false);
        return !creation.IsSuccess
            ? RepositoryFailure(creation.Error)
            : await ExecutePersistedAsync(
            request.JobId,
            request.CorrelationId,
            resumed: false,
            continueAfterReview: false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Continues a nonterminal job from its exact persisted state after restart.</summary>
    public Task<BuildJobOrchestrationResult> ResumeAsync(
        BuildJobId jobId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecutePersistedAsync(
            jobId,
            correlationId,
            resumed: true,
            continueAfterReview: false,
            cancellationToken);

    /// <summary>Explicitly releases an awaiting-review job back to inspection.</summary>
    public Task<BuildJobOrchestrationResult> ContinueAfterReviewAsync(
        BuildJobId jobId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecutePersistedAsync(
            jobId,
            correlationId,
            resumed: true,
            continueAfterReview: true,
            cancellationToken);

    private async Task<BuildJobOrchestrationResult> ExecutePersistedAsync(
        BuildJobId jobId,
        string correlationId,
        bool resumed,
        bool continueAfterReview,
        CancellationToken cancellationToken)
    {
        BuildJobOrchestrationResult? invalid = ValidateIdentity(jobId, correlationId);
        if (invalid is not null)
        {
            return invalid;
        }

        RepositoryOperationResult<PersistedBuildJob?> read = await _jobRepository.GetAsync(
            jobId,
            cancellationToken).ConfigureAwait(false);
        if (!read.IsSuccess)
        {
            return RepositoryFailure(read.Error);
        }

        PersistedBuildJob? job = read.Value;
        if (job is null)
        {
            return BuildJobOrchestrationResult.Failure(
                "ORCHESTRATION_JOB_NOT_FOUND",
                "The requested persisted job does not exist.");
        }

        var executedStages = new List<BuildJobState>();
        bool releasePromoted = false;
        if (job.State.IsTerminal)
        {
            return Success(job, correlationId, executedStages, releasePromoted, resumed);
        }

        if (job.State.Equals(BuildJobState.AwaitingReview))
        {
            if (!continueAfterReview)
            {
                return Success(job, correlationId, executedStages, releasePromoted, resumed);
            }

            RepositoryOperationResult<PersistedBuildJob> reviewTransition = await TransitionAsync(
                job,
                BuildJobState.Inspecting,
                failureCode: null,
                cancellationToken).ConfigureAwait(false);
            if (!reviewTransition.IsSuccess)
            {
                return RepositoryFailure(reviewTransition.Error);
            }

            job = reviewTransition.Value!;
        }

        if (job.State.Equals(BuildJobState.Queued))
        {
            RepositoryOperationResult<PersistedBuildJob> initialTransition = await TransitionAsync(
                job,
                BuildJobState.Preflight,
                failureCode: null,
                cancellationToken).ConfigureAwait(false);
            if (!initialTransition.IsSuccess)
            {
                return RepositoryFailure(initialTransition.Error);
            }

            job = initialTransition.Value!;
        }

        while (job.State.IsExecutionStage)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await HandleCancellationAsync(
                    job,
                    correlationId,
                    executedStages,
                    releasePromoted,
                    resumed).ConfigureAwait(false);
            }

            StructuredLogResult logResult = await WriteStageLogAsync(
                job,
                correlationId,
                StructuredLogSeverity.Information,
                "Build stage started.",
                cancellationToken).ConfigureAwait(false);
            if (!logResult.IsSuccess)
            {
                return BuildJobOrchestrationResult.Failure(
                    "ORCHESTRATION_LOG_FAILED",
                    "The build stage could not begin because its required log was not persisted.");
            }

            BuildStageExecutionResult stageResult;
            try
            {
                stageResult = await _worker.ExecuteAsync(
                    job.Id,
                    job.State,
                    correlationId,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return await HandleCancellationAsync(
                    job,
                    correlationId,
                    executedStages,
                    releasePromoted,
                    resumed).ConfigureAwait(false);
            }
            catch (Exception)
            {
                stageResult = BuildStageExecutionResult.Failure("WORKER_UNEXPECTED_FAILURE");
            }

            executedStages.Add(job.State);
            BuildJobOrchestrationResult? invalidWorkerResult = ValidateWorkerResult(job.State, stageResult);
            if (invalidWorkerResult is not null)
            {
                return invalidWorkerResult;
            }

            if (stageResult.Disposition == BuildStageExecutionDisposition.Interrupted)
            {
                return Success(job, correlationId, executedStages, releasePromoted, resumed);
            }

            if (stageResult.Disposition == BuildStageExecutionDisposition.AwaitingReview)
            {
                RepositoryOperationResult<PersistedBuildJob> review = await TransitionAsync(
                    job,
                    BuildJobState.AwaitingReview,
                    failureCode: null,
                    cancellationToken).ConfigureAwait(false);
                return review.IsSuccess
                    ? Success(review.Value!, correlationId, executedStages, releasePromoted, resumed)
                    : RepositoryFailure(review.Error);
            }

            if (stageResult.Disposition == BuildStageExecutionDisposition.Failed)
            {
                return await FailAsync(
                    job,
                    correlationId,
                    executedStages,
                    releasePromoted,
                    resumed,
                    stageResult.FailureCode!).ConfigureAwait(false);
            }

            if (job.State.Equals(BuildJobState.CleanReimport))
            {
                BuildJobPromotionResult promotion;
                try
                {
                    promotion = await _promoter.PromoteAsync(
                        job.Id,
                        correlationId,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return Success(job, correlationId, executedStages, releasePromoted, resumed);
                }
                catch (Exception)
                {
                    promotion = BuildJobPromotionResult.Failure("PROMOTION_UNEXPECTED_FAILURE");
                }

                if (!promotion.IsSuccess)
                {
                    return await FailAsync(
                        job,
                        correlationId,
                        executedStages,
                        releasePromoted,
                        resumed,
                        NormalizeFailureCode(promotion.FailureCode, "PROMOTION_FAILED")).ConfigureAwait(false);
                }

                releasePromoted = true;
                RepositoryOperationResult<PersistedBuildJob> completed = await TransitionAsync(
                    job,
                    BuildJobState.Completed,
                    failureCode: null,
                    cancellationToken).ConfigureAwait(false);
                return completed.IsSuccess
                    ? Success(completed.Value!, correlationId, executedStages, releasePromoted, resumed)
                    : RepositoryFailure(completed.Error);
            }

            BuildJobState nextState = NextExecutionState(job.State);
            RepositoryOperationResult<PersistedBuildJob> transition = await TransitionAsync(
                job,
                nextState,
                failureCode: null,
                cancellationToken).ConfigureAwait(false);
            if (!transition.IsSuccess)
            {
                return RepositoryFailure(transition.Error);
            }

            job = transition.Value!;
        }

        return Success(job, correlationId, executedStages, releasePromoted, resumed);
    }

    private async Task<BuildJobOrchestrationResult> FailAsync(
        PersistedBuildJob job,
        string correlationId,
        IReadOnlyCollection<BuildJobState> executedStages,
        bool releasePromoted,
        bool resumed,
        string failureCode)
    {
        RepositoryOperationResult<PersistedBuildJob> failed = await TransitionAsync(
            job,
            BuildJobState.Failed,
            NormalizeFailureCode(failureCode, "WORKER_FAILED"),
            CancellationToken.None).ConfigureAwait(false);
        return failed.IsSuccess
            ? Success(failed.Value!, correlationId, executedStages, releasePromoted, resumed)
            : RepositoryFailure(failed.Error);
    }

    private async Task<BuildJobOrchestrationResult> HandleCancellationAsync(
        PersistedBuildJob job,
        string correlationId,
        IReadOnlyCollection<BuildJobState> executedStages,
        bool releasePromoted,
        bool resumed)
    {
        if (!BuildJobTransitionPolicy.IsApproved(job.State, BuildJobState.Cancelled))
        {
            return Success(job, correlationId, executedStages, releasePromoted, resumed);
        }

        RepositoryOperationResult<PersistedBuildJob> cancelled = await TransitionAsync(
            job,
            BuildJobState.Cancelled,
            failureCode: null,
            CancellationToken.None).ConfigureAwait(false);
        return cancelled.IsSuccess
            ? Success(cancelled.Value!, correlationId, executedStages, releasePromoted, resumed)
            : RepositoryFailure(cancelled.Error);
    }

    private Task<RepositoryOperationResult<PersistedBuildJob>> TransitionAsync(
        PersistedBuildJob job,
        BuildJobState targetState,
        string? failureCode,
        CancellationToken cancellationToken) =>
        _jobRepository.TransitionAsync(
            job.Id,
            job.State,
            targetState,
            GetUtcNow(job.UpdatedAtUtc),
            failureCode,
            cancellationToken);

    private Task<StructuredLogResult> WriteStageLogAsync(
        PersistedBuildJob job,
        string correlationId,
        StructuredLogSeverity severity,
        string message,
        CancellationToken cancellationToken)
    {
        StructuredLogResult<StructuredLogEvent> logEvent = StructuredLogEvent.Create(
            GetUtcNow(job.UpdatedAtUtc),
            correlationId,
            "job-orchestrator",
            job.State.CanonicalIdentifier,
            severity,
            message);
        return logEvent.IsSuccess
            ? _logWriter.WriteJobAsync(job.Id, logEvent.Value!, cancellationToken)
            : Task.FromResult(StructuredLogResult.Failure(
                logEvent.Error!.Code,
                logEvent.Error.Message));
    }

    private DateTimeOffset GetUtcNow(DateTimeOffset? minimum = null)
    {
        DateTimeOffset value = _clock.GetUtcNow();
        return value.Offset != TimeSpan.Zero
            ? throw new InvalidOperationException("The orchestration clock must return UTC timestamps.")
            : minimum is not null && value < minimum.Value ? minimum.Value : value;
    }

    private static BuildJobOrchestrationResult? ValidateIdentity(
        BuildJobId? jobId,
        string? correlationId) =>
        jobId is null || string.IsNullOrWhiteSpace(correlationId)
            ? BuildJobOrchestrationResult.Failure(
                "ORCHESTRATION_INPUT_INVALID",
                "A job identifier and correlation ID are required.")
            : null;

    private static BuildJobOrchestrationResult? ValidateWorkerResult(
        BuildJobState state,
        BuildStageExecutionResult? result) =>
        result is null || !Enum.IsDefined(result.Disposition)
            ? BuildJobOrchestrationResult.Failure(
                "ORCHESTRATION_WORKER_RESULT_INVALID",
                "The worker returned an invalid stage result.")
            : ValidateDefinedWorkerResult(state, result);

    private static BuildJobOrchestrationResult? ValidateDefinedWorkerResult(
        BuildJobState state,
        BuildStageExecutionResult result) =>
        result.Disposition == BuildStageExecutionDisposition.Failed
            ? IsFailureCode(result.FailureCode) ? null : BuildJobOrchestrationResult.Failure(
                "ORCHESTRATION_WORKER_RESULT_INVALID",
                "The worker failure did not contain a stable failure code.")
            : result.FailureCode is not null ||
            result.Disposition == BuildStageExecutionDisposition.AwaitingReview &&
            !state.Equals(BuildJobState.Inspecting)
            ? BuildJobOrchestrationResult.Failure(
                "ORCHESTRATION_WORKER_RESULT_INVALID",
                "The worker stage result contradicts the persisted state.")
            : null;

    private static BuildJobState NextExecutionState(BuildJobState state) =>
        state.Equals(BuildJobState.Preflight) ? BuildJobState.Inspecting :
        state.Equals(BuildJobState.Inspecting) ? BuildJobState.Normalizing :
        state.Equals(BuildJobState.Normalizing) ? BuildJobState.BuildingTargets :
        state.Equals(BuildJobState.BuildingTargets) ? BuildJobState.RenderingPreviews :
        state.Equals(BuildJobState.RenderingPreviews) ? BuildJobState.Validating :
        state.Equals(BuildJobState.Validating) ? BuildJobState.PackagingMarketplace :
        state.Equals(BuildJobState.PackagingMarketplace) ? BuildJobState.CleanReimport :
        throw new InvalidOperationException("The persisted execution stage has no next stage.");

    private static BuildJobOrchestrationResult Success(
        PersistedBuildJob job,
        string correlationId,
        IEnumerable<BuildJobState> executedStages,
        bool releasePromoted,
        bool resumed) =>
        BuildJobOrchestrationResult.Success(
            new BuildJobOrchestrationReceipt(
                job.Id,
                job.State,
                correlationId,
                executedStages,
                releasePromoted,
                resumed));

    private static BuildJobOrchestrationResult RepositoryFailure(RepositoryOperationError? error) =>
        BuildJobOrchestrationResult.Failure(
            "ORCHESTRATION_PERSISTENCE_FAILED",
            error?.Message ?? "The persisted job operation failed.");

    private static string NormalizeFailureCode(string? value, string fallback) =>
        IsFailureCode(value) ? value! : fallback;

    private static bool IsFailureCode(string? value) =>
        value is { Length: > 0 and <= 128 } &&
        value[0] is >= 'A' and <= 'Z' &&
        value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');
}
