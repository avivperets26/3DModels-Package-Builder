namespace PackageBuilder.Domain.BuildJobs;

public sealed class BuildStepId : IEquatable<BuildStepId>
{
    private BuildStepId(string value) => Value = value;

    public string Value { get; }

    public static BuildModelValidationResult<BuildStepId> Create(string? value)
    {
        BuildModelValidationError error = BuildValueValidator.ValidateIdentity(value);
        return error == BuildModelValidationError.None
            ? BuildModelValidationResult<BuildStepId>.Success(new BuildStepId(value!))
            : BuildModelValidationResult<BuildStepId>.Failure(error);
    }

    public bool Equals(BuildStepId? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is BuildStepId other && Equals(other);

    public override int GetHashCode() => StableBuildHash.Create().Add(Value).ToHashCode();

    public override string ToString() => Value;
}
