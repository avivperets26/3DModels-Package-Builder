namespace PackageBuilder.Domain.Naming;

/// <summary>Represents a canonical token used when composing texture names.</summary>
public sealed class CanonicalTextureNameToken : IEquatable<CanonicalTextureNameToken>
{
    private CanonicalTextureNameToken(string value)
    {
        Value = value;
    }

    /// <summary>Gets the canonical base-colour texture token.</summary>
    public static CanonicalTextureNameToken Albedo { get; } = new("Albedo");

    /// <summary>Gets the canonical token text.</summary>
    public string Value { get; }

    /// <summary>Validates a currently supported canonical texture naming token.</summary>
    /// <param name="value">The token text to validate.</param>
    /// <returns>A result containing the canonical singleton or a naming-specific rejection reason.</returns>
    public static NamingValidationResult<CanonicalTextureNameToken> Create(string? value)
    {
        NamingValidationError commonError = NamingValidator.ValidateDisplayName(value);
        return commonError != NamingValidationError.None
            ? NamingValidationResult<CanonicalTextureNameToken>.Failure(commonError)
            : string.Equals(value, Albedo.Value, StringComparison.Ordinal)
            ? NamingValidationResult<CanonicalTextureNameToken>.Success(Albedo)
            : NamingValidationResult<CanonicalTextureNameToken>.Failure(
                NamingValidationError.UnsupportedCanonicalToken);
    }

    /// <inheritdoc />
    public bool Equals(CanonicalTextureNameToken? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is CanonicalTextureNameToken other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => NamingValidator.GetStableOrdinalHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
