using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Portable.Tests;

public sealed class PortableFolderComposerTests
{
    [Fact]
    public void CompleteInputProducesExactFlatAndTopLevelLayouts()
    {
        PortableValidationResult<PortableFolderLayout> result = PortableFolderComposer.Compose(
            PortableTestValues.Naming(),
            CompleteArtifacts().Reverse());

        Assert.True(result.IsValid);
        PortableFolderLayout layout = Assert.IsType<PortableFolderLayout>(result.Value);
        Assert.Equal("Silverwing_Talonbow_fbx", layout.FlatFbxFolderName);
        Assert.Equal("Silverwing_Talonbow", layout.ProductFolderName);
        Assert.Equal(
            [
                "README_SilverwingTalonbow.txt",
                "SilverwingTalonbow.fbx",
                "T_SilverwingTalonbow_AO.png",
                "T_SilverwingTalonbow_Albedo.png",
                "T_SilverwingTalonbow_Emission.png",
                "T_SilverwingTalonbow_Metallic.png",
                "T_SilverwingTalonbow_Normal.png",
                "T_SilverwingTalonbow_Roughness.png",
            ],
            layout.FlatFbxEntries.Select(entry => entry.RelativePath));
        Assert.Equal(
            [
                "Media/Silverwing_Talonbow_Back.jpg",
                "Media/Silverwing_Talonbow_Cover.jpg",
                "Media/Silverwing_Talonbow_Front.jpg",
                "Media/Silverwing_Talonbow_Left.jpg",
                "Media/Silverwing_Talonbow_Right.jpg",
                "README_Silverwing_Talonbow.txt",
                "Silverwing_Talonbow_FBX.zip",
                "Silverwing_Talonbow_rigged.glb",
            ],
            layout.ProductEntries.Select(entry => entry.RelativePath));
        Assert.Equal(16, layout.FlatFbxEntries.Count + layout.ProductEntries.Count);
        Assert.Equal(
            16,
            layout.FlatFbxEntries.Concat(layout.ProductEntries)
                .Select(entry => entry.Source.Artifact.Id.Value)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void OptionalGlbAndOptionalCollectionsMayBeAbsent()
    {
        PortableCompositionArtifact[] minimum =
        [
            PortableTestValues.Artifact("fbx", PortableArtifactPurpose.Fbx),
            PortableTestValues.Artifact("archive", PortableArtifactPurpose.FbxArchive),
            PortableTestValues.Artifact("flat-readme", PortableArtifactPurpose.FlatReadme),
            PortableTestValues.Artifact("product-readme", PortableArtifactPurpose.ProductReadme),
        ];

        PortableFolderLayout layout = Assert.IsType<PortableFolderLayout>(
            PortableFolderComposer.Compose(PortableTestValues.Naming(), minimum).Value);

        Assert.Equal(2, layout.FlatFbxEntries.Count);
        Assert.Equal(2, layout.ProductEntries.Count);
        Assert.DoesNotContain(layout.ProductEntries, entry => entry.RelativePath.EndsWith(".glb", StringComparison.Ordinal));
    }

    [Fact]
    public void InputOrderDoesNotChangeOutputOrder()
    {
        PortableCompositionArtifact[] first = CompleteArtifacts();
        PortableCompositionArtifact[] second = [.. first.OrderByDescending(value => value.Record.Artifact.Id.Value, StringComparer.Ordinal)];

        PortableFolderLayout firstLayout = Assert.IsType<PortableFolderLayout>(
            PortableFolderComposer.Compose(PortableTestValues.Naming(), first).Value);
        PortableFolderLayout secondLayout = Assert.IsType<PortableFolderLayout>(
            PortableFolderComposer.Compose(PortableTestValues.Naming(), second).Value);

        Assert.Equal(firstLayout.FlatFbxEntries.Select(value => value.RelativePath), secondLayout.FlatFbxEntries.Select(value => value.RelativePath));
        Assert.Equal(firstLayout.ProductEntries.Select(value => value.RelativePath), secondLayout.ProductEntries.Select(value => value.RelativePath));
    }

    [Fact]
    public void NullInputsFailWithoutThrowing()
    {
        Assert.Equal(PortableValidationError.NullNamingProfile, PortableFolderComposer.Compose(null, []).Error);
        Assert.Equal(PortableValidationError.NullArtifactCollection, PortableFolderComposer.Compose(PortableTestValues.Naming(), null).Error);
        Assert.Equal(PortableValidationError.NullArtifactEntry, PortableFolderComposer.Compose(PortableTestValues.Naming(), [null]).Error);
    }

    [Fact]
    public void EveryRequiredSingletonMustAppearExactlyOnce()
    {
        foreach (PortableArtifactPurpose purpose in new[]
                 {
                     PortableArtifactPurpose.Fbx,
                     PortableArtifactPurpose.FbxArchive,
                     PortableArtifactPurpose.FlatReadme,
                     PortableArtifactPurpose.ProductReadme,
                 })
        {
            PortableCompositionArtifact[] missing = [.. CompleteArtifacts().Where(value => !value.Purpose.Equals(purpose))];
            Assert.Equal(
                PortableValidationError.MissingRequiredArtifact,
                PortableFolderComposer.Compose(PortableTestValues.Naming(), missing).Error);
        }
    }

    [Theory]
    [InlineData("fbx")]
    [InlineData("glb")]
    [InlineData("fbx-archive")]
    [InlineData("flat-readme")]
    [InlineData("product-readme")]
    public void DuplicateSingletonPurposeIsRejected(string identifier)
    {
        List<PortableCompositionArtifact> artifacts = [.. CompleteArtifacts()];
        PortableCompositionArtifact original = artifacts.Single(value => value.Purpose.Identifier == identifier);
        artifacts.Add(CloneWithId(original, $"duplicate-{identifier}"));

        Assert.Equal(
            PortableValidationError.DuplicateArtifactPurpose,
            PortableFolderComposer.Compose(PortableTestValues.Naming(), artifacts).Error);
    }

    [Fact]
    public void DuplicateArtifactIdentityIsRejectedAcrossDifferentPurposes()
    {
        List<PortableCompositionArtifact> artifacts = [.. CompleteArtifacts()];
        PortableCompositionArtifact archive = PortableTestValues.Artifact("fbx", PortableArtifactPurpose.FbxArchive);
        artifacts[artifacts.FindIndex(value => value.Purpose.Equals(PortableArtifactPurpose.FbxArchive))] = archive;

        Assert.Equal(
            PortableValidationError.DuplicateArtifactId,
            PortableFolderComposer.Compose(PortableTestValues.Naming(), artifacts).Error);
    }

    [Fact]
    public void ArtifactIdentityCollisionsAreCaseInsensitiveForPortableFilesystems()
    {
        List<PortableCompositionArtifact> artifacts = [.. CompleteArtifacts()];
        _ = artifacts.RemoveAll(value => value.Purpose.Equals(PortableArtifactPurpose.Glb));
        artifacts.Add(PortableTestValues.Artifact("FBX", PortableArtifactPurpose.Glb, glbVariant: PortableGlbVariant.Rigged));

        Assert.Equal(
            PortableValidationError.DuplicateArtifactId,
            PortableFolderComposer.Compose(PortableTestValues.Naming(), artifacts).Error);
    }

    [Fact]
    public void DuplicateTextureOrMediaSlotsAreRejected()
    {
        List<PortableCompositionArtifact> textureDuplicate = [.. CompleteArtifacts()];
        textureDuplicate.Add(
            PortableTestValues.Artifact(
                "texture-albedo-duplicate",
                PortableArtifactPurpose.Texture,
                TextureRole.Albedo,
                extension: PortableFileExtension.Jpg));
        Assert.Equal(
            PortableValidationError.DuplicateTextureRole,
            PortableFolderComposer.Compose(PortableTestValues.Naming(), textureDuplicate).Error);

        List<PortableCompositionArtifact> mediaDuplicate = [.. CompleteArtifacts()];
        mediaDuplicate.Add(
            PortableTestValues.Artifact(
                "media-cover-duplicate",
                PortableArtifactPurpose.Media,
                mediaView: PortableMediaView.Cover,
                extension: PortableFileExtension.Jpg));
        Assert.Equal(
            PortableValidationError.DuplicateMediaView,
            PortableFolderComposer.Compose(PortableTestValues.Naming(), mediaDuplicate).Error);
    }

    [Fact]
    public void LayoutCollectionsAreReadOnly()
    {
        PortableFolderLayout layout = Assert.IsType<PortableFolderLayout>(
            PortableFolderComposer.Compose(PortableTestValues.Naming(), CompleteArtifacts()).Value);

        _ = Assert.Throws<NotSupportedException>(
            () => ((IList<PortableFolderEntry>)layout.FlatFbxEntries).Add(layout.FlatFbxEntries[0]));
        _ = Assert.Throws<NotSupportedException>(
            () => ((IList<PortableFolderEntry>)layout.ProductEntries).Add(layout.ProductEntries[0]));
    }

    private static PortableCompositionArtifact CloneWithId(PortableCompositionArtifact source, string id) =>
        PortableTestValues.Artifact(
            id,
            source.Purpose,
            source.TextureRole,
            source.MediaView,
            source.Extension,
            source.GlbVariant);

    private static PortableCompositionArtifact[] CompleteArtifacts()
    {
        var values = new List<PortableCompositionArtifact>
        {
            PortableTestValues.Artifact("fbx", PortableArtifactPurpose.Fbx),
            PortableTestValues.Artifact("glb", PortableArtifactPurpose.Glb, glbVariant: PortableGlbVariant.Rigged),
            PortableTestValues.Artifact("archive", PortableArtifactPurpose.FbxArchive),
            PortableTestValues.Artifact("flat-readme", PortableArtifactPurpose.FlatReadme),
            PortableTestValues.Artifact("product-readme", PortableArtifactPurpose.ProductReadme),
        };
        values.AddRange(
            new[]
            {
                TextureRole.Albedo,
                TextureRole.Normal,
                TextureRole.Metallic,
                TextureRole.Roughness,
                TextureRole.Emission,
                TextureRole.AmbientOcclusion,
            }.Select(
                role => PortableTestValues.Artifact(
                    $"texture-{role.CanonicalIdentifier}",
                    PortableArtifactPurpose.Texture,
                    role,
                    extension: PortableFileExtension.Png)));
        values.AddRange(
            PortableMediaView.All.Select(
                view => PortableTestValues.Artifact(
                    $"media-{view.Identifier}",
                    PortableArtifactPurpose.Media,
                    mediaView: view,
                    extension: PortableFileExtension.Jpg)));
        return [.. values];
    }
}
