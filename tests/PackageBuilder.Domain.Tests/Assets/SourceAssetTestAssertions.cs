using PackageBuilder.Domain.Assets;

namespace PackageBuilder.Domain.Tests.Assets;

internal static class SourceAssetTestAssertions
{
    public static SourceAsset AssertSuccess(SourceAssetValidationResult result)
    {
        Assert.True(result.IsValid);
        Assert.Equal(SourceAssetValidationError.None, result.Error);
        return Assert.IsType<SourceAsset>(result.Value);
    }

    public static void AssertFailure(
        SourceAssetValidationResult result,
        SourceAssetValidationError expectedError)
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedError, result.Error);
        Assert.Null(result.Value);
    }
}
