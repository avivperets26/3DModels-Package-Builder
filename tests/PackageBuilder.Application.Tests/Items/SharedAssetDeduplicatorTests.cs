using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PackageBuilder.Application.Items;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.Manifests;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Materials;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Application.Tests.Items;

/// <summary>Acceptance vectors for exact reuse, semantic separation, immutable ownership and fail-closed planning.</summary>
[Trait("Task", "PB-0802")]
public sealed class SharedAssetDeduplicatorTests
{
    private static readonly SourceAsset _first = SourceAsset.Create(SourceAssetKind.Image, "textures/a.png", "first.png").Value!;
    private static readonly SourceAsset _second = SourceAsset.Create(SourceAssetKind.Image, "textures/z.png", "renamed.png").Value!;

    [Theory]
    [InlineData("item-set")]
    [InlineData("item-collection")]
    public void IdenticalContentAndIntentShareOneCanonicalAssetWithoutChangingOwnership(string productCase)
    {
        ProductManifest manifest = Manifest(productCase: productCase);
        string original = ProductManifestJson.Serialize(manifest).Json!;
        List<SourceContentIdentity?> input = [.. Identities()];
        SharedAssetReuseResult result = Plan(manifest, input);
        input.Clear();
        Assert.Empty(result.Findings);
        SharedAssetReusePlan plan = Assert.IsType<SharedAssetReusePlan>(result.Plan);
        Assert.Same(manifest, plan.Manifest);
        Assert.Equal(2, plan.Materials.Count);
        Assert.Equal(2, plan.Textures.Count);
        _ = Assert.Single(plan.Materials.Select(alias => alias.Canonical).Distinct());
        _ = Assert.Single(plan.Textures.Select(alias => alias.Canonical).Distinct());
        Assert.All(plan.Materials, alias => Assert.Equal("AlphaMaterial", alias.Canonical.Id.Value));
        Assert.All(plan.Textures, alias => Assert.Equal(_first, alias.Canonical.SourceAsset));
        Assert.All(plan.Materials, alias => Assert.Equal(_first, Assert.Single(alias.Canonical.Definition.TextureAssignments).SourceAsset));
        Assert.Equal(["AlphaMaterial", "ZedMaterial"], plan.Materials.Select(alias => alias.OriginalId.Value));
        Assert.Equal(original, ProductManifestJson.Serialize(manifest).Json);
        Assert.Equal(["Zed", "Alpha"], (manifest.ItemSet?.Items ?? manifest.ItemCollection!.Items).Select(item => item.Id.Value));
        Assert.Equal(4, manifest.ItemSourceAssignments.Count);
        Assert.Equal(2, plan.SourceIdentities.Count);
        _ = Assert.Throws<NotSupportedException>(() => ((IList<SourceContentIdentity>)plan.SourceIdentities).Clear());
        _ = Assert.Throws<NotSupportedException>(() => ((IList<TextureReuse>)plan.Textures).Clear());
        _ = Assert.Throws<NotSupportedException>(() => ((IList<MaterialReuse>)plan.Materials).Clear());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DifferentBytesOrLengthNeverShareEvenWhenNamesAndSettingsLookSimilar(bool lengthOnly)
    {
        SourceContentIdentity[] identities = Identities();
        identities[1] = identities[1] with
        {
            Content = lengthOnly
                ? ArtifactContentIdentity.Create(999, identities[0].Content.Sha256).Value!
                : Identity([1, 2, 4]),
        };
        SharedAssetReusePlan plan = Plan(Manifest(), identities).Plan!;
        Assert.Equal(2, plan.Materials.Select(alias => alias.Canonical).Distinct().Count());
        Assert.Equal(2, plan.Textures.Select(alias => alias.Canonical).Distinct().Count());
    }

    [Theory]
    [InlineData("role")]
    [InlineData("colour-role")]
    [InlineData("normal")]
    public void EqualBytesWithDifferentInterpretationsStaySeparate(string variation)
    {
        TextureAssignment a = Texture(_first, variation == "normal" ? TextureRole.Normal : TextureRole.Albedo);
        TextureAssignment b = variation switch
        {
            "normal" => Texture(_second, TextureRole.Normal, NormalConvention.DirectX),
            "role" => Texture(_second, TextureRole.Emission),
            _ => Texture(_second, TextureRole.Roughness),
        };
        SharedAssetReusePlan plan = Plan(Manifest(Material([a]), Material([b])), Identities()).Plan!;
        Assert.Equal(2, plan.Textures.Select(alias => alias.Canonical).Distinct().Count());
        Assert.Equal(2, plan.Materials.Select(alias => alias.Canonical).Distinct().Count());
    }

    [Theory]
    [InlineData("metallic")]
    [InlineData("roughness")]
    [InlineData("normal-scale")]
    [InlineData("emission-red")]
    [InlineData("emission-green")]
    [InlineData("emission-blue")]
    [InlineData("emission-intensity")]
    [InlineData("ao")]
    [InlineData("height")]
    [InlineData("opacity")]
    [InlineData("surface")]
    [InlineData("cutoff")]
    [InlineData("scale-u")]
    [InlineData("scale-v")]
    [InlineData("offset-u")]
    [InlineData("offset-v")]
    [InlineData("double-sided")]
    public void EveryMaterialParameterPreventsUnsafeReuseAndSurvivesCanonicalization(string variation)
    {
        MaterialDefinition a = Material([Texture(_first)], variation == "opacity" ? "surface" : "");
        MaterialDefinition b = Material([Texture(_second)], variation);
        SharedAssetReusePlan plan = Plan(Manifest(a, b), Identities()).Plan!;
        _ = Assert.Single(plan.Textures.Select(alias => alias.Canonical).Distinct());
        Assert.Equal(2, plan.Materials.Select(alias => alias.Canonical).Distinct().Count());
        MaterialDefinition canonical = plan.Materials.Single(alias => alias.OriginalId.Value == "ZedMaterial").Canonical.Definition;
        Assert.Equal(b, canonical.WithTextureAssignments(b.TextureAssignments).Value);
    }

    [Fact]
    public void MissingTextureRoleDiffersFromAssignedTexture()
    {
        SharedAssetReusePlan plan = Plan(Manifest(Material([]), Material([Texture(_second)])), Identities()).Plan!;
        Assert.Equal(2, plan.Materials.Select(alias => alias.Canonical).Distinct().Count());
    }

    [Fact]
    public void TextureFreeEqualMaterialsNeedNoHashes()
    {
        SharedAssetReusePlan plan = Plan(Manifest(Material([]), Material([])), []).Plan!;
        Assert.Empty(plan.Textures);
        _ = Assert.Single(plan.Materials.Select(alias => alias.Canonical).Distinct());
    }

    [Fact]
    public void OneSourceCanHaveSeveralDistinctInterpretations()
    {
        MaterialDefinition a = Material([Texture(_first), Texture(_first, TextureRole.Roughness)]);
        MaterialDefinition b = Material([Texture(_second, TextureRole.Roughness), Texture(_second)]);
        SharedAssetReusePlan plan = Plan(Manifest(a, b), Identities()).Plan!;
        Assert.Equal(4, plan.Textures.Count);
        Assert.Equal(2, plan.Textures.Select(alias => alias.Canonical).Distinct().Count());
        _ = Assert.Single(plan.Materials.Select(alias => alias.Canonical).Distinct());
    }

    [Theory]
    [InlineData("missing", "SHARED_ASSET_IDENTITY_MISSING")]
    [InlineData("duplicate", "SHARED_ASSET_IDENTITY_DUPLICATE")]
    [InlineData("conflicting", "SHARED_ASSET_IDENTITY_DUPLICATE")]
    [InlineData("null-entry", "SHARED_ASSET_IDENTITY_INVALID")]
    [InlineData("null-source", "SHARED_ASSET_IDENTITY_INVALID")]
    [InlineData("null-content", "SHARED_ASSET_IDENTITY_INVALID")]
    [InlineData("unknown", "SHARED_ASSET_IDENTITY_INVALID")]
    [InlineData("wrong-case", "SHARED_ASSET_IDENTITY_INVALID")]
    [InlineData("metadata", "SHARED_ASSET_IDENTITY_INVALID")]
    [InlineData("model", "SHARED_ASSET_IDENTITY_INVALID")]
    public void InvalidIdentitySetsFailWithoutPartialPlans(string variation, string code)
    {
        List<SourceContentIdentity?> inputs = [.. Identities()];
        switch (variation)
        {
            case "missing":
                inputs.RemoveAt(1);
                break;
            case "duplicate":
                inputs.Add(inputs[0]);
                break;
            case "conflicting":
                inputs.Add(inputs[0]! with { Content = Identity([9]) });
                break;
            case "null-entry":
                inputs[1] = null;
                break;
            case "null-source":
                inputs[1] = inputs[1]! with { Source = null! };
                break;
            case "null-content":
                inputs[1] = inputs[1]! with { Content = null! };
                break;
            case "unknown":
                inputs[1] = inputs[1]! with { Source = SourceAsset.Create(SourceAssetKind.Image, "other.png").Value! };
                break;
            case "wrong-case":
                inputs[1] = inputs[1]! with { Source = SourceAsset.Create(SourceAssetKind.Image, "textures/Z.png").Value! };
                break;
            case "metadata":
                inputs[1] = inputs[1]! with { Source = SourceAsset.Create(SourceAssetKind.Image, _second.LogicalReference, "other.png").Value! };
                break;
            case "model":
                inputs[1] = inputs[1]! with { Source = SourceAsset.Create(SourceAssetKind.Glb, "models/one.glb").Value! };
                break;
        }

        SharedAssetReuseResult result = Plan(Manifest(), inputs);
        Assert.Null(result.Plan);
        Assert.Equal(code, Assert.Single(result.Findings).Code.Value);
        Assert.True(result.Findings[0].BlocksRelease);
        Assert.NotNull(result.Findings[0].SuggestedAction);
    }

    [Fact]
    public void UnresolvedNormalConventionRequiresReview()
    {
        ProductManifest manifest = Manifest(Material([Texture(_first, TextureRole.Normal, NormalConvention.Auto)]));
        SharedAssetReuseResult result = Plan(manifest, Identities());
        Assert.Null(result.Plan);
        Assert.Equal("SHARED_ASSET_REVIEW_REQUIRED", Assert.Single(result.Findings).Code.Value);
    }

    [Theory]
    [InlineData("item-set")]
    [InlineData("item-collection")]
    [InlineData("static")]
    public void DraftOrSingleProductCannotBypassReviewedMapping(string productCase)
    {
        SharedAssetReuseResult result = Plan(Manifest(productCase: productCase, mapped: false), Identities());
        Assert.Null(result.Plan);
        Assert.Equal("SHARED_ASSET_MAPPING_REQUIRED", Assert.Single(result.Findings).Code.Value);
    }

    [Fact]
    public void OrderCultureAndManifestRoundTripDoNotChangeAliases()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            ProductManifest manifest = Manifest();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            SharedAssetReusePlan first = Plan(manifest, Identities()).Plan!;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            ProductManifest restored = ProductManifestJson.Deserialize(ProductManifestJson.Serialize(manifest).Json!).Value!;
            SharedAssetReusePlan second = Plan(restored, Identities().Reverse()).Plan!;
            Assert.Equal(first.Materials, second.Materials);
            Assert.Equal(first.Textures, second.Textures);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void CancellationAndNullProgrammingArgumentsDoNotReturnPlans()
    {
        _ = Assert.Throws<ArgumentNullException>(() => Plan(null!, []));
        _ = Assert.Throws<ArgumentNullException>(() => Plan(Manifest(), null!));
        _ = Assert.Throws<OperationCanceledException>(() => SharedAssetDeduplicator.Plan(Manifest(), [], new CancellationToken(true)));
        using var cancellation = new CancellationTokenSource();
        _ = Assert.Throws<OperationCanceledException>(() => SharedAssetDeduplicator.Plan(Manifest(), CancelAfterFirst(), cancellation.Token));

        IEnumerable<SourceContentIdentity> CancelAfterFirst()
        {
            yield return Identities()[0];
            cancellation.Cancel();
            yield return Identities()[1];
        }
    }

    [Fact]
    public void DeclaredSharedReferencesRemainDistinctWhenTheirMaterialTexturesAreReused()
    {
        JsonNode node = JsonNode.Parse(ProductManifestJson.Serialize(Manifest()).Json!)!;
        JsonNode group = node["itemCollection"]!;
        string[] ids = ["SharedA", "SharedZ"];
        string[] references = [_first.LogicalReference, _second.LogicalReference];
        for (int index = 0; index < ids.Length; index++)
        {
            JsonNode source = node["sourceAssets"]!.AsArray().Single(value => value!["logicalReference"]!.GetValue<string>() == references[index])!;
            group["sharedAssets"]!.AsArray().Add(new JsonObject { ["id"] = ids[index], ["source"] = source.DeepClone() });
            group["items"]![index]!["sharedAssetReferences"]!.AsArray().Add(ids[index]);
        }

        ProductManifest manifest = ProductManifestJson.Deserialize(node.ToJsonString()).Value!;
        string before = ProductManifestJson.Serialize(manifest).Json!;
        SharedAssetReusePlan plan = Plan(manifest, Identities()).Plan!;
        _ = Assert.Single(plan.Textures.Select(alias => alias.Canonical).Distinct());
        Assert.Equal(2, plan.Manifest.ItemCollection!.SharedAssets.Count);
        Assert.Equal(ids, plan.Manifest.ItemCollection.Items.SelectMany(item => item.SharedAssetReferences).Select(id => id.Value));
        Assert.Equal(before, ProductManifestJson.Serialize(plan.Manifest).Json);
    }

    [Fact]
    public void ReplacementTexturesReuseDomainValidation()
    {
        MaterialDefinition material = Material([Texture(_first)]);
        Assert.Equal(MaterialDefinitionValidationError.NullTextureAssignment, material.WithTextureAssignments([null]).Error);
        Assert.Equal(MaterialDefinitionValidationError.DuplicateTextureRole, material.WithTextureAssignments([Texture(_first), Texture(_second)]).Error);
        _ = Assert.Single(material.TextureAssignments);
    }

    private static SharedAssetReuseResult Plan(ProductManifest manifest, IEnumerable<SourceContentIdentity?> identities) =>
        SharedAssetDeduplicator.Plan(manifest, identities, TestContext.Current.CancellationToken);

    private static TextureAssignment Texture(SourceAsset source, TextureRole? role = null, NormalConvention? normal = null)
    {
        role ??= TextureRole.Albedo;
        return TextureAssignment.Create(source, role, role.RequiredColourSpace,
            role.IsNormalMapData ? normal ?? NormalConvention.OpenGl : null).Value!;
    }

    private static MaterialDefinition Material(TextureAssignment[] textures, string variation = "") => MaterialDefinition.Create(
        variation == "metallic" ? 0.6 : 0.5, variation == "roughness" ? 0.6 : 0.5,
        variation == "normal-scale" ? 2 : 1,
        EmissionProperties.Create(variation == "emission-red" ? 2 : 1, variation == "emission-green" ? 2 : 1,
            variation == "emission-blue" ? 2 : 1, variation == "emission-intensity" ? 2 : 1).Value!,
        variation == "ao" ? 0.5 : 1, variation == "height" ? 0.1 : 0,
        variation == "opacity" ? 0.5 : 1,
        variation is "opacity" or "surface" ? SurfaceMode.Transparent : SurfaceMode.Cutout,
        variation is "opacity" or "surface" ? null : variation == "cutoff" ? 0.6 : 0.5,
        UvTransform.Create(variation == "scale-u" ? 2 : 1, variation == "scale-v" ? 2 : 1,
            variation == "offset-u" ? 0.1 : 0, variation == "offset-v" ? 0.1 : 0).Value!,
        variation == "double-sided", textures).Value!;

    private static ArtifactContentIdentity Identity(byte[] bytes) => ArtifactContentIdentity.Create(bytes.Length,
        Sha256Digest.Create(Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!).Value!;

    private static SourceContentIdentity[] Identities() => [new(_first, Identity([1, 2, 3])), new(_second, Identity([1, 2, 3]))];

    private static ProductManifest Manifest(MaterialDefinition? a = null, MaterialDefinition? b = null,
        string productCase = "item-collection", bool mapped = true)
    {
        JsonNode node = JsonNode.Parse("""
            {"schemaVersion":1,"publisherProfileReference":"ExamplePublisher",
             "product":{"displayName":"Example","assetId":"Example","folderName":"Example","case":"item-collection","version":"1.0.0"},
             "targets":["portable"],"sourceAssets":[{"kind":"glb","logicalReference":"models/one.glb"},{"kind":"glb","logicalReference":"models/two.glb"}],
             "materials":[],"animations":[],"itemCollection":{"items":[{"id":"Zed","categories":[],"sharedAssetReferences":[]},{"id":"Alpha","categories":[],"sharedAssetReferences":[]}],"relationships":[],"sharedAssets":[]}}
            """)!;
        node["product"]!["case"] = productCase;
        if (productCase == "item-set")
        {
            node["itemSet"] = node["itemCollection"]!.DeepClone();
        }

        if (productCase != "item-collection")
        {
            _ = node.AsObject().Remove("itemCollection");
        }

        ProductManifest draft = ProductManifestJson.Deserialize(node.ToJsonString()).Value!;
        ProductManifestValidationResult result = ProductManifest.Create(draft.SchemaVersion, draft.PublisherProfileReference,
            draft.DisplayName, draft.AssetId, draft.FolderName, draft.ProductCase, draft.Version, draft.Targets,
            [.. draft.SourceAssets, _second, _first],
            [ManifestMaterial.Create(Id("ZedMaterial"), b ?? Material([Texture(_second)])).Value!,
             ManifestMaterial.Create(Id("AlphaMaterial"), a ?? Material([Texture(_first)])).Value!],
            null, [], draft.ItemSet, draft.ItemCollection, itemSourceAssignments: mapped
                ? [new(Id("Zed"), "models/one.glb"), new(Id("Alpha"), "models/two.glb"),
                   new(Id("Zed"), _second.LogicalReference), new(Id("Alpha"), _first.LogicalReference)] : null);
        Assert.True(result.IsValid);
        return result.Value!;
    }

    private static InternalAssetId Id(string value) => InternalAssetId.Create(value).Value!;
}
