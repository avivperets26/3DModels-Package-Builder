using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contracts.Persistence;

/// <summary>Persisted job metadata used to resume and query work without loading binary assets.</summary>
public sealed record PersistedBuildJob(
    BuildJobId Id,
    string? ProductVersionId,
    BuildJobState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? FailureCode);

/// <summary>Persisted artifact metadata; content remains in the contained artifact store.</summary>
public sealed record PersistedBuildArtifact(
    BuildArtifactId Id,
    BuildJobId JobId,
    BuildStepId? StepId,
    BuildArtifactRole Role,
    BuildTarget? Target,
    string LogicalReference,
    string? Sha256,
    long? ByteCount,
    BuildArtifactLifecycleState LifecycleState,
    DateTimeOffset CreatedAtUtc);

/// <summary>Correlates a typed validation finding with its owning build job.</summary>
public sealed record PersistedValidationFinding(
    string Id,
    BuildJobId JobId,
    ValidationFinding Finding,
    DateTimeOffset CreatedAtUtc);

/// <summary>Describes a discovered tool using logical metadata rather than an executable handle.</summary>
public sealed record PersistedToolInstallation(
    string Id,
    string ToolName,
    string Version,
    string ExecutableReference,
    string? Sha256,
    bool IsApproved,
    DateTimeOffset DiscoveredAtUtc);
