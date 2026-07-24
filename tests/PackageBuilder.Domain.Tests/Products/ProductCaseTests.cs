using System.Globalization;
using PackageBuilder.Domain.Identifiers;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Tests.Identifiers;

namespace PackageBuilder.Domain.Tests.Products;

[Trait("Task", "PB-0102")]
public sealed class ProductCaseTests
{
    private static readonly string[] _expectedCanonicalOrder =
    [
        "static",
        "rigged",
        "rigged-animated",
        "item-set",
        "item-collection",
    ];

    [Fact]
    public void AllContainsExactlyTheFiveCasesInStableCanonicalOrder()
    {
        Assert.Equal(
            _expectedCanonicalOrder,
            ProductCase.All.Select(productCase => productCase.CanonicalIdentifier));

        Assert.Same(ProductCase.Static, ProductCase.All[0]);
        Assert.Same(ProductCase.Rigged, ProductCase.All[1]);
        Assert.Same(ProductCase.RiggedAnimated, ProductCase.All[2]);
        Assert.Same(ProductCase.ItemSet, ProductCase.All[3]);
        Assert.Same(ProductCase.ItemCollection, ProductCase.All[4]);
    }

    [Theory]
    [InlineData("static", 0)]
    [InlineData("rigged", 1)]
    [InlineData("rigged-animated", 2)]
    [InlineData("item-set", 3)]
    [InlineData("item-collection", 4)]
    public void TryParseReturnsTheCanonicalSingleton(string identifier, int expectedIndex)
    {
        ProductCase parsed = CanonicalIdentifierTestAssertions.AssertSuccess(
            ProductCase.TryParse(identifier));

        Assert.Same(ProductCase.All[expectedIndex], parsed);
        Assert.Equal(identifier, parsed.CanonicalIdentifier);
        Assert.Equal(identifier, parsed.ToString());
    }

    [Theory]
    [InlineData(null, CanonicalIdentifierParseError.Null)]
    [InlineData("", CanonicalIdentifierParseError.Empty)]
    [InlineData(" \t ", CanonicalIdentifierParseError.WhitespaceOnly)]
    [InlineData(" static", CanonicalIdentifierParseError.Malformed)]
    [InlineData("static ", CanonicalIdentifierParseError.Malformed)]
    [InlineData("Static", CanonicalIdentifierParseError.Malformed)]
    [InlineData("RIGGED", CanonicalIdentifierParseError.Malformed)]
    [InlineData("-static", CanonicalIdentifierParseError.Malformed)]
    [InlineData("static-", CanonicalIdentifierParseError.Malformed)]
    [InlineData("rigged--animated", CanonicalIdentifierParseError.Malformed)]
    [InlineData("rigged_animated", CanonicalIdentifierParseError.Malformed)]
    [InlineData("item/collection", CanonicalIdentifierParseError.Malformed)]
    [InlineData("item.collection", CanonicalIdentifierParseError.Malformed)]
    [InlineData("ítém", CanonicalIdentifierParseError.Malformed)]
    [InlineData("animated", CanonicalIdentifierParseError.Unknown)]
    [InlineData("riged", CanonicalIdentifierParseError.Unknown)]
    [InlineData("item-collections", CanonicalIdentifierParseError.Unknown)]
    public void TryParseRejectsInvalidOrUnsupportedIdentifiers(
        string? identifier,
        CanonicalIdentifierParseError expectedError) =>
        CanonicalIdentifierTestAssertions.AssertFailure(
            ProductCase.TryParse(identifier),
            expectedError);

    [Fact]
    public void EqualityAndHashingUseStableOrdinalIdentifiers()
    {
        ProductCase parsedStatic = CanonicalIdentifierTestAssertions.AssertSuccess(
            ProductCase.TryParse("static"));

        Assert.True(ProductCase.Static.Equals(parsedStatic));
        Assert.True(ProductCase.Static.Equals((object)parsedStatic));
        Assert.False(ProductCase.Static.Equals(ProductCase.Rigged));
        Assert.False(ProductCase.Static.Equals((ProductCase?)null));
        Assert.False(ProductCase.Static.Equals("static"));
        Assert.Equal(ProductCase.Static.GetHashCode(), parsedStatic.GetHashCode());
    }

    [Fact]
    public void ParsingOrderingEqualityAndHashingAreCultureInvariant()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        string[] originalOrder =
        [
            .. ProductCase.All.Select(productCase => productCase.CanonicalIdentifier),
        ];
        int originalHash = ProductCase.Rigged.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            ProductCase parsed = CanonicalIdentifierTestAssertions.AssertSuccess(
                ProductCase.TryParse("rigged"));
            Assert.Same(ProductCase.Rigged, parsed);
            Assert.Equal(originalHash, parsed.GetHashCode());
            Assert.Equal(
                originalOrder,
                ProductCase.All.Select(productCase => productCase.CanonicalIdentifier));
            CanonicalIdentifierTestAssertions.AssertFailure(
                ProductCase.TryParse("RIGGED"),
                CanonicalIdentifierParseError.Malformed);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void StaticRiggedAndRiggedAnimatedRemainDistinct()
    {
        Assert.NotEqual(ProductCase.Static, ProductCase.Rigged);
        Assert.NotEqual(ProductCase.Rigged, ProductCase.RiggedAnimated);
        Assert.NotEqual(ProductCase.Static, ProductCase.RiggedAnimated);
    }

    [Fact]
    public void ItemSetAndItemCollectionRemainDistinct()
    {
        Assert.NotEqual(ProductCase.ItemSet, ProductCase.ItemCollection);
        Assert.NotEqual(
            ProductCase.ItemSet.CanonicalIdentifier,
            ProductCase.ItemCollection.CanonicalIdentifier);
    }

    [Fact]
    public void SupportedCaseRegistryCannotBeMutated()
    {
        IList<ProductCase> list = Assert.IsType<IList<ProductCase>>(
            ProductCase.All,
            exactMatch: false);

        Assert.True(list.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => list.Add(ProductCase.Static));
    }
}
