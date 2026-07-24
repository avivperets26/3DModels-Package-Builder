namespace PackageBuilder.Domain.Materials;

/// <summary>Represents renderer-independent linear emission colour and intensity.</summary>
public sealed class EmissionProperties : IEquatable<EmissionProperties>
{
    private EmissionProperties(double red, double green, double blue, double intensity)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Intensity = intensity;
    }

    /// <summary>Gets the non-negative linear red component.</summary>
    public double Red { get; }

    /// <summary>Gets the non-negative linear green component.</summary>
    public double Green { get; }

    /// <summary>Gets the non-negative linear blue component.</summary>
    public double Blue { get; }

    /// <summary>Gets the non-negative emission intensity.</summary>
    public double Intensity { get; }

    /// <summary>
    /// Creates emission properties. No upper bound is imposed because HDR colour and
    /// intensity are valid renderer-independent inputs.
    /// </summary>
    public static EmissionPropertiesValidationResult Create(
        double red,
        double green,
        double blue,
        double intensity)
    {
        return !IsNonNegativeFinite(red)
            ? EmissionPropertiesValidationResult.Failure(
                EmissionPropertiesValidationError.InvalidRed)
            : !IsNonNegativeFinite(green)
            ? EmissionPropertiesValidationResult.Failure(
                EmissionPropertiesValidationError.InvalidGreen)
            : !IsNonNegativeFinite(blue)
            ? EmissionPropertiesValidationResult.Failure(
                EmissionPropertiesValidationError.InvalidBlue)
            : !IsNonNegativeFinite(intensity)
            ? EmissionPropertiesValidationResult.Failure(
                EmissionPropertiesValidationError.InvalidIntensity)
            : EmissionPropertiesValidationResult.Success(
                new EmissionProperties(red, green, blue, intensity));
    }

    /// <inheritdoc />
    public bool Equals(EmissionProperties? other) =>
        other is not null &&
        Red.Equals(other.Red) &&
        Green.Equals(other.Green) &&
        Blue.Equals(other.Blue) &&
        Intensity.Equals(other.Intensity);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is EmissionProperties other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StableMaterialHash.Create()
            .Add(Red)
            .Add(Green)
            .Add(Blue)
            .Add(Intensity)
            .ToHashCode();

    private static bool IsNonNegativeFinite(double value) =>
        double.IsFinite(value) && value >= 0d;
}
