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
using PackageBuilder.Domain.Tests.Items;
using PackageBuilder.Domain.Tests.Rigging;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Domain.Tests.Manifests;

public sealed class ProductManifestTests
{
    [Fact]
    public void StaticManifestNormalizesDeterministicCollectionOrder()
    {
        SourceAsset later = SourceAsset.Create(
            SourceAssetKind.Image,
            "textures/z.png",
            null).Value!;
        SourceAsset first = SourceAsset.Create(
            SourceAssetKind.Fbx,
            "models/a.fbx",
            null).Value!;

        ProductManifestValidationResult result = Create(
            [BuildTarget.Unreal, BuildTarget.Portable, BuildTarget.Unity],
            [later, first]);

        Assert.True(result.IsValid);
        Assert.Equal(
            ["portable", "unity", "unreal"],
            result.Value!.Targets.Select(target => target.CanonicalIdentifier));
        Assert.Equal(
            ["models/a.fbx", "textures/z.png"],
            result.Value.SourceAssets.Select(source => source.LogicalReference));
        Assert.Equal(ProductManifest.CurrentSchemaVersion, result.Value.SchemaVersion);
        Assert.Equal("AvivPeretsFBX", result.Value.PublisherProfileReference.Value);
        Assert.Equal("Stone Arch", result.Value.DisplayName.Value);
        Assert.Equal("StoneArch", result.Value.AssetId.Value);
        Assert.Equal("Stone_Arch", result.Value.FolderName.Value);
        Assert.Same(ProductCase.Static, result.Value.ProductCase);
        Assert.Equal("1.0.0", result.Value.Version.Value);
        Assert.Empty(result.Value.Materials);
        Assert.Null(result.Value.Rig);
        Assert.Empty(result.Value.Animations);
        Assert.Null(result.Value.ItemSet);
        Assert.Null(result.Value.ItemCollection);
        Assert.Null(result.Value.MarketplaceProfileReference);
    }

    [Fact]
    public void DuplicateTargetAndSourceProduceBlockingStructuredFindings()
    {
        SourceAsset source = SourceAsset.Create(
            SourceAssetKind.Fbx,
            "models/a.fbx",
            null).Value!;

        ProductManifestValidationResult result = Create(
            [BuildTarget.Portable, BuildTarget.Portable],
            [source, source]);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Findings,
            finding => finding.Code.Value == "MANIFEST_TARGET_DUPLICATE");
        Assert.Contains(
            result.Findings,
            finding => finding.Code.Value == "MANIFEST_SOURCE_DUPLICATE");
        Assert.All(result.Findings, finding => Assert.True(finding.BlocksRelease));
    }

    [Fact]
    public void MissingCollectionsAndUnknownSchemaVersionAreExpectedFailures()
    {
        ProductManifestValidationResult result = ProductManifest.Create(
            2,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Findings,
            finding => finding.Code.Value == "MANIFEST_SCHEMA_VERSION_UNKNOWN");
        Assert.Contains(
            result.Findings,
            finding => finding.Code.Value == "MANIFEST_REQUIRED_VALUE_MISSING");
        Assert.Contains(
            result.Findings,
            finding => finding.Code.Value == "MANIFEST_REQUIRED_COLLECTION_MISSING");
    }

    [Theory]
    [InlineData("target")]
    [InlineData("source")]
    [InlineData("material")]
    [InlineData("animation")]
    public void NullEntriesInEveryCollectionAreRejected(string collection)
    {
        SourceAsset model = Model();
        ProductManifestValidationResult result = CreateDetailed(
            ProductCase.Static,
            collection == "target" ? [null] : [BuildTarget.Portable],
            collection == "source" ? [null] : [model],
            collection == "material" ? [null] : [],
            null,
            collection == "animation" ? [null] : [],
            null,
            null);

        AssertCode(result, "MANIFEST_NULL_COLLECTION_ENTRY");
    }

    [Fact]
    public void EmptyTargetsAndImageOnlySourcesAreRejected()
    {
        ProductManifestValidationResult result = CreateDetailed(
            ProductCase.Static,
            [],
            [SourceAsset.Create(SourceAssetKind.Image, "textures/a.png").Value],
            [],
            null,
            [],
            null,
            null);

        AssertCode(result, "MANIFEST_TARGET_REQUIRED");
        AssertCode(result, "MANIFEST_MODEL_SOURCE_REQUIRED");
    }

    [Fact]
    public void DuplicateMaterialAndAnimationIdentitiesAreRejected()
    {
        ManifestMaterial material = ManifestMaterial.Create(
            InternalAssetId.Create("Material").Value,
            ManifestMaterialTests.CreateMaterial()).Value!;
        RigDefinition rig = RigTestAssertions.CreateRig();
        AnimationDefinition clip = Clip(rig);
        ProductManifestValidationResult result = CreateDetailed(
            ProductCase.RiggedAnimated,
            [BuildTarget.Portable],
            [Model()],
            [material, material],
            rig,
            [clip, clip],
            null,
            null);

        AssertCode(result, "MANIFEST_MATERIAL_DUPLICATE");
        AssertCode(result, "MANIFEST_ANIMATION_DUPLICATE");
    }

    [Fact]
    public void UndeclaredMaterialAndSharedSourcesAreRejected()
    {
        SourceAsset texture =
            SourceAsset.Create(SourceAssetKind.Image, "textures/a.png").Value!;
        TextureAssignment assignment = TextureAssignment.Create(
            texture,
            TextureRole.Albedo,
            ColourSpace.Srgb,
            null).Value!;
        MaterialDefinition definition = PackageBuilder.Domain.Tests.Materials
            .MaterialTestAssertions.AssertSuccess(
                MaterialDefinition.Create(
                    0,
                    1,
                    1,
                    PackageBuilder.Domain.Tests.Materials.MaterialTestAssertions.AssertSuccess(
                        EmissionProperties.Create(0, 0, 0, 0)),
                    1,
                    0,
                    1,
                    SurfaceMode.Opaque,
                    null,
                    PackageBuilder.Domain.Tests.Materials.MaterialTestAssertions.AssertSuccess(
                        UvTransform.Create(1, 1, 0, 0)),
                    false,
                    [assignment]));
        ManifestMaterial material = ManifestMaterial.Create(
            InternalAssetId.Create("Material").Value,
            definition).Value!;
        SharedAssetDefinition shared = ItemTestAssertions.Shared(
            "Shared",
            "textures/shared.png");
        ItemDefinition item = ItemTestAssertions.Item(
            "Item",
            shared: [shared.Id]);
        ItemSetDefinition set = ItemTestAssertions.AssertSuccess(
            ItemSetDefinition.Create([item], [], [shared], null));

        ProductManifestValidationResult materialResult = CreateDetailed(
            ProductCase.Static,
            [BuildTarget.Portable],
            [Model()],
            [material],
            null,
            [],
            null,
            null);
        ProductManifestValidationResult sharedResult = CreateDetailed(
            ProductCase.ItemSet,
            [BuildTarget.Portable],
            [Model()],
            [],
            null,
            [],
            set,
            null);

        AssertCode(materialResult, "MANIFEST_SOURCE_REFERENCE_UNKNOWN");
        AssertCode(sharedResult, "MANIFEST_SOURCE_REFERENCE_UNKNOWN");
    }

    [Theory]
    [InlineData("static-rig")]
    [InlineData("static-animation")]
    [InlineData("static-set")]
    [InlineData("static-collection")]
    [InlineData("rigged-missing-rig")]
    [InlineData("rigged-animation")]
    [InlineData("rigged-set")]
    [InlineData("rigged-collection")]
    [InlineData("animated-missing-rig")]
    [InlineData("animated-missing-animation")]
    [InlineData("animated-set")]
    [InlineData("animated-collection")]
    [InlineData("set-missing")]
    [InlineData("set-collection")]
    [InlineData("set-rig")]
    [InlineData("set-animation")]
    [InlineData("collection-missing")]
    [InlineData("collection-set")]
    [InlineData("collection-rig")]
    [InlineData("collection-animation")]
    public void EveryCaseContradictionProducesBlockingFinding(string scenario)
    {
        RigDefinition rig = RigTestAssertions.CreateRig();
        AnimationDefinition clip = Clip(rig);
        ItemSetDefinition set =
            ItemTestAssertions.AssertSuccess(ItemSetDefinition.Create([], [], [], null));
        ItemCollectionDefinition collection =
            ItemTestAssertions.AssertSuccess(ItemCollectionDefinition.Create([], [], []));
        ProductCase productCase = (scenario.Split('-')[0] switch
        {
            "static" => ProductCase.Static,
            "rigged" => ProductCase.Rigged,
            "animated" => ProductCase.RiggedAnimated,
            "set" => ProductCase.ItemSet,
            _ => ProductCase.ItemCollection,
        })!;
        bool suppliesRig =
            scenario is not "rigged-missing-rig" and not "animated-missing-rig" &&
            (productCase.Equals(ProductCase.Rigged) ||
             productCase.Equals(ProductCase.RiggedAnimated));
        List<AnimationDefinition?> animations =
            productCase.Equals(ProductCase.RiggedAnimated) &&
            scenario != "animated-missing-animation"
                ? [clip]
                : [];
        if (scenario.EndsWith("-animation", StringComparison.Ordinal) &&
            !productCase.Equals(ProductCase.RiggedAnimated))
        {
            animations = [clip];
        }

        ProductManifestValidationResult result = CreateDetailed(
            productCase,
            [BuildTarget.Portable],
            [Model()],
            [],
            suppliesRig ||
                scenario is "static-rig" or "set-rig" or "collection-rig"
                ? rig
                : null,
            animations,
            productCase.Equals(ProductCase.ItemSet) && scenario != "set-missing" ||
                scenario.EndsWith("-set", StringComparison.Ordinal)
                ? set
                : null,
            productCase.Equals(ProductCase.ItemCollection) &&
                scenario != "collection-missing" ||
                scenario.EndsWith("-collection", StringComparison.Ordinal)
                ? collection
                : null);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Findings,
            finding =>
                finding.Code.Value is
                    "MANIFEST_CASE_SECTION_CONTRADICTORY" or
                    "MANIFEST_RIG_REQUIRED" or
                    "MANIFEST_ANIMATION_REQUIRED" or
                    "MANIFEST_ITEM_SET_REQUIRED" or
                    "MANIFEST_ITEM_COLLECTION_REQUIRED");
        Assert.All(result.Findings, finding => Assert.True(finding.BlocksRelease));
    }

    [Fact]
    public void AnimatedManifestRejectsAnimationUsingDifferentRig()
    {
        RigDefinition declared = RigTestAssertions.CreateRig(RigType.Generic);
        RigDefinition other = RigTestAssertions.CreateRig(RigType.Humanoid);

        ProductManifestValidationResult result = CreateDetailed(
            ProductCase.RiggedAnimated,
            [BuildTarget.Portable],
            [Model()],
            [],
            declared,
            [Clip(other)],
            null,
            null);

        AssertCode(result, "MANIFEST_ANIMATION_RIG_MISMATCH");
    }

    private static ProductManifestValidationResult Create(
        IEnumerable<BuildTarget?> targets,
        IEnumerable<SourceAsset?> sources) =>
        ProductManifest.Create(
            ProductManifest.CurrentSchemaVersion,
            PublisherRoot.Create("AvivPeretsFBX").Value,
            ProductDisplayName.Create("Stone Arch").Value,
            InternalAssetId.Create("StoneArch").Value,
            ProductFolderName.Create("Stone_Arch").Value,
            ProductCase.Static,
            ProductVersion.Create("1.0.0").Value,
            targets,
            sources,
            [],
            null,
            [],
            null,
            null,
            null);

    private static ProductManifestValidationResult CreateDetailed(
        ProductCase productCase,
        IEnumerable<BuildTarget?> targets,
        IEnumerable<SourceAsset?> sources,
        IEnumerable<ManifestMaterial?> materials,
        RigDefinition? rig,
        IEnumerable<AnimationDefinition?> animations,
        ItemSetDefinition? itemSet,
        ItemCollectionDefinition? itemCollection) =>
        ProductManifest.Create(
            ProductManifest.CurrentSchemaVersion,
            PublisherRoot.Create("AvivPeretsFBX").Value,
            ProductDisplayName.Create("Product").Value,
            InternalAssetId.Create("Product").Value,
            ProductFolderName.Create("Product").Value,
            productCase,
            ProductVersion.Create("1.0.0").Value,
            targets,
            sources,
            materials,
            rig,
            animations,
            itemSet,
            itemCollection,
            null);

    private static SourceAsset Model() =>
        SourceAsset.Create(SourceAssetKind.Fbx, "models/a.fbx").Value!;

    private static AnimationDefinition Clip(RigDefinition rig) =>
        RigTestAssertions.AssertSuccess(
            AnimationDefinition.Create(
                "Clip",
                0,
                1,
                30,
                LoopBehavior.Once,
                RootMotionStatus.None,
                null,
                rig));

    private static void AssertCode(
        ProductManifestValidationResult result,
        string expected)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Findings, finding => finding.Code.Value == expected);
    }
}
