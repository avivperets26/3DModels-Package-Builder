using System.Text.Json;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Application.Items;

/// <summary>One stable item prefab and its exact logical model ownership before engine normalization.</summary>
public sealed class ItemPrefabEntry
{
    internal ItemPrefabEntry(InternalAssetId itemId, IEnumerable<string> sources)
    {
        ItemId = itemId;
        ModelSources = Array.AsReadOnly(sources.ToArray());
    }

    /// <summary>Gets the original reviewed item identity.</summary>
    public InternalAssetId ItemId { get; }

    /// <summary>Gets the canonical customer prefab filename.</summary>
    public string PrefabFileName => $"P_{ItemId.Value}.prefab";

    /// <summary>Gets ordinal logical model references; images are resolved through the shared material plan.</summary>
    public IReadOnlyList<string> ModelSources { get; }
}

/// <summary>Immutable item generation intent bound to a successfully reviewed ownership and asset-reuse plan.</summary>
public sealed class ItemPrefabPlan
{
    private ItemPrefabPlan(SharedAssetReusePlan reuse, IEnumerable<ItemPrefabEntry> items)
    {
        Reuse = reuse;
        Items = Array.AsReadOnly(items.ToArray());
    }

    /// <summary>Gets canonical material/texture aliases and snapshot identities for normalization/import adapters.</summary>
    public SharedAssetReusePlan Reuse { get; }

    /// <summary>Gets separate item outputs in ordinal ID order; no assembled runtime prefab is inferred.</summary>
    public IReadOnlyList<ItemPrefabEntry> Items { get; }

    /// <summary>Rejects filenames that collide on Unity's supported Windows filesystem without renaming stable IDs.</summary>
    public static ItemPrefabPlanResult Create(SharedAssetReusePlan reuse)
    {
        ArgumentNullException.ThrowIfNull(reuse);
        ProductManifest manifest = reuse.Manifest;
        var imageReferences = manifest.SourceAssets.Where(source => source.Kind.Equals(SourceAssetKind.Image))
            .Select(source => source.LogicalReference).ToHashSet(StringComparer.Ordinal);
        var items = new List<ItemPrefabEntry>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (IGrouping<InternalAssetId, ItemSourceAssignment>? group in manifest.ItemSourceAssignments.Where(assignment => !imageReferences.Contains(assignment.SourceReference))
            .GroupBy(assignment => assignment.ItemId).OrderBy(group => group.Key.Value, StringComparer.Ordinal))
        {
            var item = new ItemPrefabEntry(group.Key, group.Select(assignment => assignment.SourceReference).Order(StringComparer.Ordinal));
            if (!names.Add(item.PrefabFileName))
            {
                return new(null, "ITEM_PREFAB_NAME_COLLISION");
            }

            items.Add(item);
        }

        return new(new ItemPrefabPlan(reuse, items), string.Empty);
    }

    /// <summary>
    /// Serializes the version-1 ownership envelope consumed by the Unity batch adapter.
    /// Normalized FBX, extracted meshes and canonical compiled materials are supplied separately by import adapters.
    /// </summary>
    public string SerializeOwnership() => JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        items = Items.Select(item => new
        {
            itemId = item.ItemId.Value,
            prefabFileName = item.PrefabFileName,
            modelSources = item.ModelSources,
        }),
    });
}

/// <summary>Returns a complete prefab plan or an actionable stable collision diagnostic, never a partial plan.</summary>
public sealed record ItemPrefabPlanResult(ItemPrefabPlan? Plan, string DiagnosticCode);
