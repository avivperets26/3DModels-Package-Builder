using System.Text.Json;
using PackageBuilder.Domain.Items;

namespace PackageBuilder.Application.Items;

/// <summary>One ordered assembly member; the slot is logical metadata, never an inferred bone/socket.</summary>
public sealed record AssembledSetEntry(string ItemId, string PrefabFileName, string Slot, string ContainerName);

/// <summary>Application-owned assembly intent and compatibility document for already generated item prefabs.</summary>
public sealed class AssembledSetPlan
{
    private AssembledSetPlan(ItemPrefabPlan items, ItemSetDefinition set)
    {
        ItemPrefabs = items;
        SetId = items.Reuse.Manifest.AssetId.Value;
        Members = Array.AsReadOnly(set.Items.Select(item => new AssembledSetEntry(
            item.Id.Value,
            items.Items.Single(prefab => prefab.ItemId.Equals(item.Id)).PrefabFileName,
            item.AttachmentSlot?.CanonicalIdentifier ?? string.Empty,
            item.AttachmentSlot is null ? $"Item_{item.Id.Value}" : $"Slot_{item.AttachmentSlot.CanonicalIdentifier}_{item.Id.Value}")).ToArray());
        Rules = set.AssembledSetRules!;
    }

    /// <summary>Gets the original immutable prefab/reuse plan without reordering or changing it.</summary>
    public ItemPrefabPlan ItemPrefabs { get; }
    public string SetId { get; }
    public string PrefabFileName => $"P_{SetId}_Assembled.prefab";
    public string DocumentationFileName => $"SET_{SetId}.json";
    public IReadOnlyList<AssembledSetEntry> Members { get; }
    public AssembledSetRules Rules { get; }

    /// <summary>Requires an explicit, complete assembly declaration; collections and missing rules never infer assembly.</summary>
    public static AssembledSetPlanResult Create(ItemPrefabPlan items)
    {
        ArgumentNullException.ThrowIfNull(items);
        ItemSetDefinition? set = items.Reuse.Manifest.ItemSet;
        return set is null || set.AssembledSetRules is null || set.Items.Count == 0
            ? new(null, "SET_ASSEMBLY_DECLARATION_REQUIRED")
            : items.Items.Count != set.Items.Count || set.Items.Any(item => !items.Items.Any(prefab => prefab.ItemId.Equals(item.Id)))
            ? new(null, "SET_ASSEMBLY_ITEM_PREFAB_MISSING")
            : new(new AssembledSetPlan(items, set), string.Empty);
    }

    /// <summary>Serializes the shared v1 assembly contract, also emitted as customer compatibility documentation.</summary>
    public string Serialize() => JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        setId = SetId,
        prefabFileName = PrefabFileName,
        documentationFileName = DocumentationFileName,
        placement = "logical-slots-at-origin",
        attachmentValidation = "not-performed",
        requireUniqueAttachmentSlots = Rules.RequireUniqueAttachmentSlots,
        members = Members.Select(member => new { itemId = member.ItemId, prefabFileName = member.PrefabFileName, slot = member.Slot, containerName = member.ContainerName }),
        compatibility = Rules.CompatibilityMetadata.Select(entry => new { key = entry.Key.Value, value = entry.Value }),
    });
}

/// <summary>Returns a complete reviewed assembly intent or a stable blocking diagnostic.</summary>
public sealed record AssembledSetPlanResult(AssembledSetPlan? Plan, string DiagnosticCode);
