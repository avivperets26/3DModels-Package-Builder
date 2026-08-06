using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Portable;

/// <summary>Qualifies one validated PB-0205 record for exactly one portable layout position.</summary>
public sealed class PortableCompositionArtifact
{
    private PortableCompositionArtifact(
        ArtifactStoreRecord record,
        PortableArtifactPurpose purpose,
        TextureRole? textureRole,
        PortableMediaView? mediaView,
        PortableFileExtension? extension,
        PortableGlbVariant? glbVariant)
    {
        Record = record;
        Purpose = purpose;
        TextureRole = textureRole;
        MediaView = mediaView;
        Extension = extension;
        GlbVariant = glbVariant;
    }

    public ArtifactStoreRecord Record { get; }

    public PortableArtifactPurpose Purpose { get; }

    public TextureRole? TextureRole { get; }

    public PortableMediaView? MediaView { get; }

    public PortableFileExtension? Extension { get; }

    public PortableGlbVariant? GlbVariant { get; }

    /// <summary>Validates store state, target, role, and purpose-specific naming qualifiers.</summary>
    public static PortableValidationResult<PortableCompositionArtifact> Create(
        ArtifactStoreRecord? record,
        PortableArtifactPurpose? purpose,
        TextureRole? textureRole = null,
        PortableMediaView? mediaView = null,
        PortableFileExtension? extension = null,
        PortableGlbVariant? glbVariant = null)
    {
        if (record is null)
        {
            return Failure(PortableValidationError.NullArtifactRecord);
        }

        if (purpose is null)
        {
            return Failure(PortableValidationError.NullArtifactPurpose);
        }

        BuildArtifact artifact = record.Artifact;
        if (!artifact.LifecycleState.Equals(BuildArtifactLifecycleState.Validated))
        {
            return Failure(PortableValidationError.ArtifactNotValidated);
        }

        if (artifact.Target is null || !artifact.Target.Equals(BuildTarget.Portable))
        {
            return Failure(PortableValidationError.ArtifactTargetMismatch);
        }

        if (!string.Equals(
                artifact.Role.CanonicalIdentifier,
                purpose.ArtifactRole,
                StringComparison.Ordinal))
        {
            return Failure(PortableValidationError.ArtifactRoleMismatch);
        }

        bool qualifiersValid = purpose.Equals(PortableArtifactPurpose.Texture)
            ? textureRole is not null && extension is not null && mediaView is null && glbVariant is null
            : purpose.Equals(PortableArtifactPurpose.Media)
            ? mediaView is not null && extension is not null && extension.Equals(PortableFileExtension.Jpg) &&
                textureRole is null && glbVariant is null
            : purpose.Equals(PortableArtifactPurpose.Glb)
            ? glbVariant is not null && textureRole is null && mediaView is null && extension is null
            : textureRole is null && mediaView is null && extension is null && glbVariant is null;
        return !qualifiersValid
            ? Failure(PortableValidationError.InvalidArtifactQualifier)
            : textureRole is not null &&
            textureRole != PackageBuilder.Domain.Textures.TextureRole.Albedo &&
            textureRole != PackageBuilder.Domain.Textures.TextureRole.Normal &&
            textureRole != PackageBuilder.Domain.Textures.TextureRole.Metallic &&
            textureRole != PackageBuilder.Domain.Textures.TextureRole.Roughness &&
            textureRole != PackageBuilder.Domain.Textures.TextureRole.Emission &&
            textureRole != PackageBuilder.Domain.Textures.TextureRole.AmbientOcclusion
            ? Failure(PortableValidationError.UnsupportedTextureRole)
            : PortableValidationResult<PortableCompositionArtifact>.Success(
            new PortableCompositionArtifact(record, purpose, textureRole, mediaView, extension, glbVariant));
    }

    private static PortableValidationResult<PortableCompositionArtifact> Failure(PortableValidationError error) =>
        PortableValidationResult<PortableCompositionArtifact>.Failure(error);
}
