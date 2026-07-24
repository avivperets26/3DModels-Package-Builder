using PackageBuilder.Domain.Items;

namespace PackageBuilder.Domain.Tests.Items;

[Trait("Task", "PB-0106")]
public sealed class AssembledSetRulesTests
{
    [Fact]
    public void MemberRequiresItemIdAndHasValueSemantics()
    {
        ItemTestAssertions.AssertFailure(
            AssembledSetMember.Create(null, null),
            ItemValidationError.NullAssembledMemberItemId);

        AssembledSetMember value = ItemTestAssertions.Member(
            "Item",
            ItemTestAssertions.Slot("head"));
        AssembledSetMember same = ItemTestAssertions.Member(
            "Item",
            ItemTestAssertions.Slot("head"));
        Assert.True(value.Equals(same));
        Assert.True(value.Equals((object)same));
        Assert.False(value.Equals(ItemTestAssertions.Member("Other", value.AttachmentSlot)));
        Assert.False(value.Equals(ItemTestAssertions.Member(
            "Item",
            ItemTestAssertions.Slot("body"))));
        Assert.False(value.Equals((AssembledSetMember?)null));
        Assert.False(value.Equals("member"));
        Assert.Equal(value.GetHashCode(), same.GetHashCode());
        _ = ItemTestAssertions.Member("NoSlot").GetHashCode();
    }

    [Fact]
    public void RulesRejectInvalidMemberAndMetadataCollections()
    {
        AssembledSetMember member = ItemTestAssertions.Member("Item");
        CompatibilityMetadataEntry metadata = ItemTestAssertions.Metadata();

        ItemTestAssertions.AssertFailure(
            AssembledSetRules.Create(null, false, []),
            ItemValidationError.NullAssembledMembers);
        ItemTestAssertions.AssertFailure(
            AssembledSetRules.Create([null], false, []),
            ItemValidationError.NullAssembledMember);
        ItemTestAssertions.AssertFailure(
            AssembledSetRules.Create([member, member], false, []),
            ItemValidationError.DuplicateAssembledMember);
        ItemTestAssertions.AssertFailure(
            AssembledSetRules.Create([], false, null),
            ItemValidationError.NullCompatibilityMetadata);
        ItemTestAssertions.AssertFailure(
            AssembledSetRules.Create([], false, [null]),
            ItemValidationError.NullCompatibilityEntry);
        ItemTestAssertions.AssertFailure(
            AssembledSetRules.Create([], false, [metadata, metadata]),
            ItemValidationError.DuplicateCompatibilityKey);
    }

    [Fact]
    public void RulesCanonicallyOrderImmutableSnapshotsAndPreserveUniquenessPolicy()
    {
        var members = new List<AssembledSetMember?>
        {
            ItemTestAssertions.Member("Zed"),
            ItemTestAssertions.Member("Alpha"),
        };
        var metadata = new List<CompatibilityMetadataEntry?>
        {
            ItemTestAssertions.Metadata("ZedKey", "z"),
            ItemTestAssertions.Metadata("AlphaKey", "a"),
        };
        AssembledSetRules rules = ItemTestAssertions.AssertSuccess(
            AssembledSetRules.Create(members, true, metadata));
        members.Clear();
        metadata.Clear();

        Assert.True(rules.RequireUniqueAttachmentSlots);
        Assert.Equal(["Alpha", "Zed"], rules.Members.Select(value => value.ItemId.Value));
        Assert.Equal(
            ["AlphaKey", "ZedKey"],
            rules.CompatibilityMetadata.Select(value => value.Key.Value));
        AssertImmutable(rules.Members, ItemTestAssertions.Member("Other"));
        AssertImmutable(rules.CompatibilityMetadata, ItemTestAssertions.Metadata("Other", "x"));
    }

    [Fact]
    public void RulesEqualityAndHashingIncludeMembersPolicyAndMetadata()
    {
        AssembledSetRules value = ItemTestAssertions.Rules(
            [ItemTestAssertions.Member("Item", ItemTestAssertions.Slot("head"))],
            uniqueSlots: true,
            [ItemTestAssertions.Metadata()]);
        AssembledSetRules same = ItemTestAssertions.Rules(
            [ItemTestAssertions.Member("Item", ItemTestAssertions.Slot("head"))],
            uniqueSlots: true,
            [ItemTestAssertions.Metadata()]);

        Assert.True(value.Equals(same));
        Assert.True(value.Equals((object)same));
        Assert.False(value.Equals(ItemTestAssertions.Rules(
            [ItemTestAssertions.Member("Other")],
            uniqueSlots: true,
            [ItemTestAssertions.Metadata()])));
        Assert.False(value.Equals(ItemTestAssertions.Rules(
            [ItemTestAssertions.Member("Item", ItemTestAssertions.Slot("head"))],
            uniqueSlots: false,
            [ItemTestAssertions.Metadata()])));
        Assert.False(value.Equals(ItemTestAssertions.Rules(
            [ItemTestAssertions.Member("Item", ItemTestAssertions.Slot("head"))],
            uniqueSlots: true,
            [ItemTestAssertions.Metadata("Other", "value")])));
        Assert.False(value.Equals((AssembledSetRules?)null));
        Assert.False(value.Equals("rules"));
        Assert.Equal(value.GetHashCode(), same.GetHashCode());
        _ = ItemTestAssertions.Rules(
            [ItemTestAssertions.Member("NoSlot")],
            uniqueSlots: false).GetHashCode();
    }

    private static void AssertImmutable<T>(IReadOnlyList<T> values, T value)
    {
        IList<T> list = Assert.IsType<IList<T>>(values, exactMatch: false);
        Assert.True(list.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => list.Add(value));
    }
}
