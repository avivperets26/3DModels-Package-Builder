using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Targets.Portable.Tests;

internal static class PortableTestValues
{
    public static PortableNamingProfile Naming(
        string assetId = "SilverwingTalonbow",
        string folderName = "Silverwing_Talonbow") =>
        Assert.IsType<PortableNamingProfile>(
            PortableNamingProfile.Create(
                InternalAssetId.Create(assetId).Value,
                ProductFolderName.Create(folderName).Value).Value);

    public static ArtifactStoreRecord Record(
        string id,
        string role,
        BuildArtifactLifecycleState? state = null,
        BuildTarget? target = null)
    {
        BuildArtifactId artifactId = Assert.IsType<BuildArtifactId>(BuildArtifactId.Create(id).Value);
        BuildArtifact artifact = Assert.IsType<BuildArtifact>(
            BuildArtifact.Create(
                artifactId,
                BuildJobId.Create("Job-0502").Value,
                BuildStepId.Create("Step-Portable").Value,
                BuildArtifactRole.Create(role).Value,
                target ?? BuildTarget.Portable,
                state ?? BuildArtifactLifecycleState.Validated,
                $"inputs/{id}.bin",
                new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 6, 12, 1, 0, TimeSpan.Zero)).Value);
        ArtifactContentIdentity identity = Assert.IsType<ArtifactContentIdentity>(
            ArtifactContentIdentity.Create(
                1,
                Sha256Digest.Create(new string('a', 64)).Value).Value);
        ArtifactStoreLayout layout = Assert.IsType<ArtifactStoreLayout>(
            ArtifactStoreLayoutFactory.Create(
                Path.Combine(AppContext.BaseDirectory, "artifact-store"),
                artifact.JobId,
                artifact.Id).Value);
        return new ArtifactStoreRecord(artifact, identity, layout);
    }

    public static PortableCompositionArtifact Artifact(
        string id,
        PortableArtifactPurpose purpose,
        PackageBuilder.Domain.Textures.TextureRole? textureRole = null,
        PortableMediaView? mediaView = null,
        PortableFileExtension? extension = null,
        PortableGlbVariant? glbVariant = null) =>
        Assert.IsType<PortableCompositionArtifact>(
            PortableCompositionArtifact.Create(
                Record(id, RoleFor(purpose)),
                purpose,
                textureRole,
                mediaView,
                extension,
                glbVariant).Value);

    public static string RoleFor(PortableArtifactPurpose purpose) => purpose.Identifier switch
    {
        "fbx" => "normalized-fbx",
        "glb" => "normalized-glb",
        "fbx-archive" => "portable-fbx-archive",
        "flat-readme" or "product-readme" => "portable-readme",
        "texture" => "portable-texture",
        "media" => "portable-media",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
    };
}
