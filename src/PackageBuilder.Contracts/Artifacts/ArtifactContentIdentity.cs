namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Identifies artifact content by exact byte length and canonical SHA-256 digest.</summary>
public sealed class ArtifactContentIdentity : IEquatable<ArtifactContentIdentity>
{
    private ArtifactContentIdentity(long bytes, Sha256Digest sha256)
    {
        Bytes = bytes;
        Sha256 = sha256;
    }

    public long Bytes { get; }

    public Sha256Digest Sha256 { get; }

    /// <summary>Creates an immutable content identity without performing filesystem access.</summary>
    public static ArtifactIdentityValidationResult<ArtifactContentIdentity> Create(
        long bytes,
        Sha256Digest? sha256)
    {
        return bytes < 0
            ? ArtifactIdentityValidationResult<ArtifactContentIdentity>.Failure(
                ArtifactIdentityValidationError.NegativeByteLength)
            : sha256 is null
            ? ArtifactIdentityValidationResult<ArtifactContentIdentity>.Failure(
                ArtifactIdentityValidationError.NullContentHash)
            : ArtifactIdentityValidationResult<ArtifactContentIdentity>.Success(
                new ArtifactContentIdentity(bytes, sha256));
    }

    public bool Equals(ArtifactContentIdentity? other) =>
        other is not null && Bytes == other.Bytes && Sha256.Equals(other.Sha256);

    public override bool Equals(object? obj) => obj is ArtifactContentIdentity other && Equals(other);

    public override int GetHashCode()
    {
        const uint OffsetBasis = 2_166_136_261;
        const uint Prime = 16_777_619;
        ulong value = unchecked((ulong)Bytes);
        uint hash = OffsetBasis;
        for (int shift = 0; shift < 64; shift += 8)
        {
            hash = (hash ^ (byte)(value >> shift)) * Prime;
        }

        hash = (hash ^ unchecked((uint)Sha256.GetHashCode())) * Prime;
        return unchecked((int)hash);
    }

    public override string ToString() => $"sha256:{Sha256.Value}:{Bytes}";
}
