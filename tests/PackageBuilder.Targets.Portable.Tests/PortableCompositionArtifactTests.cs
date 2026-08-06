using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Portable.Tests;

public sealed class PortableCompositionArtifactTests
{
    [Fact]
    public void ValidPurposeShapesAreAccepted()
    {
        AssertValid("fbx", PortableArtifactPurpose.Fbx);
        AssertValid("archive", PortableArtifactPurpose.FbxArchive);
        AssertValid("flat-readme", PortableArtifactPurpose.FlatReadme);
        AssertValid("product-readme", PortableArtifactPurpose.ProductReadme);
        AssertValid("glb", PortableArtifactPurpose.Glb, glbVariant: PortableGlbVariant.Rigged);
        AssertValid("texture", PortableArtifactPurpose.Texture, textureRole: TextureRole.Albedo, extension: PortableFileExtension.Png);
        AssertValid("media", PortableArtifactPurpose.Media, mediaView: PortableMediaView.Cover, extension: PortableFileExtension.Jpg);
    }

    [Fact]
    public void MissingRecordOrPurposeIsRejected()
    {
        Assert.Equal(
            PortableValidationError.NullArtifactRecord,
            PortableCompositionArtifact.Create(null, PortableArtifactPurpose.Fbx).Error);
        Assert.Equal(
            PortableValidationError.NullArtifactPurpose,
            PortableCompositionArtifact.Create(PortableTestValues.Record("fbx", "normalized-fbx"), null).Error);
    }

    [Fact]
    public void StoreRecordMustBeValidatedPortableAndRoleMatched()
    {
        Assert.Equal(
            PortableValidationError.ArtifactNotValidated,
            PortableCompositionArtifact.Create(
                PortableTestValues.Record("staged", "normalized-fbx", BuildArtifactLifecycleState.Staged),
                PortableArtifactPurpose.Fbx).Error);
        Assert.Equal(
            PortableValidationError.ArtifactTargetMismatch,
            PortableCompositionArtifact.Create(
                PortableTestValues.Record("unity", "normalized-fbx", target: BuildTarget.Unity),
                PortableArtifactPurpose.Fbx).Error);
        Assert.Equal(
            PortableValidationError.ArtifactTargetMismatch,
            PortableCompositionArtifact.Create(
                RecordWithNullTarget(),
                PortableArtifactPurpose.Fbx).Error);
        Assert.Equal(
            PortableValidationError.ArtifactRoleMismatch,
            PortableCompositionArtifact.Create(
                PortableTestValues.Record("wrong-role", "portable-media"),
                PortableArtifactPurpose.Fbx).Error);
    }

    [Theory]
    [InlineData("fbx")]
    [InlineData("glb")]
    [InlineData("texture")]
    [InlineData("media")]
    public void InvalidPurposeQualifierShapesAreRejected(string purposeIdentifier)
    {
        PortableArtifactPurpose purpose = PortableArtifactPurpose.All.Single(value => value.Identifier == purposeIdentifier);
        PortableValidationResult<PortableCompositionArtifact> result = purposeIdentifier switch
        {
            "fbx" => PortableCompositionArtifact.Create(
                PortableTestValues.Record("fbx-extra", "normalized-fbx"),
                purpose,
                extension: PortableFileExtension.Png),
            "glb" => PortableCompositionArtifact.Create(
                PortableTestValues.Record("glb-missing", "normalized-glb"),
                purpose),
            "texture" => PortableCompositionArtifact.Create(
                PortableTestValues.Record("texture-missing", "portable-texture"),
                purpose,
                textureRole: TextureRole.Albedo),
            _ => PortableCompositionArtifact.Create(
                PortableTestValues.Record("media-png", "portable-media"),
                purpose,
                mediaView: PortableMediaView.Cover,
                extension: PortableFileExtension.Png),
        };

        Assert.Equal(PortableValidationError.InvalidArtifactQualifier, result.Error);
    }

    [Fact]
    public void UnsupportedTextureRoleIsRejected()
    {
        PortableValidationResult<PortableCompositionArtifact> result = PortableCompositionArtifact.Create(
            PortableTestValues.Record("opacity", "portable-texture"),
            PortableArtifactPurpose.Texture,
            TextureRole.Opacity,
            extension: PortableFileExtension.Png);

        Assert.Equal(PortableValidationError.UnsupportedTextureRole, result.Error);
    }

    private static void AssertValid(
        string id,
        PortableArtifactPurpose purpose,
        TextureRole? textureRole = null,
        PortableMediaView? mediaView = null,
        PortableFileExtension? extension = null,
        PortableGlbVariant? glbVariant = null)
    {
        PortableValidationResult<PortableCompositionArtifact> result = PortableCompositionArtifact.Create(
            PortableTestValues.Record(id, PortableTestValues.RoleFor(purpose)),
            purpose,
            textureRole,
            mediaView,
            extension,
            glbVariant);

        Assert.True(result.IsValid);
        Assert.Equal(PortableValidationError.None, result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(purpose, result.Value.Purpose);
    }

    private static ArtifactStoreRecord RecordWithNullTarget()
    {
        BuildArtifact artifact = Assert.IsType<BuildArtifact>(
            BuildArtifact.Create(
                BuildArtifactId.Create("null-target").Value,
                BuildJobId.Create("Job-0502").Value,
                BuildStepId.Create("Step-Portable").Value,
                BuildArtifactRole.Create("normalized-fbx").Value,
                null,
                BuildArtifactLifecycleState.Validated,
                "inputs/null-target.fbx",
                new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 6, 12, 1, 0, TimeSpan.Zero)).Value);
        ArtifactContentIdentity identity = Assert.IsType<ArtifactContentIdentity>(
            ArtifactContentIdentity.Create(1, Sha256Digest.Create(new string('b', 64)).Value).Value);
        ArtifactStoreLayout layout = Assert.IsType<ArtifactStoreLayout>(
            ArtifactStoreLayoutFactory.Create(
                Path.Combine(AppContext.BaseDirectory, "artifact-store"),
                artifact.JobId,
                artifact.Id).Value);
        return new ArtifactStoreRecord(artifact, identity, layout);
    }
}
