using System.Text.Json;
using PackageBuilder.Domain.Items;
using PackageBuilder.MultiItem;

namespace PackageBuilder.Application.Items;

/// <summary>Maps reviewed ownership to ordered collection export and set attachment adapter contracts.</summary>
public static class MultiItemBuildPlan
{
    private static readonly JsonSerializerOptions _wireOptions = new() { IncludeFields = true };
    /// <summary>Serializes original collection order; no assembled runtime object is inferred.</summary>
    public static string Collection(ItemPrefabPlan prefabs)
    {
        ArgumentNullException.ThrowIfNull(prefabs);
        ItemCollectionDefinition collection = prefabs.Reuse.Manifest.ItemCollection ?? throw new ArgumentException("A reviewed collection is required.", nameof(prefabs));
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            productId = prefabs.Reuse.Manifest.AssetId.Value,
            items = collection.Items.Select(item => new { itemId = item.Id.Value, prefabFileName = prefabs.Items.Single(value => value.ItemId.Equals(item.Id)).PrefabFileName })
        });
    }

    /// <summary>Creates explicit target requirements from the immutable set, retaining logical slots and item order.</summary>
    public static AttachmentRequirement[] AttachmentRequirements(AssembledSetPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return [.. plan.Members.Select(member => new AttachmentRequirement { itemId = member.ItemId, slot = member.Slot })];
    }

    /// <summary>Serializes declared bindings; target adapters must inspect current targets and validate before generation.</summary>
    public static string Attachments(AssembledSetPlan plan, AttachmentBinding[] bindings)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(bindings);
        return JsonSerializer.Serialize(new { schemaVersion = 1, setId = plan.SetId, members = AttachmentRequirements(plan), bindings }, _wireOptions);
    }
}
