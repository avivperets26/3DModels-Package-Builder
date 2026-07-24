namespace PackageBuilder.Domain.Profiles;

/// <summary>Represents a stable profile identity within one marketplace.</summary>
public sealed class MarketplaceProfileIdentifier : IEquatable<MarketplaceProfileIdentifier>
{
    private MarketplaceProfileIdentifier(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ProfileValidationResult<MarketplaceProfileIdentifier> Create(string? value)
    {
        ProfileValidationError error = ProfileTextValidator.ValidateExtensibleIdentifier(
            value,
            ProfileValidationError.NullMarketplaceProfileIdentifier,
            ProfileValidationError.EmptyMarketplaceProfileIdentifier,
            ProfileValidationError.WhitespaceOnlyMarketplaceProfileIdentifier,
            ProfileValidationError.MalformedMarketplaceProfileIdentifier);
        return error == ProfileValidationError.None
            ? ProfileValidationResult<MarketplaceProfileIdentifier>.Success(
                new MarketplaceProfileIdentifier(value!))
            : ProfileValidationResult<MarketplaceProfileIdentifier>.Failure(error);
    }

    public bool Equals(MarketplaceProfileIdentifier? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is MarketplaceProfileIdentifier other && Equals(other);

    public override int GetHashCode() => StableProfileHash.Create().Add(Value).ToHashCode();

    public override string ToString() => Value;
}
