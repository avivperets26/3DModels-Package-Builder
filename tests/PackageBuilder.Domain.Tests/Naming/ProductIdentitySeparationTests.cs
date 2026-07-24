using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Tests.Naming;

public sealed class ProductIdentitySeparationTests
{
    [Fact]
    public void DisplayIdAndFolderRepresentationsRemainExplicitlySeparate()
    {
        ProductDisplayName displayName = NamingTestAssertions.AssertSuccess(
            ProductDisplayName.Create("Silverwing Talonbow"));
        InternalAssetId assetId = NamingTestAssertions.AssertSuccess(
            InternalAssetId.Create("SilverwingTalonbow"));
        ProductFolderName folderName = NamingTestAssertions.AssertSuccess(
            ProductFolderName.Create("Silverwing_Talonbow"));

        Assert.Equal("Silverwing Talonbow", displayName.Value);
        Assert.Equal("SilverwingTalonbow", assetId.Value);
        Assert.Equal("Silverwing_Talonbow", folderName.Value);
        Assert.NotEqual(displayName.Value, assetId.Value);
        Assert.NotEqual(assetId.Value, folderName.Value);
        Assert.NotEqual(displayName.Value, folderName.Value);
        Assert.False(displayName.Equals(assetId));
    }
}
