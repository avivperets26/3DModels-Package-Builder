using System.Text.Json.Nodes;
using PackageBuilder.Application.Items;
using PackageBuilder.Contracts.Manifests;

namespace PackageBuilder.Application.Tests.Items;

/// <summary>Proves the shared assembly contract derives order, slots and compatibility from reviewed intent.</summary>
[Trait("Task", "PB-0804")]
public sealed class AssembledSetPlanTests
{
    [Fact]
    public void DeclaredOrderOverridesCanonicalPrefabAndMemberOrder()
    {
        ItemPrefabPlan items = Prefabs(JsonNode.Parse(Read("assembled-set-manifest.json"))!);
        AssembledSetPlan plan = AssembledSetPlan.Create(items).Plan!;
        Assert.Equal(["Alpha", "Zed"], items.Items.Select(item => item.ItemId.Value));
        Assert.Equal(["Zed", "Alpha"], plan.Members.Select(item => item.ItemId));
        Assert.Same(items, plan.ItemPrefabs);
        Assert.Equal(Read("assembled-set-plan.json").Trim(), plan.Serialize());
        _ = Assert.Throws<NotSupportedException>(() => ((IList<AssembledSetEntry>)plan.Members).Clear());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingAssemblyDeclarationOrCollectionDoesNotInferAnAssembly(bool collection)
    {
        JsonNode node = JsonNode.Parse(Read("assembled-set-manifest.json"))!;
        _ = node["itemSet"]!.AsObject().Remove("assembledSetRules");
        if (collection)
        {
            node["product"]!["case"] = "item-collection";
            node["itemCollection"] = node["itemSet"]!.DeepClone();
            _ = node.AsObject().Remove("itemSet");
        }
        AssembledSetPlanResult result = AssembledSetPlan.Create(Prefabs(node));
        Assert.Null(result.Plan);
        Assert.Equal("SET_ASSEMBLY_DECLARATION_REQUIRED", result.DiagnosticCode);
    }

    [Fact]
    public void OptionalSlotsAndCompatibilityRemainExplicit()
    {
        JsonNode node = JsonNode.Parse(Read("assembled-set-manifest.json"))!;
        _ = node["itemSet"]!["items"]![0]!.AsObject().Remove("attachmentSlot");
        _ = node["itemSet"]!["assembledSetRules"]!["members"]![1]!.AsObject().Remove("attachmentSlot");
        node["itemSet"]!["assembledSetRules"]!["compatibilityMetadata"] = new JsonArray();
        AssembledSetPlan plan = AssembledSetPlan.Create(Prefabs(node)).Plan!;
        Assert.Equal("", plan.Members[0].Slot);
        Assert.Equal("Item_Zed", plan.Members[0].ContainerName);
        Assert.Empty(plan.Rules.CompatibilityMetadata);
        Assert.Contains("\"attachmentValidation\":\"not-performed\"", plan.Serialize());
    }

    [Fact]
    public void RepeatedLogicalSlotsKeepUniqueMemberContainersWhenPermitted()
    {
        JsonNode node = JsonNode.Parse(Read("assembled-set-manifest.json"))!;
        node["itemSet"]!["items"]![1]!["attachmentSlot"] = "head";
        node["itemSet"]!["assembledSetRules"]!["members"]![0]!["attachmentSlot"] = "head";
        node["itemSet"]!["assembledSetRules"]!["requireUniqueAttachmentSlots"] = false;
        AssembledSetPlan plan = AssembledSetPlan.Create(Prefabs(node)).Plan!;
        Assert.Equal(["Slot_head_Zed", "Slot_head_Alpha"], plan.Members.Select(member => member.ContainerName));
    }

    [Fact]
    public void NullInputIsRejected() => Assert.Throws<ArgumentNullException>(() => AssembledSetPlan.Create(null!));

    [Fact]
    public void SingleItemSetHasADistinctAssemblyFilename()
    {
        JsonNode node = JsonNode.Parse(Read("assembled-set-manifest.json"))!;
        node["product"]!["assetId"] = "Zed";
        node["sourceAssets"]!.AsArray().RemoveAt(2);
        node["sourceAssets"]!.AsArray().RemoveAt(1);
        node["itemSourceAssignments"]!.AsArray().RemoveAt(2);
        node["itemSourceAssignments"]!.AsArray().RemoveAt(1);
        node["itemSet"]!["items"]!.AsArray().RemoveAt(1);
        node["itemSet"]!["assembledSetRules"]!["members"]!.AsArray().RemoveAt(0);
        AssembledSetPlan plan = AssembledSetPlan.Create(Prefabs(node)).Plan!;
        _ = Assert.Single(plan.Members);
        Assert.Equal("P_Zed_Assembled.prefab", plan.PrefabFileName);
        Assert.NotEqual(plan.Members[0].PrefabFileName, plan.PrefabFileName);
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
