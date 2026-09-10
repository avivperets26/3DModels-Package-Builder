using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using PackageBuilder.Application.Items;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.Manifests;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Textures;
using PackageBuilder.MultiItem;

namespace PackageBuilder.Targets.Portable.Tests;

[Trait("Task", "PB-0809")]
public sealed class PortableMultiItemTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetAndCollectionArchiveHaveExactStableInventoryAndDocuments(bool collection)
    {
        var fixture = new EquipmentArchiveFixture(collection);
        PortableMultiItemPlan plan = fixture.Plan();
        Assert.True(plan.InventoryText.IndexOf("Helmet", StringComparison.Ordinal) < plan.InventoryText.IndexOf("Armour", StringComparison.Ordinal));
        Assert.Contains("Helmet: slot head", plan.ReadmeText);
        Assert.Contains("Armour: slot body", plan.ReadmeText);
        using var archive = new MemoryStream();
        PortableFolderLayout layout = fixture.Layout(plan);
        PortableFbxArchiveReceipt receipt = await fixture.BuildAsync(layout, archive);
        Assert.True((await PortableTargetValidator.ValidateArchiveAsync(layout, receipt, archive, TestContext.Current.CancellationToken)).Passed);
        using var second = new MemoryStream();
        PortableFbxArchiveReceipt repeat = await fixture.BuildAsync(layout, second);
        Assert.Equal(receipt.ArchiveIdentity, repeat.ArchiveIdentity);
        Assert.Equal(archive.ToArray(), second.ToArray());
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
        Assert.Equal(["EquipmentSet_fbx/Armour.fbx", "EquipmentSet_fbx/Helmet.fbx", "EquipmentSet_fbx/INVENTORY.md",
            "EquipmentSet_fbx/README_EquipmentSet.txt", "EquipmentSet_fbx/T_SharedSteel_Albedo.png"], zip.Entries.Select(entry => entry.FullName));
        using var reader = new StreamReader(zip.GetEntry("EquipmentSet_fbx/INVENTORY.md")!.Open());
        Assert.Equal(plan.InventoryText, await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void MissingDuplicateUnknownWrongPurposeAndCollidingItemsFailClosed()
    {
        var fixture = new EquipmentArchiveFixture();
        Assert.Null(PortableMultiItemPlan.Create(fixture.Manifest, fixture.Items.Take(1), fixture.Shared).Plan);
        Assert.Null(PortableMultiItemPlan.Create(fixture.Manifest, [fixture.Items[0], fixture.Items[0]], fixture.Shared).Plan);
        Assert.Null(PortableMultiItemPlan.Create(fixture.Manifest, fixture.Items, []).Plan);
        Assert.Null(PortableMultiItemPlan.Create(fixture.Manifest, fixture.Items, [new(InternalAssetId.Create("Unknown").Value!, fixture.Shared[0].Artifact)]).Plan);
        Assert.Equal("PORTABLE_ITEM_FBX_REQUIRED", PortableMultiItemPlan.Create(fixture.Manifest,
            [fixture.Items[0] with { Artifact = fixture.Shared[0].Artifact }, fixture.Items[1]], fixture.Shared).DiagnosticCode);
        Assert.Equal("PORTABLE_SHARED_ARTIFACT_INVALID", PortableMultiItemPlan.Create(fixture.Manifest,
            fixture.Items, [fixture.Shared[0] with { Artifact = fixture.Items[0].Artifact }]).DiagnosticCode);
        Assert.Equal("PORTABLE_ITEM_NAME_COLLISION", PortableMultiItemPlan.Create(fixture.Manifest,
            [fixture.Items[0], fixture.Items[1] with { Artifact = fixture.Items[0].Artifact }], fixture.Shared).DiagnosticCode);
        _ = Assert.Throws<ArgumentNullException>(() => PortableMultiItemPlan.Create(null!, fixture.Items, fixture.Shared));
        _ = Assert.Throws<ArgumentNullException>(() => PortableMultiItemPlan.Create(fixture.Manifest, null!, fixture.Shared));
        _ = Assert.Throws<ArgumentNullException>(() => PortableMultiItemPlan.Create(fixture.Manifest, fixture.Items, null!));
    }

    [Fact]
    public void DraftMappingsNullBindingsAndCaseCollisionsAreRejected()
    {
        var fixture = new EquipmentArchiveFixture();
        JsonNode json = JsonNode.Parse(File.ReadAllText(Path.Combine(EquipmentArchiveFixture.FixtureRoot, "manifest.json")))!;
        _ = json.AsObject().Remove("itemSourceAssignments");
        ProductManifest draft = Assert.IsType<ProductManifest>(ProductManifestJson.Deserialize(json.ToJsonString()).Value);
        Assert.Equal("PORTABLE_ITEMS_REVIEW_REQUIRED", PortableMultiItemPlan.Create(draft, fixture.Items, fixture.Shared).DiagnosticCode);
        Assert.Null(PortableMultiItemPlan.Create(fixture.Manifest, [null!, fixture.Items[1]], fixture.Shared).Plan);
        string collisionJson = File.ReadAllText(Path.Combine(EquipmentArchiveFixture.FixtureRoot, "manifest.json")).Replace("Armour", "helmet", StringComparison.Ordinal);
        ProductManifest collision = Assert.IsType<ProductManifest>(ProductManifestJson.Deserialize(collisionJson).Value);
        Assert.Equal("PORTABLE_ITEM_NAME_COLLISION", PortableMultiItemPlan.Create(collision,
            [fixture.Items[0], fixture.Items[1] with { Id = InternalAssetId.Create("helmet").Value! }], fixture.Shared).DiagnosticCode);
    }

    [Fact]
    public void CanonicalSharedAliasesAreStoredOnceAndIncompatibleAliasesFail()
    {
        var fixture = new EquipmentArchiveFixture();
        JsonNode json = JsonNode.Parse(File.ReadAllText(Path.Combine(EquipmentArchiveFixture.FixtureRoot, "manifest.json")))!;
        JsonNode alias = json["itemSet"]!["sharedAssets"]![0]!.DeepClone();
        alias["id"] = "SteelAlias";
        json["itemSet"]!["sharedAssets"]!.AsArray().Add(alias);
        json["itemSet"]!["items"]![1]!["sharedAssetReferences"]!.AsArray().Add("SteelAlias");
        ProductManifest manifest = Assert.IsType<ProductManifest>(ProductManifestJson.Deserialize(json.ToJsonString()).Value);
        PortableItemArtifact second = fixture.Shared[0] with { Id = InternalAssetId.Create("SteelAlias").Value! };
        PortableMultiItemPlan plan = Assert.IsType<PortableMultiItemPlan>(PortableMultiItemPlan.Create(manifest, fixture.Items, [second, fixture.Shared[0]]).Plan);
        _ = Assert.Single(fixture.Layout(plan).FlatFbxEntries, entry => entry.RelativePath.EndsWith(".png", StringComparison.Ordinal));
        Assert.Contains("SharedSteel, SteelAlias", plan.InventoryText);
        PortableCompositionArtifact wrong = PortableCompositionArtifact.Create(second.Artifact.Record, PortableArtifactPurpose.Texture,
            TextureRole.Normal, extension: PortableFileExtension.Png).Value!;
        Assert.Equal("PORTABLE_SHARED_ARTIFACT_INVALID", PortableMultiItemPlan.Create(manifest, fixture.Items,
            [fixture.Shared[0], second with { Artifact = wrong }]).DiagnosticCode);
    }

    [Fact]
    public void DocumentRecordsMustBeValidatedDistinctAndMatchBothContents()
    {
        var fixture = new EquipmentArchiveFixture();
        PortableMultiItemPlan plan = fixture.Plan();
        ArtifactStoreRecord readme = PortableTestValues.RecordForBytes("Readme", Encoding.UTF8.GetBytes(plan.ReadmeText), "portable-readme");
        ArtifactStoreRecord inventory = PortableTestValues.RecordForBytes("Inventory", Encoding.UTF8.GetBytes(plan.InventoryText), "portable-readme");
        Assert.NotNull(plan.CreateLayout(readme, inventory));
        Assert.Null(plan.CreateLayout(readme, readme));
        ArtifactStoreRecord duplicateId = PortableTestValues.RecordForBytes("Readme", Encoding.UTF8.GetBytes(plan.InventoryText), "portable-readme");
        Assert.Null(plan.CreateLayout(readme, duplicateId));
        _ = Assert.Throws<ArgumentNullException>(() => plan.CreateLayout(null!, inventory));
        _ = Assert.Throws<ArgumentNullException>(() => plan.CreateLayout(readme, null!));
    }

    [Fact]
    public async Task StaleDocumentsCancellationMissingSourcesAndTamperedArchivesAreRejected()
    {
        var fixture = new EquipmentArchiveFixture();
        PortableMultiItemPlan plan = fixture.Plan();
        ArtifactStoreRecord wrong = PortableTestValues.RecordForBytes("WrongReadme", "old"u8.ToArray(), "portable-readme");
        Assert.Null(plan.CreateLayout(wrong, wrong));
        PortableFolderLayout layout = fixture.Layout(plan);
        using var archive = new MemoryStream();
        Assert.Equal(PortableFbxArchiveError.MissingSource,
            (await PortableFbxArchiveBuilder.CreateAsync(layout, [], archive, TestContext.Current.CancellationToken)).Error);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        PortableFbxArchiveSource[] sources = fixture.Sources(layout);
        try
        {
            Assert.Equal(PortableFbxArchiveError.Cancelled,
                (await PortableFbxArchiveBuilder.CreateAsync(layout, sources, archive, cancelled.Token)).Error);
            Assert.Equal(0, archive.Length);
        }
        finally { foreach (PortableFbxArchiveSource source in sources) { source.Stream.Dispose(); } }
        PortableFbxArchiveReceipt receipt = await fixture.BuildAsync(layout, archive);
        archive.Position = archive.Length - 1;
        archive.WriteByte(255);
        Assert.False((await PortableTargetValidator.ValidateArchiveAsync(layout, receipt, archive, TestContext.Current.CancellationToken)).Passed);
    }
}

/// <summary>Exports real equipment source bytes and the same reviewed application contracts used by Unity integration.</summary>
[Trait("Task", "PB-0810")]
public sealed class EquipmentSetEndToEndTests
{
    [Fact]
    public async Task EquipmentFixtureBuildsPortableArchiveAndUnityPlansFromOneManifest()
    {
        var fixture = new EquipmentArchiveFixture();
        SharedAssetReusePlan reuse = Assert.IsType<SharedAssetReusePlan>(SharedAssetDeduplicator.Plan(fixture.Manifest,
            [new(fixture.Manifest.SourceAssets.Single(source => source.LogicalReference.EndsWith(".png", StringComparison.Ordinal)),
                fixture.Shared[0].Artifact.Record.ContentIdentity)], TestContext.Current.CancellationToken).Plan);
        ItemPrefabPlan prefabs = Assert.IsType<ItemPrefabPlan>(ItemPrefabPlan.Create(reuse).Plan);
        AssembledSetPlan set = Assert.IsType<AssembledSetPlan>(AssembledSetPlan.Create(prefabs).Plan);
        Assert.Equal(["Helmet", "Armour"], set.Members.Select(member => member.ItemId));
        string attachments = MultiItemBuildPlan.Attachments(set, [.. set.Members.Select(member => new AttachmentBinding
        { itemId = member.ItemId, slot = member.Slot, targetId = "Character", kind = "socket", point = member.Slot })]);
        PortableMultiItemPlan plan = fixture.Plan();
        PortableFolderLayout layout = fixture.Layout(plan);
        using var archive = new MemoryStream();
        PortableFbxArchiveReceipt receipt = await fixture.BuildAsync(layout, archive);
        Assert.True((await PortableTargetValidator.ValidateArchiveAsync(layout, receipt, archive, TestContext.Current.CancellationToken)).Passed);
        string? output = Environment.GetEnvironmentVariable("PACKAGEBUILDER_EQUIPMENT_OUTPUT");
        if (output is null)
        { return; }
        string fullOutput = Path.GetFullPath(output);
        Assert.StartsWith(EquipmentArchiveFixture.Root + Path.DirectorySeparatorChar, fullOutput, StringComparison.OrdinalIgnoreCase);
        _ = Directory.CreateDirectory(fullOutput);
        await File.WriteAllBytesAsync(Path.Combine(fullOutput, receipt.ArchiveFileName), archive.ToArray(), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(fullOutput, "ownership.json"), prefabs.SerializeOwnership(), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(fullOutput, "set.json"), set.Serialize(), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(fullOutput, "attachments.json"), attachments, TestContext.Current.CancellationToken);
    }
}

/// <summary>Shares fixture bindings and document records across positive, failure and real-engine integration tests.</summary>
internal sealed class EquipmentArchiveFixture
{
    private readonly Dictionary<string, byte[]> _bytes = new(StringComparer.Ordinal);

    internal EquipmentArchiveFixture(bool collection = false)
    {
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureRoot, "manifest.json")))!;
        if (collection)
        {
            manifest["product"]!["case"] = "item-collection";
            manifest["itemCollection"] = manifest["itemSet"]!.DeepClone();
            _ = manifest["itemCollection"]!.AsObject().Remove("assembledSetRules");
            _ = manifest.AsObject().Remove("itemSet");
        }
        Manifest = Assert.IsType<ProductManifest>(ProductManifestJson.Deserialize(manifest.ToJsonString()).Value);
        Items = [Model("Helmet"), Model("Armour")];
        ArtifactStoreRecord texture = Add("Steel", File.ReadAllBytes(Path.Combine(FixtureRoot, "source", "T_SharedSteel_Albedo.png")), "portable-texture");
        Shared = [new(InternalAssetId.Create("SharedSteel").Value!, PortableCompositionArtifact.Create(texture,
            PortableArtifactPurpose.Texture, TextureRole.Albedo, extension: PortableFileExtension.Png).Value!)];
    }

    internal static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PackageBuilder.sln")))
            { directory = directory.Parent; }
            return directory!.FullName;
        }
    }

    internal static string FixtureRoot => Path.Combine(Root, "tests", "fixtures", "portable", "equipment-set");
    internal ProductManifest Manifest { get; }
    internal PortableItemArtifact[] Items { get; }
    internal PortableItemArtifact[] Shared { get; }
    internal PortableMultiItemPlan Plan() => Assert.IsType<PortableMultiItemPlan>(PortableMultiItemPlan.Create(Manifest, Items, Shared).Plan);
    internal PortableFolderLayout Layout(PortableMultiItemPlan plan) => Assert.IsType<PortableFolderLayout>(plan.CreateLayout(
        Add("Readme", Encoding.UTF8.GetBytes(plan.ReadmeText), "portable-readme"),
        Add("Inventory", Encoding.UTF8.GetBytes(plan.InventoryText), "portable-readme")));

    internal PortableFbxArchiveSource[] Sources(PortableFolderLayout layout) =>
        [.. layout.FlatFbxEntries.Select(entry => new PortableFbxArchiveSource(entry.Source, new MemoryStream(_bytes[entry.Source.Artifact.Id.Value])))];

    internal async Task<PortableFbxArchiveReceipt> BuildAsync(PortableFolderLayout layout, Stream archive)
    {
        PortableFbxArchiveSource[] sources = Sources(layout);
        try
        { return Assert.IsType<PortableFbxArchiveReceipt>((await PortableFbxArchiveBuilder.CreateAsync(layout, sources, archive, TestContext.Current.CancellationToken)).Receipt); }
        finally { foreach (PortableFbxArchiveSource source in sources) { source.Stream.Dispose(); } }
    }

    private PortableItemArtifact Model(string id) => new(InternalAssetId.Create(id).Value!, PortableCompositionArtifact.Create(
        Add(id, File.ReadAllBytes(Path.Combine(FixtureRoot, "source", id + ".fbx")), "normalized-fbx"), PortableArtifactPurpose.Fbx).Value!);

    private ArtifactStoreRecord Add(string id, byte[] bytes, string role)
    {
        _bytes[id] = bytes;
        return PortableTestValues.RecordForBytes(id, bytes, role);
    }
}
