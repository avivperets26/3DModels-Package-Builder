using System.Globalization;
using System.Reflection;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Domain.Tests.Profiles;

public sealed class ProfileAggregateMarketplaceTests
{
    [Fact]
    public void MarketplaceIdentifiersAreExtensibleOrdinalAndCaseSensitive()
    {
        MarketplaceIdentifier marketplace = ProfileTestAssertions.Marketplace("market-2");
        MarketplaceProfileIdentifier identity =
            ProfileTestAssertions.MarketplaceProfileId("standard-2026");

        Assert.Equal("market-2", marketplace.Value);
        Assert.Equal("standard-2026", identity.Value);
        Assert.Equal("market-2", marketplace.ToString());
        Assert.Equal("standard-2026", identity.ToString());
        Assert.Equal(marketplace, ProfileTestAssertions.Marketplace("market-2"));
        Assert.NotEqual(
            marketplace,
            ProfileTestAssertions.Marketplace("market-3"));
        Assert.Equal(identity, ProfileTestAssertions.MarketplaceProfileId("standard-2026"));
        Assert.NotEqual(identity, ProfileTestAssertions.MarketplaceProfileId("standard-2027"));
        Assert.False(marketplace.Equals((object)ProfileTestAssertions.Marketplace("market-3")));
        Assert.False(identity.Equals(
            (object)ProfileTestAssertions.MarketplaceProfileId("standard-2027")));
        Assert.Equal(
            marketplace.GetHashCode(),
            ProfileTestAssertions.Marketplace("market-2").GetHashCode());
        Assert.Equal(
            identity.GetHashCode(),
            ProfileTestAssertions.MarketplaceProfileId("standard-2026").GetHashCode());
        Assert.False(marketplace.Equals((MarketplaceIdentifier?)null));
        Assert.False(marketplace.Equals(new object()));
        Assert.False(identity.Equals((MarketplaceProfileIdentifier?)null));
        Assert.False(identity.Equals(new object()));
    }

    [Theory]
    [InlineData(null, ProfileValidationError.NullMarketplaceIdentifier)]
    [InlineData("", ProfileValidationError.EmptyMarketplaceIdentifier)]
    [InlineData(" ", ProfileValidationError.WhitespaceOnlyMarketplaceIdentifier)]
    [InlineData("Market", ProfileValidationError.MalformedMarketplaceIdentifier)]
    [InlineData("2market", ProfileValidationError.MalformedMarketplaceIdentifier)]
    [InlineData("market--one", ProfileValidationError.MalformedMarketplaceIdentifier)]
    [InlineData("market-", ProfileValidationError.MalformedMarketplaceIdentifier)]
    [InlineData("market_one", ProfileValidationError.MalformedMarketplaceIdentifier)]
    public void MarketplaceIdentifierRejectsInvalidInput(
        string? value,
        ProfileValidationError error) =>
        ProfileTestAssertions.Failure(MarketplaceIdentifier.Create(value), error);

    [Theory]
    [InlineData(null, ProfileValidationError.NullMarketplaceProfileIdentifier)]
    [InlineData("", ProfileValidationError.EmptyMarketplaceProfileIdentifier)]
    [InlineData(" ", ProfileValidationError.WhitespaceOnlyMarketplaceProfileIdentifier)]
    [InlineData("Profile", ProfileValidationError.MalformedMarketplaceProfileIdentifier)]
    [InlineData("2026-profile", ProfileValidationError.MalformedMarketplaceProfileIdentifier)]
    [InlineData("profile--one", ProfileValidationError.MalformedMarketplaceProfileIdentifier)]
    [InlineData("profile-", ProfileValidationError.MalformedMarketplaceProfileIdentifier)]
    [InlineData("profile/one", ProfileValidationError.MalformedMarketplaceProfileIdentifier)]
    public void MarketplaceProfileIdentifierRejectsInvalidInput(
        string? value,
        ProfileValidationError error) =>
        ProfileTestAssertions.Failure(MarketplaceProfileIdentifier.Create(value), error);

    [Fact]
    public void MarketplaceProfileContainsOnlyGenericMarketplaceIdentity()
    {
        MarketplaceIdentifier marketplace = ProfileTestAssertions.Marketplace();
        MarketplaceProfileIdentifier identity = ProfileTestAssertions.MarketplaceProfileId();
        MarketplaceProfile profile = ProfileTestAssertions.Success(
            MarketplaceProfile.Create(marketplace, identity));

        Assert.Same(marketplace, profile.Marketplace);
        Assert.Same(identity, profile.Identity);
        Assert.Equal(
            profile,
            ProfileTestAssertions.Success(
                MarketplaceProfile.Create(
                    ProfileTestAssertions.Marketplace(),
                    ProfileTestAssertions.MarketplaceProfileId())));
        Assert.NotEqual(
            profile,
            ProfileTestAssertions.Success(
                MarketplaceProfile.Create(
                    ProfileTestAssertions.Marketplace("other-market"),
                    ProfileTestAssertions.MarketplaceProfileId())));
        Assert.NotEqual(
            profile,
            ProfileTestAssertions.Success(
                MarketplaceProfile.Create(
                    marketplace,
                    ProfileTestAssertions.MarketplaceProfileId("other-profile"))));
        Assert.False(profile.Equals((object)ProfileTestAssertions.Success(
            MarketplaceProfile.Create(
                ProfileTestAssertions.Marketplace("other-market"),
                identity))));
        Assert.Equal(
            profile.GetHashCode(),
            ProfileTestAssertions.Success(
                MarketplaceProfile.Create(
                    ProfileTestAssertions.Marketplace(),
                    ProfileTestAssertions.MarketplaceProfileId())).GetHashCode());
        Assert.False(profile.Equals((MarketplaceProfile?)null));
        Assert.False(profile.Equals(new object()));
        ProfileTestAssertions.Failure(
            MarketplaceProfile.Create(null, identity),
            ProfileValidationError.NullMarketplaceProfileMarketplace);
        ProfileTestAssertions.Failure(
            MarketplaceProfile.Create(marketplace, null),
            ProfileValidationError.NullMarketplaceProfileIdentity);
    }

    [Fact]
    public void PublisherProfileReusesConfigurablePublisherRoot()
    {
        PublisherProfile first = ProfileTestAssertions.Publisher("PublisherOne");
        PublisherProfile second = ProfileTestAssertions.Publisher("PublisherTwo");

        Assert.Equal("PublisherOne", first.Root.Value);
        Assert.Equal("PublisherTwo", second.Root.Value);
        Assert.NotEqual(first, second);
        Assert.DoesNotContain(
            typeof(MarketplaceProfile),
            typeof(PublisherProfile).GetProperties().Select(property => property.PropertyType));
        Assert.DoesNotContain(
            typeof(PackageBuilder.Domain.Naming.PublisherRoot),
            typeof(MarketplaceProfile).GetProperties().Select(property => property.PropertyType));
    }

    [Fact]
    public void PublisherProfileAcceptsOptionalBrandingAndRetainsTypedValues()
    {
        PublisherBranding branding = ProfileTestAssertions.Branding(
            ProfileTestAssertions.BrandingImage());
        PublisherProfile profile = ProfileTestAssertions.Publisher(branding: branding);

        Assert.Same(branding, profile.Branding);
        Assert.NotNull(profile.Root);
        Assert.NotNull(profile.DisplayName);
        Assert.NotNull(profile.SupportContact);
        Assert.NotNull(profile.Copyright);
        Assert.NotNull(profile.AiDisclosure);
        Assert.Null(ProfileTestAssertions.Publisher().Branding);
        Assert.Equal(profile, ProfileTestAssertions.Publisher(branding: branding));
        Assert.False(profile.Equals((object)ProfileTestAssertions.Publisher("OtherPublisher")));
        Assert.Equal(
            profile.GetHashCode(),
            ProfileTestAssertions.Publisher(branding: branding).GetHashCode());
        Assert.False(profile.Equals((PublisherProfile?)null));
        Assert.False(profile.Equals(new object()));
    }

    [Fact]
    public void PublisherProfileRejectsEveryMissingRequiredValue()
    {
        PublisherRoot root = ProfileTestAssertions.Root();
        PublisherDisplayName display = ProfileTestAssertions.Display();
        SupportContact contact = ProfileTestAssertions.Contact();
        CopyrightNotice copyright = ProfileTestAssertions.Copyright();
        AiDisclosure disclosure = ProfileTestAssertions.Disclosure();

        ProfileTestAssertions.Failure(
            PublisherProfile.Create(null, display, contact, copyright, disclosure),
            ProfileValidationError.NullPublisherRoot);
        ProfileTestAssertions.Failure(
            PublisherProfile.Create(root, null, contact, copyright, disclosure),
            ProfileValidationError.NullPublisherProfileDisplayName);
        ProfileTestAssertions.Failure(
            PublisherProfile.Create(root, display, null, copyright, disclosure),
            ProfileValidationError.NullPublisherSupportContact);
        ProfileTestAssertions.Failure(
            PublisherProfile.Create(root, display, contact, null, disclosure),
            ProfileValidationError.NullPublisherCopyright);
        ProfileTestAssertions.Failure(
            PublisherProfile.Create(root, display, contact, copyright, null),
            ProfileValidationError.NullPublisherAiDisclosure);
    }

    [Fact]
    public void EqualityHashingAndValidationAreCultureIndependent()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            PublisherProfile invariant = ProfileTestAssertions.Publisher();
            int invariantHash = invariant.GetHashCode();
            MarketplaceProfile market = ProfileTestAssertions.Success(
                MarketplaceProfile.Create(
                    ProfileTestAssertions.Marketplace("istanbul-market"),
                    ProfileTestAssertions.MarketplaceProfileId("identity-1")));
            int marketHash = market.GetHashCode();

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            Assert.Equal(invariant, ProfileTestAssertions.Publisher());
            Assert.Equal(invariantHash, ProfileTestAssertions.Publisher().GetHashCode());
            Assert.Equal(
                marketHash,
                ProfileTestAssertions.Success(
                    MarketplaceProfile.Create(
                        ProfileTestAssertions.Marketplace("istanbul-market"),
                        ProfileTestAssertions.MarketplaceProfileId("identity-1")))
                    .GetHashCode());
            Assert.Equal(
                ProfileValidationError.MalformedMarketplaceIdentifier,
                MarketplaceIdentifier.Create("İstanbul").Error);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void DomainAssemblyHasNoForbiddenProfileDependenciesOrHardCodedPersonalDefaults()
    {
        Assembly assembly = typeof(PublisherProfile).Assembly;
        string[] references = [.. assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty)];

        Assert.DoesNotContain(references, name => name.Contains("Wpf", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Unity", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Unreal", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Blender", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Marketplaces", StringComparison.Ordinal));

        string sourceRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string profilesRoot = Path.Combine(
            sourceRoot,
            "src",
            "PackageBuilder.Domain",
            "Profiles");
        string productionText = string.Join(
            "\n",
            Directory.GetFiles(profilesRoot, "*.cs")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("AvivPeretsFBX", productionText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"fab\"", productionText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.IO", productionText, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", productionText, StringComparison.Ordinal);
    }
}
