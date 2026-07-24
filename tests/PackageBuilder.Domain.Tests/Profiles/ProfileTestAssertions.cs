using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Domain.Tests.Profiles;

internal static class ProfileTestAssertions
{
    public static T Success<T>(ProfileValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid);
        Assert.Equal(ProfileValidationError.None, result.Error);
        return Assert.IsType<T>(result.Value);
    }

    public static void Failure<T>(
        ProfileValidationResult<T> result,
        ProfileValidationError error)
        where T : class
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static PublisherRoot Root(string value = "ExamplePublisher") =>
        Assert.IsType<PublisherRoot>(PublisherRoot.Create(value).Value);

    public static PublisherDisplayName Display(string value = "Example Publisher") =>
        Success(PublisherDisplayName.Create(value));

    public static SupportContact Contact(string value = "support@example.test") =>
        Success(SupportContact.CreateEmail(value));

    public static CopyrightHolder Holder(string value = "Example Publisher") =>
        Success(CopyrightHolder.Create(value));

    public static CopyrightYearPolicy Year(
        CopyrightYearPolicyKind? kind = null,
        int year = 2026,
        int? start = null) =>
        Success(CopyrightYearPolicy.Create(kind ?? CopyrightYearPolicyKind.SingleYear, year, start));

    public static CopyrightNotice Copyright() =>
        Success(CopyrightNotice.Create(Holder(), Year()));

    public static AiDisclosure Disclosure(
        AiDisclosureState? state = null,
        string? text = null) =>
        Success(AiDisclosure.Create(state ?? AiDisclosureState.Undeclared, text));

    public static SourceAsset Image(string reference = "branding/logo.png") =>
        Assert.IsType<SourceAsset>(
            SourceAsset.Create(SourceAssetKind.Image, reference).Value);

    public static BrandingImage BrandingImage(
        BrandingImageRole? role = null,
        string reference = "branding/logo.png") =>
        Success(
            PackageBuilder.Domain.Profiles.BrandingImage.Create(
                role ?? BrandingImageRole.Logo,
                Image(reference)));

    public static PublisherBranding Branding(params BrandingImage[] images) =>
        Success(PublisherBranding.Create(images));

    public static PublisherProfile Publisher(
        string root = "ExamplePublisher",
        PublisherBranding? branding = null) =>
        Success(
            PublisherProfile.Create(
                Root(root),
                Display(),
                Contact(),
                Copyright(),
                Disclosure(),
                branding));

    public static MarketplaceIdentifier Marketplace(string value = "example-market") =>
        Success(MarketplaceIdentifier.Create(value));

    public static MarketplaceProfileIdentifier MarketplaceProfileId(
        string value = "standard-2026") =>
        Success(MarketplaceProfileIdentifier.Create(value));
}
