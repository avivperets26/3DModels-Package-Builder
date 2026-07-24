using PackageBuilder.Domain.Items;

namespace PackageBuilder.Domain.Tests.Items;

[Trait("Task", "PB-0106")]
public sealed class ItemRelationshipTests
{
    [Fact]
    public void RelationshipRejectsMissingAndSelfEndpoints()
    {
        ItemTestAssertions.AssertFailure(
            ItemRelationship.Create(null, ItemTestAssertions.Id("B")),
            ItemValidationError.NullRelationshipEndpoint);
        ItemTestAssertions.AssertFailure(
            ItemRelationship.Create(ItemTestAssertions.Id("A"), null),
            ItemValidationError.NullRelationshipEndpoint);
        ItemTestAssertions.AssertFailure(
            ItemRelationship.Create(ItemTestAssertions.Id("A"), ItemTestAssertions.Id("A")),
            ItemValidationError.SelfRelationship);
    }

    [Fact]
    public void RelationshipCanonicalizesDirectionAndUsesOrdinalCasing()
    {
        ItemRelationship forward = ItemTestAssertions.Relationship("Alpha", "Zed");
        ItemRelationship reversed = ItemTestAssertions.Relationship("Zed", "Alpha");
        ItemRelationship caseDifferent = ItemTestAssertions.Relationship("alpha", "Zed");

        Assert.Equal("Alpha", forward.FirstItemId.Value);
        Assert.Equal("Zed", forward.SecondItemId.Value);
        Assert.True(forward.Equals(reversed));
        Assert.True(forward.Equals((object)reversed));
        Assert.False(forward.Equals(caseDifferent));
        Assert.False(forward.Equals((ItemRelationship?)null));
        Assert.False(forward.Equals("relationship"));
        Assert.Equal(forward.GetHashCode(), reversed.GetHashCode());
    }
}
