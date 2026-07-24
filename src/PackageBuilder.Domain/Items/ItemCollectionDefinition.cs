using System.Collections.ObjectModel;
using PackageBuilder.Domain.Products;

namespace PackageBuilder.Domain.Items;

/// <summary>
/// Represents independently usable items. It never implies or creates an assembled runtime object.
/// </summary>
public sealed class ItemCollectionDefinition : IEquatable<ItemCollectionDefinition>
{
    private ItemCollectionDefinition(
        ProductCase productCase,
        ReadOnlyCollection<ItemDefinition> items,
        ReadOnlyCollection<ItemRelationship> relationships,
        ReadOnlyCollection<SharedAssetDefinition> sharedAssets)
    {
        ProductCase = productCase;
        Items = items;
        Relationships = relationships;
        SharedAssets = sharedAssets;
    }

    public ProductCase ProductCase { get; }

    /// <summary>Gets independently usable items in exact user-controlled order.</summary>
    public IReadOnlyList<ItemDefinition> Items { get; }

    public IReadOnlyList<ItemRelationship> Relationships { get; }

    public IReadOnlyList<SharedAssetDefinition> SharedAssets { get; }

    /// <summary>
    /// Creates a collection. The assembly argument exists only to reject contradictory input
    /// explicitly; a collection never silently acquires assembled-set behavior.
    /// Empty and single-item collections are valid because PB-0106 defines no minimum size.
    /// </summary>
    public static ItemValidationResult<ItemCollectionDefinition> Create(
        IEnumerable<ItemDefinition?>? items,
        IEnumerable<ItemRelationship?>? relationships,
        IEnumerable<SharedAssetDefinition?>? sharedAssets,
        AssembledSetRules? assembledSetRules = null)
    {
        if (assembledSetRules is not null)
        {
            return ItemValidationResult<ItemCollectionDefinition>.Failure(
                ItemValidationError.AssembledSetRulesNotAllowed);
        }

        ItemValidationResult<ValidatedItemGroup> groupResult =
            ItemGroupValidator.Validate(items, relationships, sharedAssets);
        return !groupResult.IsValid
            ? ItemValidationResult<ItemCollectionDefinition>.Failure(groupResult.Error)
            : ItemValidationResult<ItemCollectionDefinition>.Success(
                new ItemCollectionDefinition(
                    ProductCase.ItemCollection,
                    groupResult.Value!.Items,
                    groupResult.Value.Relationships,
                    groupResult.Value.SharedAssets));
    }

    public bool Equals(ItemCollectionDefinition? other) =>
        other is not null &&
        Items.SequenceEqual(other.Items) &&
        Relationships.SequenceEqual(other.Relationships) &&
        SharedAssets.SequenceEqual(other.SharedAssets);

    public override bool Equals(object? obj) =>
        obj is ItemCollectionDefinition other && Equals(other);

    public override int GetHashCode()
    {
        var hash = StableItemHash.Create();
        foreach (ItemDefinition item in Items)
        {
            hash = hash.Add(item.GetHashCode());
        }

        foreach (ItemRelationship relationship in Relationships)
        {
            hash = hash.Add(relationship.GetHashCode());
        }

        foreach (SharedAssetDefinition asset in SharedAssets)
        {
            hash = hash.Add(asset.GetHashCode());
        }

        return hash.ToHashCode();
    }
}
