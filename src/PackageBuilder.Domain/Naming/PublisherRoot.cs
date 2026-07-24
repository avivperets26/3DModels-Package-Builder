namespace PackageBuilder.Domain.Naming;

/// <summary>Represents a configurable publisher-owned root identifier and folder segment.</summary>
public sealed class PublisherRoot : IEquatable<PublisherRoot>
{
    private PublisherRoot(string value)
    {
        Value = value;
    }

    /// <summary>Gets the validated publisher root exactly as supplied.</summary>
    public string Value { get; }

    /// <summary>Validates and creates a publisher root without throwing for expected input errors.</summary>
    /// <param name="value">The user-supplied publisher root.</param>
    /// <returns>A result containing the immutable value or a naming-specific rejection reason.</returns>
    public static NamingValidationResult<PublisherRoot> Create(string? value)
    {
        NamingValidationError error = NamingValidator.ValidatePublisherRoot(value);
        return error == NamingValidationError.None
            ? NamingValidationResult<PublisherRoot>.Success(new PublisherRoot(value!))
            : NamingValidationResult<PublisherRoot>.Failure(error);
    }

    /// <inheritdoc />
    public bool Equals(PublisherRoot? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PublisherRoot other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => NamingValidator.GetStableOrdinalHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
