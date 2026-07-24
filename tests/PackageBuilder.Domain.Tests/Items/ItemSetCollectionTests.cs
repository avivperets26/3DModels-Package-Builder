using System.Globalization;
using System.Reflection;
using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Products;

namespace PackageBuilder.Domain.Tests.Items;

[Trait("Task", "PB-0106")]
public sealed class ItemSetCollectionTests
{
    [Fact]
    public void ValidSetPreservesCaseOrderingRelationshipsSharedAssetsAndRules()
    {
        AttachmentSlot head = ItemTestAssertions.Slot("head");
        SharedAssetDefinition shared = ItemTestAssertions.Shared("Shared");
        ItemDefinition helmet = ItemTestAssertions.Item(
            "Helmet",
            slot: head,
            shared: [ItemTestAssertions.Id("Shared")]);
        ItemDefinition boots = ItemTestAssertions.Item("Boots", slot: ItemTestAssertions.Slot("feet"));
        AssembledSetRules rules = ItemTestAssertions.Rules(
            [
                ItemTestAssertions.Member("Boots", boots.AttachmentSlot),
                ItemTestAssertions.Member("Helmet", head),
            ],
            uniqueSlots: true,
            [ItemTestAssertions.Metadata()]);

        ItemSetDefinition set = ItemTestAssertions.AssertSuccess(
            ItemSetDefinition.Create(
                [helmet, boots],
                [ItemTestAssertions.Relationship("Helmet", "Boots")],
                [shared],
                rules));

        Assert.Same(ProductCase.ItemSet, set.ProductCase);
        Assert.Equal(["Helmet", "Boots"], set.Items.Select(value => value.Id.Value));
        Assert.Same(rules, set.AssembledSetRules);
        _ = set.GetHashCode();
    }

    [Fact]
    public void SetRejectsUnknownMissingAndContradictoryAssembledMembers()
    {
        AttachmentSlot head = ItemTestAssertions.Slot("head");
        ItemDefinition helmet = ItemTestAssertions.Item("Helmet", slot: head);
        ItemDefinition boots = ItemTestAssertions.Item("Boots");

        AssertSetFailure(
            [helmet],
            ItemTestAssertions.Rules([ItemTestAssertions.Member("Unknown")]),
            ItemValidationError.UnknownAssembledMember);
        AssertSetFailure(
            [helmet, boots],
            ItemTestAssertions.Rules([ItemTestAssertions.Member("Helmet", head)]),
            ItemValidationError.MissingAssembledMember);
        AssertSetFailure(
            [helmet],
            ItemTestAssertions.Rules(
                [ItemTestAssertions.Member("Helmet", ItemTestAssertions.Slot("body"))]),
            ItemValidationError.ContradictoryAssembledMemberSlot);
        AssertSetFailure(
            [helmet],
            ItemTestAssertions.Rules([ItemTestAssertions.Member("Helmet")]),
            ItemValidationError.ContradictoryAssembledMemberSlot);
        AssertSetFailure(
            [boots],
            ItemTestAssertions.Rules(
                [ItemTestAssertions.Member("Boots", ItemTestAssertions.Slot("feet"))]),
            ItemValidationError.ContradictoryAssembledMemberSlot);
    }

    [Fact]
    public void SetRejectsConflictingSlotsOnlyWhenRulesRequireUniqueness()
    {
        AttachmentSlot hand = ItemTestAssertions.Slot("hand");
        ItemDefinition left = ItemTestAssertions.Item("Left", slot: hand);
        ItemDefinition right = ItemTestAssertions.Item("Right", slot: hand);
        AssembledSetRules unique = ItemTestAssertions.Rules(
            [
                ItemTestAssertions.Member("Left", hand),
                ItemTestAssertions.Member("Right", hand),
            ],
            uniqueSlots: true);
        AssembledSetRules shared = ItemTestAssertions.Rules(
            [
                ItemTestAssertions.Member("Left", hand),
                ItemTestAssertions.Member("Right", hand),
            ],
            uniqueSlots: false);

        AssertSetFailure(
            [left, right],
            unique,
            ItemValidationError.ConflictingAttachmentSlot);
        Assert.True(ItemSetDefinition.Create([left, right], [], [], shared).IsValid);

        ItemDefinition noSlot = ItemTestAssertions.Item("NoSlot");
        AssembledSetRules nullSlots = ItemTestAssertions.Rules(
            [ItemTestAssertions.Member("NoSlot")],
            uniqueSlots: true);
        Assert.True(ItemSetDefinition.Create([noSlot], [], [], nullSlots).IsValid);
    }

    [Fact]
    public void DuplicateAssembledMembersAreRejectedBeforeSetCreation()
    {
        AssembledSetMember member = ItemTestAssertions.Member("Item");
        ItemTestAssertions.AssertFailure(
            AssembledSetRules.Create([member, member], false, []),
            ItemValidationError.DuplicateAssembledMember);
    }

    [Fact]
    public void CollectionRejectsAssemblyRulesAndNeverExposesCombinedRuntimeObject()
    {
        AssembledSetRules rules = ItemTestAssertions.Rules([]);
        ItemTestAssertions.AssertFailure(
            ItemCollectionDefinition.Create([], [], [], rules),
            ItemValidationError.AssembledSetRulesNotAllowed);

        ItemCollectionDefinition collection = ItemTestAssertions.AssertSuccess(
            ItemCollectionDefinition.Create([], [], []));
        Assert.Same(ProductCase.ItemCollection, collection.ProductCase);
        Assert.DoesNotContain(
            typeof(ItemCollectionDefinition).GetProperties(),
            property => property.PropertyType == typeof(AssembledSetRules));
    }

    [Fact]
    public void SetAndCollectionEqualityHashingOrderingAndCasingAreStable()
    {
        ItemDefinition upper = ItemTestAssertions.Item("Item");
        ItemDefinition lower = ItemTestAssertions.Item("item");
        ItemSetDefinition firstSet = ItemTestAssertions.AssertSuccess(
            ItemSetDefinition.Create([upper, lower], [], [], null));
        ItemSetDefinition sameSet = ItemTestAssertions.AssertSuccess(
            ItemSetDefinition.Create(
                [ItemTestAssertions.Item("Item"), ItemTestAssertions.Item("item")],
                [],
                [],
                null));
        ItemSetDefinition reorderedSet = ItemTestAssertions.AssertSuccess(
            ItemSetDefinition.Create([lower, upper], [], [], null));
        ItemCollectionDefinition firstCollection = ItemTestAssertions.AssertSuccess(
            ItemCollectionDefinition.Create([upper, lower], [], []));
        ItemCollectionDefinition sameCollection = ItemTestAssertions.AssertSuccess(
            ItemCollectionDefinition.Create(
                [ItemTestAssertions.Item("Item"), ItemTestAssertions.Item("item")],
                [],
                []));
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        int setHash = firstSet.GetHashCode();
        int collectionHash = firstCollection.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.True(firstSet.Equals(sameSet));
            Assert.True(firstSet.Equals((object)sameSet));
            Assert.False(firstSet.Equals(reorderedSet));
            Assert.False(firstSet.Equals(ItemTestAssertions.AssertSuccess(
                ItemSetDefinition.Create(
                    [upper, lower],
                    [],
                    [],
                    ItemTestAssertions.Rules(
                        [ItemTestAssertions.Member("Item"), ItemTestAssertions.Member("item")])))));
            Assert.False(firstSet.Equals((ItemSetDefinition?)null));
            Assert.False(firstSet.Equals("set"));
            Assert.Equal(setHash, sameSet.GetHashCode());

            Assert.True(firstCollection.Equals(sameCollection));
            Assert.True(firstCollection.Equals((object)sameCollection));
            Assert.False(firstCollection.Equals(ItemTestAssertions.AssertSuccess(
                ItemCollectionDefinition.Create([lower, upper], [], []))));
            Assert.False(firstCollection.Equals((ItemCollectionDefinition?)null));
            Assert.False(firstCollection.Equals("collection"));
            Assert.Equal(collectionHash, sameCollection.GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void GroupEqualityIncludesRelationshipsSharedAssetsAndAssemblyRules()
    {
        SharedAssetDefinition shared = ItemTestAssertions.Shared("Shared");
        SharedAssetDefinition otherShared = ItemTestAssertions.Shared("Other", "other.png");
        ItemDefinition a = ItemTestAssertions.Item(
            "A",
            shared: [ItemTestAssertions.Id("Shared")]);
        ItemDefinition b = ItemTestAssertions.Item("B");
        ItemDefinition aOther = ItemTestAssertions.Item(
            "A",
            shared: [ItemTestAssertions.Id("Other")]);
        ItemRelationship relationship = ItemTestAssertions.Relationship("A", "B");

        ItemCollectionDefinition baseCollection = ItemTestAssertions.AssertSuccess(
            ItemCollectionDefinition.Create([a, b], [relationship], [shared]));
        ItemCollectionDefinition noRelationshipCollection = ItemTestAssertions.AssertSuccess(
            ItemCollectionDefinition.Create([a, b], [], [shared]));
        ItemCollectionDefinition otherAssetCollection = ItemTestAssertions.AssertSuccess(
            ItemCollectionDefinition.Create([aOther, b], [relationship], [otherShared]));
        Assert.False(baseCollection.Equals(noRelationshipCollection));
        Assert.False(baseCollection.Equals(otherAssetCollection));
        _ = baseCollection.GetHashCode();

        ItemSetDefinition baseSet = ItemTestAssertions.AssertSuccess(
            ItemSetDefinition.Create([a, b], [relationship], [shared], null));
        ItemSetDefinition noRelationshipSet = ItemTestAssertions.AssertSuccess(
            ItemSetDefinition.Create([a, b], [], [shared], null));
        ItemSetDefinition otherAssetSet = ItemTestAssertions.AssertSuccess(
            ItemSetDefinition.Create([aOther, b], [relationship], [otherShared], null));
        Assert.False(baseSet.Equals(noRelationshipSet));
        Assert.False(baseSet.Equals(otherAssetSet));
        _ = baseSet.GetHashCode();

        ItemTestAssertions.AssertFailure(
            ItemSetDefinition.Create(null, [], [], null),
            ItemValidationError.NullItems);
    }

    [Fact]
    public void DomainAssemblyRetainsRendererEngineFilesystemAndMarketplaceIndependence()
    {
        Assembly assembly = typeof(ItemSetDefinition).Assembly;
        string[] references =
        [
            .. assembly.GetReferencedAssemblies().Select(value => value.Name ?? string.Empty),
        ];
        string[] forbidden =
        [
            "Blender",
            "Unity",
            "Unreal",
            "WPF",
            "Presentation",
            "PackageBuilder.Targets",
            "PackageBuilder.Marketplaces",
            "PackageBuilder.Infrastructure",
            "System.IO.FileSystem",
            "System.Net",
        ];
        Assert.DoesNotContain(
            references,
            reference => forbidden.Any(
                token => reference.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    private static void AssertSetFailure(
        IEnumerable<ItemDefinition?> items,
        AssembledSetRules rules,
        ItemValidationError error) =>
        ItemTestAssertions.AssertFailure(
            ItemSetDefinition.Create(items, [], [], rules),
            error);
}
