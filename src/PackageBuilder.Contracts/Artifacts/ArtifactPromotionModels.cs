using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Requests recoverable publication of one validated artifact into the configured Builds root.</summary>
public sealed class ArtifactPromotionRequest(
    string projectRoot,
    string artifactsRoot,
    string buildsRoot,
    BuildJobId jobId,
    BuildArtifactId artifactId,
    DateTimeOffset promotedAtUtc)
{
    public string ProjectRoot { get; } = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));

    public string ArtifactsRoot { get; } = artifactsRoot ?? throw new ArgumentNullException(nameof(artifactsRoot));

    public string BuildsRoot { get; } = buildsRoot ?? throw new ArgumentNullException(nameof(buildsRoot));

    public BuildJobId JobId { get; } = jobId ?? throw new ArgumentNullException(nameof(jobId));

    public BuildArtifactId ArtifactId { get; } = artifactId ?? throw new ArgumentNullException(nameof(artifactId));

    public DateTimeOffset PromotedAtUtc { get; } = promotedAtUtc;
}

/// <summary>Records the stable release location and final artifact-store state after publication.</summary>
public sealed class ArtifactPromotionReceipt(
    ArtifactStoreRecord artifactRecord,
    string releasePath,
    string releaseRelativePath,
    int collisionVersion,
    bool recovered)
{
    public ArtifactStoreRecord ArtifactRecord { get; } =
        artifactRecord ?? throw new ArgumentNullException(nameof(artifactRecord));

    public string ReleasePath { get; } = releasePath ?? throw new ArgumentNullException(nameof(releasePath));

    public string ReleaseRelativePath { get; } =
        releaseRelativePath ?? throw new ArgumentNullException(nameof(releaseRelativePath));

    /// <summary>Gets one for the requested name or a higher value for a collision-safe version.</summary>
    public int CollisionVersion { get; } = collisionVersion;

    /// <summary>Gets whether persisted promotion state was resumed rather than started in this call.</summary>
    public bool Recovered { get; } = recovered;
}
