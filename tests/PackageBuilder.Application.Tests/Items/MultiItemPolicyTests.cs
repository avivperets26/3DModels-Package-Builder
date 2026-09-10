using System.Text.Json;
using System.Text.Json.Nodes;
using PackageBuilder.Application.Items;
using PackageBuilder.Contracts.Manifests;
using PackageBuilder.MultiItem;

namespace PackageBuilder.Application.Tests.Items;

/// <summary>Checks the same policy source compiled into Application and Unity, including shared JSON vectors.</summary>
[Trait("Task", "PB-0805-PB-0807")]
public sealed class MultiItemPolicyTests
{
    private static readonly JsonSerializerOptions _wireOptions = new() { IncludeFields = true };
    [Fact]
    public void AdapterPlansPreserveDeclaredOrderAndMatchUnityVectors()
    {
        JsonNode node = JsonNode.Parse(Read("assembled-set-manifest.json"))!;
        ItemPrefabPlan items = Prefabs(node);
        AssembledSetPlan set = AssembledSetPlan.Create(items).Plan!;
        AttachmentBinding[] bindings = JsonSerializer.Deserialize<AttachmentBinding[]>(JsonNode.Parse(Read("set-attachments.json"))!["bindings"]!.ToJsonString(),
            _wireOptions)!;
        Assert.Equal(Read("set-attachments.json").Trim(), MultiItemBuildPlan.Attachments(set, bindings));
        _ = Assert.Throws<ArgumentException>(() => MultiItemBuildPlan.Collection(items));
        node["product"]!["case"] = "item-collection";
        node["product"]!["assetId"] = "ExampleCollection";
        node["product"]!["folderName"] = "ExampleCollection";
        node["itemCollection"] = node["itemSet"]!.DeepClone();
        _ = node["itemCollection"]!.AsObject().Remove("assembledSetRules");
        _ = node.AsObject().Remove("itemSet");
        Assert.Equal(Read("collection-plan.json").Trim(), MultiItemBuildPlan.Collection(Prefabs(node)));
    }

    [Theory]
    [InlineData("socket")]
    [InlineData("bone")]
    [InlineData("body-slot")]
    public void AttachmentsRequireExactCurrentTargetAndUniqueBinding(string kind)
    {
        AttachmentRequirement[] members = [new() { itemId = "Hat", slot = "head" }];
        AttachmentBinding[] bindings = [new() { itemId = "Hat", slot = "head", targetId = "Character", kind = kind, point = "Root/Head" }];
        AttachmentPoint[] points = [new() { targetId = "Character", kind = kind, point = "Root/Head" }];
        Assert.True(AttachmentPolicy.Validate(members, bindings, points, out _));
        Assert.False(AttachmentPolicy.Validate(members, [], points, out _));
        Assert.False(AttachmentPolicy.Validate(members, bindings, [], out _));
        Assert.False(AttachmentPolicy.Validate(members, [bindings[0], bindings[0]], points, out _));
        Assert.False(AttachmentPolicy.Validate(members, bindings, [points[0], points[0]], out _));
        points[0].point = "Root/head";
        Assert.False(AttachmentPolicy.Validate(members, bindings, points, out _));
        points[0].point = "Root/Head";
        bindings[0].slot = "body";
        Assert.False(AttachmentPolicy.Validate(members, bindings, points, out _));
        bindings[0].slot = "head";
        bindings[0].kind = "guessed";
        Assert.False(AttachmentPolicy.Validate(members, bindings, points, out _));
    }

    [Fact]
    public void UnslottedMembersDoNotInferAttachmentsAndUnknownMembersAreRejected()
    {
        AttachmentRequirement[] members = [new() { itemId = "Prop" }];
        Assert.True(AttachmentPolicy.Validate(members, [], [], out _));
        Assert.False(AttachmentPolicy.Validate(members, [new() { itemId = "Unknown" }], [], out _));
        Assert.False(AttachmentPolicy.Validate(members, [new() { itemId = "Prop" }], [], out _));
        Assert.False(AttachmentPolicy.Validate([members[0], members[0]], [], [], out _));
        Assert.False(AttachmentPolicy.Validate(null!, [], [], out _));
    }

    [Fact]
    public void LayoutPreservesOrderGroundsAndCentersUnequalOffsetBounds()
    {
        ItemBounds[] bounds = [new() { ItemId = "Zed", MinX = -2, MaxX = 2, MinY = -3, MaxY = 4, MinZ = 2, MaxZ = 4 },
            new() { ItemId = "Alpha", MinX = 5, MaxX = 6, MinY = 7, MaxY = 9, MinZ = -4, MaxZ = 0 }];
        Assert.True(OverviewLayoutPolicy.TryPlan(bounds, 0.5, out ItemPlacement[] result));
        Assert.Equal(["Zed", "Alpha"], result.Select(item => item.ItemId));
        Assert.Equal(0.5, bounds[1].MinX + result[1].X - bounds[0].MaxX - result[0].X, 8);
        Assert.Equal(0, bounds[0].MinX + result[0].X + bounds[1].MaxX + result[1].X, 8);
        Assert.Equal([3.0, -7.0], result.Select(item => item.Y));
        Assert.Equal([-3.0, 2.0], result.Select(item => item.Z));
        Assert.True(OverviewLayoutPolicy.TryPlan(bounds, 0.5, out ItemPlacement[] repeated));
        Assert.Equal(result.Select(item => item.X), repeated.Select(item => item.X));
        Assert.True(OverviewLayoutPolicy.TryPlan([bounds[0]], 0, out ItemPlacement[] single));
        Assert.Equal(0, single[0].X);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1000001)]
    public void InvalidGapsReturnNoPartialLayout(double gap)
    {
        Assert.False(OverviewLayoutPolicy.TryPlan([new() { ItemId = "A" }], gap, out ItemPlacement[] result));
        Assert.Empty(result);
    }

    [Fact]
    public void InvalidBoundsAndExcessiveInputsAreRejected()
    {
        Assert.False(OverviewLayoutPolicy.TryPlan([], 0, out _));
        Assert.False(OverviewLayoutPolicy.TryPlan(null!, 0, out _));
        Assert.False(OverviewLayoutPolicy.TryPlan([new() { ItemId = "A", MinX = 1 }], 0, out _));
        Assert.False(OverviewLayoutPolicy.TryPlan([new() { ItemId = "A", MaxY = double.NaN }], 0, out _));
        Assert.False(OverviewLayoutPolicy.TryPlan([new() { ItemId = "A" }, new() { ItemId = "a" }], 0, out _));
        Assert.False(OverviewLayoutPolicy.TryPlan(new ItemBounds[10001], 0, out _));
        Assert.False(OverviewLayoutPolicy.TryPlan([.. Enumerable.Range(0, 20).Select(index => new ItemBounds
        {
            ItemId = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            MaxX = 1000000
        })], 0, out _));
    }

    private static ItemPrefabPlan Prefabs(JsonNode node)
    {
        ProductManifestDeserializationResult manifest = ProductManifestJson.Deserialize(node.ToJsonString());
        Assert.True(manifest.IsSuccessful);
        SharedAssetReuseResult reuse = SharedAssetDeduplicator.Plan(manifest.Value!, [], TestContext.Current.CancellationToken);
        Assert.Empty(reuse.Findings);
        return ItemPrefabPlan.Create(reuse.Plan!).Plan!;
    }

    private static string Read(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PackageBuilder.sln")))
        { directory = directory.Parent; }
        return File.ReadAllText(Path.Combine(directory!.FullName, "tests", "fixtures", "manifests", name));
    }
}
