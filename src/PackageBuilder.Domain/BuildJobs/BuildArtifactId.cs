namespace PackageBuilder.Domain.BuildJobs;

public sealed class BuildArtifactId : IEquatable<BuildArtifactId>
{
    private BuildArtifactId(string value) => Value = value;

    public string Value { get; }

    public static BuildModelValidationResult<BuildArtifactId> Create(string? value)
    {
        BuildModelValidationError error = BuildValueValidator.ValidateIdentity(value);
        return error == BuildModelValidationError.None
            ? BuildModelValidationResult<BuildArtifactId>.Success(new BuildArtifactId(value!))
            : BuildModelValidationResult<BuildArtifactId>.Failure(error);
    }

    public bool Equals(BuildArtifactId? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is BuildArtifactId other && Equals(other);

    public override int GetHashCode() => StableBuildHash.Create().Add(Value).ToHashCode();

    public override string ToString() => Value;
}
