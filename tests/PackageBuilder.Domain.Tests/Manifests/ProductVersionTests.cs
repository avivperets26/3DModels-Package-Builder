using PackageBuilder.Domain.Manifests;

namespace PackageBuilder.Domain.Tests.Manifests;

public sealed class ProductVersionTests
{
    [Theory]
    [InlineData("0.0.0")]
    [InlineData("1.0.0")]
    [InlineData("10.20.300")]
    public void CreateAcceptsExactThreeComponentDecimalVersions(string input)
    {
        ProductVersionResult result = ProductVersion.Create(input);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Value);
        Assert.Equal(input, result.Value.Value);
        Assert.Equal(input, result.Value.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.00.0")]
    [InlineData("1.0.-1")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.0 ")]
    [InlineData("١.٠.٠")]
    public void CreateRejectsNonCanonicalVersions(string? input)
    {
        ProductVersionResult result = ProductVersion.Create(input);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.NotEqual(ProductVersionError.None, result.Error);
    }

    [Fact]
    public void EqualityAndHashingAreOrdinalAndStable()
    {
        ProductVersion left = ProductVersion.Create("1.2.3").Value!;
        ProductVersion equal = ProductVersion.Create("1.2.3").Value!;
        ProductVersion other = ProductVersion.Create("1.2.4").Value!;

        Assert.Equal(left, equal);
        Assert.Equal(left.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(left, other);
        Assert.True(left.Equals((object)equal));
        Assert.False(left.Equals(null));
        Assert.False(left.Equals(new object()));
    }
}
