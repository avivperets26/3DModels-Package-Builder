using System.Globalization;
using PackageBuilder.Domain.Items;

namespace PackageBuilder.Domain.Tests.Items;

[Trait("Task", "PB-0106")]
public sealed class ItemValueTests
{
    [Theory]
    [InlineData(null, ItemValidationError.NullCategoryIdentifier)]
    [InlineData("", ItemValidationError.EmptyCategoryIdentifier)]
    [InlineData(" ", ItemValidationError.WhitespaceOnlyCategoryIdentifier)]
    [InlineData("Armor", ItemValidationError.MalformedCategoryIdentifier)]
    [InlineData("armor--set", ItemValidationError.MalformedCategoryIdentifier)]
    public void CategoryRejectsInvalidCanonicalIdentifiers(
        string? value,
        ItemValidationError error) =>
        ItemTestAssertions.AssertFailure(ItemCategory.Create(value), error);

    [Theory]
    [InlineData(null, ItemValidationError.NullSlotIdentifier)]
    [InlineData("", ItemValidationError.EmptySlotIdentifier)]
    [InlineData(" ", ItemValidationError.WhitespaceOnlySlotIdentifier)]
    [InlineData("Left Hand", ItemValidationError.MalformedSlotIdentifier)]
    [InlineData("-left", ItemValidationError.MalformedSlotIdentifier)]
    public void SlotRejectsInvalidCanonicalIdentifiers(
        string? value,
        ItemValidationError error) =>
        ItemTestAssertions.AssertFailure(AttachmentSlot.Create(value), error);

    [Fact]
    public void CategoriesAndSlotsAreExtensibleOrdinalValues()
    {
        ItemCategory category = ItemTestAssertions.Category("ritual-instrument");
        ItemCategory sameCategory = ItemTestAssertions.Category("ritual-instrument");
        ItemCategory differentCategory = ItemTestAssertions.Category("Ritual".ToLowerInvariant());
        AttachmentSlot slot = ItemTestAssertions.Slot("left-forearm");
        AttachmentSlot sameSlot = ItemTestAssertions.Slot("left-forearm");
        AttachmentSlot differentSlot = ItemTestAssertions.Slot("right-forearm");
        CultureInfo previous = CultureInfo.CurrentCulture;
        int categoryHash = category.GetHashCode();
        int slotHash = slot.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("ritual-instrument", category.CanonicalIdentifier);
            Assert.Equal("ritual-instrument", category.ToString());
            Assert.True(category.Equals(sameCategory));
            Assert.True(category.Equals((object)sameCategory));
            Assert.False(category.Equals(differentCategory));
            Assert.False(category.Equals((ItemCategory?)null));
            Assert.False(category.Equals("category"));
            Assert.Equal(categoryHash, sameCategory.GetHashCode());

            Assert.Equal("left-forearm", slot.CanonicalIdentifier);
            Assert.Equal("left-forearm", slot.ToString());
            Assert.True(slot.Equals(sameSlot));
            Assert.True(slot.Equals((object)sameSlot));
            Assert.False(slot.Equals(differentSlot));
            Assert.False(slot.Equals((AttachmentSlot?)null));
            Assert.False(slot.Equals("slot"));
            Assert.Equal(slotHash, sameSlot.GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void CompatibilityMetadataValidatesKeyAndExactUnicodeValue()
    {
        ItemTestAssertions.AssertFailure(
            CompatibilityMetadataEntry.Create(null, "value"),
            ItemValidationError.NullCompatibilityKey);

        (string? Value, ItemValidationError Error)[] invalid =
        [
            (null, ItemValidationError.NullCompatibilityValue),
            ("", ItemValidationError.EmptyCompatibilityValue),
            (" ", ItemValidationError.WhitespaceOnlyCompatibilityValue),
            (" value", ItemValidationError.CompatibilityValueEdgeWhitespace),
            ("value ", ItemValidationError.CompatibilityValueEdgeWhitespace),
            ("val\u0000ue", ItemValidationError.CompatibilityValueContainsControlCharacter),
        ];
        foreach ((string? value, ItemValidationError error) in invalid)
        {
            ItemTestAssertions.AssertFailure(
                CompatibilityMetadataEntry.Create(ItemTestAssertions.Id("Key"), value),
                error);
        }

        CompatibilityMetadataEntry entry = ItemTestAssertions.Metadata("RigFamily", "Ä°nsan Rig");
        CompatibilityMetadataEntry same = ItemTestAssertions.Metadata("RigFamily", "Ä°nsan Rig");
        Assert.Equal("Ä°nsan Rig", entry.Value);
        Assert.True(entry.Equals(same));
        Assert.True(entry.Equals((object)same));
        Assert.False(entry.Equals(ItemTestAssertions.Metadata("OtherKey", "Ä°nsan Rig")));
        Assert.False(entry.Equals(ItemTestAssertions.Metadata("RigFamily", "Other")));
        Assert.False(entry.Equals((CompatibilityMetadataEntry?)null));
        Assert.False(entry.Equals("metadata"));
        Assert.Equal(entry.GetHashCode(), same.GetHashCode());
    }
}
