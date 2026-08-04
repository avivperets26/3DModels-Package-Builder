using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Domain.Tests.Tools;

internal static class ToolTestAssertions
{
    public static T AssertSuccess<T>(ToolModelValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid);
        Assert.Equal(ToolModelValidationError.None, result.Error);
        return Assert.IsType<T>(result.Value);
    }

    public static void AssertFailure<T>(
        ToolModelValidationResult<T> result,
        ToolModelValidationError expected)
        where T : class
    {
        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Equal(expected, result.Error);
    }

    public static ToolVersion Version(ToolKind tool, string value) =>
        AssertSuccess(ToolVersion.Create(tool, value));
}
