using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Portable;

/// <summary>Composes deterministic marketplace-neutral portable filenames from typed product identity.</summary>
public sealed class PortableNamingProfile
{
    private static readonly Dictionary<string, string> _textureTokens =
        new(StringComparer.Ordinal)
        {
            [TextureRole.Albedo.CanonicalIdentifier] = "Albedo",
            [TextureRole.Normal.CanonicalIdentifier] = "Normal",
            [TextureRole.Metallic.CanonicalIdentifier] = "Metallic",
            [TextureRole.Roughness.CanonicalIdentifier] = "Roughness",
            [TextureRole.Emission.CanonicalIdentifier] = "Emission",
            [TextureRole.AmbientOcclusion.CanonicalIdentifier] = "AO",
        };

    private PortableNamingProfile(InternalAssetId assetId, ProductFolderName folderName)
    {
        AssetId = assetId;
        FolderName = folderName;
        FlatFbxFolderName = $"{folderName.Value}_fbx";
        FbxFileName = $"{assetId.Value}.fbx";
        FbxArchiveFileName = $"{folderName.Value}_FBX.zip";
        FlatReadmeFileName = $"README_{assetId.Value}.txt";
        ProductReadmeFileName = $"README_{folderName.Value}.txt";
    }

    public InternalAssetId AssetId { get; }

    public ProductFolderName FolderName { get; }

    public string ProductFolderName => FolderName.Value;

    public string FlatFbxFolderName { get; }

    public string FbxFileName { get; }

    public string FbxArchiveFileName { get; }

    public string FlatReadmeFileName { get; }

    public string ProductReadmeFileName { get; }

    /// <summary>Creates the profile from already validated PB-0101 identities.</summary>
    public static PortableValidationResult<PortableNamingProfile> Create(
        InternalAssetId? assetId,
        ProductFolderName? folderName)
    {
        return assetId is null
            ? PortableValidationResult<PortableNamingProfile>.Failure(PortableValidationError.NullAssetId)
            : folderName is null
            ? PortableValidationResult<PortableNamingProfile>.Failure(PortableValidationError.NullFolderName)
            : PortableValidationResult<PortableNamingProfile>.Success(
            new PortableNamingProfile(assetId, folderName));
    }

    /// <summary>Composes one canonical separate texture name while preserving its explicit byte-format extension.</summary>
    public PortableValidationResult<string> GetTextureFileName(
        TextureRole? role,
        PortableFileExtension? extension)
    {
        return role is null
            ? PortableValidationResult<string>.Failure(PortableValidationError.NullTextureRole)
            : extension is null
            ? PortableValidationResult<string>.Failure(PortableValidationError.NullExtension)
            : _textureTokens.TryGetValue(role.CanonicalIdentifier, out string? token)
            ? PortableValidationResult<string>.Success($"T_{AssetId.Value}_{token}{extension.Value}")
            : PortableValidationResult<string>.Failure(PortableValidationError.UnsupportedTextureRole);
    }

    /// <summary>Composes the approved standard or rigged GLB filename.</summary>
    public PortableValidationResult<string> GetGlbFileName(PortableGlbVariant? variant) =>
        variant is null
            ? PortableValidationResult<string>.Failure(PortableValidationError.NullGlbVariant)
            : PortableValidationResult<string>.Success(
                $"{FolderName.Value}{variant.FileSuffix}.glb");

    /// <summary>Composes one top-level media filename using an explicit image extension.</summary>
    public PortableValidationResult<string> GetMediaFileName(
        PortableMediaView? view,
        PortableFileExtension? extension)
    {
        return view is null
            ? PortableValidationResult<string>.Failure(PortableValidationError.NullMediaView)
            : extension is null
            ? PortableValidationResult<string>.Failure(PortableValidationError.NullExtension)
            : PortableValidationResult<string>.Success(
                $"{FolderName.Value}_{view.NameToken}{extension.Value}");
    }
}
