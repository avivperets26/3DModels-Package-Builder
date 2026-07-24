using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Domain.Tests.Textures;

internal static class TextureAssignmentTestAssertions
{
    public static TextureAssignment AssertSuccess(TextureAssignmentValidationResult result)
    {
        Assert.True(result.IsValid);
        Assert.Equal(TextureAssignmentValidationError.None, result.Error);
        return Assert.IsType<TextureAssignment>(result.Value);
    }

    public static void AssertFailure(
        TextureAssignmentValidationResult result,
        TextureAssignmentValidationError expectedError)
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedError, result.Error);
        Assert.Null(result.Value);
    }
}
