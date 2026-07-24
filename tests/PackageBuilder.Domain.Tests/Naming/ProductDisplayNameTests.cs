using System.Globalization;
using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Tests.Naming;

public sealed class ProductDisplayNameTests
{
    [Theory]
    [InlineData("Silverwing Talonbow")]
    [InlineData("Épée d’Argent")]
    [InlineData("Product: Edition 2")]
    public void CreatePreservesValidHumanReadableNames(string input)
    {
        ProductDisplayName value = NamingTestAssertions.AssertSuccess(ProductDisplayName.Create(input));

        Assert.Equal(input, value.Value);
        Assert.Equal(input, value.ToString());
    }

    [Theory]
    [InlineData(null, NamingValidationError.Null)]
    [InlineData("", NamingValidationError.Empty)]
    [InlineData(" \t ", NamingValidationError.WhitespaceOnly)]
    [InlineData(" Silverwing", NamingValidationError.LeadingOrTrailingWhitespace)]
    [InlineData("Silverwing ", NamingValidationError.LeadingOrTrailingWhitespace)]
    [InlineData("Silver\twing", NamingValidationError.ControlCharacter)]
    [InlineData("C:\\Model", NamingValidationError.RootedPath)]
    [InlineData("c:Model", NamingValidationError.RootedPath)]
    [InlineData("/Model", NamingValidationError.RootedPath)]
    [InlineData("\\Model", NamingValidationError.RootedPath)]
    [InlineData(".", NamingValidationError.Traversal)]
    [InlineData("../Model", NamingValidationError.Traversal)]
    [InlineData("Part/./Model", NamingValidationError.Traversal)]
    [InlineData("Model/Part", NamingValidationError.DirectorySeparator)]
    [InlineData("Model\\Part", NamingValidationError.DirectorySeparator)]
    public void CreateRejectsUnsafeOrAmbiguousInput(
        string? input,
        NamingValidationError expectedError) => NamingTestAssertions.AssertFailure(ProductDisplayName.Create(input), expectedError);

    [Fact]
    public void EqualityAndHashingAreOrdinalAndTypeSafe()
    {
        ProductDisplayName first = NamingTestAssertions.AssertSuccess(
            ProductDisplayName.Create("Silverwing Talonbow"));
        ProductDisplayName same = NamingTestAssertions.AssertSuccess(
            ProductDisplayName.Create("Silverwing Talonbow"));
        ProductDisplayName differentCase = NamingTestAssertions.AssertSuccess(
            ProductDisplayName.Create("silverwing talonbow"));

        Assert.True(first.Equals(same));
        Assert.True(first.Equals((object)same));
        Assert.False(first.Equals(differentCase));
        Assert.False(first.Equals((ProductDisplayName?)null));
        Assert.False(first.Equals("Silverwing Talonbow"));
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
    }

    [Fact]
    public void EqualityAndHashingDoNotChangeUnderTurkishCulture()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        ProductDisplayName upper = NamingTestAssertions.AssertSuccess(ProductDisplayName.Create("FILE"));
        ProductDisplayName lower = NamingTestAssertions.AssertSuccess(ProductDisplayName.Create("file"));
        int originalHash = upper.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            ProductDisplayName same = NamingTestAssertions.AssertSuccess(ProductDisplayName.Create("FILE"));
            Assert.True(upper.Equals(same));
            Assert.False(upper.Equals(lower));
            Assert.Equal(originalHash, same.GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
