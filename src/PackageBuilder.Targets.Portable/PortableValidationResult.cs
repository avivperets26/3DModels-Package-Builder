namespace PackageBuilder.Targets.Portable;

/// <summary>Identifies an expected portable naming or layout validation failure.</summary>
public enum PortableValidationError
{
    None = 0,
    NullNamingProfile,
    NullAssetId,
    NullFolderName,
    NullExtension,
    InvalidExtension,
    NullTextureRole,
    UnsupportedTextureRole,
    NullGlbVariant,
    NullMediaView,
    NullArtifactRecord,
    NullArtifactPurpose,
    InvalidArtifactQualifier,
    ArtifactNotValidated,
    ArtifactTargetMismatch,
    ArtifactRoleMismatch,
    NullArtifactCollection,
    NullArtifactEntry,
    MissingRequiredArtifact,
    DuplicateArtifactPurpose,
    DuplicateArtifactId,
    DuplicateTextureRole,
    DuplicateMediaView,
}

/// <summary>Returns an immutable value or one stable expected-input error without throwing.</summary>
public sealed class PortableValidationResult<T>
    where T : class
{
    private PortableValidationResult(bool isValid, T? value, PortableValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public T? Value { get; }

    public PortableValidationError Error { get; }

    internal static PortableValidationResult<T> Success(T value) => new(true, value, PortableValidationError.None);

    internal static PortableValidationResult<T> Failure(PortableValidationError error) => new(false, null, error);
}
