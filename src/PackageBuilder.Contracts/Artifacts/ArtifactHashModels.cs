using System.Collections.ObjectModel;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Names one contained file and logical build artifact for streamed hashing.</summary>
public sealed class ArtifactHashRequest(
    string projectRoot,
    string filePath,
    BuildArtifactId artifactId)
{
    public string ProjectRoot { get; } = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));

    public string FilePath { get; } = filePath ?? throw new ArgumentNullException(nameof(filePath));

    public BuildArtifactId ArtifactId { get; } = artifactId ?? throw new ArgumentNullException(nameof(artifactId));
}

/// <summary>Records the logical artifact and immutable identity produced by one complete hash pass.</summary>
public sealed class ArtifactHashReceipt(BuildArtifactId artifactId, ArtifactContentIdentity contentIdentity)
{
    public BuildArtifactId ArtifactId { get; } = artifactId ?? throw new ArgumentNullException(nameof(artifactId));

    public ArtifactContentIdentity ContentIdentity { get; } =
        contentIdentity ?? throw new ArgumentNullException(nameof(contentIdentity));
}

/// <summary>Groups two or more logical artifacts that have the same content identity.</summary>
public sealed class ArtifactDuplicateGroup
{
    private readonly ReadOnlyCollection<BuildArtifactId> _artifactIds;

    internal ArtifactDuplicateGroup(
        ArtifactContentIdentity contentIdentity,
        IEnumerable<BuildArtifactId> artifactIds)
    {
        ArgumentNullException.ThrowIfNull(artifactIds);
        ContentIdentity = contentIdentity ?? throw new ArgumentNullException(nameof(contentIdentity));
        _artifactIds = new ReadOnlyCollection<BuildArtifactId>(artifactIds.ToArray());
    }

    public ArtifactContentIdentity ContentIdentity { get; }

    public IReadOnlyList<BuildArtifactId> ArtifactIds => _artifactIds;
}
