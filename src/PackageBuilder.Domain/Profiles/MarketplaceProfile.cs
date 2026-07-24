namespace PackageBuilder.Domain.Profiles;

/// <summary>
/// Represents generic marketplace profile identity without marketplace-specific listing rules.
/// </summary>
public sealed class MarketplaceProfile : IEquatable<MarketplaceProfile>
{
    private MarketplaceProfile(
        MarketplaceIdentifier marketplace,
        MarketplaceProfileIdentifier identity)
    {
        Marketplace = marketplace;
        Identity = identity;
    }

    public MarketplaceIdentifier Marketplace { get; }

    public MarketplaceProfileIdentifier Identity { get; }

    public static ProfileValidationResult<MarketplaceProfile> Create(
        MarketplaceIdentifier? marketplace,
        MarketplaceProfileIdentifier? identity)
    {
        return marketplace is null
            ? ProfileValidationResult<MarketplaceProfile>.Failure(
                ProfileValidationError.NullMarketplaceProfileMarketplace)
            : identity is null
            ? ProfileValidationResult<MarketplaceProfile>.Failure(
                ProfileValidationError.NullMarketplaceProfileIdentity)
            : ProfileValidationResult<MarketplaceProfile>.Success(
            new MarketplaceProfile(marketplace, identity));
    }

    public bool Equals(MarketplaceProfile? other) =>
        other is not null &&
        Marketplace.Equals(other.Marketplace) &&
        Identity.Equals(other.Identity);

    public override bool Equals(object? obj) => obj is MarketplaceProfile other && Equals(other);

    public override int GetHashCode() =>
        StableProfileHash.Create()
            .Add(Marketplace.Value)
            .Add(Identity.Value)
            .ToHashCode();
}
