using System.Collections.ObjectModel;
using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Items;

/// <summary>Represents one immutable independently addressable item.</summary>
public sealed class ItemDefinition : IEquatable<ItemDefinition>
{
    private ItemDefinition(
        InternalAssetId id,
        ReadOnlyCollection<ItemCategory> categories,
        AttachmentSlot? attachmentSlot,
        ReadOnlyCollection<InternalAssetId> sharedAssetReferences)
    {
        Id = id;
        Categories = categories;
        AttachmentSlot = attachmentSlot;
        SharedAssetReferences = sharedAssetReferences;
    }

    public InternalAssetId Id { get; }

    /// <summary>Gets extensible categories in deterministic ordinal identifier order.</summary>
    public IReadOnlyList<ItemCategory> Categories { get; }

    /// <summary>Gets the optional logical attachment/body slot.</summary>
    public AttachmentSlot? AttachmentSlot { get; }

    /// <summary>Gets declared shared-asset IDs in deterministic ordinal order.</summary>
    public IReadOnlyList<InternalAssetId> SharedAssetReferences { get; }

    public static ItemValidationResult<ItemDefinition> Create(
        InternalAssetId? id,
        IEnumerable<ItemCategory?>? categories,
        AttachmentSlot? attachmentSlot,
        IEnumerable<InternalAssetId?>? sharedAssetReferences)
    {
        if (id is null)
        {
            return Failure(ItemValidationError.NullItemId);
        }

        if (categories is null)
        {
            return Failure(ItemValidationError.NullCategories);
        }

        var categorySet = new HashSet<ItemCategory>();
        foreach (ItemCategory? category in categories)
        {
            if (category is null)
            {
                return Failure(ItemValidationError.NullCategory);
            }

            if (!categorySet.Add(category))
            {
                return Failure(ItemValidationError.DuplicateCategory);
            }
        }

        if (sharedAssetReferences is null)
        {
            return Failure(ItemValidationError.NullSharedAssetReferences);
        }

        var references = new Dictionary<string, InternalAssetId>(StringComparer.Ordinal);
        foreach (InternalAssetId? reference in sharedAssetReferences)
        {
            if (reference is null)
            {
                return Failure(ItemValidationError.NullSharedAssetReference);
            }

            if (!references.TryAdd(reference.Value, reference))
            {
                return Failure(ItemValidationError.DuplicateSharedAssetReference);
            }
        }

        ItemCategory[] orderedCategories =
        [
            .. categorySet.OrderBy(
                category => category.CanonicalIdentifier,
                StringComparer.Ordinal),
        ];
        InternalAssetId[] orderedReferences =
        [
            .. references.Values.OrderBy(reference => reference.Value, StringComparer.Ordinal),
        ];
        return ItemValidationResult<ItemDefinition>.Success(
            new ItemDefinition(
                id,
                Array.AsReadOnly(orderedCategories),
                attachmentSlot,
                Array.AsReadOnly(orderedReferences)));
    }

    public bool Equals(ItemDefinition? other) =>
        other is not null &&
        Id.Equals(other.Id) &&
        Categories.SequenceEqual(other.Categories) &&
        Equals(AttachmentSlot, other.AttachmentSlot) &&
        SharedAssetReferences.SequenceEqual(other.SharedAssetReferences);

    public override bool Equals(object? obj) => obj is ItemDefinition other && Equals(other);

    public override int GetHashCode()
    {
        StableItemHash hash = StableItemHash.Create()
            .Add(Id.Value)
            .Add(AttachmentSlot?.CanonicalIdentifier ?? string.Empty);
        foreach (ItemCategory category in Categories)
        {
            hash = hash.Add(category.CanonicalIdentifier);
        }

        foreach (InternalAssetId reference in SharedAssetReferences)
        {
            hash = hash.Add(reference.Value);
        }

        return hash.ToHashCode();
    }

    private static ItemValidationResult<ItemDefinition> Failure(ItemValidationError error) =>
        ItemValidationResult<ItemDefinition>.Failure(error);
}
