using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Contracts.Workers;

internal static class WorkerJsonTokens
{
    public static string EventKind(WorkerEventKind value) => value switch
    {
        WorkerEventKind.Progress => "progress",
        WorkerEventKind.Finding => "finding",
        WorkerEventKind.Metric => "metric",
        _ => string.Empty,
    };

    public static WorkerEventKind ParseEventKind(string value) => value switch
    {
        "progress" => WorkerEventKind.Progress,
        "finding" => WorkerEventKind.Finding,
        _ => WorkerEventKind.Metric,
    };

    public static string ResultStatus(WorkerResultStatus value) => value switch
    {
        WorkerResultStatus.Success => "success",
        WorkerResultStatus.Failure => "failure",
        WorkerResultStatus.Cancelled => "cancelled",
        _ => string.Empty,
    };

    public static WorkerResultStatus ParseResultStatus(string value) => value switch
    {
        "success" => WorkerResultStatus.Success,
        "failure" => WorkerResultStatus.Failure,
        _ => WorkerResultStatus.Cancelled,
    };

    public static string RetrySafety(WorkerRetrySafety value) => value switch
    {
        WorkerRetrySafety.Safe => "safe",
        WorkerRetrySafety.Unsafe => "unsafe",
        WorkerRetrySafety.RequiresCleanup => "requires-cleanup",
        _ => string.Empty,
    };

    public static WorkerRetrySafety ParseRetrySafety(string value) => value switch
    {
        "safe" => WorkerRetrySafety.Safe,
        "unsafe" => WorkerRetrySafety.Unsafe,
        _ => WorkerRetrySafety.RequiresCleanup,
    };

    public static string MetricUnit(WorkerMetricUnit value) => value switch
    {
        WorkerMetricUnit.Milliseconds => "milliseconds",
        WorkerMetricUnit.Bytes => "bytes",
        WorkerMetricUnit.Count => "count",
        WorkerMetricUnit.Percent => "percent",
        _ => string.Empty,
    };

    public static WorkerMetricUnit ParseMetricUnit(string value) => value switch
    {
        "milliseconds" => WorkerMetricUnit.Milliseconds,
        "bytes" => WorkerMetricUnit.Bytes,
        "count" => WorkerMetricUnit.Count,
        _ => WorkerMetricUnit.Percent,
    };

    public static string CancellationOutcome(WorkerCancellationOutcome value) => value switch
    {
        WorkerCancellationOutcome.Acknowledged => "acknowledged",
        WorkerCancellationOutcome.Partial => "partial",
        _ => string.Empty,
    };

    public static WorkerCancellationOutcome ParseCancellationOutcome(string value) =>
        value == "acknowledged"
            ? WorkerCancellationOutcome.Acknowledged
            : WorkerCancellationOutcome.Partial;

    public static BuildTarget ParseTarget(string value) => value switch
    {
        "portable" => BuildTarget.Portable,
        "unity" => BuildTarget.Unity,
        _ => BuildTarget.Unreal,
    };
}
