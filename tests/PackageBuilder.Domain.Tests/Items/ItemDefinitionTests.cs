using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Tests.Items;

[Trait("Task", "PB-0106")]
public sealed class ItemDefinitionTests
{
    [Fact]
    public void SharedAssetDefinitionRequiresIdAndSourceAndHasValueSemantics()
    {
        ItemTestAssertions.AssertFailure(
            SharedAssetDefinition.Create(null, ItemTestAssertions.Source()),
            ItemValidationError.NullSharedAssetId);
        ItemTestAssertions.AssertFailure(
            SharedAssetDefinition.Create(ItemTestAssertions.Id("Shared"), null),
            ItemValidationError.NullSharedAssetSource);

        SharedAssetDefinition value = ItemTestAssertions.Shared();
        SharedAssetDefinition same = ItemTestAssertions.Shared();
        Assert.True(value.Equals(same));
        Assert.True(value.Equals((object)same));
        Assert.False(value.Equals(ItemTestAssertions.Shared("Other")));
        Assert.False(value.Equals(ItemTestAssertions.Shared("SharedAsset", "other.png")));
        Assert.False(value.Equals((SharedAssetDefinition?)null));
        Assert.False(value.Equals("asset"));
        Assert.Equal(value.GetHashCode(), same.GetHashCode());
    }

    [Fact]
    public void ItemRequiresValidCollectionsAndRejectsDuplicates()
    {
        InternalAssetId id = ItemTestAssertions.Id("Item");
        ItemCategory category = ItemTestAssertions.Category();
        InternalAssetId shared = ItemTestAssertions.Id("Shared");

        ItemTestAssertions.AssertFailure(
            ItemDefinition.Create(null, [], null, []),
            ItemValidationError.NullItemId);
        ItemTestAssertions.AssertFailure(
            ItemDefinition.Create(id, null, null, []),
            ItemValidationError.NullCategories);
        ItemTestAssertions.AssertFailure(
            ItemDefinition.Create(id, [null], null, []),
            ItemValidationError.NullCategory);
        ItemTestAssertions.AssertFailure(
            ItemDefinition.Create(id, [category, category], null, []),
            ItemValidationError.DuplicateCategory);
        ItemTestAssertions.AssertFailure(
            ItemDefinition.Create(id, [], null, null),
            ItemValidationError.NullSharedAssetReferences);
        ItemTestAssertions.AssertFailure(
            ItemDefinition.Create(id, [], null, [null]),
            ItemValidationError.NullSharedAssetReference);
        ItemTestAssertions.AssertFailure(
            ItemDefinition.Create(id, [], null, [shared, shared]),
            ItemValidationError.DuplicateSharedAssetReference);
    }

    [Fact]
    public void ItemPreservesIdAndSlotAndCanonicallyOrdersImmutableCollections()
    {
        var categories = new List<ItemCategory?>
        {
            ItemTestAssertions.Category("weapon"),
            ItemTestAssertions.Category("artifact"),
        };
        var references = new List<InternalAssetId?>
        {
            ItemTestAssertions.Id("Zed"),
            ItemTestAssertions.Id("Alpha"),
        };
        AttachmentSlot slot = ItemTestAssertions.Slot("right-hand");
        ItemDefinition item = ItemTestAssertions.AssertSuccess(
            ItemDefinition.Create(
                ItemTestAssertions.Id("Sword"),
                categories,
                slot,
                references));
        categories.Clear();
        references.Clear();

        Assert.Equal("Sword", item.Id.Value);
        Assert.Same(slot, item.AttachmentSlot);
        Assert.Equal(
            ["artifact", "weapon"],
            item.Categories.Select(value => value.CanonicalIdentifier));
        Assert.Equal(
            ["Alpha", "Zed"],
            item.SharedAssetReferences.Select(value => value.Value));
        AssertImmutable(item.Categories, ItemTestAssertions.Category());
        AssertImmutable(item.SharedAssetReferences, ItemTestAssertions.Id("Other"));
    }

    [Fact]
    public void ItemEqualityAndHashingIncludeEveryField()
    {
        ItemDefinition value = ItemTestAssertions.Item(
            "Item",
            [ItemTestAssertions.Category("equipment")],
            ItemTestAssertions.Slot("head"),
            [ItemTestAssertions.Id("Shared")]);
        ItemDefinition same = ItemTestAssertions.Item(
            "Item",
            [ItemTestAssertions.Category("equipment")],
            ItemTestAssertions.Slot("head"),
            [ItemTestAssertions.Id("Shared")]);

        Assert.True(value.Equals(same));
        Assert.True(value.Equals((object)same));
        Assert.False(value.Equals(ItemTestAssertions.Item(
            "Other",
            [ItemTestAssertions.Category("equipment")],
            ItemTestAssertions.Slot("head"),
            [ItemTestAssertions.Id("Shared")])));
        Assert.False(value.Equals(ItemTestAssertions.Item(
            "Item",
            [ItemTestAssertions.Category("other")],
            ItemTestAssertions.Slot("head"),
            [ItemTestAssertions.Id("Shared")])));
        Assert.False(value.Equals(ItemTestAssertions.Item(
            "Item",
            [ItemTestAssertions.Category("equipment")],
            ItemTestAssertions.Slot("body"),
            [ItemTestAssertions.Id("Shared")])));
        Assert.False(value.Equals(ItemTestAssertions.Item(
            "Item",
            [ItemTestAssertions.Category("equipment")],
            ItemTestAssertions.Slot("head"),
            [ItemTestAssertions.Id("Other")])));
        Assert.False(value.Equals((ItemDefinition?)null));
        Assert.False(value.Equals("item"));
        Assert.Equal(value.GetHashCode(), same.GetHashCode());
    }

    private static void AssertImmutable<T>(IReadOnlyList<T> values, T value)
    {
        IList<T> list = Assert.IsType<IList<T>>(values, exactMatch: false);
        Assert.True(list.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => list.Add(value));
    }
}
