using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Tests.Items;

internal static class ItemTestAssertions
{
    public static T AssertSuccess<T>(ItemValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid);
        Assert.Equal(ItemValidationError.None, result.Error);
        return Assert.IsType<T>(result.Value);
    }

    public static void AssertFailure<T>(
        ItemValidationResult<T> result,
        ItemValidationError error)
        where T : class
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static InternalAssetId Id(string value) =>
        Assert.IsType<InternalAssetId>(InternalAssetId.Create(value).Value);

    public static ItemCategory Category(string value = "equipment") =>
        AssertSuccess(ItemCategory.Create(value));

    public static AttachmentSlot Slot(string value = "body-slot") =>
        AssertSuccess(AttachmentSlot.Create(value));

    public static SourceAsset Source(string reference = "shared/albedo.png") =>
        Assert.IsType<SourceAsset>(
            SourceAsset.Create(SourceAssetKind.Image, reference).Value);

    public static SharedAssetDefinition Shared(
        string id = "SharedAsset",
        string reference = "shared/albedo.png") =>
        AssertSuccess(SharedAssetDefinition.Create(Id(id), Source(reference)));

    public static ItemDefinition Item(
        string id,
        IEnumerable<ItemCategory?>? categories = null,
        AttachmentSlot? slot = null,
        IEnumerable<InternalAssetId?>? shared = null) =>
        AssertSuccess(
            ItemDefinition.Create(
                Id(id),
                categories ?? [Category()],
                slot,
                shared ?? []));

    public static ItemRelationship Relationship(string first, string second) =>
        AssertSuccess(ItemRelationship.Create(Id(first), Id(second)));

    public static AssembledSetMember Member(string id, AttachmentSlot? slot = null) =>
        AssertSuccess(AssembledSetMember.Create(Id(id), slot));

    public static CompatibilityMetadataEntry Metadata(
        string key = "SkeletonFamily",
        string value = "Humanoid A") =>
        AssertSuccess(CompatibilityMetadataEntry.Create(Id(key), value));

    public static AssembledSetRules Rules(
        IEnumerable<AssembledSetMember?> members,
        bool uniqueSlots = false,
        IEnumerable<CompatibilityMetadataEntry?>? metadata = null) =>
        AssertSuccess(
            AssembledSetRules.Create(members, uniqueSlots, metadata ?? []));
}
