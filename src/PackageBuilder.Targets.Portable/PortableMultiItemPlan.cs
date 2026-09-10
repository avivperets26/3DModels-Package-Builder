using System.Security.Cryptography;
using System.Text;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Targets.Portable;

/// <summary>Binds a reviewed item or shared-asset ID to a validated normalized portable artifact.</summary>
public sealed record PortableItemArtifact(InternalAssetId Id, PortableCompositionArtifact Artifact);

/// <summary>Plans an exact set/collection archive using canonical naming and the existing streaming ZIP writer.</summary>
public sealed class PortableMultiItemPlan
{
    private readonly PortableFolderEntry[] _entries;

    private PortableMultiItemPlan(PortableNamingProfile naming, PortableFolderEntry[] entries, string readme, string inventory)
    {
        Naming = naming;
        _entries = entries;
        ReadmeText = readme;
        InventoryText = inventory;
    }

    /// <summary>Gets canonical archive/root/document names from the reviewed product identity.</summary>
    public PortableNamingProfile Naming { get; }
    /// <summary>Gets deterministic UTF-8 README content, to be stored before layout creation.</summary>
    public string ReadmeText { get; }
    /// <summary>Gets declaration-ordered items and canonical shared aliases with their content identities.</summary>
    public string InventoryText { get; }
    public const string InventoryFileName = "INVENTORY.md";

    /// <summary>Requires every declared item/shared asset exactly once; only validated FBX and separate textures qualify.</summary>
    public static PortableMultiItemPlanResult Create(
        ProductManifest manifest,
        IEnumerable<PortableItemArtifact> itemArtifacts,
        IEnumerable<PortableItemArtifact> sharedArtifacts)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(itemArtifacts);
        ArgumentNullException.ThrowIfNull(sharedArtifacts);
        IReadOnlyList<ItemDefinition>? items = manifest.ItemSet?.Items ?? manifest.ItemCollection?.Items;
        IReadOnlyList<SharedAssetDefinition>? shared = manifest.ItemSet?.SharedAssets ?? manifest.ItemCollection?.SharedAssets;
        if (items is null || shared is null || items.Count == 0 || manifest.ItemSourceAssignments.Count == 0)
        {
            return Failure("PORTABLE_ITEMS_REVIEW_REQUIRED");
        }

        PortableItemArtifact[] models = [.. itemArtifacts];
        PortableItemArtifact[] assets = [.. sharedArtifacts];
        if (!ExactIds(items.Select(item => item.Id), models) || !ExactIds(shared.Select(asset => asset.Id), assets))
        {
            return Failure("PORTABLE_ITEM_INVENTORY_INVALID");
        }

        PortableNamingProfile naming = PortableNamingProfile.Create(manifest.AssetId, manifest.FolderName).Value!;
        var entries = new List<PortableFolderEntry>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { naming.FlatReadmeFileName, InventoryFileName };
        var records = new HashSet<string>(StringComparer.Ordinal);
        var inventory = new StringBuilder("# Package inventory\n\n| Kind | ID | File | Shared assets | Bytes | SHA-256 |\n| --- | --- | --- | --- | ---: | --- |\n");
        foreach (ItemDefinition item in items)
        {
            PortableItemArtifact model = models.Single(value => value.Id.Equals(item.Id));
            if (!model.Artifact.Purpose.Equals(PortableArtifactPurpose.Fbx))
            { return Failure("PORTABLE_ITEM_FBX_REQUIRED"); }
            string file = PortableNamingProfile.Create(item.Id, manifest.FolderName).Value!.FbxFileName;
            if (!Add(model.Artifact.Record, file))
            { return Failure("PORTABLE_ITEM_NAME_COLLISION"); }
            Row("Item", item.Id.Value, file, string.Join(", ", item.SharedAssetReferences.Select(id => id.Value)), model.Artifact.Record);
        }

        // A normalized shared artifact may have multiple declared aliases; retain one file and list every alias.
        foreach (IGrouping<string, PortableItemArtifact> group in assets.OrderBy(value => value.Id.Value, StringComparer.Ordinal)
            .GroupBy(value => value.Artifact.Record.Artifact.Id.Value, StringComparer.Ordinal))
        {
            PortableItemArtifact canonical = group.First();
            PortableCompositionArtifact artifact = canonical.Artifact;
            foreach (PortableItemArtifact alias in group)
            {
                SharedAssetDefinition declaration = shared.Single(value => value.Id.Equals(alias.Id));
                bool image = declaration.Source.Kind.Equals(SourceAssetKind.Image);
                if (!(image ? alias.Artifact.Purpose.Equals(PortableArtifactPurpose.Texture) : alias.Artifact.Purpose.Equals(PortableArtifactPurpose.Fbx)) ||
                    !alias.Artifact.Record.ContentIdentity.Equals(artifact.Record.ContentIdentity) ||
                    !Equals(alias.Artifact.TextureRole, artifact.TextureRole) || !Equals(alias.Artifact.Extension, artifact.Extension))
                { return Failure("PORTABLE_SHARED_ARTIFACT_INVALID"); }
            }

            PortableNamingProfile sharedNaming = PortableNamingProfile.Create(canonical.Id, manifest.FolderName).Value!;
            string file = artifact.Purpose.Equals(PortableArtifactPurpose.Fbx) ? sharedNaming.FbxFileName :
                sharedNaming.GetTextureFileName(artifact.TextureRole, artifact.Extension).Value!;
            if (!Add(artifact.Record, file))
            { return Failure("PORTABLE_ITEM_NAME_COLLISION"); }
            Row("Shared", string.Join(", ", group.Select(value => value.Id.Value)), file, string.Empty, artifact.Record);
        }

        string readme = $"{manifest.DisplayName.Value}\n\nPortable {manifest.ProductCase.CanonicalIdentifier}\n" +
            $"Items: {items.Count}\nInventory: {InventoryFileName}\n\n" +
            "Each item FBX is independent. Keep shared files beside the FBX files when importing.\n" +
            "Materials are carried by the FBX files; textures remain separate.\n" +
            "Assembly slots and compatibility describe intent, not automatic attachment or guaranteed character compatibility.\n\n" +
            string.Join("\n", items.Select(item => $"{item.Id.Value}: slot {item.AttachmentSlot?.CanonicalIdentifier ?? "none"}")) + "\n";
        return new(new PortableMultiItemPlan(naming, [.. entries.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)], readme, inventory.ToString()), string.Empty);

        bool Add(ArtifactStoreRecord record, string file)
        {
            if (!names.Add(file) || !records.Add(record.Artifact.Id.Value))
            { return false; }
            entries.Add(new(record, file));
            return true;
        }

        void Row(string kind, string id, string file, string references, ArtifactStoreRecord record) =>
            inventory.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"| {kind} | {id} | {file} | {references} | {record.ContentIdentity.Bytes} | {record.ContentIdentity.Sha256.Value} |\n");
    }

    /// <summary>Binds exact generated UTF-8 documents to stored records, preventing stale inventories from being archived.</summary>
    public PortableFolderLayout? CreateLayout(ArtifactStoreRecord readme, ArtifactStoreRecord inventory)
    {
        ArgumentNullException.ThrowIfNull(readme);
        ArgumentNullException.ThrowIfNull(inventory);
        if (!DocumentMatches(readme, ReadmeText) || !DocumentMatches(inventory, InventoryText))
        { return null; }
        PortableFolderEntry[] entries = [.. _entries, new(readme, Naming.FlatReadmeFileName), new(inventory, InventoryFileName)];
        return entries.Select(entry => entry.Source.Artifact.Id.Value).Distinct(StringComparer.Ordinal).Count() != entries.Length ? null :
            new PortableFolderLayout(Naming.FlatFbxFolderName, Naming.ProductFolderName,
                entries.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal), []);
    }

    private static bool DocumentMatches(ArtifactStoreRecord record, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        return PortableCompositionArtifact.Create(record, PortableArtifactPurpose.FlatReadme).IsValid &&
            record.ContentIdentity.Bytes == bytes.LongLength &&
            record.ContentIdentity.Sha256.Value.Equals(Convert.ToHexString(SHA256.HashData(bytes)), StringComparison.OrdinalIgnoreCase);
    }

    private static bool ExactIds(IEnumerable<InternalAssetId> expected, PortableItemArtifact[] actual) =>
        actual.All(value => value is not null && value.Id is not null && value.Artifact is not null) &&
        expected.Select(id => id.Value).Order(StringComparer.Ordinal).SequenceEqual(actual.Select(value => value.Id.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static PortableMultiItemPlanResult Failure(string code) => new(null, code);
}

/// <summary>Contains a complete plan or a stable diagnostic; invalid inputs never produce partial inventories.</summary>
public sealed record PortableMultiItemPlanResult(PortableMultiItemPlan? Plan, string DiagnosticCode);
