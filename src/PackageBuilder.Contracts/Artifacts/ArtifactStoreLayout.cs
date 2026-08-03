using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Names the deterministic contained paths assigned to one stored artifact.</summary>
public sealed class ArtifactStoreLayout
{
    internal ArtifactStoreLayout(
        string artifactsRoot,
        string jobKey,
        string artifactKey,
        string artifactRoot,
        string payloadPath,
        string metadataPath)
    {
        ArtifactsRoot = artifactsRoot;
        JobKey = jobKey;
        ArtifactKey = artifactKey;
        ArtifactRoot = artifactRoot;
        PayloadPath = payloadPath;
        MetadataPath = metadataPath;
    }

    public string ArtifactsRoot { get; }

    /// <summary>Gets the lowercase SHA-256 key derived from the case-sensitive job ID.</summary>
    public string JobKey { get; }

    /// <summary>Gets the lowercase SHA-256 key derived from the case-sensitive artifact ID.</summary>
    public string ArtifactKey { get; }

    public string ArtifactRoot { get; }

    public string PayloadPath { get; }

    public string MetadataPath { get; }

    public string PayloadRelativePath => $"Jobs/{JobKey}/{ArtifactKey}/payload";

    public string MetadataRelativePath => $"Jobs/{JobKey}/{ArtifactKey}/artifact.json";
}

/// <summary>Creates deterministic typed artifact-store paths without filesystem access.</summary>
public static class ArtifactStoreLayoutFactory
{
    public static ArtifactStoreOperationResult<ArtifactStoreLayout> Create(
        string artifactsRoot,
        BuildJobId jobId,
        BuildArtifactId artifactId)
    {
        ArgumentNullException.ThrowIfNull(jobId);
        ArgumentNullException.ThrowIfNull(artifactId);
        if (string.IsNullOrWhiteSpace(artifactsRoot) || !Path.IsPathFullyQualified(artifactsRoot))
        {
            return Failed("ARTIFACT_STORE_PATH_INVALID", "artifactsRoot", "A fully qualified artifact root is required.");
        }

        string canonical;
        try
        {
            canonical = Path.TrimEndingDirectorySeparator(Path.GetFullPath(artifactsRoot));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failed("ARTIFACT_STORE_PATH_INVALID", "artifactsRoot", "The artifact root is invalid.");
        }

        if (!StringComparer.OrdinalIgnoreCase.Equals(canonical, Path.TrimEndingDirectorySeparator(artifactsRoot)))
        {
            return Failed("ARTIFACT_STORE_PATH_NOT_CANONICAL", "artifactsRoot", "The artifact root must already be canonical.");
        }

        string jobKey = HashIdentity(jobId.Value);
        string artifactKey = HashIdentity(artifactId.Value);
        string artifactRoot = Path.Combine(canonical, "Jobs", jobKey, artifactKey);
        return ArtifactStoreOperationResult.Success(
            new ArtifactStoreLayout(
                canonical,
                jobKey,
                artifactKey,
                artifactRoot,
                Path.Combine(artifactRoot, "payload"),
                Path.Combine(artifactRoot, "artifact.json")));
    }

    private static string HashIdentity(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static ArtifactStoreOperationResult<ArtifactStoreLayout> Failed(
        string code,
        string location,
        string diagnostic) =>
        ArtifactStoreOperationResult.Failed<ArtifactStoreLayout>(
            new ArtifactStoreFailure(code, location, diagnostic));
}
