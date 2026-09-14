using System.Collections.Immutable;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PackageBuilder.App.Wpf.Media;
using PackageBuilder.Contracts.Media;
using PackageBuilder.Domain.Preview;
using PackageBuilder.Domain.Products;
using PackageBuilder.Marketplaces.Fab;
using static PackageBuilder.App.Wpf.Tests.FabValidatorFixtures;

namespace PackageBuilder.App.Wpf.Tests;

public sealed class FabMediaGalleryValidatorTests
{
    [Theory]
    [InlineData("static")]
    [InlineData("rigged")]
    [InlineData("rigged-animated")]
    [InlineData("item-set")]
    [InlineData("item-collection")]
    public void RequiredViewsForAllFiveProductCasesUseTheSharedPolicy(string productCase)
    {
        ProductCase selected = ProductCase.TryParse(productCase).Value!;
        FabValidationContext context = Context() with { Listing = new(selected, "3d-model", ["unity"]) };
        FabGalleryImage hero = Image();
        PreviewViewKind? additional = productCase switch
        {
            "rigged-animated" => PreviewViewKind.AnimationPose,
            "item-set" => PreviewViewKind.SetOverview,
            "item-collection" => PreviewViewKind.CollectionOverview,
            _ => null,
        };
        ImmutableArray<FabGalleryImage> images = additional is null ? [hero]
            : [hero, hero with { ArtifactId = Id("Extra"), FileName = "Extra.png", View = additional, IsThumbnail = false }];
        Assert.True(new FabMediaGalleryValidator(new WindowsPreviewImageCodec()).Validate(Profile(), context, images, Token).Passed);
    }

    [Fact]
    public void MissingRuleAndCancelledGalleryNeverProducePass()
    {
        FabGalleryImage image = Image();
        Has(Validate([image], FabRequirementsBaseline.Profile), "FAB_RULE_REVIEW_REQUIRED");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _ = Assert.ThrowsAny<OperationCanceledException>(() => new FabMediaGalleryValidator(new WindowsPreviewImageCodec()).Validate(Profile(), Context(), [image], cancelled.Token));
    }
    [Theory]
    [InlineData(false, 1920, 1080)]
    [InlineData(true, 1920, 1080)]
    [InlineData(false, 2048, 1080)]
    public void RealPngAndJpegDecodeAndLargerValidImagesPass(bool jpeg, int width, int height)
    {
        FabGalleryImage image = Image(jpeg, width, height);
        Assert.True(Validate([image]).Passed);
        EncodedImageInfo measured = new WindowsPreviewImageCodec().Inspect(image.Bytes);
        Assert.Equal(width, measured.Width);
        Assert.Equal(jpeg ? EncodedImageFormat.Jpeg : EncodedImageFormat.Png, measured.Format);
    }

    [Theory]
    [InlineData(1919, 1080)]
    [InlineData(1920, 1079)]
    public void MinimumImageDimensionsAreMeasuredFromPixels(int width, int height) =>
        Has(Validate([Image(false, width, height)]), "FAB_LIMIT_EXCEEDED");

    [Theory]
    [InlineData("truncated", "FAB_IMAGE_INVALID")]
    [InlineData("wrong-extension", "FAB_IMAGE_FORMAT_INVALID")]
    [InlineData("changed-bytes", "FAB_CONTENT_CHANGED")]
    [InlineData("wrong-product", "FAB_MEDIA_IRRELEVANT")]
    [InlineData("unknown-item", "FAB_MEDIA_IRRELEVANT")]
    [InlineData("thumbnail", "FAB_THUMBNAIL_INVALID")]
    [InlineData("wrong-view", "FAB_MEDIA_IRRELEVANT")]
    public void InvalidFinalImagesCannotPass(string mutation, string code)
    {
        FabGalleryImage image = Image();
        image = mutation switch
        {
            "truncated" => image with { Bytes = image.Bytes[..^10], Content = Identity(image.Bytes.Span[..^10]) },
            "wrong-extension" => image with { FileName = "Hero.jpg" },
            "changed-bytes" => image with { Content = Identity("changed"u8) },
            "wrong-product" => image with { ProductKey = "Other" },
            "unknown-item" => image with { ItemKeys = ["Other"] },
            "thumbnail" => image with { IsThumbnail = false },
            _ => image with { View = PreviewViewKind.CollectionOverview },
        };
        Has(Validate([image]), code);
    }

    [Fact]
    public void StrictImageAndGalleryLimitsArePinnedAndThumbnailIsNotDoubleCounted()
    {
        FabGalleryImage image = Image();
        long bytes = image.Bytes.Length;
        Assert.True(Validate([image], Profile(root => { Limit(root, "image-bytes", bytes + 1); Limit(root, "gallery-bytes", bytes + 1); })).Passed);
        Has(Validate([image], Profile(root => { Limit(root, "image-bytes", bytes); Limit(root, "gallery-bytes", bytes); })), "FAB_LIMIT_EXCEEDED");
        FabGalleryImage second = image with { ArtifactId = Id("Second"), FileName = "Front.png", View = PreviewViewKind.OrthographicFront, IsThumbnail = false };
        Has(Validate([image, second], Profile(root => { Limit(root, "image-bytes", bytes + 1); Limit(root, "gallery-bytes", bytes * 2); })), "FAB_LIMIT_EXCEEDED");
    }

    [Fact]
    public void CollectionsNeedOverviewEvidenceCoveringEveryManifestItem()
    {
        FabValidationContext context = Context() with { Listing = new(ProductCase.ItemCollection, "3d-model", ["unity"]), ItemKeys = ["Item", "Other"] };
        FabGalleryImage hero = Image();
        FabGalleryImage overview = hero with { ArtifactId = Id("Overview"), FileName = "Overview.png", View = PreviewViewKind.CollectionOverview, ItemKeys = context.ItemKeys, IsThumbnail = false };
        Assert.True(new FabMediaGalleryValidator(new WindowsPreviewImageCodec()).Validate(Profile(), context, [hero, overview], Token).Passed);
        Has(new FabMediaGalleryValidator(new WindowsPreviewImageCodec()).Validate(Profile(), context, [hero, overview with { ItemKeys = ["Item"] }], Token), "FAB_GALLERY_COVERAGE_MISSING");
    }

    [Fact]
    public void SharedAnimatedProductViewPolicyRequiresPose()
    {
        FabValidationContext context = Context() with { Listing = new(ProductCase.RiggedAnimated, "3d-model", ["unity"]) };
        Has(new FabMediaGalleryValidator(new WindowsPreviewImageCodec()).Validate(Profile(), context, [Image()], Token), "FAB_GALLERY_COVERAGE_MISSING");
    }

    [Fact]
    public void DuplicateAndEmptyGalleriesAreRejected()
    {
        Has(Validate([]), "FAB_GALLERY_MISSING");
        FabGalleryImage image = Image();
        Has(Validate([image, image]), "FAB_GALLERY_DUPLICATE");
    }

    [Fact]
    public void GalleryFindingsSerializeThroughExistingReportContract()
    {
        FabArtifactValidation result = Validate([Image() with { ProductKey = "Other" }]);
        foreach (PackageBuilder.Domain.Validation.ValidationFinding finding in result.Findings)
        { Assert.True(PackageBuilder.Contracts.Validation.ValidationFindingJson.Serialize(finding).IsSuccessful); }
    }

    private static FabArtifactValidation Validate(ImmutableArray<FabGalleryImage> images, FabRequirementsProfile? profile = null) =>
        new FabMediaGalleryValidator(new WindowsPreviewImageCodec()).Validate(profile ?? Profile(), Context(), images, Token);

    private static FabGalleryImage Image(bool jpeg = false, int width = 1920, int height = 1080)
    {
        byte[] pixels = new byte[width * height * 3];
        Array.Fill(pixels, (byte)100);
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Rgb24, null, pixels, width * 3);
        BitmapEncoder encoder = jpeg ? new JpegBitmapEncoder() : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        byte[] bytes = stream.ToArray();
        return new(Id(), Identity(bytes), jpeg ? "Hero.jpg" : "Hero.png", bytes, "Product", PreviewViewKind.Hero, ["Item"], true);
    }
}
