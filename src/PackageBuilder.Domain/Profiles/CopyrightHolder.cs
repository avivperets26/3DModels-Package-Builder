namespace PackageBuilder.Domain.Profiles;

/// <summary>Represents the validated holder named in a copyright notice.</summary>
public sealed class CopyrightHolder : IEquatable<CopyrightHolder>
{
    public const int MaximumLength = 256;

    private CopyrightHolder(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ProfileValidationResult<CopyrightHolder> Create(string? value)
    {
        ProfileValidationError error = ProfileTextValidator.Validate(
            value,
            MaximumLength,
            ProfileValidationError.NullCopyrightHolder,
            ProfileValidationError.EmptyCopyrightHolder,
            ProfileValidationError.WhitespaceOnlyCopyrightHolder,
            ProfileValidationError.CopyrightHolderEdgeWhitespace,
            ProfileValidationError.CopyrightHolderContainsControlCharacter,
            ProfileValidationError.CopyrightHolderTooLong);
        return error == ProfileValidationError.None
            ? ProfileValidationResult<CopyrightHolder>.Success(new CopyrightHolder(value!))
            : ProfileValidationResult<CopyrightHolder>.Failure(error);
    }

    public bool Equals(CopyrightHolder? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is CopyrightHolder other && Equals(other);

    public override int GetHashCode() => StableProfileHash.Create().Add(Value).ToHashCode();

    public override string ToString() => Value;
}
