using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Tests.Naming;

public sealed class InternalAssetIdTests
{
    [Theory]
    [InlineData("SilverwingTalonbow")]
    [InlineData("A")]
    [InlineData("asset9")]
    public void CreateAcceptsCompactAsciiIdentifiers(string input)
    {
        InternalAssetId value = NamingTestAssertions.AssertSuccess(InternalAssetId.Create(input));

        Assert.Equal(input, value.Value);
        Assert.Equal(input, value.ToString());
    }

    [Theory]
    [InlineData(null, NamingValidationError.Null)]
    [InlineData("", NamingValidationError.Empty)]
    [InlineData("   ", NamingValidationError.WhitespaceOnly)]
    [InlineData(" Asset", NamingValidationError.LeadingOrTrailingWhitespace)]
    [InlineData("Asset ", NamingValidationError.LeadingOrTrailingWhitespace)]
    [InlineData("Asset\nId", NamingValidationError.ControlCharacter)]
    [InlineData("D:Asset", NamingValidationError.RootedPath)]
    [InlineData("../Asset", NamingValidationError.Traversal)]
    [InlineData("Asset/Id", NamingValidationError.DirectorySeparator)]
    [InlineData("Asset\\Id", NamingValidationError.DirectorySeparator)]
    [InlineData("1Asset", NamingValidationError.InvalidCharacter)]
    [InlineData("_Asset", NamingValidationError.InvalidCharacter)]
    [InlineData("Épée", NamingValidationError.InvalidCharacter)]
    [InlineData("Asset_Id", NamingValidationError.InvalidCharacter)]
    [InlineData("Asset-Id", NamingValidationError.InvalidCharacter)]
    [InlineData("Asset Id", NamingValidationError.InvalidCharacter)]
    [InlineData("Asseté", NamingValidationError.InvalidCharacter)]
    public void CreateRejectsNonIdentifierInput(
        string? input,
        NamingValidationError expectedError) => NamingTestAssertions.AssertFailure(InternalAssetId.Create(input), expectedError);

    [Fact]
    public void EqualityAndHashingUseExactOrdinalText()
    {
        InternalAssetId first = NamingTestAssertions.AssertSuccess(InternalAssetId.Create("Asset9"));
        InternalAssetId same = NamingTestAssertions.AssertSuccess(InternalAssetId.Create("Asset9"));
        InternalAssetId differentCase = NamingTestAssertions.AssertSuccess(InternalAssetId.Create("asset9"));

        Assert.True(first.Equals(same));
        Assert.True(first.Equals((object)same));
        Assert.False(first.Equals(differentCase));
        Assert.False(first.Equals((InternalAssetId?)null));
        Assert.False(first.Equals("Asset9"));
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
    }
}
