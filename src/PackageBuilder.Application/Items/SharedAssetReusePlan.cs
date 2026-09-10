using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Textures;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Application.Items;

/// <summary>Associates an exact manifest source with its previously streamed, immutable snapshot identity.</summary>
public sealed record SourceContentIdentity(SourceAsset Source, ArtifactContentIdentity Content);

/// <summary>Resolves one original texture interpretation to the shared interpretation to compile once.</summary>
public sealed record TextureReuse(TextureAssignment Original, TextureAssignment Canonical);

/// <summary>Preserves a declared material ID while resolving it to a shared material with canonical textures.</summary>
public sealed record MaterialReuse(InternalAssetId OriginalId, ManifestMaterial Canonical);

/// <summary>
/// Immutable build-local reuse instructions. Original manifest ownership and IDs remain unchanged;
/// consumers emit each canonical material/texture once and resolve original references through these aliases.
/// </summary>
public sealed class SharedAssetReusePlan
{
    internal SharedAssetReusePlan(ProductManifest manifest, IEnumerable<SourceContentIdentity> identities,
        IEnumerable<TextureReuse> textures, IEnumerable<MaterialReuse> materials)
    {
        Manifest = manifest;
        SourceIdentities = Array.AsReadOnly(identities.ToArray());
        Textures = Array.AsReadOnly(textures.ToArray());
        Materials = Array.AsReadOnly(materials.ToArray());
    }

    /// <summary>Gets the immutable reviewed manifest for which this plan was calculated.</summary>
    public ProductManifest Manifest { get; }

    /// <summary>Gets the supplied snapshot identities in ordinal source order for consumers to verify before emission.</summary>
    public IReadOnlyList<SourceContentIdentity> SourceIdentities { get; }

    /// <summary>Gets all texture aliases, including self-aliases, in ordinal source/role order.</summary>
    public IReadOnlyList<TextureReuse> Textures { get; }

    /// <summary>Gets all material aliases, including self-aliases, in ordinal original-ID order.</summary>
    public IReadOnlyList<MaterialReuse> Materials { get; }
}

/// <summary>Returns either a complete reuse plan or blocking findings, never a partial plan.</summary>
public sealed class SharedAssetReuseResult
{
    internal SharedAssetReuseResult(SharedAssetReusePlan? plan, IEnumerable<ValidationFinding> findings)
    {
        Plan = plan;
        Findings = Array.AsReadOnly(findings.ToArray());
    }

    /// <summary>Gets the complete plan, or null when validation failed.</summary>
    public SharedAssetReusePlan? Plan { get; }

    /// <summary>Gets actionable validation failures; empty on success.</summary>
    public IReadOnlyList<ValidationFinding> Findings { get; }
}
