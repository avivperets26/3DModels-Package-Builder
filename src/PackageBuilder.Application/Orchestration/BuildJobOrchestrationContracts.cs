using System.Collections.ObjectModel;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Application.Orchestration;

/// <summary>Provides explicit UTC timestamps so orchestration is deterministic and testable.</summary>
public interface IBuildJobClock
{
    DateTimeOffset GetUtcNow();
}

/// <summary>Executes one persisted job stage without owning state transitions or promotion.</summary>
public interface IBuildJobStageWorker
{
    Task<BuildStageExecutionResult> ExecuteAsync(
        BuildJobId jobId,
        BuildJobState stage,
        string correlationId,
        CancellationToken cancellationToken = default);
}

/// <summary>Promotes a release only after every execution stage has succeeded.</summary>
public interface IBuildJobReleasePromoter
{
    Task<BuildJobPromotionResult> PromoteAsync(
        BuildJobId jobId,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public enum BuildStageExecutionDisposition
{
    Succeeded,
    Failed,
    AwaitingReview,
    Interrupted,
}

/// <summary>Stable result from one worker stage.</summary>
public sealed record BuildStageExecutionResult(
    BuildStageExecutionDisposition Disposition,
    string? FailureCode = null)
{
    public static BuildStageExecutionResult Success() =>
        new(BuildStageExecutionDisposition.Succeeded);

    public static BuildStageExecutionResult Failure(string failureCode) =>
        new(BuildStageExecutionDisposition.Failed, failureCode);

    public static BuildStageExecutionResult ReviewRequired() =>
        new(BuildStageExecutionDisposition.AwaitingReview);

    public static BuildStageExecutionResult Interrupted() =>
        new(BuildStageExecutionDisposition.Interrupted);
}

/// <summary>Stable result from the release-promotion boundary.</summary>
public sealed record BuildJobPromotionResult(bool IsSuccess, string? FailureCode = null)
{
    public static BuildJobPromotionResult Success() => new(true);

    public static BuildJobPromotionResult Failure(string failureCode) => new(false, failureCode);
}

/// <summary>Input required to create and immediately execute a persisted job.</summary>
public sealed record StartBuildJobRequest(
    BuildJobId JobId,
    string CorrelationId,
    string? ProductVersionId = null);

/// <summary>Sanitized expected orchestration failure.</summary>
public sealed record BuildJobOrchestrationError(string Code, string Message);

/// <summary>Observable orchestration receipt safe for CLI and future WPF consumers.</summary>
public sealed class BuildJobOrchestrationReceipt
{
    public BuildJobOrchestrationReceipt(
        BuildJobId jobId,
        BuildJobState state,
        string correlationId,
        IEnumerable<BuildJobState> executedStages,
        bool releasePromoted,
        bool resumed)
    {
        JobId = jobId ?? throw new ArgumentNullException(nameof(jobId));
        State = state ?? throw new ArgumentNullException(nameof(state));
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        ArgumentNullException.ThrowIfNull(executedStages);
        ExecutedStages = new ReadOnlyCollection<BuildJobState>(executedStages.ToArray());
        ReleasePromoted = releasePromoted;
        Resumed = resumed;
    }

    public BuildJobId JobId { get; }

    public BuildJobState State { get; }

    public string CorrelationId { get; }

    public IReadOnlyList<BuildJobState> ExecutedStages { get; }

    public bool ReleasePromoted { get; }

    public bool Resumed { get; }
}

/// <summary>Non-throwing expected result for a complete orchestration request.</summary>
public sealed class BuildJobOrchestrationResult
{
    private BuildJobOrchestrationResult(
        BuildJobOrchestrationReceipt? value,
        BuildJobOrchestrationError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public BuildJobOrchestrationReceipt? Value { get; }

    public BuildJobOrchestrationError? Error { get; }

    public static BuildJobOrchestrationResult Success(BuildJobOrchestrationReceipt value) =>
        new(value, null);

    public static BuildJobOrchestrationResult Failure(string code, string message) =>
        new(null, new BuildJobOrchestrationError(code, message));
}
