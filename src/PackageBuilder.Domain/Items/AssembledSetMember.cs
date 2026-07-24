using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Items;

/// <summary>Declares one item and its expected slot in an assembled set.</summary>
public sealed class AssembledSetMember : IEquatable<AssembledSetMember>
{
    private AssembledSetMember(InternalAssetId itemId, AttachmentSlot? attachmentSlot)
    {
        ItemId = itemId;
        AttachmentSlot = attachmentSlot;
    }

    public InternalAssetId ItemId { get; }

    public AttachmentSlot? AttachmentSlot { get; }

    public static ItemValidationResult<AssembledSetMember> Create(
        InternalAssetId? itemId,
        AttachmentSlot? attachmentSlot) =>
        itemId is null
            ? ItemValidationResult<AssembledSetMember>.Failure(
                ItemValidationError.NullAssembledMemberItemId)
            : ItemValidationResult<AssembledSetMember>.Success(
                new AssembledSetMember(itemId, attachmentSlot));

    public bool Equals(AssembledSetMember? other) =>
        other is not null &&
        ItemId.Equals(other.ItemId) &&
        Equals(AttachmentSlot, other.AttachmentSlot);

    public override bool Equals(object? obj) => obj is AssembledSetMember other && Equals(other);

    public override int GetHashCode() =>
        StableItemHash.Create()
            .Add(ItemId.Value)
            .Add(AttachmentSlot?.CanonicalIdentifier ?? string.Empty)
            .ToHashCode();
}
