using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Items;

/// <summary>
/// Represents one undirected relationship between two distinct items. Endpoint order is
/// canonicalized ordinally so exact and reversed duplicates have identical value semantics.
/// </summary>
public sealed class ItemRelationship : IEquatable<ItemRelationship>
{
    private ItemRelationship(InternalAssetId firstItemId, InternalAssetId secondItemId)
    {
        FirstItemId = firstItemId;
        SecondItemId = secondItemId;
    }

    public InternalAssetId FirstItemId { get; }

    public InternalAssetId SecondItemId { get; }

    public static ItemValidationResult<ItemRelationship> Create(
        InternalAssetId? firstItemId,
        InternalAssetId? secondItemId) =>
        firstItemId is null || secondItemId is null
            ? ItemValidationResult<ItemRelationship>.Failure(
                ItemValidationError.NullRelationshipEndpoint)
            : firstItemId.Equals(secondItemId)
            ? ItemValidationResult<ItemRelationship>.Failure(
                ItemValidationError.SelfRelationship)
            : string.Compare(firstItemId.Value, secondItemId.Value, StringComparison.Ordinal) < 0
            ? ItemValidationResult<ItemRelationship>.Success(
                new ItemRelationship(firstItemId, secondItemId))
            : ItemValidationResult<ItemRelationship>.Success(
                new ItemRelationship(secondItemId, firstItemId));

    public bool Equals(ItemRelationship? other) =>
        other is not null &&
        FirstItemId.Equals(other.FirstItemId) &&
        SecondItemId.Equals(other.SecondItemId);

    public override bool Equals(object? obj) => obj is ItemRelationship other && Equals(other);

    public override int GetHashCode() =>
        StableItemHash.Create()
            .Add(FirstItemId.Value)
            .Add(SecondItemId.Value)
            .ToHashCode();
}
