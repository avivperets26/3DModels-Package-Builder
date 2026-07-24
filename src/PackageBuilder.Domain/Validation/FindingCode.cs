namespace PackageBuilder.Domain.Validation;

/// <summary>
/// Stable machine identity using uppercase ASCII letter-led segments separated by one underscore.
/// Codes are compatibility identifiers and must never contain filenames, user data, timestamps,
/// GUIDs, or changing diagnostic prose.
/// </summary>
public sealed class FindingCode : IEquatable<FindingCode>
{
    private FindingCode(string value) => Value = value;

    public string Value { get; }

    public static ValidationFindingResult<FindingCode> Create(string? value)
    {
        ValidationFindingError error = FindingValueValidator.ValidateCode(value);
        return error == ValidationFindingError.None
            ? ValidationFindingResult<FindingCode>.Success(new FindingCode(value!))
            : ValidationFindingResult<FindingCode>.Failure(error);
    }

    public bool Equals(FindingCode? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is FindingCode other && Equals(other);

    public override int GetHashCode() => StableFindingHash.Create().Add(Value).ToHashCode();

    public override string ToString() => Value;
}
