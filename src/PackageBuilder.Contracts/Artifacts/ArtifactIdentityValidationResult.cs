namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Names expected validation failures for immutable artifact content identities.</summary>
public enum ArtifactIdentityValidationError
{
    None,
    NullSha256,
    NonCanonicalSha256,
    NegativeByteLength,
    NullContentHash,
    NullArtifactId,
    NullContentIdentity,
    NullReceipt,
    DuplicateArtifactId,
}

/// <summary>Returns either one validated artifact identity value or one expected validation error.</summary>
public sealed class ArtifactIdentityValidationResult<T>
    where T : class
{
    private ArtifactIdentityValidationResult(T? value, ArtifactIdentityValidationError error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Value is not null;

    public T? Value { get; }

    public ArtifactIdentityValidationError Error { get; }

    internal static ArtifactIdentityValidationResult<T> Success(T value) =>
        new(value ?? throw new ArgumentNullException(nameof(value)), ArtifactIdentityValidationError.None);

    internal static ArtifactIdentityValidationResult<T> Failure(ArtifactIdentityValidationError error) =>
        error == ArtifactIdentityValidationError.None
            ? throw new ArgumentOutOfRangeException(nameof(error), "A failure must name an error.")
            : new(null, error);
}
