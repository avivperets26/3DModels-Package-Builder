namespace PackageBuilder.Domain.BuildJobs;

public sealed class BuildJobId : IEquatable<BuildJobId>
{
    private BuildJobId(string value) => Value = value;

    public string Value { get; }

    public static BuildModelValidationResult<BuildJobId> Create(string? value)
    {
        BuildModelValidationError error = BuildValueValidator.ValidateIdentity(value);
        return error == BuildModelValidationError.None
            ? BuildModelValidationResult<BuildJobId>.Success(new BuildJobId(value!))
            : BuildModelValidationResult<BuildJobId>.Failure(error);
    }

    public bool Equals(BuildJobId? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is BuildJobId other && Equals(other);

    public override int GetHashCode() => StableBuildHash.Create().Add(Value).ToHashCode();

    public override string ToString() => Value;
}
