using System.Security.Cryptography;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Textures;

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

    public static ArtifactStoreRecord RecordForBytes(
        string id,
        byte[] bytes,
        string role = "normalized-texture",
        BuildArtifactLifecycleState? state = null,
        BuildTarget? target = null)
    {
        ArtifactStoreRecord original = Record(id, role, state, target);
        string digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        ArtifactContentIdentity identity = Assert.IsType<ArtifactContentIdentity>(
            ArtifactContentIdentity.Create(bytes.LongLength, Sha256Digest.Create(digest).Value).Value);
        return new ArtifactStoreRecord(original.Artifact, identity, original.Layout);
    }

    public static TextureAssignment Texture(
        TextureRole? role = null,
        string logicalReference = "textures/source.png")
    {
        TextureRole actualRole = role ?? TextureRole.Albedo;
        SourceAsset source = Assert.IsType<SourceAsset>(
            SourceAsset.Create(SourceAssetKind.Image, logicalReference).Value);
        return Assert.IsType<TextureAssignment>(
            TextureAssignment.Create(
                source,
                actualRole,
                actualRole.RequiredColourSpace,
                actualRole.Equals(TextureRole.Normal) ? NormalConvention.OpenGl : null).Value);
    }

    public static byte[] Png(int width = 4, int height = 2)
    {
        byte[] value = new byte[45];
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        signature.CopyTo(value, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(value.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(value.AsSpan(12, 4));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(value.AsSpan(16, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(value.AsSpan(20, 4), height);
        value[24] = 8;
        value[25] = 6;
        "IEND"u8.CopyTo(value.AsSpan(37, 4));
        return value;
    }

    public static byte[] Jpeg(int width = 3, int height = 2) =>
    [
        0xff, 0xd8,
        0xff, 0xe0, 0x00, 0x02,
        0xff, 0xc0, 0x00, 0x0b, 0x08,
        (byte)(height >> 8), (byte)height,
        (byte)(width >> 8), (byte)width,
        0x01, 0x01, 0x11, 0x00,
        0xff, 0xd9,
    ];

    public static byte[] JpegWithFrameMarker(byte marker, int width = 3, int height = 2)
    {
        byte[] value = Jpeg(width, height);
        value[7] = marker;
        return value;
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
