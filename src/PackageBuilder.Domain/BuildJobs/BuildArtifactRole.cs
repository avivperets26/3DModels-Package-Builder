namespace PackageBuilder.Domain.BuildJobs;

/// <summary>Extensible logical role such as <c>normalized-model</c> or <c>validation-log</c>.</summary>
public sealed class BuildArtifactRole : IEquatable<BuildArtifactRole>
{
    private BuildArtifactRole(string canonicalIdentifier) =>
        CanonicalIdentifier = canonicalIdentifier;

    public string CanonicalIdentifier { get; }

    public static BuildModelValidationResult<BuildArtifactRole> Create(string? value)
    {
        BuildModelValidationError error = BuildValueValidator.ValidateCanonicalIdentifier(
            value,
            BuildModelValidationError.NullArtifactRole,
            BuildModelValidationError.EmptyArtifactRole,
            BuildModelValidationError.WhitespaceOnlyArtifactRole,
            BuildModelValidationError.MalformedArtifactRole);
        return error == BuildModelValidationError.None
            ? BuildModelValidationResult<BuildArtifactRole>.Success(new BuildArtifactRole(value!))
            : BuildModelValidationResult<BuildArtifactRole>.Failure(error);
    }

    public bool Equals(BuildArtifactRole? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is BuildArtifactRole other && Equals(other);

    public override int GetHashCode() =>
        StableBuildHash.Create().Add(CanonicalIdentifier).ToHashCode();

    public override string ToString() => CanonicalIdentifier;
}
