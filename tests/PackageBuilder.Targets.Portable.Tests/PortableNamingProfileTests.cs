using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Portable.Tests;

public sealed class PortableNamingProfileTests
{
    [Fact]
    public void ExampleIdentityProducesApprovedPortableNames()
    {
        PortableNamingProfile profile = PortableTestValues.Naming();

        Assert.Equal("Silverwing_Talonbow", profile.ProductFolderName);
        Assert.Equal("Silverwing_Talonbow_fbx", profile.FlatFbxFolderName);
        Assert.Equal("SilverwingTalonbow.fbx", profile.FbxFileName);
        Assert.Equal("Silverwing_Talonbow_FBX.zip", profile.FbxArchiveFileName);
        Assert.Equal("Silverwing_Talonbow.glb", profile.GetGlbFileName(PortableGlbVariant.Standard).Value);
        Assert.Equal("Silverwing_Talonbow_rigged.glb", profile.GetGlbFileName(PortableGlbVariant.Rigged).Value);
        Assert.Equal("README_SilverwingTalonbow.txt", profile.FlatReadmeFileName);
        Assert.Equal("README_Silverwing_Talonbow.txt", profile.ProductReadmeFileName);
    }

    [Theory]
    [InlineData("albedo", "T_SilverwingTalonbow_Albedo.png")]
    [InlineData("normal", "T_SilverwingTalonbow_Normal.png")]
    [InlineData("metallic", "T_SilverwingTalonbow_Metallic.png")]
    [InlineData("roughness", "T_SilverwingTalonbow_Roughness.png")]
    [InlineData("emission", "T_SilverwingTalonbow_Emission.png")]
    [InlineData("ambient-occlusion", "T_SilverwingTalonbow_AO.png")]
    public void CanonicalTextureRolesProduceExactNames(string identifier, string expected)
    {
        TextureRole role = Assert.IsType<TextureRole>(TextureRole.TryParse(identifier).Value);

        Assert.Equal(expected, PortableTestValues.Naming().GetTextureFileName(role, PortableFileExtension.Png).Value);
    }

    [Fact]
    public void MediaNamesMatchApprovedTopLevelExamples()
    {
        PortableNamingProfile profile = PortableTestValues.Naming();
        string[] expected =
        [
            "Silverwing_Talonbow_Cover.jpg",
            "Silverwing_Talonbow_Front.jpg",
            "Silverwing_Talonbow_Back.jpg",
            "Silverwing_Talonbow_Left.jpg",
            "Silverwing_Talonbow_Right.jpg",
        ];

        Assert.Equal(
            expected,
            PortableMediaView.All.Select(view => profile.GetMediaFileName(view, PortableFileExtension.Jpg).Value));
    }

    [Fact]
    public void GeneratedExampleNamesAreCaseInsensitiveCollisionFree()
    {
        PortableNamingProfile profile = PortableTestValues.Naming();
        string[] names =
        [
            profile.FbxFileName,
            profile.FbxArchiveFileName,
            profile.FlatReadmeFileName,
            profile.ProductReadmeFileName,
            profile.GetGlbFileName(PortableGlbVariant.Standard).Value!,
            profile.GetGlbFileName(PortableGlbVariant.Rigged).Value!,
            .. TextureRole.All.Take(6).Select(role => profile.GetTextureFileName(role, PortableFileExtension.Png).Value!),
            .. PortableMediaView.All.Select(view => profile.GetMediaFileName(view, PortableFileExtension.Jpg).Value!),
        ];

        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ProfileRejectsMissingTypedIdentity()
    {
        Assert.Equal(
            PortableValidationError.NullAssetId,
            PortableNamingProfile.Create(null, ProductFolderName.Create("Product").Value).Error);
        Assert.Equal(
            PortableValidationError.NullFolderName,
            PortableNamingProfile.Create(InternalAssetId.Create("Product").Value, null).Error);
    }

    [Fact]
    public void NameMethodsRejectMissingOrUnsupportedQualifiers()
    {
        PortableNamingProfile profile = PortableTestValues.Naming();

        Assert.Equal(PortableValidationError.NullTextureRole, profile.GetTextureFileName(null, PortableFileExtension.Png).Error);
        Assert.Equal(PortableValidationError.NullExtension, profile.GetTextureFileName(TextureRole.Albedo, null).Error);
        Assert.Equal(PortableValidationError.UnsupportedTextureRole, profile.GetTextureFileName(TextureRole.Opacity, PortableFileExtension.Png).Error);
        Assert.Equal(PortableValidationError.UnsupportedTextureRole, profile.GetTextureFileName(TextureRole.Height, PortableFileExtension.Png).Error);
        Assert.Equal(PortableValidationError.NullGlbVariant, profile.GetGlbFileName(null).Error);
        Assert.Equal(PortableValidationError.NullMediaView, profile.GetMediaFileName(null, PortableFileExtension.Jpg).Error);
        Assert.Equal(PortableValidationError.NullExtension, profile.GetMediaFileName(PortableMediaView.Cover, null).Error);
    }

    [Theory]
    [InlineData(null, PortableValidationError.NullExtension)]
    [InlineData("", PortableValidationError.InvalidExtension)]
    [InlineData("png", PortableValidationError.InvalidExtension)]
    [InlineData(".", PortableValidationError.InvalidExtension)]
    [InlineData(".PNG", PortableValidationError.InvalidExtension)]
    [InlineData(".p-ng", PortableValidationError.InvalidExtension)]
    [InlineData(".{", PortableValidationError.InvalidExtension)]
    [InlineData(".abcdefghijkl", PortableValidationError.InvalidExtension)]
    public void ExtensionRejectsNoncanonicalInput(string? input, PortableValidationError expected) =>
        Assert.Equal(expected, PortableFileExtension.Create(input).Error);

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".exr")]
    [InlineData(".ktx2")]
    public void ExtensionPreservesApprovedCanonicalInput(string input)
    {
        PortableFileExtension extension = Assert.IsType<PortableFileExtension>(PortableFileExtension.Create(input).Value);

        Assert.Equal(input, extension.Value);
        Assert.Equal(input, extension.ToString());
        Assert.Equal(extension, PortableFileExtension.Create(input).Value);
        Assert.Equal(extension.GetHashCode(), PortableFileExtension.Create(input).Value!.GetHashCode());
        Assert.True(extension.Equals((object)PortableFileExtension.Create(input).Value!));
        Assert.False(extension.Equals(null));
        Assert.False(extension.Equals(new object()));
    }

    [Fact]
    public void CanonicalQualifierCollectionsAreStableAndImmutable()
    {
        Assert.Equal(["standard", "rigged"], PortableGlbVariant.All.Select(value => value.Identifier));
        Assert.Equal(["cover", "front", "back", "left", "right"], PortableMediaView.All.Select(value => value.Identifier));
        Assert.Equal(
            ["fbx", "glb", "fbx-archive", "flat-readme", "product-readme", "texture", "media"],
            PortableArtifactPurpose.All.Select(value => value.Identifier));
        _ = Assert.Throws<NotSupportedException>(() => ((IList<PortableMediaView>)PortableMediaView.All).Add(PortableMediaView.Cover));
        Assert.Equal("rigged", PortableGlbVariant.Rigged.ToString());
        Assert.Equal("cover", PortableMediaView.Cover.ToString());
        Assert.Equal("fbx", PortableArtifactPurpose.Fbx.ToString());
        Assert.Equal(PortableGlbVariant.Rigged, PortableGlbVariant.Rigged);
        Assert.True(PortableGlbVariant.Rigged.Equals((object)PortableGlbVariant.Rigged));
        Assert.False(PortableGlbVariant.Rigged.Equals(PortableGlbVariant.Standard));
        Assert.False(PortableGlbVariant.Rigged.Equals(null));
        Assert.False(PortableGlbVariant.Rigged.Equals(new object()));
        Assert.Equal(PortableGlbVariant.Rigged.GetHashCode(), PortableGlbVariant.Rigged.GetHashCode());
        Assert.True(PortableMediaView.Cover.Equals((object)PortableMediaView.Cover));
        Assert.False(PortableMediaView.Cover.Equals(PortableMediaView.Front));
        Assert.False(PortableMediaView.Cover.Equals(null));
        Assert.False(PortableMediaView.Cover.Equals(new object()));
        Assert.Equal(PortableMediaView.Cover.GetHashCode(), PortableMediaView.Cover.GetHashCode());
        Assert.True(PortableArtifactPurpose.Fbx.Equals((object)PortableArtifactPurpose.Fbx));
        Assert.False(PortableArtifactPurpose.Fbx.Equals(PortableArtifactPurpose.Glb));
        Assert.False(PortableArtifactPurpose.Fbx.Equals(null));
        Assert.False(PortableArtifactPurpose.Fbx.Equals(new object()));
        Assert.Equal(PortableArtifactPurpose.Fbx.GetHashCode(), PortableArtifactPurpose.Fbx.GetHashCode());
    }
}
