namespace PackageBuilder.Domain.Profiles;

/// <summary>Represents an extensible generic marketplace identity.</summary>
public sealed class MarketplaceIdentifier : IEquatable<MarketplaceIdentifier>
{
    private MarketplaceIdentifier(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ProfileValidationResult<MarketplaceIdentifier> Create(string? value)
    {
        ProfileValidationError error = ProfileTextValidator.ValidateExtensibleIdentifier(
            value,
            ProfileValidationError.NullMarketplaceIdentifier,
            ProfileValidationError.EmptyMarketplaceIdentifier,
            ProfileValidationError.WhitespaceOnlyMarketplaceIdentifier,
            ProfileValidationError.MalformedMarketplaceIdentifier);
        return error == ProfileValidationError.None
            ? ProfileValidationResult<MarketplaceIdentifier>.Success(
                new MarketplaceIdentifier(value!))
            : ProfileValidationResult<MarketplaceIdentifier>.Failure(error);
    }

    public bool Equals(MarketplaceIdentifier? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is MarketplaceIdentifier other && Equals(other);

    public override int GetHashCode() => StableProfileHash.Create().Add(Value).ToHashCode();

    public override string ToString() => Value;
}
