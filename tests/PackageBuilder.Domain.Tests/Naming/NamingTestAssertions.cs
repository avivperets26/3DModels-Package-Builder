using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Tests.Naming;

internal static class NamingTestAssertions
{
    public static T AssertSuccess<T>(NamingValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid);
        Assert.Equal(NamingValidationError.None, result.Error);
        return Assert.IsType<T>(result.Value);
    }

    public static void AssertFailure<T>(
        NamingValidationResult<T> result,
        NamingValidationError expectedError)
        where T : class
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedError, result.Error);
        Assert.Null(result.Value);
    }
}
