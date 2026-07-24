using System.Globalization;
using PackageBuilder.Domain.Materials;

namespace PackageBuilder.Domain.Tests.Materials;

[Trait("Task", "PB-0104")]
public sealed class MaterialComponentTests
{
    [Theory]
    [InlineData(0d, 0d, 0d, 0d)]
    [InlineData(0.25d, 1d, 4d, 2d)]
    [InlineData(double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue)]
    public void EmissionAcceptsNonNegativeFiniteHdrValues(
        double red,
        double green,
        double blue,
        double intensity)
    {
        EmissionProperties emission = MaterialTestAssertions.AssertSuccess(
            EmissionProperties.Create(red, green, blue, intensity));

        Assert.Equal(red, emission.Red);
        Assert.Equal(green, emission.Green);
        Assert.Equal(blue, emission.Blue);
        Assert.Equal(intensity, emission.Intensity);
    }

    [Theory]
    [InlineData(0, -1d)]
    [InlineData(0, double.NaN)]
    [InlineData(0, double.PositiveInfinity)]
    [InlineData(0, double.NegativeInfinity)]
    [InlineData(1, -1d)]
    [InlineData(1, double.NaN)]
    [InlineData(1, double.PositiveInfinity)]
    [InlineData(1, double.NegativeInfinity)]
    [InlineData(2, -1d)]
    [InlineData(2, double.NaN)]
    [InlineData(2, double.PositiveInfinity)]
    [InlineData(2, double.NegativeInfinity)]
    [InlineData(3, -1d)]
    [InlineData(3, double.NaN)]
    [InlineData(3, double.PositiveInfinity)]
    [InlineData(3, double.NegativeInfinity)]
    public void EmissionRejectsNegativeAndNonFiniteValues(int component, double invalid)
    {
        double[] values = [1d, 1d, 1d, 1d];
        values[component] = invalid;
        EmissionPropertiesValidationError[] errors =
        [
            EmissionPropertiesValidationError.InvalidRed,
            EmissionPropertiesValidationError.InvalidGreen,
            EmissionPropertiesValidationError.InvalidBlue,
            EmissionPropertiesValidationError.InvalidIntensity,
        ];

        MaterialTestAssertions.AssertFailure(
            EmissionProperties.Create(values[0], values[1], values[2], values[3]),
            errors[component]);
    }

    [Theory]
    [InlineData(0d, 0d, 0d, 0d)]
    [InlineData(-2d, 3d, -4d, 5d)]
    [InlineData(double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue)]
    public void UvTransformAcceptsAnyFiniteScaleAndOffset(
        double scaleU,
        double scaleV,
        double offsetU,
        double offsetV)
    {
        UvTransform transform = MaterialTestAssertions.AssertSuccess(
            UvTransform.Create(scaleU, scaleV, offsetU, offsetV));

        Assert.Equal(scaleU, transform.ScaleU);
        Assert.Equal(scaleV, transform.ScaleV);
        Assert.Equal(offsetU, transform.OffsetU);
        Assert.Equal(offsetV, transform.OffsetV);
    }

    [Theory]
    [InlineData(0, double.NaN)]
    [InlineData(0, double.PositiveInfinity)]
    [InlineData(0, double.NegativeInfinity)]
    [InlineData(1, double.NaN)]
    [InlineData(1, double.PositiveInfinity)]
    [InlineData(1, double.NegativeInfinity)]
    [InlineData(2, double.NaN)]
    [InlineData(2, double.PositiveInfinity)]
    [InlineData(2, double.NegativeInfinity)]
    [InlineData(3, double.NaN)]
    [InlineData(3, double.PositiveInfinity)]
    [InlineData(3, double.NegativeInfinity)]
    public void UvTransformRejectsEveryNonFiniteComponent(int component, double invalid)
    {
        double[] values = [1d, 1d, 0d, 0d];
        values[component] = invalid;
        UvTransformValidationError[] errors =
        [
            UvTransformValidationError.ScaleUNotFinite,
            UvTransformValidationError.ScaleVNotFinite,
            UvTransformValidationError.OffsetUNotFinite,
            UvTransformValidationError.OffsetVNotFinite,
        ];

        MaterialTestAssertions.AssertFailure(
            UvTransform.Create(values[0], values[1], values[2], values[3]),
            errors[component]);
    }

    [Fact]
    public void ComponentsAreImmutableValueObjectsWithStableSignedZeroHashing()
    {
        EmissionProperties emission = CreateEmission(0d, 2d, 3d, 4d);
        EmissionProperties sameEmission = CreateEmission(-0d, 2d, 3d, 4d);
        EmissionProperties differentEmission = CreateEmission(0d, 2d, 3d, 5d);
        UvTransform transform = CreateUv(0d, -2d, 3d, 4d);
        UvTransform sameTransform = CreateUv(-0d, -2d, 3d, 4d);
        UvTransform differentTransform = CreateUv(0d, -2d, 3d, 5d);

        Assert.True(emission.Equals(sameEmission));
        Assert.True(emission.Equals((object)sameEmission));
        Assert.False(emission.Equals(CreateEmission(1d, 2d, 3d, 4d)));
        Assert.False(emission.Equals(CreateEmission(0d, 1d, 3d, 4d)));
        Assert.False(emission.Equals(CreateEmission(0d, 2d, 2d, 4d)));
        Assert.False(emission.Equals(differentEmission));
        Assert.False(emission.Equals((EmissionProperties?)null));
        Assert.False(emission.Equals("emission"));
        Assert.Equal(emission.GetHashCode(), sameEmission.GetHashCode());

        Assert.True(transform.Equals(sameTransform));
        Assert.True(transform.Equals((object)sameTransform));
        Assert.False(transform.Equals(CreateUv(1d, -2d, 3d, 4d)));
        Assert.False(transform.Equals(CreateUv(0d, -1d, 3d, 4d)));
        Assert.False(transform.Equals(CreateUv(0d, -2d, 2d, 4d)));
        Assert.False(transform.Equals(differentTransform));
        Assert.False(transform.Equals((UvTransform?)null));
        Assert.False(transform.Equals("uv"));
        Assert.Equal(transform.GetHashCode(), sameTransform.GetHashCode());
    }

    [Fact]
    public void ComponentEqualityAndHashingAreCultureIndependent()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        EmissionProperties emission = CreateEmission(1.25d, 2.5d, 3.75d, 4.125d);
        UvTransform uv = CreateUv(-1.25d, 2.5d, -3.75d, 4.125d);
        int emissionHash = emission.GetHashCode();
        int uvHash = uv.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(
                emissionHash,
                CreateEmission(1.25d, 2.5d, 3.75d, 4.125d).GetHashCode());
            Assert.Equal(uvHash, CreateUv(-1.25d, 2.5d, -3.75d, 4.125d).GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static EmissionProperties CreateEmission(
        double red,
        double green,
        double blue,
        double intensity) =>
        MaterialTestAssertions.AssertSuccess(
            EmissionProperties.Create(red, green, blue, intensity));

    private static UvTransform CreateUv(
        double scaleU,
        double scaleV,
        double offsetU,
        double offsetV) =>
        MaterialTestAssertions.AssertSuccess(
            UvTransform.Create(scaleU, scaleV, offsetU, offsetV));
}
