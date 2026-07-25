using System.Collections.ObjectModel;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contracts.Workers;

public enum WorkerEventKind
{
    Progress = 0,
    Finding,
    Metric,
}

public enum WorkerResultStatus
{
    Success = 0,
    Failure,
    Cancelled,
}

public enum WorkerRetrySafety
{
    Safe = 0,
    Unsafe,
    RequiresCleanup,
}

public enum WorkerMetricUnit
{
    Milliseconds = 0,
    Bytes,
    Count,
    Percent,
}

public enum WorkerCancellationOutcome
{
    Acknowledged = 0,
    Partial,
}

public sealed class WorkerRequest(
    int protocolVersion,
    BuildJobId jobId,
    string operation,
    string productManifestReference,
    string inputDirectoryReference,
    string outputDirectoryReference,
    string resultFileReference,
    string? engineVersion = null,
    BuildTarget? target = null)
{
    public int ProtocolVersion { get; } = protocolVersion;
    public BuildJobId JobId { get; } = jobId;
    public string Operation { get; } = operation;
    public string ProductManifestReference { get; } = productManifestReference;
    public string InputDirectoryReference { get; } = inputDirectoryReference;
    public string OutputDirectoryReference { get; } = outputDirectoryReference;
    public string ResultFileReference { get; } = resultFileReference;
    public string? EngineVersion { get; } = engineVersion;
    public BuildTarget? Target { get; } = target;
}

public sealed class WorkerMetric(string metricId, double value, WorkerMetricUnit unit)
{
    public string MetricId { get; } = metricId;
    public double Value { get; } = value;
    public WorkerMetricUnit Unit { get; } = unit;
}

public sealed class WorkerProgressEvent(
    int protocolVersion,
    WorkerEventKind eventKind,
    BuildJobId jobId,
    string? stage = null,
    string? message = null,
    double? percent = null,
    ValidationFinding? finding = null,
    WorkerMetric? metric = null)
{
    public int ProtocolVersion { get; } = protocolVersion;
    public WorkerEventKind EventKind { get; } = eventKind;
    public BuildJobId JobId { get; } = jobId;
    public string? Stage { get; } = stage;
    public string? Message { get; } = message;
    public double? Percent { get; } = percent;
    public ValidationFinding? Finding { get; } = finding;
    public WorkerMetric? Metric { get; } = metric;
}

public sealed class WorkerArtifact(
    BuildArtifactId artifactId,
    BuildJobId jobId,
    BuildArtifactRole role,
    string logicalReference,
    BuildTarget? target = null,
    string? sha256 = null,
    long? byteCount = null)
{
    public BuildArtifactId ArtifactId { get; } = artifactId;
    public BuildJobId JobId { get; } = jobId;
    public BuildArtifactRole Role { get; } = role;
    public string LogicalReference { get; } = logicalReference;
    public BuildTarget? Target { get; } = target;
    public string? Sha256 { get; } = sha256;
    public long? ByteCount { get; } = byteCount;
}

public sealed class WorkerCancellation(WorkerCancellationOutcome outcome, string? details = null)
{
    public WorkerCancellationOutcome Outcome { get; } = outcome;
    public string? Details { get; } = details;
}

public sealed class WorkerResult(
    int protocolVersion,
    BuildJobId jobId,
    WorkerResultStatus status,
    string workerVersion,
    bool outputsPromoted,
    IEnumerable<WorkerArtifact> artifacts,
    IEnumerable<ValidationFinding> findings,
    IEnumerable<WorkerMetric> metrics,
    IEnumerable<string> logReferences,
    WorkerRetrySafety retrySafety,
    string? engineVersion = null,
    WorkerCancellation? cancellation = null)
{
    public int ProtocolVersion { get; } = protocolVersion;
    public BuildJobId JobId { get; } = jobId;
    public WorkerResultStatus Status { get; } = status;
    public string WorkerVersion { get; } = workerVersion;
    public string? EngineVersion { get; } = engineVersion;
    public bool OutputsPromoted { get; } = outputsPromoted;
    public IReadOnlyList<WorkerArtifact> Artifacts { get; } = Snapshot(artifacts);
    public IReadOnlyList<ValidationFinding> Findings { get; } = Snapshot(findings);
    public IReadOnlyList<WorkerMetric> Metrics { get; } = Snapshot(metrics);
    public IReadOnlyList<string> LogReferences { get; } = Snapshot(logReferences);
    public WorkerRetrySafety RetrySafety { get; } = retrySafety;
    public WorkerCancellation? Cancellation { get; } = cancellation;

    private static ReadOnlyCollection<T> Snapshot<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());
}
