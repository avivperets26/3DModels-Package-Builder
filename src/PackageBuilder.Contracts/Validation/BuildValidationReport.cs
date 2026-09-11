using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contracts.Validation;

/// <summary>Cross-target report input. Status is derived from job state and release blockers,
/// never supplied by a caller. Artifact references are opaque IDs, not workstation paths.</summary>
public sealed record BuildValidationReport(BuildJobId JobId, BuildLock Versions, BuildJobState JobState,
    IReadOnlyList<ValidationReportArtifact> Artifacts, BuildResourceMetrics Resources,
    IReadOnlyList<ValidationReportMetric> Metrics, IReadOnlyList<ValidationFinding> Findings);

/// <summary>Final artifact identity and measured byte count. Hashes describe the delivered bytes.</summary>
public sealed record ValidationReportArtifact(BuildArtifactId Id, string Role, Sha256Digest Sha256, long SizeBytes);

/// <summary>Named nonnegative measurement with explicit units and an optional artifact reference.</summary>
public sealed record ValidationReportMetric(string Name, string Unit, double Value, BuildArtifactId? ArtifactId = null);

/// <summary>Resource evidence in seconds and bytes. Null means unmeasured, never zero;
/// stage durations may overlap and therefore are not required to sum to wall-clock duration.</summary>
public sealed record BuildResourceMetrics(double TotalDurationSeconds, IReadOnlyList<BuildStageDuration> Stages,
    long? PeakProcessMemoryBytes, long? PeakOwnedDiskBytes, long? PeakTemporaryBytes, long? BytesRead, long? BytesWritten);

/// <summary>Measured wall-clock duration of one named stage.</summary>
public sealed record BuildStageDuration(string Stage, double DurationSeconds);

/// <summary>Expected report contract failures return no partial JSON or report.</summary>
public sealed record BuildValidationReportResult(BuildValidationReport? Value, string? Json, string? Error)
{
    public bool IsSuccessful => Error is null;
    internal static BuildValidationReportResult Failure(string error) => new(null, null, error);
}
