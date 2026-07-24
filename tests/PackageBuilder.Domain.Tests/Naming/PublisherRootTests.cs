using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Tests.Naming;

public sealed class PublisherRootTests
{
    [Theory]
    [InlineData("AvivPeretsFBX")]
    [InlineData("BrothersPublisherName")]
    [InlineData("Publisher_2")]
    [InlineData("p")]
    public void CreateAcceptsConfigurablePublisherRoots(string input)
    {
        PublisherRoot value = NamingTestAssertions.AssertSuccess(PublisherRoot.Create(input));

        Assert.Equal(input, value.Value);
        Assert.Equal(input, value.ToString());
    }

    [Theory]
    [InlineData(null, NamingValidationError.Null)]
    [InlineData("", NamingValidationError.Empty)]
    [InlineData("\t", NamingValidationError.WhitespaceOnly)]
    [InlineData(" Publisher", NamingValidationError.LeadingOrTrailingWhitespace)]
    [InlineData("Publisher\rName", NamingValidationError.ControlCharacter)]
    [InlineData("E:\\Publisher", NamingValidationError.RootedPath)]
    [InlineData("./Publisher", NamingValidationError.Traversal)]
    [InlineData("Publisher/Root", NamingValidationError.DirectorySeparator)]
    [InlineData("Publisher\\Root", NamingValidationError.DirectorySeparator)]
    [InlineData("Publisher.", NamingValidationError.TrailingDotOrSpace)]
    [InlineData("Publisher ", NamingValidationError.TrailingDotOrSpace)]
    [InlineData("PRN", NamingValidationError.ReservedFileSystemName)]
    [InlineData("aux", NamingValidationError.ReservedFileSystemName)]
    [InlineData("COM9", NamingValidationError.ReservedFileSystemName)]
    [InlineData("LPT1", NamingValidationError.ReservedFileSystemName)]
    [InlineData("2Publisher", NamingValidationError.InvalidCharacter)]
    [InlineData("_Publisher", NamingValidationError.InvalidCharacter)]
    [InlineData("Publisher-Root", NamingValidationError.InvalidCharacter)]
    [InlineData("Publisher.Root", NamingValidationError.InvalidCharacter)]
    [InlineData("Publísher", NamingValidationError.InvalidCharacter)]
    public void CreateRejectsUnsafePublisherIdentifiers(
        string? input,
        NamingValidationError expectedError) => NamingTestAssertions.AssertFailure(PublisherRoot.Create(input), expectedError);

    [Fact]
    public void DifferentConfiguredPublishersRemainDistinct()
    {
        PublisherRoot defaultPublisher = NamingTestAssertions.AssertSuccess(
            PublisherRoot.Create("AvivPeretsFBX"));
        PublisherRoot otherPublisher = NamingTestAssertions.AssertSuccess(
            PublisherRoot.Create("BrothersPublisherName"));
        PublisherRoot samePublisher = NamingTestAssertions.AssertSuccess(
            PublisherRoot.Create("AvivPeretsFBX"));

        Assert.True(defaultPublisher.Equals(samePublisher));
        Assert.True(defaultPublisher.Equals((object)samePublisher));
        Assert.False(defaultPublisher.Equals(otherPublisher));
        Assert.False(defaultPublisher.Equals((PublisherRoot?)null));
        Assert.False(defaultPublisher.Equals("AvivPeretsFBX"));
        Assert.Equal(defaultPublisher.GetHashCode(), samePublisher.GetHashCode());
    }
}
