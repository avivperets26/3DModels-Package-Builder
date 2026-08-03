using System.Collections.ObjectModel;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Detects deterministic duplicate groups from previously calculated content identities.</summary>
public static class ArtifactDuplicateDetector
{
    /// <summary>Groups equal byte-length and SHA-256 identities without reading artifact files again.</summary>
    public static ArtifactIdentityValidationResult<IReadOnlyList<ArtifactDuplicateGroup>> Detect(
        IEnumerable<ArtifactHashReceipt> receipts)
    {
        ArgumentNullException.ThrowIfNull(receipts);
        var uniqueIds = new HashSet<BuildArtifactId>();
        var accepted = new List<ArtifactHashReceipt>();
        foreach (ArtifactHashReceipt? receipt in receipts)
        {
            if (receipt is null)
            {
                return ArtifactIdentityValidationResult<IReadOnlyList<ArtifactDuplicateGroup>>.Failure(
                    ArtifactIdentityValidationError.NullReceipt);
            }

            if (!uniqueIds.Add(receipt.ArtifactId))
            {
                return ArtifactIdentityValidationResult<IReadOnlyList<ArtifactDuplicateGroup>>.Failure(
                    ArtifactIdentityValidationError.DuplicateArtifactId);
            }

            accepted.Add(receipt);
        }

        ArtifactDuplicateGroup[] groups = [.. accepted
            .GroupBy(static receipt => receipt.ContentIdentity)
            .Where(static group => group.Count() > 1)
            .OrderBy(static group => group.Key.Sha256.Value, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.Bytes)
            .Select(static group => new ArtifactDuplicateGroup(
                group.Key,
                group.Select(static receipt => receipt.ArtifactId)
                    .OrderBy(static id => id.Value, StringComparer.Ordinal)))];

        return ArtifactIdentityValidationResult<IReadOnlyList<ArtifactDuplicateGroup>>.Success(
            new ReadOnlyCollection<ArtifactDuplicateGroup>(groups));
    }
}
