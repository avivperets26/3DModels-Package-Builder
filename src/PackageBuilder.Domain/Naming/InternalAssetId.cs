namespace PackageBuilder.Domain.Naming;

/// <summary>Represents a compact ASCII identifier used for product assets.</summary>
public sealed class InternalAssetId : IEquatable<InternalAssetId>
{
    private InternalAssetId(string value)
    {
        Value = value;
    }

    /// <summary>Gets the validated internal asset identifier exactly as supplied.</summary>
    public string Value { get; }

    /// <summary>Validates and creates an internal asset ID without throwing for expected input errors.</summary>
    /// <param name="value">The user-supplied internal identifier.</param>
    /// <returns>A result containing the immutable value or a naming-specific rejection reason.</returns>
    public static NamingValidationResult<InternalAssetId> Create(string? value)
    {
        NamingValidationError error = NamingValidator.ValidateInternalAssetId(value);
        return error == NamingValidationError.None
            ? NamingValidationResult<InternalAssetId>.Success(new InternalAssetId(value!))
            : NamingValidationResult<InternalAssetId>.Failure(error);
    }

    /// <inheritdoc />
    public bool Equals(InternalAssetId? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is InternalAssetId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => NamingValidator.GetStableOrdinalHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
