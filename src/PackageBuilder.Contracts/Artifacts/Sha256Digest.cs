namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Represents one canonical lowercase SHA-256 digest.</summary>
public sealed class Sha256Digest : IEquatable<Sha256Digest>
{
    private const int DigestLength = 64;

    private Sha256Digest(string value) => Value = value;

    public string Value { get; }

    /// <summary>Validates the exact lowercase hexadecimal representation used by artifact contracts.</summary>
    public static ArtifactIdentityValidationResult<Sha256Digest> Create(string? value)
    {
        return value is null
            ? ArtifactIdentityValidationResult<Sha256Digest>.Failure(
                ArtifactIdentityValidationError.NullSha256)
            : value.Length != DigestLength || value.Any(static character => !IsLowerHex(character))
            ? ArtifactIdentityValidationResult<Sha256Digest>.Failure(
                ArtifactIdentityValidationError.NonCanonicalSha256)
            : ArtifactIdentityValidationResult<Sha256Digest>.Success(new Sha256Digest(value));
    }

    public bool Equals(Sha256Digest? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is Sha256Digest other && Equals(other);

    public override int GetHashCode()
    {
        const uint OffsetBasis = 2_166_136_261;
        const uint Prime = 16_777_619;
        uint hash = OffsetBasis;
        foreach (char character in Value)
        {
            hash = (hash ^ character) * Prime;
        }

        return unchecked((int)hash);
    }

    public override string ToString() => Value;

    private static bool IsLowerHex(char character) =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f';
}
