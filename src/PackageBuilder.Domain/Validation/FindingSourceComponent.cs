namespace PackageBuilder.Domain.Validation;

/// <summary>
/// Extensible lowercase ASCII component identity, formed from words separated by single hyphens.
/// </summary>
public sealed class FindingSourceComponent : IEquatable<FindingSourceComponent>
{
    private FindingSourceComponent(string value) => Value = value;

    public string Value { get; }

    public static ValidationFindingResult<FindingSourceComponent> Create(string? value)
    {
        ValidationFindingError error = FindingValueValidator.ValidateSource(value);
        return error == ValidationFindingError.None
            ? ValidationFindingResult<FindingSourceComponent>.Success(
                new FindingSourceComponent(value!))
            : ValidationFindingResult<FindingSourceComponent>.Failure(error);
    }

    public bool Equals(FindingSourceComponent? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is FindingSourceComponent other && Equals(other);

    public override int GetHashCode() => StableFindingHash.Create().Add(Value).ToHashCode();

    public override string ToString() => Value;
}
