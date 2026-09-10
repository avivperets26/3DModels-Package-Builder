using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using PackageBuilder.Application.Items;

namespace PackageBuilder.Targets.Portable.Tests;

/// <summary>One twelve-item manifest drives real portable bytes and the Unity adapter plans.</summary>
[Trait("Task", "PB-0811")]
public sealed class CollectionEndToEndTests
{
    [Fact]
    public async Task TwelveItemsHaveExactDeterministicArchiveInventoryAndUnityOwnership()
    {
        string root = Path.Combine(EquipmentArchiveFixture.Root, "tests", "fixtures", "portable", "twelve-item-collection");
        var fixture = new EquipmentArchiveFixture(fixtureRoot: root);
        string[] ids = ["Column12", "Column01", "Column10", "Column02", "Column11", "Column03",
            "Column09", "Column04", "Column08", "Column05", "Column07", "Column06"];
        Assert.Equal(ids, fixture.Items.Select(item => item.Id.Value));
        SharedAssetReusePlan reuse = Assert.IsType<SharedAssetReusePlan>(SharedAssetDeduplicator.Plan(fixture.Manifest,
            [new(fixture.Manifest.SourceAssets.Single(source => source.LogicalReference.EndsWith(".png", StringComparison.Ordinal)),
                fixture.Shared[0].Artifact.Record.ContentIdentity)], TestContext.Current.CancellationToken).Plan);
        ItemPrefabPlan prefabs = Assert.IsType<ItemPrefabPlan>(ItemPrefabPlan.Create(reuse).Plan);
        Assert.Equal(ids.Order(StringComparer.Ordinal).Select(id => "P_" + id + ".prefab"), prefabs.Items.Select(item => item.PrefabFileName));
        string collection = MultiItemBuildPlan.Collection(prefabs);
        Assert.Equal(ids, JsonNode.Parse(collection)!["items"]!.AsArray().Select(item => item!["itemId"]!.GetValue<string>()));
        PortableMultiItemPlan plan = fixture.Plan();
        PortableFolderLayout layout = fixture.Layout(plan);
        using var archive = new MemoryStream();
        PortableFbxArchiveReceipt receipt = await fixture.BuildAsync(layout, archive);
        Assert.True((await PortableTargetValidator.ValidateArchiveAsync(layout, receipt, archive, TestContext.Current.CancellationToken)).Passed);
        using var repeat = new MemoryStream();
        _ = await fixture.BuildAsync(layout, repeat);
        Assert.Equal(archive.ToArray(), repeat.ToArray());
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
        string[] expected = [.. ids.Select(id => id + ".fbx"), "T_SharedStone_Albedo.png", "README_TwelveColumns.txt", "INVENTORY.md"];
        Assert.Equal(expected.Order(StringComparer.Ordinal).Select(name => "TwelveColumns_fbx/" + name), zip.Entries.Select(entry => entry.FullName));
        int previous = -1;
        foreach (PortableItemArtifact item in fixture.Items)
        {
            int position = plan.InventoryText.IndexOf(item.Id.Value, StringComparison.Ordinal);
            Assert.True(position > previous);
            previous = position;
            byte[] original = await File.ReadAllBytesAsync(Path.Combine(root, "source", item.Id.Value + ".fbx"), TestContext.Current.CancellationToken);
            using var extracted = new MemoryStream();
            using Stream entryStream = zip.GetEntry("TwelveColumns_fbx/" + item.Id.Value + ".fbx")!.Open();
            await entryStream.CopyToAsync(extracted, TestContext.Current.CancellationToken);
            Assert.Equal(original, extracted.ToArray());
            Assert.Contains(FormattableString.Invariant($"| Item | {item.Id.Value} | {item.Id.Value}.fbx | SharedStone | {original.Length} | {item.Artifact.Record.ContentIdentity.Sha256.Value} |"), plan.InventoryText);
        }
        using var reader = new StreamReader(zip.GetEntry("TwelveColumns_fbx/INVENTORY.md")!.Open(), Encoding.UTF8);
        Assert.Equal(plan.InventoryText, await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        string? output = Environment.GetEnvironmentVariable("PACKAGEBUILDER_TWELVE_OUTPUT");
        if (output is null)
        { return; }
        string fullOutput = Path.GetFullPath(output);
        Assert.StartsWith(EquipmentArchiveFixture.Root + Path.DirectorySeparatorChar, fullOutput, StringComparison.OrdinalIgnoreCase);
        _ = Directory.CreateDirectory(fullOutput);
        await File.WriteAllBytesAsync(Path.Combine(fullOutput, receipt.ArchiveFileName), archive.ToArray(), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(fullOutput, "ownership.json"), prefabs.SerializeOwnership(), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(fullOutput, "collection.json"), collection, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(fullOutput, "INVENTORY.md"), plan.InventoryText, TestContext.Current.CancellationToken);
    }
}
