namespace PackageBuilder.Domain.Validation;

/// <summary>Validated human-readable finding explanation that preserves useful Unicode.</summary>
public sealed class FindingExplanation : IEquatable<FindingExplanation>
{
    private FindingExplanation(string value) => Value = value;

    public string Value { get; }

    public static ValidationFindingResult<FindingExplanation> Create(string? value)
    {
        ValidationFindingError error = FindingValueValidator.ValidateText(
            value,
            ValidationFindingError.NullExplanation,
            ValidationFindingError.EmptyExplanation,
            ValidationFindingError.WhitespaceOnlyExplanation,
            ValidationFindingError.ExplanationEdgeWhitespace,
            ValidationFindingError.ExplanationContainsControlCharacter);
        return error == ValidationFindingError.None
            ? ValidationFindingResult<FindingExplanation>.Success(new FindingExplanation(value!))
            : ValidationFindingResult<FindingExplanation>.Failure(error);
    }

    public bool Equals(FindingExplanation? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is FindingExplanation other && Equals(other);

    public override int GetHashCode() => StableFindingHash.Create().Add(Value).ToHashCode();

    public override string ToString() => Value;
}
