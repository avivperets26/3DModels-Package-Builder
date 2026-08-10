namespace PackageBuilder.Domain.Preview;

/// <summary>Represents an engine-neutral normalized RGB colour.</summary>
public sealed class PreviewColour : IEquatable<PreviewColour>
{
    private PreviewColour(double red, double green, double blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    /// <summary>Gets the normalized red channel.</summary>
    public double Red { get; }

    /// <summary>Gets the normalized green channel.</summary>
    public double Green { get; }

    /// <summary>Gets the normalized blue channel.</summary>
    public double Blue { get; }

    /// <summary>Creates a colour whose finite channels are in the inclusive unit interval.</summary>
    public static PreviewPresentationValidationResult<PreviewColour> Create(
        double red,
        double green,
        double blue)
    {
        PreviewPresentationValidationError error = !IsUnit(red)
            ? PreviewPresentationValidationError.ColourRedOutsideUnitInterval
            : !IsUnit(green)
            ? PreviewPresentationValidationError.ColourGreenOutsideUnitInterval
            : !IsUnit(blue)
            ? PreviewPresentationValidationError.ColourBlueOutsideUnitInterval
            : PreviewPresentationValidationError.None;
        return error == PreviewPresentationValidationError.None
            ? PreviewPresentationValidationResult<PreviewColour>.Success(
                new PreviewColour(red, green, blue))
            : PreviewPresentationValidationResult<PreviewColour>.Failure(error);
    }

    /// <inheritdoc />
    public bool Equals(PreviewColour? other) =>
        other is not null && Red.Equals(other.Red) && Green.Equals(other.Green) &&
        Blue.Equals(other.Blue);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PreviewColour other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StablePreviewHash.Create().Add(Red).Add(Green).Add(Blue).ToHashCode();

    private static bool IsUnit(double value) => double.IsFinite(value) && value >= 0d && value <= 1d;
}
