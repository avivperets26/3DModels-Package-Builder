using PackageBuilder.Domain.Items;

namespace PackageBuilder.Domain.Tests.Items;

[Trait("Task", "PB-0106")]
public sealed class ItemGroupValidationTests
{
    [Fact]
    public void GroupRejectsInvalidItemCollectionsAndOrdinalDuplicateIds()
    {
        ItemDefinition item = ItemTestAssertions.Item("Item");
        AssertCollectionFailure(null, [], [], ItemValidationError.NullItems);
        AssertCollectionFailure([null], [], [], ItemValidationError.NullItem);
        AssertCollectionFailure(
            [item, ItemTestAssertions.Item("Item")],
            [],
            [],
            ItemValidationError.DuplicateItemId);

        ItemCollectionDefinition distinctCase = CreateCollection(
            [item, ItemTestAssertions.Item("item")]);
        Assert.Equal(["Item", "item"], distinctCase.Items.Select(value => value.Id.Value));
    }

    [Fact]
    public void GroupRejectsInvalidUnknownAndDuplicateRelationships()
    {
        ItemDefinition first = ItemTestAssertions.Item("A");
        ItemDefinition second = ItemTestAssertions.Item("B");
        ItemRelationship relationship = ItemTestAssertions.Relationship("A", "B");

        AssertCollectionFailure([first, second], null, [], ItemValidationError.NullRelationships);
        AssertCollectionFailure(
            [first, second],
            [null],
            [],
            ItemValidationError.NullRelationship);
        AssertCollectionFailure(
            [first],
            [relationship],
            [],
            ItemValidationError.UnknownRelationshipEndpoint);
        AssertCollectionFailure(
            [second],
            [relationship],
            [],
            ItemValidationError.UnknownRelationshipEndpoint);
        AssertCollectionFailure(
            [first, second],
            [relationship, ItemTestAssertions.Relationship("B", "A")],
            [],
            ItemValidationError.DuplicateRelationship);
        AssertCollectionFailure(
            [first, second],
            [relationship, ItemTestAssertions.Relationship("A", "B")],
            [],
            ItemValidationError.DuplicateRelationship);
    }

    [Fact]
    public void GroupRejectsInvalidSharedAssetDeclarationsAndReferences()
    {
        SharedAssetDefinition shared = ItemTestAssertions.Shared("Shared");
        ItemDefinition referencing = ItemTestAssertions.Item(
            "A",
            shared: [ItemTestAssertions.Id("Shared")]);
        ItemDefinition unknown = ItemTestAssertions.Item(
            "A",
            shared: [ItemTestAssertions.Id("Unknown")]);

        AssertCollectionFailure([referencing], [], null, ItemValidationError.NullSharedAssets);
        AssertCollectionFailure(
            [referencing],
            [],
            [null],
            ItemValidationError.NullSharedAsset);
        AssertCollectionFailure(
            [referencing],
            [],
            [shared, ItemTestAssertions.Shared("Shared", "other.png")],
            ItemValidationError.DuplicateSharedAssetId);
        AssertCollectionFailure(
            [unknown],
            [],
            [shared],
            ItemValidationError.UnknownSharedAssetReference);
        AssertCollectionFailure(
            [ItemTestAssertions.Item("A")],
            [],
            [shared],
            ItemValidationError.UnreferencedSharedAsset);
    }

    [Fact]
    public void SharedAssetsMayBeUsedByMultipleItemsAndAreReturnedDeterministically()
    {
        SharedAssetDefinition alpha = ItemTestAssertions.Shared("Alpha", "alpha.png");
        SharedAssetDefinition zed = ItemTestAssertions.Shared("Zed", "zed.png");
        var items = new List<ItemDefinition?>
        {
            ItemTestAssertions.Item(
                "Second",
                shared: [ItemTestAssertions.Id("Alpha")]),
            ItemTestAssertions.Item(
                "First",
                shared: [ItemTestAssertions.Id("Zed"), ItemTestAssertions.Id("Alpha")]),
        };
        var relationships = new List<ItemRelationship?>
        {
            ItemTestAssertions.Relationship("Second", "First"),
        };
        var assets = new List<SharedAssetDefinition?> { zed, alpha };
        ItemCollectionDefinition collection = CreateCollection(items, relationships, assets);
        items.Clear();
        relationships.Clear();
        assets.Clear();

        Assert.Same(PackageBuilder.Domain.Products.ProductCase.ItemCollection, collection.ProductCase);
        Assert.Equal(["Second", "First"], collection.Items.Select(value => value.Id.Value));
        Assert.Equal(
            [("First", "Second")],
            collection.Relationships.Select(
                value => (value.FirstItemId.Value, value.SecondItemId.Value)));
        Assert.Equal(["Alpha", "Zed"], collection.SharedAssets.Select(value => value.Id.Value));
        _ = collection.GetHashCode();
        AssertImmutable(collection.Items, ItemTestAssertions.Item("Other"));
        AssertImmutable(
            collection.Relationships,
            ItemTestAssertions.Relationship("A", "B"));
        AssertImmutable(collection.SharedAssets, ItemTestAssertions.Shared("Other", "other.png"));
    }

    [Fact]
    public void EmptyAndSingleItemGroupsAreValidWithoutInventedMinimums()
    {
        Assert.Empty(CreateCollection([]).Items);
        _ = Assert.Single(CreateCollection([ItemTestAssertions.Item("Only")]).Items);
        Assert.Empty(CreateSet([]).Items);
        _ = Assert.Single(CreateSet([ItemTestAssertions.Item("Only")]).Items);
    }

    [Fact]
    public void MultipleRelationshipsUseCanonicalReturnedOrdering()
    {
        ItemCollectionDefinition collection = CreateCollection(
            [
                ItemTestAssertions.Item("A"),
                ItemTestAssertions.Item("B"),
                ItemTestAssertions.Item("C"),
            ],
            [
                ItemTestAssertions.Relationship("B", "C"),
                ItemTestAssertions.Relationship("A", "C"),
                ItemTestAssertions.Relationship("A", "B"),
            ]);

        Assert.Equal(
            [("A", "B"), ("A", "C"), ("B", "C")],
            collection.Relationships.Select(
                value => (value.FirstItemId.Value, value.SecondItemId.Value)));
    }

    private static ItemCollectionDefinition CreateCollection(
        IEnumerable<ItemDefinition?> items,
        IEnumerable<ItemRelationship?>? relationships = null,
        IEnumerable<SharedAssetDefinition?>? assets = null) =>
        ItemTestAssertions.AssertSuccess(
            ItemCollectionDefinition.Create(
                items,
                relationships ?? [],
                assets ?? []));

    private static ItemSetDefinition CreateSet(IEnumerable<ItemDefinition?> items) =>
        ItemTestAssertions.AssertSuccess(ItemSetDefinition.Create(items, [], [], null));

    private static void AssertCollectionFailure(
        IEnumerable<ItemDefinition?>? items,
        IEnumerable<ItemRelationship?>? relationships,
        IEnumerable<SharedAssetDefinition?>? assets,
        ItemValidationError error) =>
        ItemTestAssertions.AssertFailure(
            ItemCollectionDefinition.Create(items, relationships, assets),
            error);

    private static void AssertImmutable<T>(IReadOnlyList<T> values, T value)
    {
        IList<T> list = Assert.IsType<IList<T>>(values, exactMatch: false);
        Assert.True(list.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => list.Add(value));
    }
}
