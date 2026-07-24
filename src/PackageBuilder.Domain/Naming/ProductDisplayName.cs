namespace PackageBuilder.Domain.Naming;

/// <summary>Represents a human-readable product name without applying normalization.</summary>
public sealed class ProductDisplayName : IEquatable<ProductDisplayName>
{
    private ProductDisplayName(string value)
    {
        Value = value;
    }

    /// <summary>Gets the validated display name exactly as supplied.</summary>
    public string Value { get; }

    /// <summary>Validates and creates a product display name without throwing for expected input errors.</summary>
    /// <param name="value">The user-supplied display name.</param>
    /// <returns>A result containing the immutable value or a naming-specific rejection reason.</returns>
    public static NamingValidationResult<ProductDisplayName> Create(string? value)
    {
        NamingValidationError error = NamingValidator.ValidateDisplayName(value);
        return error == NamingValidationError.None
            ? NamingValidationResult<ProductDisplayName>.Success(new ProductDisplayName(value!))
            : NamingValidationResult<ProductDisplayName>.Failure(error);
    }

    /// <inheritdoc />
    public bool Equals(ProductDisplayName? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ProductDisplayName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => NamingValidator.GetStableOrdinalHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
