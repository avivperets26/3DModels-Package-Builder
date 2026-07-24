using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Tests.Identifiers;

internal static class CanonicalIdentifierTestAssertions
{
    public static T AssertSuccess<T>(CanonicalIdentifierParseResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid);
        Assert.Equal(CanonicalIdentifierParseError.None, result.Error);
        return Assert.IsType<T>(result.Value);
    }

    public static void AssertFailure<T>(
        CanonicalIdentifierParseResult<T> result,
        CanonicalIdentifierParseError expectedError)
        where T : class
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedError, result.Error);
        Assert.Null(result.Value);
    }
}
