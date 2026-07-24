using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Identifiers;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Domain.Tests.Profiles;

public sealed class BrandingTests
{
    [Fact]
    public void BrandingRolesHaveStableOrderAndExactParsing()
    {
        Assert.Equal(
            ["logo", "watermark"],
            BrandingImageRole.All.Select(value => value.CanonicalIdentifier));
        foreach (BrandingImageRole role in BrandingImageRole.All)
        {
            CanonicalIdentifierParseResult<BrandingImageRole> parsed = BrandingImageRole.TryParse(role.CanonicalIdentifier);
            Assert.True(parsed.IsValid);
            Assert.Same(role, parsed.Value);
            Assert.Equal(role.CanonicalIdentifier, role.ToString());
            Assert.Equal(role, parsed.Value);
            Assert.NotEqual(
                role,
                BrandingImageRole.All.First(value => !value.Equals(role)));
            Assert.False(role.Equals(
                (object)BrandingImageRole.All.First(value => !value.Equals(role))));
            Assert.Equal(role.GetHashCode(), parsed.Value!.GetHashCode());
            Assert.False(role.Equals((BrandingImageRole?)null));
            Assert.False(role.Equals(new object()));
        }

        Assert.Equal(
            CanonicalIdentifierParseError.WhitespaceOnly,
            BrandingImageRole.TryParse(" ").Error);
        Assert.Equal(
            CanonicalIdentifierParseError.Unknown,
            BrandingImageRole.TryParse("banner").Error);
    }

    [Fact]
    public void BrandingImageRequiresRoleAndImageSource()
    {
        SourceAsset image = ProfileTestAssertions.Image();
        BrandingImage logo = ProfileTestAssertions.Success(
            BrandingImage.Create(BrandingImageRole.Logo, image));

        Assert.Same(BrandingImageRole.Logo, logo.Role);
        Assert.Same(image, logo.Source);
        Assert.Equal(logo, ProfileTestAssertions.BrandingImage());
        Assert.NotEqual(
            logo,
            ProfileTestAssertions.BrandingImage(
                BrandingImageRole.Watermark,
                "branding/watermark.png"));
        Assert.NotEqual(
            logo,
            ProfileTestAssertions.BrandingImage(
                BrandingImageRole.Logo,
                "branding/other.png"));
        Assert.False(logo.Equals((object)ProfileTestAssertions.BrandingImage(
            BrandingImageRole.Logo,
            "branding/other.png")));
        Assert.Equal(logo.GetHashCode(), ProfileTestAssertions.BrandingImage().GetHashCode());
        Assert.False(logo.Equals((BrandingImage?)null));
        Assert.False(logo.Equals(new object()));
        ProfileTestAssertions.Failure(
            BrandingImage.Create(null, image),
            ProfileValidationError.NullBrandingImageRole);
        ProfileTestAssertions.Failure(
            BrandingImage.Create(BrandingImageRole.Logo, null),
            ProfileValidationError.NullBrandingSource);
    }

    [Fact]
    public void BrandingImageRejectsEveryNonImageSourceKind()
    {
        foreach ((SourceAssetKind kind, string reference) in new[]
        {
            (SourceAssetKind.Fbx, "model.fbx"),
            (SourceAssetKind.Glb, "model.glb"),
            (SourceAssetKind.Archive, "model.zip"),
        })
        {
            SourceAsset source = Assert.IsType<SourceAsset>(
                SourceAsset.Create(kind, reference).Value);
            ProfileTestAssertions.Failure(
                BrandingImage.Create(BrandingImageRole.Logo, source),
                ProfileValidationError.NonImageBrandingSource);
        }
    }

    [Fact]
    public void BrandingRequiresAtLeastOneUniqueRole()
    {
        BrandingImage logo = ProfileTestAssertions.BrandingImage();
        ProfileTestAssertions.Failure(
            PublisherBranding.Create(null),
            ProfileValidationError.NullBrandingImages);
        ProfileTestAssertions.Failure(
            PublisherBranding.Create([]),
            ProfileValidationError.EmptyBrandingImages);
        ProfileTestAssertions.Failure(
            PublisherBranding.Create([null]),
            ProfileValidationError.NullBrandingImage);
        ProfileTestAssertions.Failure(
            PublisherBranding.Create([logo, ProfileTestAssertions.BrandingImage()]),
            ProfileValidationError.DuplicateBrandingImageRole);
    }

    [Fact]
    public void BrandingReturnsImmutableDeterministicRoleOrder()
    {
        BrandingImage logo = ProfileTestAssertions.BrandingImage(
            BrandingImageRole.Logo,
            "branding/logo.png");
        BrandingImage watermark = ProfileTestAssertions.BrandingImage(
            BrandingImageRole.Watermark,
            "branding/watermark.png");
        var input = new List<BrandingImage?> { watermark, logo };
        PublisherBranding branding = ProfileTestAssertions.Success(
            PublisherBranding.Create(input));
        input.Clear();

        Assert.Equal(
            ["logo", "watermark"],
            branding.Images.Select(image => image.Role.CanonicalIdentifier));
        Assert.Same(logo, branding.Logo);
        Assert.Same(watermark, branding.Watermark);
        IList<BrandingImage> list = Assert.IsType<IList<BrandingImage>>(
            branding.Images,
            exactMatch: false);
        Assert.True(list.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => list.Add(logo));
        Assert.Equal(
            branding,
            ProfileTestAssertions.Branding(watermark, logo));
        Assert.NotEqual(branding, ProfileTestAssertions.Branding(logo));
        Assert.False(branding.Equals((object)ProfileTestAssertions.Branding(logo)));
        Assert.Equal(
            branding.GetHashCode(),
            ProfileTestAssertions.Branding(watermark, logo).GetHashCode());
        Assert.False(branding.Equals((PublisherBranding?)null));
        Assert.False(branding.Equals(new object()));
    }

    [Fact]
    public void SingleBrandingRoleLeavesOtherRoleAbsent()
    {
        PublisherBranding logoOnly = ProfileTestAssertions.Branding(
            ProfileTestAssertions.BrandingImage());

        Assert.NotNull(logoOnly.Logo);
        Assert.Null(logoOnly.Watermark);

        PublisherBranding watermarkOnly = ProfileTestAssertions.Branding(
            ProfileTestAssertions.BrandingImage(
                BrandingImageRole.Watermark,
                "branding/watermark.png"));
        Assert.Null(watermarkOnly.Logo);
        Assert.NotNull(watermarkOnly.Watermark);
    }
}
