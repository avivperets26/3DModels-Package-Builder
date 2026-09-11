using System.Globalization;
using System.Net;
using PackageBuilder.Application.Documentation;
using PackageBuilder.Domain.Animations;
using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Targets.Portable.Tests;

public sealed class MeasuredReadmeTests
{
    private static ReadmeMeasurements Metrics => new(1.25, 2.5, .75, 456, 2, 3, 1, "+Y", "-Z", "Base centre");
    private static ReadmeBuildData Build => ReadmeBuildData.Create(BuildTarget.Portable,
        [new("Models/model.fbx", "FBX", "7.4"), new("Models/second.fbx", "FBX", "7.4")], Metrics, null, [], ["Import the supplied FBX."]).Value!;

    [Theory]
    [InlineData("static", "Static model")]
    [InlineData("rigged", "Rig")]
    [InlineData("rigged-animated", "Animation usage")]
    [InlineData("item-set", "Item set")]
    [InlineData("item-collection", "Item collection")]
    public void FiveCasesHaveOnlyApplicableSectionsAndAccurateTables(string caseName, string heading)
    {
        ProductManifest manifest = PortableReadmeTestValues.Manifest(ProductCase.TryParse(caseName).Value!);
        IReadOnlyList<ItemDefinition>? items = manifest.ItemSet?.Items ?? manifest.ItemCollection?.Items;
        ReadmeItemMeasurements[] measurements = items?.Select(item => new ReadmeItemMeasurements(item.Id, "Models/model.fbx", Metrics)).ToArray() ?? [];
        DocumentationResult<PortableReadmeDocument> result = Render(manifest, measurements);
        Assert.True(result.IsSuccess, result.Error);
        string text = WebUtility.HtmlDecode(result.Value!.Text);
        Assert.Contains("## " + heading + "\n", text);
        Assert.Equal(manifest.Rig is not null, text.Contains("## Rig\n", StringComparison.Ordinal));
        Assert.Equal(manifest.Animations.Count > 0, text.Contains("## Animations\n", StringComparison.Ordinal));
        Assert.Equal(items is not null, text.Contains("## Item inventory\n", StringComparison.Ordinal));
        Assert.Equal(manifest.ItemSet is not null, text.Contains("## Assembly and compatibility\n", StringComparison.Ordinal));
        if (items is not null)
        {
            Assert.Contains($"| {items[0].Id.Value} | Models/model.fbx | 1.25 | 2.5 | 0.75 | 456 | 2 | 3 | None |", text);
        }

        if (manifest.Animations.Count > 0)
        {
            Assert.Contains("| BowShot | 1 | 31 | 1 | 30 | once | none | None |", text);
        }

        if (caseName == "rigged")
        {
            Assert.Contains("No animation clips are included.", text);
        }

        Assert.DoesNotContain('\r', result.Value.Text);
        string expected = result.Value.Text;
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(expected, Render(manifest, measurements).Value!.Text);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void InventoryRequiresExactIdentityAndDeliveredFileCoverage()
    {
        ProductManifest manifest = PortableReadmeTestValues.Manifest(ProductCase.ItemCollection);
        var item = new ReadmeItemMeasurements(manifest.ItemCollection!.Items[0].Id, "Models/model.fbx", Metrics);
        Assert.Equal("DOC_ITEM_METRICS_REQUIRED", Render(manifest, null).Error);
        Assert.Equal("DOC_ITEM_METRICS_INVALID", Render(manifest, []).Error);
        Assert.Equal("DOC_ITEM_METRICS_INVALID", Render(manifest, [item, item]).Error);
        Assert.Equal("DOC_ITEM_METRICS_INVALID", Render(manifest, [item with { ItemId = InternalAssetId.Create("Foreign").Value! }]).Error);
        Assert.Equal("DOC_ITEM_METRICS_INVALID", Render(manifest, [item with { File = "Models/missing.fbx" }]).Error);
        Assert.Equal("DOC_ITEM_METRICS_INVALID", Render(manifest, [item with { Measurements = Metrics with { Width = double.NaN } }]).Error);
        Assert.Equal("DOC_UNEXPECTED_ITEM_METRICS", Render(PortableReadmeTestValues.Manifest(ProductCase.Static), [item]).Error);
    }

    [Fact]
    public void InventoryPreservesDeclarationOrderAndDoesNotSumSharedResources()
    {
        ProductManifest source = PortableReadmeTestValues.Manifest(ProductCase.ItemCollection);
        ItemDefinition first = source.ItemCollection!.Items[0];
        ItemDefinition second = ItemDefinition.Create(InternalAssetId.Create("Alpha").Value, [], null, []).Value!;
        ItemCollectionDefinition collection = ItemCollectionDefinition.Create([first, second], [], []).Value!;
        ProductManifest manifest = ProductManifest.Create(source.SchemaVersion, source.PublisherProfileReference, source.DisplayName,
            source.AssetId, source.FolderName, source.ProductCase, source.Version, source.Targets, source.SourceAssets,
            source.Materials, null, [], null, collection).Value!;
        ReadmeItemMeasurements[] rows = [ new ReadmeItemMeasurements(second.Id, "Models/second.fbx", Metrics with { Triangles = 123 }),
            new ReadmeItemMeasurements(first.Id, "Models/model.fbx", Metrics) ];
        string text = WebUtility.HtmlDecode(Render(manifest, rows).Value!.Text);
        Assert.True(text.IndexOf("| Sword |", StringComparison.Ordinal) < text.IndexOf("| Alpha |", StringComparison.Ordinal));
        Assert.Contains("Triangles: 456", text);
        Assert.Contains("| 123 | 2 | 3 |", text);
        Assert.Equal("DOC_ITEM_METRICS_INVALID", Render(manifest, [rows[0], rows[1] with { File = rows[0].File }]).Error);
    }

    [Fact]
    public void SetDocumentsDeclaredSlotsMembershipAndCompatibilityWithoutInventingRigSupport()
    {
        ProductManifest source = PortableReadmeTestValues.Manifest(ProductCase.ItemSet);
        AttachmentSlot slot = AttachmentSlot.Create("head").Value!;
        ItemDefinition item = ItemDefinition.Create(source.ItemSet!.Items[0].Id, [], slot, []).Value!;
        AssembledSetRules rules = AssembledSetRules.Create([AssembledSetMember.Create(item.Id, slot).Value], true,
            [CompatibilityMetadataEntry.Create(InternalAssetId.Create("Skeleton").Value, "Example rig only").Value]).Value!;
        ItemSetDefinition set = ItemSetDefinition.Create([item], [], [], rules).Value!;
        ProductManifest manifest = ProductManifest.Create(source.SchemaVersion, source.PublisherProfileReference, source.DisplayName,
            source.AssetId, source.FolderName, source.ProductCase, source.Version, source.Targets, source.SourceAssets,
            source.Materials, null, [], set, null).Value!;
        string text = WebUtility.HtmlDecode(Render(manifest, [new(item.Id, "Models/model.fbx", Metrics)]).Value!.Text);
        Assert.Contains("Member: Helm; slot: head", text);
        Assert.Contains("Skeleton: Example rig only", text);
        Assert.Contains("Unique attachment slots required: True", text);
        Assert.DoesNotContain("## Rig", text);
        Assert.DoesNotContain("No assembled-set compatibility", text);
    }

    [Fact]
    public void TableCellsCannotInjectMarkdownOrTemplateCode()
    {
        ProductManifest source = PortableReadmeTestValues.Manifest(ProductCase.RiggedAnimated);
        AnimationDefinition clip = PackageBuilder.Domain.Animations.AnimationDefinition.Create("<script>|{{7*7}}", -3, -3, 29.97,
            source.Animations[0].LoopBehavior, source.Animations[0].RootMotionStatus, null, source.Rig).Value!;
        ProductManifest manifest = ProductManifest.Create(source.SchemaVersion, source.PublisherProfileReference, source.DisplayName,
            source.AssetId, source.FolderName, source.ProductCase, source.Version, source.Targets, source.SourceAssets,
            source.Materials, source.Rig, [clip], null, null).Value!;
        string text = Render(manifest, []).Value!.Text;
        Assert.DoesNotContain("<script>", text);
        Assert.DoesNotContain("{{", text);
        Assert.Contains("&#124;", text);
        Assert.Contains("| -3 | -3 | 0 |", WebUtility.HtmlDecode(text));
        Assert.Equal("0", ReadmeTableGenerator.Animations(manifest, [clip]).Value!.Rows[0][3]);
    }

    [Fact]
    public void AnimationTablesUseInspectedValuesInsteadOfPlannedFramesAndRejectIncompleteCoverage()
    {
        ProductManifest manifest = PortableReadmeTestValues.Manifest(ProductCase.RiggedAnimated);
        AnimationDefinition planned = manifest.Animations[0];
        AnimationDefinition inspected = AnimationDefinition.Create(planned.Name, 0, 48, 24,
            planned.LoopBehavior, planned.RootMotionStatus, null, manifest.Rig).Value!;
        ReadmeTable table = ReadmeTableGenerator.Animations(manifest, [inspected]).Value!;
        string[] expected = ["BowShot", "0", "48", "2", "24", "once", "none", "None"];
        Assert.Equal(expected, table.Rows[0]);
        Assert.Equal("DOC_ANIMATION_METRICS_REQUIRED", ReadmeTableGenerator.Animations(manifest, null).Error);
        Assert.Equal("DOC_ANIMATION_METRICS_INVALID", ReadmeTableGenerator.Animations(manifest, []).Error);
        Assert.Equal("DOC_ANIMATION_METRICS_INVALID", ReadmeTableGenerator.Animations(manifest, [inspected, inspected]).Error);
        Assert.Equal("DOC_ANIMATION_METRICS_REQUIRED", SharedReadmeGenerator.Generate(manifest,
            PortableReadmeTestValues.Publisher(), Build, TestContext.Current.CancellationToken).Error);
        string text = WebUtility.HtmlDecode(SharedReadmeGenerator.Generate(manifest,
            PortableReadmeTestValues.Publisher(), Build, [], [inspected], TestContext.Current.CancellationToken).Value!.Text);
        Assert.Contains("| BowShot | 0 | 48 | 2 | 24 |", text);
    }

    private static DocumentationResult<PortableReadmeDocument> Render(ProductManifest manifest, IEnumerable<ReadmeItemMeasurements>? items) =>
        PortableReadmeGenerator.Generate(manifest, PortableReadmeTestValues.Publisher(), Build, items, manifest.Animations, TestContext.Current.CancellationToken);
}
