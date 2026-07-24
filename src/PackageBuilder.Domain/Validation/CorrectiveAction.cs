namespace PackageBuilder.Domain.Validation;

/// <summary>
/// Optional caller-authored corrective action. It may be absent when no safe, practical user
/// correction exists; when present it must be useful human-readable text.
/// </summary>
public sealed class CorrectiveAction : IEquatable<CorrectiveAction>
{
    private CorrectiveAction(string value) => Value = value;

    public string Value { get; }

    public static ValidationFindingResult<CorrectiveAction> Create(string? value)
    {
        ValidationFindingError error = FindingValueValidator.ValidateText(
            value,
            ValidationFindingError.NullCorrectiveAction,
            ValidationFindingError.EmptyCorrectiveAction,
            ValidationFindingError.WhitespaceOnlyCorrectiveAction,
            ValidationFindingError.CorrectiveActionEdgeWhitespace,
            ValidationFindingError.CorrectiveActionContainsControlCharacter);
        return error == ValidationFindingError.None
            ? ValidationFindingResult<CorrectiveAction>.Success(new CorrectiveAction(value!))
            : ValidationFindingResult<CorrectiveAction>.Failure(error);
    }

    public bool Equals(CorrectiveAction? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is CorrectiveAction other && Equals(other);

    public override int GetHashCode() => StableFindingHash.Create().Add(Value).ToHashCode();

    public override string ToString() => Value;
}
