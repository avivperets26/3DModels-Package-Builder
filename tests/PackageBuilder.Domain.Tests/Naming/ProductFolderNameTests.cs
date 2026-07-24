using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Tests.Naming;

public sealed class ProductFolderNameTests
{
    [Theory]
    [InlineData("Silverwing_Talonbow")]
    [InlineData("Silverwing-Talonbow")]
    [InlineData("9Product")]
    [InlineData("A")]
    public void CreateAcceptsSafeSingleFolderSegments(string input)
    {
        ProductFolderName value = NamingTestAssertions.AssertSuccess(ProductFolderName.Create(input));

        Assert.Equal(input, value.Value);
        Assert.Equal(input, value.ToString());
    }

    [Theory]
    [InlineData(null, NamingValidationError.Null)]
    [InlineData("", NamingValidationError.Empty)]
    [InlineData("   ", NamingValidationError.WhitespaceOnly)]
    [InlineData(" Folder", NamingValidationError.LeadingOrTrailingWhitespace)]
    [InlineData("Folder\tName", NamingValidationError.ControlCharacter)]
    [InlineData("C:\\Folder", NamingValidationError.RootedPath)]
    [InlineData("../Folder", NamingValidationError.Traversal)]
    [InlineData("Folder/Child", NamingValidationError.DirectorySeparator)]
    [InlineData("Folder\\Child", NamingValidationError.DirectorySeparator)]
    [InlineData("Folder.", NamingValidationError.TrailingDotOrSpace)]
    [InlineData("Folder ", NamingValidationError.TrailingDotOrSpace)]
    [InlineData("CON", NamingValidationError.ReservedFileSystemName)]
    [InlineData("con", NamingValidationError.ReservedFileSystemName)]
    [InlineData("NUL", NamingValidationError.ReservedFileSystemName)]
    [InlineData("COM1", NamingValidationError.ReservedFileSystemName)]
    [InlineData("lpt9", NamingValidationError.ReservedFileSystemName)]
    [InlineData("_Folder", NamingValidationError.InvalidCharacter)]
    [InlineData("-Folder", NamingValidationError.InvalidCharacter)]
    [InlineData("Folder.Name", NamingValidationError.InvalidCharacter)]
    [InlineData("Folder:Name", NamingValidationError.InvalidCharacter)]
    [InlineData("DossierÉ", NamingValidationError.InvalidCharacter)]
    public void CreateRejectsUnsafeFileSystemSegments(
        string? input,
        NamingValidationError expectedError) => NamingTestAssertions.AssertFailure(ProductFolderName.Create(input), expectedError);

    [Fact]
    public void EqualityAndHashingUseExactOrdinalText()
    {
        ProductFolderName first = NamingTestAssertions.AssertSuccess(
            ProductFolderName.Create("Silverwing_Talonbow"));
        ProductFolderName same = NamingTestAssertions.AssertSuccess(
            ProductFolderName.Create("Silverwing_Talonbow"));
        ProductFolderName differentCase = NamingTestAssertions.AssertSuccess(
            ProductFolderName.Create("silverwing_Talonbow"));

        Assert.True(first.Equals(same));
        Assert.True(first.Equals((object)same));
        Assert.False(first.Equals(differentCase));
        Assert.False(first.Equals((ProductFolderName?)null));
        Assert.False(first.Equals("Silverwing_Talonbow"));
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
    }
}
