namespace PackageBuilder.Domain.Materials;

/// <summary>Represents a renderer-independent two-dimensional UV scale and offset.</summary>
public sealed class UvTransform : IEquatable<UvTransform>
{
    private UvTransform(double scaleU, double scaleV, double offsetU, double offsetV)
    {
        ScaleU = scaleU;
        ScaleV = scaleV;
        OffsetU = offsetU;
        OffsetV = offsetV;
    }

    /// <summary>Gets the horizontal UV scale.</summary>
    public double ScaleU { get; }

    /// <summary>Gets the vertical UV scale.</summary>
    public double ScaleV { get; }

    /// <summary>Gets the horizontal UV offset.</summary>
    public double OffsetU { get; }

    /// <summary>Gets the vertical UV offset.</summary>
    public double OffsetV { get; }

    /// <summary>
    /// Creates a UV transform. Zero and negative scale are retained because mirroring and
    /// deliberate coordinate collapse are renderer-independent intents rather than errors.
    /// </summary>
    public static UvTransformValidationResult Create(
        double scaleU,
        double scaleV,
        double offsetU,
        double offsetV)
    {
        return !double.IsFinite(scaleU)
            ? UvTransformValidationResult.Failure(
                UvTransformValidationError.ScaleUNotFinite)
            : !double.IsFinite(scaleV)
            ? UvTransformValidationResult.Failure(
                UvTransformValidationError.ScaleVNotFinite)
            : !double.IsFinite(offsetU)
            ? UvTransformValidationResult.Failure(
                UvTransformValidationError.OffsetUNotFinite)
            : !double.IsFinite(offsetV)
            ? UvTransformValidationResult.Failure(UvTransformValidationError.OffsetVNotFinite)
            : UvTransformValidationResult.Success(
                new UvTransform(scaleU, scaleV, offsetU, offsetV));
    }

    /// <inheritdoc />
    public bool Equals(UvTransform? other) =>
        other is not null &&
        ScaleU.Equals(other.ScaleU) &&
        ScaleV.Equals(other.ScaleV) &&
        OffsetU.Equals(other.OffsetU) &&
        OffsetV.Equals(other.OffsetV);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UvTransform other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StableMaterialHash.Create()
            .Add(ScaleU)
            .Add(ScaleV)
            .Add(OffsetU)
            .Add(OffsetV)
            .ToHashCode();
}
