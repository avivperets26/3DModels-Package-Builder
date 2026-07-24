using System.Collections.ObjectModel;

namespace PackageBuilder.Domain.Items;

internal static class ItemGroupValidator
{
    public static ItemValidationResult<ValidatedItemGroup> Validate(
        IEnumerable<ItemDefinition?>? items,
        IEnumerable<ItemRelationship?>? relationships,
        IEnumerable<SharedAssetDefinition?>? sharedAssets)
    {
        if (items is null)
        {
            return Failure(ItemValidationError.NullItems);
        }

        var itemList = new List<ItemDefinition>();
        var itemsById = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        foreach (ItemDefinition? item in items)
        {
            if (item is null)
            {
                return Failure(ItemValidationError.NullItem);
            }

            if (!itemsById.TryAdd(item.Id.Value, item))
            {
                return Failure(ItemValidationError.DuplicateItemId);
            }

            // Item order is user-controlled domain data, so it is retained exactly.
            itemList.Add(item);
        }

        if (relationships is null)
        {
            return Failure(ItemValidationError.NullRelationships);
        }

        var relationshipSet = new HashSet<ItemRelationship>();
        foreach (ItemRelationship? relationship in relationships)
        {
            if (relationship is null)
            {
                return Failure(ItemValidationError.NullRelationship);
            }

            // Relationships are valid only inside the declaring group. Because endpoints are
            // canonicalized by ItemRelationship, this also catches exact and reversed duplicates.
            if (!itemsById.ContainsKey(relationship.FirstItemId.Value) ||
                !itemsById.ContainsKey(relationship.SecondItemId.Value))
            {
                return Failure(ItemValidationError.UnknownRelationshipEndpoint);
            }

            if (!relationshipSet.Add(relationship))
            {
                return Failure(ItemValidationError.DuplicateRelationship);
            }
        }

        if (sharedAssets is null)
        {
            return Failure(ItemValidationError.NullSharedAssets);
        }

        var assetsById = new Dictionary<string, SharedAssetDefinition>(StringComparer.Ordinal);
        foreach (SharedAssetDefinition? asset in sharedAssets)
        {
            if (asset is null)
            {
                return Failure(ItemValidationError.NullSharedAsset);
            }

            if (!assetsById.TryAdd(asset.Id.Value, asset))
            {
                return Failure(ItemValidationError.DuplicateSharedAssetId);
            }
        }

        var referencedAssets = new HashSet<string>(StringComparer.Ordinal);
        foreach (ItemDefinition item in itemList)
        {
            foreach (PackageBuilder.Domain.Naming.InternalAssetId reference
                in item.SharedAssetReferences)
            {
                if (!assetsById.ContainsKey(reference.Value))
                {
                    return Failure(ItemValidationError.UnknownSharedAssetReference);
                }

                _ = referencedAssets.Add(reference.Value);
            }
        }

        // A declaration with no item reference is contradictory: it is not shared group data.
        if (assetsById.Keys.Any(id => !referencedAssets.Contains(id)))
        {
            return Failure(ItemValidationError.UnreferencedSharedAsset);
        }

        ItemRelationship[] orderedRelationships =
        [
            .. relationshipSet
                .OrderBy(value => value.FirstItemId.Value, StringComparer.Ordinal)
                .ThenBy(value => value.SecondItemId.Value, StringComparer.Ordinal),
        ];
        SharedAssetDefinition[] orderedAssets =
        [
            .. assetsById.Values.OrderBy(value => value.Id.Value, StringComparer.Ordinal),
        ];
        return ItemValidationResult<ValidatedItemGroup>.Success(
            new ValidatedItemGroup(
                itemList.AsReadOnly(),
                Array.AsReadOnly(orderedRelationships),
                Array.AsReadOnly(orderedAssets),
                itemsById));
    }

    public static ItemValidationError ValidateAssembledSet(
        ValidatedItemGroup group,
        AssembledSetRules? rules)
    {
        if (rules is null)
        {
            return ItemValidationError.None;
        }

        foreach (AssembledSetMember member in rules.Members)
        {
            if (!group.ItemsById.TryGetValue(member.ItemId.Value, out ItemDefinition? item))
            {
                return ItemValidationError.UnknownAssembledMember;
            }

            // Assembly metadata must agree with the item's declared logical slot; the assembly
            // cannot silently override, add, or remove an item's slot.
            if (!Equals(item.AttachmentSlot, member.AttachmentSlot))
            {
                return ItemValidationError.ContradictoryAssembledMemberSlot;
            }
        }

        if (rules.Members.Count != group.Items.Count)
        {
            return ItemValidationError.MissingAssembledMember;
        }

        if (rules.RequireUniqueAttachmentSlots)
        {
            var assignedSlots = new HashSet<AttachmentSlot>();
            foreach (AssembledSetMember member in rules.Members)
            {
                if (member.AttachmentSlot is not null &&
                    !assignedSlots.Add(member.AttachmentSlot))
                {
                    return ItemValidationError.ConflictingAttachmentSlot;
                }
            }
        }

        return ItemValidationError.None;
    }

    private static ItemValidationResult<ValidatedItemGroup> Failure(ItemValidationError error) =>
        ItemValidationResult<ValidatedItemGroup>.Failure(error);
}

internal sealed class ValidatedItemGroup(
    ReadOnlyCollection<ItemDefinition> items,
    ReadOnlyCollection<ItemRelationship> relationships,
    ReadOnlyCollection<SharedAssetDefinition> sharedAssets,
    IReadOnlyDictionary<string, ItemDefinition> itemsById)
{
    public ReadOnlyCollection<ItemDefinition> Items { get; } = items;

    public ReadOnlyCollection<ItemRelationship> Relationships { get; } = relationships;

    public ReadOnlyCollection<SharedAssetDefinition> SharedAssets { get; } = sharedAssets;

    public IReadOnlyDictionary<string, ItemDefinition> ItemsById { get; } = itemsById;
}
