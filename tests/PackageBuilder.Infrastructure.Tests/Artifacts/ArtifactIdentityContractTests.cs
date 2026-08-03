using System.Reflection;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

public sealed class ArtifactIdentityContractTests
{
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    [Fact]
    public void Sha256RequiresCanonicalLowercaseHex()
    {
        Assert.Equal(ArtifactIdentityValidationError.NullSha256, Sha256Digest.Create(null).Error);
        Assert.Equal(ArtifactIdentityValidationError.NonCanonicalSha256, Sha256Digest.Create(string.Empty).Error);
        Assert.Equal(ArtifactIdentityValidationError.NonCanonicalSha256, Sha256Digest.Create(new string('a', 63)).Error);
        Assert.Equal(ArtifactIdentityValidationError.NonCanonicalSha256, Sha256Digest.Create(new string('A', 64)).Error);
        Assert.Equal(ArtifactIdentityValidationError.NonCanonicalSha256, Sha256Digest.Create(new string('g', 64)).Error);

        ArtifactIdentityValidationResult<Sha256Digest> result = Sha256Digest.Create(EmptySha256);

        Assert.True(result.IsSuccess);
        Assert.Equal(ArtifactIdentityValidationError.None, result.Error);
        Assert.Equal(EmptySha256, result.Value!.Value);
        Assert.Equal(EmptySha256, result.Value.ToString());
    }

    [Fact]
    public void ContentIdentityRequiresNonNegativeBytesAndDigest()
    {
        Sha256Digest digest = Digest(EmptySha256);

        Assert.Equal(
            ArtifactIdentityValidationError.NegativeByteLength,
            ArtifactContentIdentity.Create(-1, digest).Error);
        Assert.Equal(
            ArtifactIdentityValidationError.NullContentHash,
            ArtifactContentIdentity.Create(0, null).Error);

        ArtifactContentIdentity identity = ArtifactContentIdentity.Create(0, digest).Value!;
        Assert.Equal(0, identity.Bytes);
        Assert.Same(digest, identity.Sha256);
        Assert.Equal($"sha256:{EmptySha256}:0", identity.ToString());
    }

    [Fact]
    public void IdentityEqualityAndHashingAreDeterministicAndTypeSafe()
    {
        ArtifactContentIdentity first = Identity(10, EmptySha256);
        ArtifactContentIdentity equal = Identity(10, EmptySha256);
        ArtifactContentIdentity differentBytes = Identity(11, EmptySha256);
        Sha256Digest digest = Digest(EmptySha256);

        Assert.Equal(first, equal);
        Assert.True(first.Equals((object)equal));
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(first, differentBytes);
        Assert.False(first.Equals(null));
        Assert.False(first.Equals(new object()));
        Assert.Equal(digest, Digest(EmptySha256));
        Assert.True(digest.Equals((object)Digest(EmptySha256)));
        Assert.Equal(digest.GetHashCode(), Digest(EmptySha256).GetHashCode());
        Assert.False(digest.Equals(null));
        Assert.False(digest.Equals(new object()));
    }

    [Fact]
    public void RequestReceiptAndGroupDefensivelyValidateNullsAndSnapshotCollections()
    {
        BuildArtifactId id = ArtifactId("Artifact-01");
        ArtifactContentIdentity identity = Identity(1, EmptySha256);
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactHashRequest(null!, "file", id));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactHashRequest("root", null!, id));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactHashRequest("root", "file", null!));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactHashReceipt(null!, identity));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactHashReceipt(id, null!));

        var ids = new List<BuildArtifactId> { id, ArtifactId("Artifact-02") };
        var group = (ArtifactDuplicateGroup)Activator.CreateInstance(
            typeof(ArtifactDuplicateGroup),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: [identity, ids],
            culture: null)!;
        ids.Clear();
        Assert.Equal(2, group.ArtifactIds.Count);
        Assert.Same(identity, group.ContentIdentity);

        ConstructorInfo constructor = typeof(ArtifactDuplicateGroup).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();
        TargetInvocationException nullIdentity = Assert.Throws<TargetInvocationException>(
            () => constructor.Invoke([null, Array.Empty<BuildArtifactId>()]));
        _ = Assert.IsType<ArgumentNullException>(nullIdentity.InnerException);
    }

    [Fact]
    public void HashResultFactoriesRequireValidValuesAndFailures()
    {
        ArtifactContentIdentity identity = Identity(1, EmptySha256);
        BuildArtifactId id = ArtifactId("Artifact-01");
        ArtifactHashReceipt receipt = new(id, identity);
        var success = ArtifactHashOperationResult.Success(receipt);
        Assert.True(success.IsSuccess);
        Assert.Same(receipt, success.Value);
        Assert.Empty(success.Failures);

        ArtifactHashFailure failure = new("HASH_FAILED", "file", "The hash failed.");
        var failed = ArtifactHashOperationResult.Failed<ArtifactHashReceipt>(failure);
        Assert.False(failed.IsSuccess);
        Assert.Null(failed.Value);
        Assert.Same(failure, Assert.Single(failed.Failures));

        _ = Assert.Throws<ArgumentNullException>(() => ArtifactHashOperationResult.Success<ArtifactHashReceipt>(null!));
        _ = Assert.Throws<ArgumentNullException>(() => ArtifactHashOperationResult.Failed<ArtifactHashReceipt>(null!));
        _ = Assert.Throws<ArgumentException>(() => ArtifactHashOperationResult.Failed<ArtifactHashReceipt>());
        _ = Assert.Throws<ArgumentException>(() => ArtifactHashOperationResult.Failed<ArtifactHashReceipt>([null!]));
        _ = Assert.Throws<ArgumentException>(() => new ArtifactHashFailure(string.Empty, "file", "diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => new ArtifactHashFailure("CODE", " ", "diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => new ArtifactHashFailure("CODE", "file", string.Empty));
    }

    [Fact]
    public void InternalValidationFactoriesRejectImpossibleArguments()
    {
        Type resultType = typeof(ArtifactIdentityValidationResult<Sha256Digest>);
        MethodInfo success = resultType.GetMethod("Success", BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo failure = resultType.GetMethod("Failure", BindingFlags.Static | BindingFlags.NonPublic)!;

        TargetInvocationException nullSuccess = Assert.Throws<TargetInvocationException>(
            () => success.Invoke(null, [null]));
        _ = Assert.IsType<ArgumentNullException>(nullSuccess.InnerException);

        TargetInvocationException emptyFailure = Assert.Throws<TargetInvocationException>(
            () => failure.Invoke(null, [ArtifactIdentityValidationError.None]));
        _ = Assert.IsType<ArgumentOutOfRangeException>(emptyFailure.InnerException);
    }

    private static Sha256Digest Digest(string value) => Sha256Digest.Create(value).Value!;

    private static ArtifactContentIdentity Identity(long bytes, string sha256) =>
        ArtifactContentIdentity.Create(bytes, Digest(sha256)).Value!;

    private static BuildArtifactId ArtifactId(string value) => BuildArtifactId.Create(value).Value!;
}
