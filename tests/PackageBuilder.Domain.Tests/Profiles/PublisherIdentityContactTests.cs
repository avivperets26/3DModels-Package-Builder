using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Domain.Tests.Profiles;

public sealed class PublisherIdentityContactTests
{
    [Fact]
    public void PublisherDisplayNamePreservesValidBoundariesAndUnicode()
    {
        string maximum = new('A', PublisherDisplayName.MaximumLength);
        PublisherDisplayName unicode = ProfileTestAssertions.Success(
            PublisherDisplayName.Create("Élan Studio"));
        PublisherDisplayName boundary = ProfileTestAssertions.Success(
            PublisherDisplayName.Create(maximum));

        Assert.Equal("Élan Studio", unicode.Value);
        Assert.Equal(maximum, boundary.Value);
        Assert.Equal("Élan Studio", unicode.ToString());
        Assert.Equal(unicode, ProfileTestAssertions.Display("Élan Studio"));
        Assert.NotEqual(unicode, ProfileTestAssertions.Display("élan Studio"));
        Assert.False(unicode.Equals((object)ProfileTestAssertions.Display("Other")));
        Assert.Equal(
            unicode.GetHashCode(),
            ProfileTestAssertions.Display("Élan Studio").GetHashCode());
        Assert.False(unicode.Equals((PublisherDisplayName?)null));
        Assert.False(unicode.Equals(new object()));
    }

    [Theory]
    [InlineData(null, ProfileValidationError.NullPublisherDisplayName)]
    [InlineData("", ProfileValidationError.EmptyPublisherDisplayName)]
    [InlineData("  ", ProfileValidationError.WhitespaceOnlyPublisherDisplayName)]
    [InlineData(" leading", ProfileValidationError.PublisherDisplayNameEdgeWhitespace)]
    [InlineData("trailing ", ProfileValidationError.PublisherDisplayNameEdgeWhitespace)]
    [InlineData("bad\u0000name", ProfileValidationError.PublisherDisplayNameContainsControlCharacter)]
    public void PublisherDisplayNameRejectsInvalidText(
        string? value,
        ProfileValidationError error) =>
        ProfileTestAssertions.Failure(PublisherDisplayName.Create(value), error);

    [Fact]
    public void PublisherDisplayNameRejectsUnusuallyLargeInput() =>
        ProfileTestAssertions.Failure(
            PublisherDisplayName.Create(new string('A', PublisherDisplayName.MaximumLength + 1)),
            ProfileValidationError.PublisherDisplayNameTooLong);

    [Theory]
    [InlineData("support@example.test")]
    [InlineData("first.last+packages@example-domain.test")]
    [InlineData("a@localhost")]
    [InlineData("AZ09!#$%&'*+-/=?^_`{|}~@example.test")]
    public void SupportEmailAcceptsDeterministicSyntacticAddresses(string value)
    {
        SupportContact contact = ProfileTestAssertions.Success(
            SupportContact.CreateEmail(value));

        Assert.Equal(SupportContactKind.Email, contact.Kind);
        Assert.Equal(value, contact.Value);
        Assert.Equal(value, contact.ToString());
        Assert.Equal(contact, ProfileTestAssertions.Success(SupportContact.CreateEmail(value)));
        Assert.False(contact.Equals(
            ProfileTestAssertions.Success(SupportContact.CreateEmail("other@example.test"))));
        Assert.False(contact.Equals(
            (object)ProfileTestAssertions.Success(
                SupportContact.CreateEmail("other@example.test"))));
        Assert.NotEqual(contact, ProfileTestAssertions.Success(
            SupportContact.CreateSecureUrl("https://example.test/support")));
        Assert.Equal(
            contact.GetHashCode(),
            ProfileTestAssertions.Success(SupportContact.CreateEmail(value)).GetHashCode());
        Assert.False(contact.Equals((SupportContact?)null));
        Assert.False(contact.Equals(new object()));
    }

    [Theory]
    [InlineData(null, ProfileValidationError.NullSupportContactValue)]
    [InlineData("", ProfileValidationError.EmptySupportContactValue)]
    [InlineData(" ", ProfileValidationError.WhitespaceOnlySupportContactValue)]
    [InlineData("a b@example.test", ProfileValidationError.SupportContactContainsWhitespace)]
    [InlineData("a\u0000b@example.test", ProfileValidationError.SupportContactContainsControlCharacter)]
    [InlineData("plain-address", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("@example.test", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("a@@example.test", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("a@", ProfileValidationError.MalformedSupportEmail)]
    [InlineData(".a@example.test", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("a.@example.test", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("a..b@example.test", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("a()@example.test", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("a@.example.test", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("a@example..test", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("a@-example.test", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("a@example-.test", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("a@example_", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("a@example-", ProfileValidationError.MalformedSupportEmail)]
    [InlineData("a@example.", ProfileValidationError.MalformedSupportEmail)]
    public void SupportEmailRejectsUnsafeOrMalformedInput(
        string? value,
        ProfileValidationError error) =>
        ProfileTestAssertions.Failure(SupportContact.CreateEmail(value), error);

    [Fact]
    public void SupportEmailRejectsLongLocalDomainLabelAndTotalValues()
    {
        ProfileTestAssertions.Failure(
            SupportContact.CreateEmail($"{new string('a', 65)}@example.test"),
            ProfileValidationError.MalformedSupportEmail);
        ProfileTestAssertions.Failure(
            SupportContact.CreateEmail($"a@{new string('b', 64)}.test"),
            ProfileValidationError.MalformedSupportEmail);
        ProfileTestAssertions.Failure(
            SupportContact.CreateEmail(new string('a', SupportContact.MaximumEmailLength + 1)),
            ProfileValidationError.SupportContactTooLong);
    }

    [Theory]
    [InlineData("https://example.test/support")]
    [InlineData("HTTPS://example.test:8443/help?topic=packages")]
    public void SecureSupportUrlAcceptsHttpsWithoutNetworkAccess(string value)
    {
        SupportContact contact = ProfileTestAssertions.Success(
            SupportContact.CreateSecureUrl(value));

        Assert.Equal(SupportContactKind.SecureUrl, contact.Kind);
        Assert.Equal(value, contact.Value);
    }

    [Theory]
    [InlineData(null, ProfileValidationError.NullSupportContactValue)]
    [InlineData("", ProfileValidationError.EmptySupportContactValue)]
    [InlineData(" ", ProfileValidationError.WhitespaceOnlySupportContactValue)]
    [InlineData("https://example.test/a b", ProfileValidationError.SupportContactContainsWhitespace)]
    [InlineData("https://example.test/\u0001", ProfileValidationError.SupportContactContainsControlCharacter)]
    [InlineData("not-a-url", ProfileValidationError.MalformedSupportUrl)]
    [InlineData("https://", ProfileValidationError.MalformedSupportUrl)]
    [InlineData("http://example.test", ProfileValidationError.UnsafeSupportUrlScheme)]
    [InlineData("ftp://example.test", ProfileValidationError.UnsafeSupportUrlScheme)]
    [InlineData("https:example.test", ProfileValidationError.MalformedSupportUrl)]
    public void SecureSupportUrlRejectsUnsafeOrMalformedInput(
        string? value,
        ProfileValidationError error) =>
        ProfileTestAssertions.Failure(SupportContact.CreateSecureUrl(value), error);

    [Fact]
    public void SecureSupportUrlRejectsEmbeddedCredentials() =>
        ProfileTestAssertions.Failure(
            SupportContact.CreateSecureUrl(
                string.Concat("https://example", ":placeholder@", "example.test/help")),
            ProfileValidationError.SupportUrlContainsCredentials);

    [Fact]
    public void SecureSupportUrlRejectsUnusuallyLargeInput() =>
        ProfileTestAssertions.Failure(
            SupportContact.CreateSecureUrl(
                $"https://example.test/{new string('a', SupportContact.MaximumUrlLength)}"),
            ProfileValidationError.SupportContactTooLong);
}
