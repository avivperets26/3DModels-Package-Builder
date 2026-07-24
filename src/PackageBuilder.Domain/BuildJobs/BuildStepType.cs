namespace PackageBuilder.Domain.BuildJobs;

/// <summary>Extensible logical operation identity, such as <c>build-unity-target</c>.</summary>
public sealed class BuildStepType : IEquatable<BuildStepType>
{
    private BuildStepType(string canonicalIdentifier) =>
        CanonicalIdentifier = canonicalIdentifier;

    public string CanonicalIdentifier { get; }

    public static BuildModelValidationResult<BuildStepType> Create(string? value)
    {
        BuildModelValidationError error = BuildValueValidator.ValidateCanonicalIdentifier(
            value,
            BuildModelValidationError.NullStepType,
            BuildModelValidationError.EmptyStepType,
            BuildModelValidationError.WhitespaceOnlyStepType,
            BuildModelValidationError.MalformedStepType);
        return error == BuildModelValidationError.None
            ? BuildModelValidationResult<BuildStepType>.Success(new BuildStepType(value!))
            : BuildModelValidationResult<BuildStepType>.Failure(error);
    }

    public bool Equals(BuildStepType? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is BuildStepType other && Equals(other);

    public override int GetHashCode() =>
        StableBuildHash.Create().Add(CanonicalIdentifier).ToHashCode();

    public override string ToString() => CanonicalIdentifier;
}
