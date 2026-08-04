using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Application.Orchestration;

/// <summary>
/// Deterministic no-engine worker used by the first vertical slice and automated orchestration
/// tests. It performs no filesystem, process, network, or promotion operation.
/// </summary>
public sealed class DeterministicFakeBuildJobWorker(
    BuildJobState? failureStage = null,
    BuildJobState? interruptionStage = null,
    bool requestInspectionReview = false) : IBuildJobStageWorker
{
    private readonly BuildJobState? _failureStage = failureStage;
    private readonly BuildJobState? _interruptionStage = interruptionStage;
    private readonly bool _requestInspectionReview = requestInspectionReview;

    public Task<BuildStageExecutionResult> ExecuteAsync(
        BuildJobId jobId,
        BuildJobState stage,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobId);
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        cancellationToken.ThrowIfCancellationRequested();

        BuildStageExecutionResult result = stage.Equals(_interruptionStage)
            ? BuildStageExecutionResult.Interrupted()
            : stage.Equals(_failureStage)
            ? BuildStageExecutionResult.Failure("FAKE_WORKER_FAILED")
            : _requestInspectionReview && stage.Equals(BuildJobState.Inspecting)
            ? BuildStageExecutionResult.ReviewRequired()
            : BuildStageExecutionResult.Success();
        return Task.FromResult(result);
    }
}
