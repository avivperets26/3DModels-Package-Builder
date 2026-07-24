using System.Globalization;
using PackageBuilder.Domain.Rigging;

namespace PackageBuilder.Domain.Tests.Rigging;

[Trait("Task", "PB-0105")]
public sealed class RigTransformTests
{
    [Theory]
    [InlineData(0d, 0d, 0d, 1d, 1d, 1d)]
    [InlineData(-double.MaxValue, double.MaxValue, -1d, -2d, 0d, 3d)]
    public void TransformPreservesEveryFiniteSignedTranslationAndScale(
        double tx,
        double ty,
        double tz,
        double sx,
        double sy,
        double sz)
    {
        RigTransform transform = RigTestAssertions.CreateTransform(
            tx,
            ty,
            tz,
            0d,
            0d,
            0d,
            1d,
            sx,
            sy,
            sz);

        Assert.Equal(tx, transform.TranslationX);
        Assert.Equal(ty, transform.TranslationY);
        Assert.Equal(tz, transform.TranslationZ);
        Assert.Equal(sx, transform.ScaleX);
        Assert.Equal(sy, transform.ScaleY);
        Assert.Equal(sz, transform.ScaleZ);
    }

    [Theory]
    [InlineData(0d, 0d, 0d, 2d, 0d, 0d, 0d, 1d)]
    [InlineData(-2d, 0d, 0d, 0d, 1d, 0d, 0d, 0d)]
    [InlineData(2d, 0d, 0d, 0d, 1d, 0d, 0d, 0d)]
    [InlineData(0d, -2d, 0d, 0d, 0d, 1d, 0d, 0d)]
    [InlineData(0d, 2d, 0d, 0d, 0d, 1d, 0d, 0d)]
    [InlineData(0d, 0d, -2d, 0d, 0d, 0d, 1d, 0d)]
    [InlineData(0d, 0d, 2d, 0d, 0d, 0d, 1d, 0d)]
    [InlineData(0d, 0d, 0d, -2d, 0d, 0d, 0d, 1d)]
    [InlineData(1d, 2d, 3d, 4d, 0.18257418583505536d, 0.3651483716701107d, 0.5477225575051661d, 0.7302967433402214d)]
    public void RotationIsNormalizedAndSignCanonicalized(
        double x,
        double y,
        double z,
        double w,
        double expectedX,
        double expectedY,
        double expectedZ,
        double expectedW)
    {
        RigTransform transform = RigTestAssertions.CreateTransform(
            rotationX: x,
            rotationY: y,
            rotationZ: z,
            rotationW: w);

        Assert.Equal(expectedX, transform.RotationX, 14);
        Assert.Equal(expectedY, transform.RotationY, 14);
        Assert.Equal(expectedZ, transform.RotationZ, 14);
        Assert.Equal(expectedW, transform.RotationW, 14);
    }

    [Theory]
    [InlineData(double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue)]
    [InlineData(double.Epsilon, double.Epsilon, double.Epsilon, double.Epsilon)]
    public void RotationNormalizationHandlesFiniteNumericBoundaries(
        double x,
        double y,
        double z,
        double w)
    {
        RigTransform transform = RigTestAssertions.CreateTransform(
            rotationX: x,
            rotationY: y,
            rotationZ: z,
            rotationW: w);

        Assert.Equal(1d, Math.Sqrt(
            (transform.RotationX * transform.RotationX) +
            (transform.RotationY * transform.RotationY) +
            (transform.RotationZ * transform.RotationZ) +
            (transform.RotationW * transform.RotationW)), 14);
    }

    [Theory]
    [MemberData(nameof(NonFiniteComponents))]
    public void TransformRejectsNaNAndBothInfinitiesInEveryComponent(
        int component,
        double invalid)
    {
        double[] values = [0d, 0d, 0d, 0d, 0d, 0d, 1d, 1d, 1d, 1d];
        values[component] = invalid;

        RigTestAssertions.AssertFailure(
            RigTransform.Create(
                values[0],
                values[1],
                values[2],
                values[3],
                values[4],
                values[5],
                values[6],
                values[7],
                values[8],
                values[9]),
            (RigTransformValidationError)(component + 1));
    }

    public static TheoryData<int, double> NonFiniteComponents
    {
        get
        {
            var data = new TheoryData<int, double>();
            double[] values =
            [
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            ];
            for (int component = 0; component < 10; component++)
            {
                foreach (double value in values)
                {
                    data.Add(component, value);
                }
            }

            return data;
        }
    }

    [Fact]
    public void TransformRejectsZeroLengthRotation() =>
        RigTestAssertions.AssertFailure(
            RigTransform.Create(0d, 0d, 0d, 0d, 0d, 0d, 0d, 1d, 1d, 1d),
            RigTransformValidationError.ZeroLengthRotation);

    [Fact]
    public void EqualityHashingAndSignedZeroAreDeterministicAndCultureIndependent()
    {
        RigTransform first = RigTestAssertions.CreateTransform(
            -0d,
            2d,
            3d,
            1d,
            2d,
            3d,
            4d,
            -0d,
            6d,
            7d);
        RigTransform same = RigTestAssertions.CreateTransform(
            0d,
            2d,
            3d,
            -1d,
            -2d,
            -3d,
            -4d,
            0d,
            6d,
            7d);
        CultureInfo previous = CultureInfo.CurrentCulture;
        int hash = first.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.True(first.Equals(same));
            Assert.True(first.Equals((object)same));
            Assert.False(first.Equals((RigTransform?)null));
            Assert.False(first.Equals("transform"));
            Assert.Equal(hash, same.GetHashCode());

            for (int component = 0; component < 10; component++)
            {
                Assert.False(first.Equals(CreateDifferent(first, component)));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static RigTransform CreateDifferent(RigTransform value, int component)
    {
        double[] values =
        [
            value.TranslationX,
            value.TranslationY,
            value.TranslationZ,
            value.RotationX,
            value.RotationY,
            value.RotationZ,
            value.RotationW,
            value.ScaleX,
            value.ScaleY,
            value.ScaleZ,
        ];
        values[component] += component is >= 3 and <= 6 ? 0.1d : 1d;
        return RigTestAssertions.CreateTransform(
            values[0],
            values[1],
            values[2],
            values[3],
            values[4],
            values[5],
            values[6],
            values[7],
            values[8],
            values[9]);
    }
}
