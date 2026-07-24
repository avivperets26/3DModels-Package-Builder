namespace PackageBuilder.Domain.Naming;

/// <summary>Represents one Windows-safe product folder segment.</summary>
public sealed class ProductFolderName : IEquatable<ProductFolderName>
{
    private ProductFolderName(string value)
    {
        Value = value;
    }

    /// <summary>Gets the validated folder name exactly as supplied.</summary>
    public string Value { get; }

    /// <summary>Validates and creates a product folder name without throwing for expected input errors.</summary>
    /// <param name="value">The user-supplied folder segment.</param>
    /// <returns>A result containing the immutable value or a naming-specific rejection reason.</returns>
    public static NamingValidationResult<ProductFolderName> Create(string? value)
    {
        NamingValidationError error = NamingValidator.ValidateProductFolderName(value);
        return error == NamingValidationError.None
            ? NamingValidationResult<ProductFolderName>.Success(new ProductFolderName(value!))
            : NamingValidationResult<ProductFolderName>.Failure(error);
    }

    /// <inheritdoc />
    public bool Equals(ProductFolderName? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ProductFolderName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => NamingValidator.GetStableOrdinalHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
