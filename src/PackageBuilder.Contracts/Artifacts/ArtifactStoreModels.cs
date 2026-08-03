using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Requests that one source file be staged into the contained artifact store.</summary>
public sealed class ArtifactStoreStageRequest(
    string projectRoot,
    string artifactsRoot,
    string sourceFilePath,
    BuildArtifact artifact)
{
    public string ProjectRoot { get; } = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));

    public string ArtifactsRoot { get; } = artifactsRoot ?? throw new ArgumentNullException(nameof(artifactsRoot));

    public string SourceFilePath { get; } = sourceFilePath ?? throw new ArgumentNullException(nameof(sourceFilePath));

    public BuildArtifact Artifact { get; } = artifact ?? throw new ArgumentNullException(nameof(artifact));
}

/// <summary>Requests the persisted record for one typed job artifact.</summary>
public sealed class ArtifactStoreReadRequest(
    string projectRoot,
    string artifactsRoot,
    BuildJobId jobId,
    BuildArtifactId artifactId)
{
    public string ProjectRoot { get; } = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));

    public string ArtifactsRoot { get; } = artifactsRoot ?? throw new ArgumentNullException(nameof(artifactsRoot));

    public BuildJobId JobId { get; } = jobId ?? throw new ArgumentNullException(nameof(jobId));

    public BuildArtifactId ArtifactId { get; } = artifactId ?? throw new ArgumentNullException(nameof(artifactId));
}

/// <summary>Requests one explicit persisted lifecycle transition.</summary>
public sealed class ArtifactStoreTransitionRequest(
    string projectRoot,
    string artifactsRoot,
    BuildJobId jobId,
    BuildArtifactId artifactId,
    BuildArtifactLifecycleState expectedState,
    BuildArtifactLifecycleState targetState,
    DateTimeOffset changedAtUtc)
{
    public string ProjectRoot { get; } = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));

    public string ArtifactsRoot { get; } = artifactsRoot ?? throw new ArgumentNullException(nameof(artifactsRoot));

    public BuildJobId JobId { get; } = jobId ?? throw new ArgumentNullException(nameof(jobId));

    public BuildArtifactId ArtifactId { get; } = artifactId ?? throw new ArgumentNullException(nameof(artifactId));

    public BuildArtifactLifecycleState ExpectedState { get; } = expectedState ?? throw new ArgumentNullException(nameof(expectedState));

    public BuildArtifactLifecycleState TargetState { get; } = targetState ?? throw new ArgumentNullException(nameof(targetState));

    public DateTimeOffset ChangedAtUtc { get; } = changedAtUtc;
}

/// <summary>Combines typed logical metadata, physical layout, and immutable content identity.</summary>
public sealed class ArtifactStoreRecord(
    BuildArtifact artifact,
    ArtifactContentIdentity contentIdentity,
    ArtifactStoreLayout layout)
{
    public BuildArtifact Artifact { get; } = artifact ?? throw new ArgumentNullException(nameof(artifact));

    public ArtifactContentIdentity ContentIdentity { get; } = contentIdentity ?? throw new ArgumentNullException(nameof(contentIdentity));

    public ArtifactStoreLayout Layout { get; } = layout ?? throw new ArgumentNullException(nameof(layout));
}
