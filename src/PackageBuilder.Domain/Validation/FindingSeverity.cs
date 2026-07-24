using System.Collections.ObjectModel;

namespace PackageBuilder.Domain.Validation;

/// <summary>
/// Closed finding severity whose lowercase token is a stable serialization compatibility value.
/// Release blocking is deliberately modeled separately and is never inferred from severity.
/// </summary>
public sealed class FindingSeverity : IEquatable<FindingSeverity>
{
    private static readonly ReadOnlyCollection<FindingSeverity> _all;

    private FindingSeverity(string name, string serializedToken)
    {
        Name = name;
        SerializedToken = serializedToken;
    }

    public static FindingSeverity Info { get; } = new(nameof(Info), "info");

    public static FindingSeverity Warning { get; } = new(nameof(Warning), "warning");

    public static FindingSeverity Error { get; } = new(nameof(Error), "error");

    public static FindingSeverity Fatal { get; } = new(nameof(Fatal), "fatal");

    static FindingSeverity()
    {
        _all = Array.AsReadOnly([Info, Warning, Error, Fatal]);
    }

    public static IReadOnlyList<FindingSeverity> All => _all;

    public string Name { get; }

    public string SerializedToken { get; }

    public static ValidationFindingResult<FindingSeverity> ParseToken(string? token)
    {
        if (token is null)
        {
            return ValidationFindingResult<FindingSeverity>.Failure(
                ValidationFindingError.NullSeverity);
        }

        if (token.Length == 0)
        {
            return ValidationFindingResult<FindingSeverity>.Failure(
                ValidationFindingError.EmptySeverityToken);
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return ValidationFindingResult<FindingSeverity>.Failure(
                ValidationFindingError.WhitespaceOnlySeverityToken);
        }

        FindingSeverity? severity = _all.FirstOrDefault(
            candidate => string.Equals(
                candidate.SerializedToken,
                token,
                StringComparison.Ordinal));
        return severity is null
            ? ValidationFindingResult<FindingSeverity>.Failure(
                ValidationFindingError.UnknownSeverityToken)
            : ValidationFindingResult<FindingSeverity>.Success(severity);
    }

    public bool Equals(FindingSeverity? other) =>
        other is not null &&
        string.Equals(SerializedToken, other.SerializedToken, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is FindingSeverity other && Equals(other);

    public override int GetHashCode() =>
        StableFindingHash.Create().Add(SerializedToken).ToHashCode();

    public override string ToString() => Name;
}
