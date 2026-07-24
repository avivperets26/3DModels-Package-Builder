using System.Globalization;
using PackageBuilder.Domain.Assets;

namespace PackageBuilder.Domain.Tests.Assets;

[Trait("Task", "PB-0103")]
public sealed class SourceAssetTests
{
    [Theory]
    [InlineData("Model.fbx", null)]
    [InlineData("Models/Épée Finale.FBX", "Épée Finale.FBX")]
    public void CreateAcceptsFbxReferencesAndPreservesUnicodeAndCase(
        string reference,
        string? originalFileName)
    {
        SourceAsset asset = SourceAssetTestAssertions.AssertSuccess(
            SourceAsset.Create(SourceAssetKind.Fbx, reference, originalFileName));

        Assert.Same(SourceAssetKind.Fbx, asset.Kind);
        Assert.Equal(reference, asset.LogicalReference);
        Assert.Equal(originalFileName, asset.OriginalFileName);
        Assert.Equal(reference, asset.ToString());
    }

    [Theory]
    [InlineData("scene.glb", "SCENE.GLB")]
    [InlineData("Downloads/source.ZIP", "source.zip")]
    [InlineData("Textures/刀 Base Color.custom-image", "刀 Base Color.custom-image")]
    [InlineData("Textures/image-without-extension", null)]
    [InlineData("a", null)]
    public void CreateAcceptsOtherKindsWithoutInventingImageExtensions(
        string reference,
        string? originalFileName)
    {
        SourceAssetKind kind = reference.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
            ? SourceAssetKind.Glb
            : reference.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? SourceAssetKind.Archive
                : SourceAssetKind.Image;

        SourceAsset asset = SourceAssetTestAssertions.AssertSuccess(
            SourceAsset.Create(kind, reference, originalFileName));

        Assert.Same(kind, asset.Kind);
        Assert.Equal(reference, asset.LogicalReference);
    }

    [Theory]
    [InlineData(null, SourceAssetValidationError.NullLogicalReference)]
    [InlineData("", SourceAssetValidationError.EmptyLogicalReference)]
    [InlineData(" \t ", SourceAssetValidationError.WhitespaceOnlyLogicalReference)]
    [InlineData("/root/model.fbx", SourceAssetValidationError.RootedLogicalReference)]
    [InlineData("\\\\server\\model.fbx", SourceAssetValidationError.RootedLogicalReference)]
    [InlineData("C:/root/model.fbx", SourceAssetValidationError.RootedLogicalReference)]
    [InlineData("C:\\root\\model.fbx", SourceAssetValidationError.RootedLogicalReference)]
    [InlineData("C:", SourceAssetValidationError.DriveRelativeLogicalReference)]
    [InlineData("C:model.fbx", SourceAssetValidationError.DriveRelativeLogicalReference)]
    [InlineData("c:model.fbx", SourceAssetValidationError.DriveRelativeLogicalReference)]
    [InlineData("1:model.fbx", SourceAssetValidationError.UriLikeLogicalReference)]
    [InlineData("https://example.test/model.fbx", SourceAssetValidationError.UriLikeLogicalReference)]
    [InlineData("folder/model:alternate.fbx", SourceAssetValidationError.UriLikeLogicalReference)]
    [InlineData("folder\\model.fbx", SourceAssetValidationError.InvalidSeparator)]
    [InlineData("folder//model.fbx", SourceAssetValidationError.EmptySegment)]
    [InlineData("folder/", SourceAssetValidationError.EmptySegment)]
    [InlineData("folder/./model.fbx", SourceAssetValidationError.TraversalSegment)]
    [InlineData("../model.fbx", SourceAssetValidationError.TraversalSegment)]
    [InlineData("folder/ model.fbx", SourceAssetValidationError.LeadingOrTrailingWhitespace)]
    [InlineData("folder/model.fbx ", SourceAssetValidationError.LeadingOrTrailingWhitespace)]
    [InlineData("folder/model\0.fbx", SourceAssetValidationError.ControlCharacter)]
    public void CreateRejectsUnsafeOrMalformedLogicalReferences(
        string? reference,
        SourceAssetValidationError expectedError) =>
        SourceAssetTestAssertions.AssertFailure(
            SourceAsset.Create(SourceAssetKind.Fbx, reference),
            expectedError);

    [Fact]
    public void CreateRejectsNullKindBeforeReferenceValidation() =>
        SourceAssetTestAssertions.AssertFailure(
            SourceAsset.Create(null, null),
            SourceAssetValidationError.NullKind);

    [Theory]
    [InlineData(SourceAssetValidationError.ExtensionKindMismatch, "model.glb", null)]
    [InlineData(SourceAssetValidationError.ExtensionKindMismatch, ".fbx", null)]
    [InlineData(SourceAssetValidationError.EmptyOriginalFileName, "model.fbx", "")]
    [InlineData(SourceAssetValidationError.InvalidOriginalFileName, "model.fbx", " ")]
    [InlineData(SourceAssetValidationError.InvalidOriginalFileName, "model.fbx", "folder/model.fbx")]
    [InlineData(SourceAssetValidationError.InvalidOriginalFileName, "model.fbx", "bad\0.fbx")]
    [InlineData(
        SourceAssetValidationError.OriginalFileNameExtensionKindMismatch,
        "model.fbx",
        "model.glb")]
    public void CreateRejectsExtensionAndOriginalFilenameContradictions(
        SourceAssetValidationError expectedError,
        string reference,
        string? originalFileName) =>
        SourceAssetTestAssertions.AssertFailure(
            SourceAsset.Create(SourceAssetKind.Fbx, reference, originalFileName),
            expectedError);

    [Theory]
    [InlineData("archive.rar")]
    [InlineData("archive")]
    public void ArchiveKindAcceptsOnlyTheExplicitZipExtension(string reference) =>
        SourceAssetTestAssertions.AssertFailure(
            SourceAsset.Create(SourceAssetKind.Archive, reference),
            SourceAssetValidationError.ExtensionKindMismatch);

    [Fact]
    public void EqualityAndHashingAreOrdinalAndIncludeEveryField()
    {
        SourceAsset first = CreateImage("Textures/Base.png", "Base.png");
        SourceAsset same = CreateImage("Textures/Base.png", "Base.png");
        SourceAsset differentCase = CreateImage("textures/Base.png", "Base.png");
        SourceAsset differentOriginal = CreateImage("Textures/Base.png", "base.png");
        SourceAsset noOriginal = CreateImage("Textures/Base.png", null);
        SourceAsset fbx = SourceAssetTestAssertions.AssertSuccess(
            SourceAsset.Create(SourceAssetKind.Fbx, "Textures/Base.fbx", "Base.fbx"));

        Assert.True(first.Equals(same));
        Assert.True(first.Equals((object)same));
        Assert.False(first.Equals(differentCase));
        Assert.False(first.Equals(differentOriginal));
        Assert.False(first.Equals(noOriginal));
        Assert.False(first.Equals(fbx));
        Assert.False(first.Equals((SourceAsset?)null));
        Assert.False(first.Equals("Textures/Base.png"));
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        _ = noOriginal.GetHashCode();
    }

    [Fact]
    public void EqualityAndHashingAreCultureInvariant()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        SourceAsset upper = CreateImage("Textures/FILE.PNG", "FILE.PNG");
        SourceAsset lower = CreateImage("Textures/file.png", "file.png");
        int hash = upper.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            SourceAsset same = CreateImage("Textures/FILE.PNG", "FILE.PNG");
            Assert.True(upper.Equals(same));
            Assert.False(upper.Equals(lower));
            Assert.Equal(hash, same.GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private static SourceAsset CreateImage(string reference, string? originalFileName) =>
        SourceAssetTestAssertions.AssertSuccess(
            SourceAsset.Create(SourceAssetKind.Image, reference, originalFileName));
}
