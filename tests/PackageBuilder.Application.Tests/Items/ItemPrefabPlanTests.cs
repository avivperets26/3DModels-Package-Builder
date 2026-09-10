using System.Text.Json.Nodes;
using PackageBuilder.Application.Items;
using PackageBuilder.Contracts.Manifests;

namespace PackageBuilder.Application.Tests.Items;

/// <summary>Checks the exact application-to-Unity ownership envelope for sets and collections.</summary>
[Trait("Task", "PB-0803")]
public sealed class ItemPrefabPlanTests
{
    [Theory]
    [InlineData("item-set")]
    [InlineData("item-collection")]
    public void MultipleFilesProduceOnePrefabPerItemAndMatchSharedUnityVector(string productCase)
    {
        SharedAssetReusePlan reuse = Reuse(productCase);
        ItemPrefabPlan plan = ItemPrefabPlan.Create(reuse).Plan!;
        Assert.Same(reuse, plan.Reuse);
        Assert.Equal(["P_Alpha.prefab", "P_Zed.prefab"], plan.Items.Select(item => item.PrefabFileName));
        Assert.Equal(2, plan.Items[0].ModelSources.Count);
        _ = Assert.Single(plan.Items[1].ModelSources);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PackageBuilder.sln")))
        {
            directory = directory.Parent;
        }

        string expected = File.ReadAllText(Path.Combine(directory!.FullName, "tests", "fixtures", "manifests", "item-prefab-ownership.json")).Trim();
        Assert.Equal(expected, plan.SerializeOwnership());
        _ = Assert.Throws<NotSupportedException>(() => ((IList<string>)plan.Items[0].ModelSources).Clear());
    }

    [Fact]
    public void CaseCollidingPrefabNamesFailWithoutRenamingItems()
    {
        ItemPrefabPlanResult result = ItemPrefabPlan.Create(Reuse("item-collection", collision: true));
        Assert.Null(result.Plan);
        Assert.Equal("ITEM_PREFAB_NAME_COLLISION", result.DiagnosticCode);
    }

    [Fact]
    public void PlanRequiresValidatedReuseInput() => Assert.Throws<ArgumentNullException>(() => ItemPrefabPlan.Create(null!));

    [Fact]
    public void ImageOwnershipDoesNotCreateAnExtraModelPart()
    {
        ItemPrefabPlan plan = ItemPrefabPlan.Create(Reuse("item-collection", includeImage: true)).Plan!;
        Assert.Equal(2, plan.Items.Count);
        Assert.Equal(2, plan.Items[0].ModelSources.Count);
        Assert.DoesNotContain(plan.Items.SelectMany(item => item.ModelSources), source => source.EndsWith(".png", StringComparison.Ordinal));
        _ = Assert.Single(plan.Reuse.Manifest.SourceAssets, source => source.LogicalReference == "images/alpha.png");
    }

    private static SharedAssetReusePlan Reuse(string productCase, bool collision = false, bool includeImage = false)
    {
        JsonNode node = JsonNode.Parse("""
            {"schemaVersion":1,"publisherProfileReference":"ExamplePublisher",
             "product":{"displayName":"Example","assetId":"Example","folderName":"Example","case":"item-collection","version":"1.0.0"},
             "targets":["unity"],"sourceAssets":[{"kind":"fbx","logicalReference":"models/zed.fbx"},{"kind":"fbx","logicalReference":"models/alpha-b.fbx"},{"kind":"fbx","logicalReference":"models/alpha-a.fbx"}],
             "materials":[],"animations":[],"itemCollection":{"items":[{"id":"Zed","categories":[],"sharedAssetReferences":[]},{"id":"Alpha","categories":[],"sharedAssetReferences":[]}],"relationships":[],"sharedAssets":[]},
             "itemSourceAssignments":[{"itemId":"Zed","sourceReference":"models/zed.fbx"},{"itemId":"Alpha","sourceReference":"models/alpha-b.fbx"},{"itemId":"Alpha","sourceReference":"models/alpha-a.fbx"}]}
            """)!;
        node["product"]!["case"] = productCase;
        if (includeImage)
        {
            node["sourceAssets"]!.AsArray().Add(JsonNode.Parse("""{"kind":"image","logicalReference":"images/alpha.png"}"""));
            node["itemSourceAssignments"]!.AsArray().Add(JsonNode.Parse("""{"itemId":"Alpha","sourceReference":"images/alpha.png"}"""));
        }
        if (collision)
        {
            node["itemCollection"]!["items"]![0]!["id"] = "alpha";
            node["itemSourceAssignments"]![0]!["itemId"] = "alpha";
        }

        if (productCase == "item-set")
        {
            node["itemSet"] = node["itemCollection"]!.DeepClone();
            _ = node.AsObject().Remove("itemCollection");
        }

        ProductManifestDeserializationResult manifest = ProductManifestJson.Deserialize(node.ToJsonString());
        Assert.True(manifest.IsSuccessful);
        SharedAssetReuseResult result = SharedAssetDeduplicator.Plan(manifest.Value!, [], TestContext.Current.CancellationToken);
        Assert.Empty(result.Findings);
        return result.Plan!;
    }
}
