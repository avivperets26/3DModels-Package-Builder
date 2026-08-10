namespace PackageBuilder.Domain.Preview;

/// <summary>
/// Defines a seamless, horizon-free radial studio background independently of renderer assets.
/// </summary>
public sealed class PreviewBackground : IEquatable<PreviewBackground>
{
    private PreviewBackground(
        PreviewColour outerColour,
        PreviewColour centreColour,
        double centreX,
        double centreY,
        double radius,
        double horizontalScale)
    {
        OuterColour = outerColour;
        CentreColour = centreColour;
        CentreX = centreX;
        CentreY = centreY;
        Radius = radius;
        HorizontalScale = horizontalScale;
    }

    /// <summary>Gets the near-black colour at the image perimeter.</summary>
    public PreviewColour OuterColour { get; }

    /// <summary>Gets the brighter neutral colour behind the product.</summary>
    public PreviewColour CentreColour { get; }

    /// <summary>Gets the normalized horizontal radial centre.</summary>
    public double CentreX { get; }

    /// <summary>Gets the normalized vertical radial centre.</summary>
    public double CentreY { get; }

    /// <summary>Gets the positive normalized radial falloff radius.</summary>
    public double Radius { get; }

    /// <summary>Gets the positive horizontal distance scale used by the radial falloff.</summary>
    public double HorizontalScale { get; }

    /// <summary>
    /// Creates radial background intent. Centre coordinates use normalized image space; radius and
    /// horizontal scale must be positive and finite.
    /// </summary>
    public static PreviewPresentationValidationResult<PreviewBackground> Create(
        PreviewColour? outerColour,
        PreviewColour? centreColour,
        double centreX,
        double centreY,
        double radius,
        double horizontalScale)
    {
        if (outerColour is null)
        {
            return PreviewPresentationValidationResult<PreviewBackground>.Failure(
                PreviewPresentationValidationError.NullOuterColour);
        }

        if (centreColour is null)
        {
            return PreviewPresentationValidationResult<PreviewBackground>.Failure(
                PreviewPresentationValidationError.NullCentreColour);
        }

        PreviewPresentationValidationError error = !IsUnit(centreX)
            ? PreviewPresentationValidationError.BackgroundCentreXOutsideUnitInterval
            : !IsUnit(centreY)
            ? PreviewPresentationValidationError.BackgroundCentreYOutsideUnitInterval
            : !IsPositiveFinite(radius)
            ? PreviewPresentationValidationError.BackgroundRadiusNotPositiveFinite
            : !IsPositiveFinite(horizontalScale)
            ? PreviewPresentationValidationError.BackgroundHorizontalScaleNotPositiveFinite
            : PreviewPresentationValidationError.None;
        return error == PreviewPresentationValidationError.None
            ? PreviewPresentationValidationResult<PreviewBackground>.Success(
                new PreviewBackground(
                    outerColour,
                    centreColour,
                    centreX,
                    centreY,
                    radius,
                    horizontalScale))
            : PreviewPresentationValidationResult<PreviewBackground>.Failure(error);
    }

    /// <inheritdoc />
    public bool Equals(PreviewBackground? other) =>
        other is not null && OuterColour.Equals(other.OuterColour) &&
        CentreColour.Equals(other.CentreColour) && CentreX.Equals(other.CentreX) &&
        CentreY.Equals(other.CentreY) && Radius.Equals(other.Radius) &&
        HorizontalScale.Equals(other.HorizontalScale);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PreviewBackground other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StablePreviewHash.Create()
            .Add(OuterColour.GetHashCode())
            .Add(CentreColour.GetHashCode())
            .Add(CentreX)
            .Add(CentreY)
            .Add(Radius)
            .Add(HorizontalScale)
            .ToHashCode();

    private static bool IsUnit(double value) => double.IsFinite(value) && value >= 0d && value <= 1d;

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0d;
}
