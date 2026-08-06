using PackageBuilder.Domain.Animations;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Materials;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Profiles;
using PackageBuilder.Domain.Rigging;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Portable.Tests;

internal static class PortableReadmeTestValues
{
    public static PublisherProfile Publisher(
        AiDisclosureState? aiState = null,
        string? aiText = "Concept generation and texture drafting were AI-assisted.")
    {
        AiDisclosureState state = aiState ?? AiDisclosureState.AiAssisted;
        AiDisclosure disclosure = Assert.IsType<AiDisclosure>(AiDisclosure.Create(state, aiText).Value);
        CopyrightYearPolicy policy = Assert.IsType<CopyrightYearPolicy>(
            CopyrightYearPolicy.Create(CopyrightYearPolicyKind.YearRange, 2026, 2024).Value);
        CopyrightNotice notice = Assert.IsType<CopyrightNotice>(
            CopyrightNotice.Create(
                CopyrightHolder.Create("Aviv Perets").Value,
                policy).Value);
        return Assert.IsType<PublisherProfile>(
            PublisherProfile.Create(
                PublisherRoot.Create("AvivPeretsFBX").Value,
                PublisherDisplayName.Create("Aviv Perets FBX").Value,
                SupportContact.CreateEmail("support@example.com").Value,
                notice,
                disclosure).Value);
    }

    public static PublisherProfile PublisherWithUrlAndSingleYear()
    {
        CopyrightYearPolicy policy = Assert.IsType<CopyrightYearPolicy>(
            CopyrightYearPolicy.Create(CopyrightYearPolicyKind.SingleYear, 2026).Value);
        return Assert.IsType<PublisherProfile>(
            PublisherProfile.Create(
                PublisherRoot.Create("AvivPeretsFBX").Value,
                PublisherDisplayName.Create("Aviv Perets FBX").Value,
                SupportContact.CreateSecureUrl("https://example.com/support").Value,
                CopyrightNotice.Create(
                    CopyrightHolder.Create("Aviv Perets").Value,
                    policy).Value,
                AiDisclosure.Create(AiDisclosureState.NoAiAssistance).Value).Value);
    }

    public static ProductManifest Manifest(
        ProductCase productCase,
        bool withTexture = false,
        bool withUntexturedMaterial = false,
        bool uncategorizedItem = false)
    {
        RigDefinition? rig = productCase.Equals(ProductCase.Rigged) || productCase.Equals(ProductCase.RiggedAnimated)
            ? Rig()
            : null;
        AnimationDefinition[] animations = productCase.Equals(ProductCase.RiggedAnimated)
            ? [Animation(rig!)]
            : [];
        ItemSetDefinition? itemSet = productCase.Equals(ProductCase.ItemSet)
            ? Assert.IsType<ItemSetDefinition>(
                ItemSetDefinition.Create([Item("Helm", !uncategorizedItem)], [], [], null).Value)
            : null;
        ItemCollectionDefinition? collection = productCase.Equals(ProductCase.ItemCollection)
            ? Assert.IsType<ItemCollectionDefinition>(
                ItemCollectionDefinition.Create([Item("Sword", !uncategorizedItem)], [], []).Value)
            : null;
        SourceAsset model = Assert.IsType<SourceAsset>(
            SourceAsset.Create(SourceAssetKind.Fbx, "models/silverwing.fbx").Value);
        TextureAssignment? texture = withTexture ? PortableTestValues.Texture() : null;
        SourceAsset[] sources = texture is null ? [model] : [model, texture.SourceAsset];
        ManifestMaterial[] materials = texture is not null
            ? [Material(texture)]
            : withUntexturedMaterial
            ? [MaterialWithoutTextures()]
            : [];

        return Assert.IsType<ProductManifest>(
            ProductManifest.Create(
                ProductManifest.CurrentSchemaVersion,
                PublisherRoot.Create("AvivPeretsFBX").Value,
                ProductDisplayName.Create("Silverwing Talonbow").Value,
                InternalAssetId.Create("SilverwingTalonbow").Value,
                ProductFolderName.Create("Silverwing_Talonbow").Value,
                productCase,
                ProductVersion.Create("1.2.0").Value,
                [BuildTarget.Portable],
                sources,
                materials,
                rig,
                animations,
                itemSet,
                collection).Value);
    }

    public static async Task<PortableTextureCopyReceipt> TextureReceiptAsync(
        TextureAssignment? assignment = null)
    {
        byte[] bytes = PortableTestValues.Png(2048, 2048);
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new MemoryStream();
        PortableTextureCopyResult result = await PortableTextureProcessor.CopyAsync(
            PortableTestValues.RecordForBytes("readme-texture", bytes),
            assignment ?? PortableTestValues.Texture(),
            PortableTestValues.Naming(),
            PortableFileExtension.Png,
            PortableFileExtension.Png,
            source,
            destination,
            TestContext.Current.CancellationToken);
        return Assert.IsType<PortableTextureCopyReceipt>(result.Receipt);
    }

    public static PortableReadmeRequest Request(
        ProductCase? productCase = null,
        PublisherProfile? publisher = null,
        IEnumerable<PortableTextureCopyReceipt?>? receipts = null,
        ProductManifest? manifest = null,
        PortableGlbVariant? glb = null) =>
        Assert.IsType<PortableReadmeRequest>(
            PortableReadmeRequest.Create(
                manifest ?? Manifest(productCase ?? ProductCase.Static),
                publisher ?? Publisher(),
                PortableTestValues.Naming(),
                PortableModelDimensions.Create(1.25, 2.5, 0.75).Value,
                glb,
                receipts ?? [],
                ["Import the FBX or GLB into your preferred engine.", "Assign the supplied textures by their canonical roles."]).Value);

    public static ProductManifest Recreate(
        ProductManifest source,
        IEnumerable<BuildTarget?>? targets = null,
        IEnumerable<ManifestMaterial?>? materials = null,
        IEnumerable<SourceAsset?>? sourceAssets = null,
        PublisherRoot? publisher = null) =>
        Assert.IsType<ProductManifest>(
            ProductManifest.Create(
                source.SchemaVersion,
                publisher ?? source.PublisherProfileReference,
                source.DisplayName,
                source.AssetId,
                source.FolderName,
                source.ProductCase,
                source.Version,
                targets ?? source.Targets,
                sourceAssets ?? source.SourceAssets,
                materials ?? source.Materials,
                source.Rig,
                source.Animations,
                source.ItemSet,
                source.ItemCollection,
                source.MarketplaceProfileReference).Value);

    public static ManifestMaterial Material(TextureAssignment texture, string id = "BowMaterial")
    {
        MaterialDefinition definition = Assert.IsType<MaterialDefinition>(
            MaterialDefinition.Create(
                0.8,
                0.35,
                1,
                EmissionProperties.Create(0, 0, 0, 0).Value,
                1,
                0,
                1,
                SurfaceMode.Opaque,
                null,
                UvTransform.Create(1, 1, 0, 0).Value,
                false,
                [texture]).Value);
        return Assert.IsType<ManifestMaterial>(
            ManifestMaterial.Create(InternalAssetId.Create(id).Value, definition).Value);
    }

    public static ManifestMaterial MaterialWithoutTextures(string id = "BowMaterial")
    {
        MaterialDefinition definition = Assert.IsType<MaterialDefinition>(
            MaterialDefinition.Create(
                0.8,
                0.35,
                1,
                EmissionProperties.Create(0, 0, 0, 0).Value,
                1,
                0,
                1,
                SurfaceMode.Opaque,
                null,
                UvTransform.Create(1, 1, 0, 0).Value,
                false,
                []).Value);
        return Assert.IsType<ManifestMaterial>(
            ManifestMaterial.Create(InternalAssetId.Create(id).Value, definition).Value);
    }

    private static ItemDefinition Item(string id, bool includeCategory = true) =>
        Assert.IsType<ItemDefinition>(
            ItemDefinition.Create(
                InternalAssetId.Create(id).Value,
                includeCategory ? [ItemCategory.Create("equipment").Value] : [],
                null,
                []).Value);

    private static RigDefinition Rig()
    {
        BoneDefinition root = Assert.IsType<BoneDefinition>(BoneDefinition.Create("Root", null).Value);
        BoneDefinition child = Assert.IsType<BoneDefinition>(BoneDefinition.Create("String", "Root").Value);
        SkeletonDefinition skeleton = Assert.IsType<SkeletonDefinition>(
            SkeletonDefinition.Create([root, child]).Value);
        RigTransform transform = Assert.IsType<RigTransform>(
            RigTransform.Create(0, 0, 0, 0, 0, 0, 1, 1, 1, 1).Value);
        PoseDefinition pose = Assert.IsType<PoseDefinition>(
            PoseDefinition.Create(
                skeleton,
                [
                    BonePose.Create("Root", transform).Value,
                    BonePose.Create("String", transform).Value,
                ]).Value);
        return Assert.IsType<RigDefinition>(RigDefinition.Create(RigType.Generic, skeleton, pose).Value);
    }

    private static AnimationDefinition Animation(RigDefinition rig) =>
        Assert.IsType<AnimationDefinition>(
            AnimationDefinition.Create(
                "BowShot",
                1,
                31,
                30,
                LoopBehavior.Once,
                RootMotionStatus.None,
                null,
                rig).Value);
}
