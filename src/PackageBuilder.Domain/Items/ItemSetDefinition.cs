using System.Collections.ObjectModel;
using PackageBuilder.Domain.Products;

namespace PackageBuilder.Domain.Items;

/// <summary>
/// Represents related items that may declare assembly rules and compatibility metadata.
/// </summary>
public sealed class ItemSetDefinition : IEquatable<ItemSetDefinition>
{
    private ItemSetDefinition(
        ProductCase productCase,
        ReadOnlyCollection<ItemDefinition> items,
        ReadOnlyCollection<ItemRelationship> relationships,
        ReadOnlyCollection<SharedAssetDefinition> sharedAssets,
        AssembledSetRules? assembledSetRules)
    {
        ProductCase = productCase;
        Items = items;
        Relationships = relationships;
        SharedAssets = sharedAssets;
        AssembledSetRules = assembledSetRules;
    }

    public ProductCase ProductCase { get; }

    /// <summary>Gets items in the exact user-controlled order supplied at creation.</summary>
    public IReadOnlyList<ItemDefinition> Items { get; }

    public IReadOnlyList<ItemRelationship> Relationships { get; }

    public IReadOnlyList<SharedAssetDefinition> SharedAssets { get; }

    public AssembledSetRules? AssembledSetRules { get; }

    /// <summary>
    /// Creates a set. Empty and single-item sets are retained because no approved PB-0106
    /// requirement imposes an arbitrary minimum group size.
    /// </summary>
    public static ItemValidationResult<ItemSetDefinition> Create(
        IEnumerable<ItemDefinition?>? items,
        IEnumerable<ItemRelationship?>? relationships,
        IEnumerable<SharedAssetDefinition?>? sharedAssets,
        AssembledSetRules? assembledSetRules)
    {
        ItemValidationResult<ValidatedItemGroup> groupResult =
            ItemGroupValidator.Validate(items, relationships, sharedAssets);
        if (!groupResult.IsValid)
        {
            return ItemValidationResult<ItemSetDefinition>.Failure(groupResult.Error);
        }

        ValidatedItemGroup group = groupResult.Value!;
        ItemValidationError assemblyError =
            ItemGroupValidator.ValidateAssembledSet(group, assembledSetRules);
        return assemblyError != ItemValidationError.None
            ? ItemValidationResult<ItemSetDefinition>.Failure(assemblyError)
            : ItemValidationResult<ItemSetDefinition>.Success(
                new ItemSetDefinition(
                    ProductCase.ItemSet,
                    group.Items,
                    group.Relationships,
                    group.SharedAssets,
                    assembledSetRules));
    }

    public bool Equals(ItemSetDefinition? other) =>
        other is not null &&
        Items.SequenceEqual(other.Items) &&
        Relationships.SequenceEqual(other.Relationships) &&
        SharedAssets.SequenceEqual(other.SharedAssets) &&
        Equals(AssembledSetRules, other.AssembledSetRules);

    public override bool Equals(object? obj) => obj is ItemSetDefinition other && Equals(other);

    public override int GetHashCode() =>
        AddGroupToHash(StableItemHash.Create())
            .Add(AssembledSetRules?.GetHashCode() ?? 0)
            .ToHashCode();

    private StableItemHash AddGroupToHash(StableItemHash hash)
    {
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

        return hash;
    }
}
