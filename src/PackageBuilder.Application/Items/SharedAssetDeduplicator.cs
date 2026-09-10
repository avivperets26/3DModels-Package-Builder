using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Materials;
using PackageBuilder.Domain.Textures;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Application.Items;

/// <summary>
/// Plans exact texture/material reuse for reviewed item sets and collections without IO or manifest mutation.
/// Callers supply PB-0204 identities from immutable source snapshots; this does not hash or trust filenames.
/// </summary>
public static class SharedAssetDeduplicator
{
    /// <summary>
    /// Combines byte identity with canonical texture interpretation, then reuses Domain material equality.
    /// Unknown, duplicated or missing identities fail closed. Cancellation throws without publishing a plan.
    /// </summary>
    public static SharedAssetReuseResult Plan(
        ProductManifest manifest,
        IEnumerable<SourceContentIdentity?> identities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(identities);
        cancellationToken.ThrowIfCancellationRequested();
        if (manifest.ItemSourceAssignments.Count == 0)
        {
            return Failure("SHARED_ASSET_MAPPING_REQUIRED", "A reviewed item source mapping is required.",
                "Complete the multi-item source mapping before planning shared assets.");
        }

        var sources = manifest.SourceAssets.ToDictionary(source => source.LogicalReference, StringComparer.Ordinal);
        var content = new Dictionary<string, ArtifactContentIdentity>(StringComparer.Ordinal);
        foreach (SourceContentIdentity? identity in identities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (identity?.Source is null || identity.Content is null ||
                !identity.Source.Kind.Equals(SourceAssetKind.Image) ||
                !sources.TryGetValue(identity.Source.LogicalReference, out SourceAsset? source) ||
                !source.Equals(identity.Source))
            {
                return Failure("SHARED_ASSET_IDENTITY_INVALID", "A content identity does not describe an exact manifest image source.",
                    "Supply verified image identities from the current immutable source snapshot.");
            }

            if (!content.TryAdd(identity.Source.LogicalReference, identity.Content))
            {
                return Failure("SHARED_ASSET_IDENTITY_DUPLICATE", "More than one identity was supplied for an image source.",
                    "Supply exactly one identity per source, even when repeated identities agree.");
            }
        }

        TextureAssignment[] textures = [.. manifest.Materials.SelectMany(material => material.Definition.TextureAssignments)
            .Distinct().OrderBy(texture => texture.SourceAsset.LogicalReference, StringComparer.Ordinal)
            .ThenBy(texture => texture.Role.CanonicalIdentifier, StringComparer.Ordinal)
            .ThenBy(texture => texture.ColourSpace.CanonicalIdentifier, StringComparer.Ordinal)
            .ThenBy(texture => texture.NormalConvention?.CanonicalIdentifier, StringComparer.Ordinal)];
        var canonicalTextures = new Dictionary<TextureKey, TextureAssignment>();
        var textureAliases = new Dictionary<TextureAssignment, TextureAssignment>();
        foreach (TextureAssignment texture in textures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (NormalConvention.Auto.Equals(texture.NormalConvention))
            {
                return Failure("SHARED_ASSET_REVIEW_REQUIRED", "A normal texture has an unresolved orientation.",
                    "Resolve Auto normal conventions to OpenGL or DirectX before planning reuse.");
            }

            if (!content.TryGetValue(texture.SourceAsset.LogicalReference, out ArtifactContentIdentity? identity))
            {
                return Failure("SHARED_ASSET_IDENTITY_MISSING", "A material texture has no verified content identity.",
                    "Hash every assigned material image in the immutable source snapshot before retrying.");
            }

            var key = new TextureKey(identity, texture.Role, texture.ColourSpace, texture.NormalConvention);
            if (!canonicalTextures.TryGetValue(key, out TextureAssignment? canonical))
            {
                canonical = texture;
                canonicalTextures.Add(key, canonical);
            }

            textureAliases.Add(texture, canonical);
        }

        var canonicalMaterials = new Dictionary<MaterialDefinition, ManifestMaterial>();
        var materialAliases = new List<MaterialReuse>();
        foreach (ManifestMaterial material in manifest.Materials.OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            MaterialDefinition definition = material.Definition.WithTextureAssignments(
                material.Definition.TextureAssignments.Select(texture => textureAliases[texture])).Value!;
            if (!canonicalMaterials.TryGetValue(definition, out ManifestMaterial? canonical))
            {
                canonical = ManifestMaterial.Create(material.Id, definition).Value!;
                canonicalMaterials.Add(definition, canonical);
            }

            materialAliases.Add(new MaterialReuse(material.Id, canonical));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new SharedAssetReuseResult(new SharedAssetReusePlan(manifest,
            content.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new SourceContentIdentity(sources[pair.Key], pair.Value)),
            textures.Select(texture => new TextureReuse(texture, textureAliases[texture])), materialAliases), []);
    }

    // Dictionary hashes only index candidates: record equality still compares full content and semantics.
    private sealed record TextureKey(ArtifactContentIdentity Content, TextureRole Role, ColourSpace ColourSpace, NormalConvention? NormalConvention);

    private static SharedAssetReuseResult Failure(string code, string explanation, string action) => new(null,
        [ValidationFinding.Create(FindingCode.Create(code).Value!, FindingSeverity.Error,
            FindingExplanation.Create(explanation).Value!, FindingSourceComponent.Create("shared-asset-deduplicator").Value!,
            null, CorrectiveAction.Create(action).Value!, true).Value!]);
}
