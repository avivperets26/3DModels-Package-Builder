using PackageBuilder.Domain.Materials;

namespace PackageBuilder.Domain.Tests.Materials;

internal static class MaterialTestAssertions
{
    public static EmissionProperties AssertSuccess(EmissionPropertiesValidationResult result)
    {
        Assert.True(result.IsValid);
        Assert.Equal(EmissionPropertiesValidationError.None, result.Error);
        return Assert.IsType<EmissionProperties>(result.Value);
    }

    public static UvTransform AssertSuccess(UvTransformValidationResult result)
    {
        Assert.True(result.IsValid);
        Assert.Equal(UvTransformValidationError.None, result.Error);
        return Assert.IsType<UvTransform>(result.Value);
    }

    public static MaterialDefinition AssertSuccess(MaterialDefinitionValidationResult result)
    {
        Assert.True(result.IsValid);
        Assert.Equal(MaterialDefinitionValidationError.None, result.Error);
        return Assert.IsType<MaterialDefinition>(result.Value);
    }

    public static void AssertFailure(
        EmissionPropertiesValidationResult result,
        EmissionPropertiesValidationError error)
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static void AssertFailure(
        UvTransformValidationResult result,
        UvTransformValidationError error)
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static void AssertFailure(
        MaterialDefinitionValidationResult result,
        MaterialDefinitionValidationError error)
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }
}
