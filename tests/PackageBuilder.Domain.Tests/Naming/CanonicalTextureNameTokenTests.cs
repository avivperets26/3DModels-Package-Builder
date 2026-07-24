using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Tests.Naming;

public sealed class CanonicalTextureNameTokenTests
{
    [Fact]
    public void AlbedoUsesTheExactCanonicalSpelling()
    {
        CanonicalTextureNameToken parsed = NamingTestAssertions.AssertSuccess(
            CanonicalTextureNameToken.Create("Albedo"));

        Assert.Same(CanonicalTextureNameToken.Albedo, parsed);
        Assert.Equal("Albedo", parsed.Value);
        Assert.Equal("Albedo", parsed.ToString());
        Assert.DoesNotContain("Albeado", parsed.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Albeado")]
    [InlineData("albedo")]
    [InlineData("BaseColor")]
    public void CreateRejectsNonCanonicalTokens(string input)
    {
        NamingTestAssertions.AssertFailure(
            CanonicalTextureNameToken.Create(input),
            NamingValidationError.UnsupportedCanonicalToken);
    }

    [Theory]
    [InlineData(null, NamingValidationError.Null)]
    [InlineData("", NamingValidationError.Empty)]
    [InlineData(" ", NamingValidationError.WhitespaceOnly)]
    [InlineData(" Albedo", NamingValidationError.LeadingOrTrailingWhitespace)]
    [InlineData("Albedo\t", NamingValidationError.LeadingOrTrailingWhitespace)]
    [InlineData("Albe\0do", NamingValidationError.ControlCharacter)]
    [InlineData("C:\\Albedo", NamingValidationError.RootedPath)]
    [InlineData("../Albedo", NamingValidationError.Traversal)]
    [InlineData("Texture/Albedo", NamingValidationError.DirectorySeparator)]
    public void CreatePreservesCommonNamingFailures(
        string? input,
        NamingValidationError expectedError)
    {
        NamingTestAssertions.AssertFailure(
            CanonicalTextureNameToken.Create(input),
            expectedError);
    }

    [Fact]
    public void EqualityAndHashingAreOrdinal()
    {
        CanonicalTextureNameToken albedo = CanonicalTextureNameToken.Albedo;
        CanonicalTextureNameToken same = NamingTestAssertions.AssertSuccess(
            CanonicalTextureNameToken.Create("Albedo"));

        Assert.True(albedo.Equals(same));
        Assert.True(albedo.Equals((object)same));
        Assert.False(albedo.Equals((CanonicalTextureNameToken?)null));
        Assert.False(albedo.Equals("Albedo"));
        Assert.Equal(albedo.GetHashCode(), same.GetHashCode());
    }
}
