using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

public sealed class ArtifactDuplicateDetectorTests
{
    [Fact]
    public void EqualContentIsGroupedDeterministicallyWhileDifferentContentRemainsSeparate()
    {
        ArtifactHashReceipt[] receipts =
        [
            Receipt("Artifact-Z", 4, Sha('b')),
            Receipt("Artifact-C", 4, Sha('a')),
            Receipt("Artifact-A", 4, Sha('a')),
            Receipt("Artifact-B", 4, Sha('b')),
            Receipt("Artifact-Unique", 4, Sha('c')),
            Receipt("Artifact-DifferentSize", 5, Sha('a')),
        ];

        ArtifactIdentityValidationResult<IReadOnlyList<ArtifactDuplicateGroup>> result =
            ArtifactDuplicateDetector.Detect(receipts);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal(Sha('a'), result.Value[0].ContentIdentity.Sha256.Value);
        Assert.Equal(["Artifact-A", "Artifact-C"], result.Value[0].ArtifactIds.Select(static id => id.Value));
        Assert.Equal(Sha('b'), result.Value[1].ContentIdentity.Sha256.Value);
        Assert.Equal(["Artifact-B", "Artifact-Z"], result.Value[1].ArtifactIds.Select(static id => id.Value));
        Assert.Equal("Artifact-Z", receipts[0].ArtifactId.Value);
    }

    [Fact]
    public void EmptyAndUniqueInputsSucceedWithoutGroups()
    {
        Assert.Empty(ArtifactDuplicateDetector.Detect([]).Value!);
        Assert.Empty(ArtifactDuplicateDetector.Detect([Receipt("Artifact-01", 1, Sha('a'))]).Value!);
    }

    [Fact]
    public void NullCollectionThrowsAndInvalidEntriesReturnStructuredFailures()
    {
        _ = Assert.Throws<ArgumentNullException>(() => ArtifactDuplicateDetector.Detect(null!));

        ArtifactIdentityValidationResult<IReadOnlyList<ArtifactDuplicateGroup>> nullReceipt =
            ArtifactDuplicateDetector.Detect([null!]);
        Assert.Equal(ArtifactIdentityValidationError.NullReceipt, nullReceipt.Error);

        ArtifactHashReceipt receipt = Receipt("Artifact-01", 1, Sha('a'));
        ArtifactIdentityValidationResult<IReadOnlyList<ArtifactDuplicateGroup>> duplicateId =
            ArtifactDuplicateDetector.Detect([receipt, Receipt("Artifact-01", 2, Sha('b'))]);
        Assert.Equal(ArtifactIdentityValidationError.DuplicateArtifactId, duplicateId.Error);
    }

    private static ArtifactHashReceipt Receipt(string id, long bytes, string sha256) =>
        new(
            BuildArtifactId.Create(id).Value!,
            ArtifactContentIdentity.Create(bytes, Sha256Digest.Create(sha256).Value!).Value!);

    private static string Sha(char character) => new(character, 64);
}
