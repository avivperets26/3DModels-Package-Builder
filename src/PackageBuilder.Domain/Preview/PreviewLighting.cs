namespace PackageBuilder.Domain.Preview;

/// <summary>Defines one renderer-independent directional studio light.</summary>
public sealed class PreviewDirectionalLight : IEquatable<PreviewDirectionalLight>
{
    private PreviewDirectionalLight(
        double yawDegrees,
        double pitchDegrees,
        double intensity,
        PreviewColour colour)
    {
        YawDegrees = yawDegrees;
        PitchDegrees = pitchDegrees;
        Intensity = intensity;
        Colour = colour;
    }

    /// <summary>Gets canonical yaw in degrees.</summary>
    public double YawDegrees { get; }

    /// <summary>Gets pitch in degrees.</summary>
    public double PitchDegrees { get; }

    /// <summary>Gets the non-negative renderer-relative light intensity.</summary>
    public double Intensity { get; }

    /// <summary>Gets the normalized light colour.</summary>
    public PreviewColour Colour { get; }

    /// <summary>
    /// Creates a directional light with canonical yaw [-180, 180], pitch [-90, 90], and a finite
    /// non-negative renderer-relative intensity.
    /// </summary>
    public static PreviewPresentationValidationResult<PreviewDirectionalLight> Create(
        double yawDegrees,
        double pitchDegrees,
        double intensity,
        PreviewColour? colour)
    {
        if (colour is null)
        {
            return PreviewPresentationValidationResult<PreviewDirectionalLight>.Failure(
                PreviewPresentationValidationError.NullLightColour);
        }

        PreviewPresentationValidationError error =
            !double.IsFinite(yawDegrees) || yawDegrees < -180d || yawDegrees > 180d
            ? PreviewPresentationValidationError.LightYawOutsideRange
            : !double.IsFinite(pitchDegrees) || pitchDegrees < -90d || pitchDegrees > 90d
            ? PreviewPresentationValidationError.LightPitchOutsideRange
            : !double.IsFinite(intensity) || intensity < 0d
            ? PreviewPresentationValidationError.LightIntensityNegativeOrNotFinite
            : PreviewPresentationValidationError.None;
        return error == PreviewPresentationValidationError.None
            ? PreviewPresentationValidationResult<PreviewDirectionalLight>.Success(
                new PreviewDirectionalLight(yawDegrees, pitchDegrees, intensity, colour))
            : PreviewPresentationValidationResult<PreviewDirectionalLight>.Failure(error);
    }

    /// <inheritdoc />
    public bool Equals(PreviewDirectionalLight? other) =>
        other is not null && YawDegrees.Equals(other.YawDegrees) &&
        PitchDegrees.Equals(other.PitchDegrees) && Intensity.Equals(other.Intensity) &&
        Colour.Equals(other.Colour);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PreviewDirectionalLight other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StablePreviewHash.Create()
            .Add(YawDegrees)
            .Add(PitchDegrees)
            .Add(Intensity)
            .Add(Colour.GetHashCode())
            .ToHashCode();
}

/// <summary>Pairs the required neutral key and fill lights for a preview presentation.</summary>
public sealed class PreviewLighting : IEquatable<PreviewLighting>
{
    private PreviewLighting(PreviewDirectionalLight keyLight, PreviewDirectionalLight fillLight)
    {
        KeyLight = keyLight;
        FillLight = fillLight;
    }

    /// <summary>Gets the primary form-defining light.</summary>
    public PreviewDirectionalLight KeyLight { get; }

    /// <summary>Gets the lower-intensity fill light.</summary>
    public PreviewDirectionalLight FillLight { get; }

    /// <summary>Creates required key/fill lighting without renderer dependencies.</summary>
    public static PreviewPresentationValidationResult<PreviewLighting> Create(
        PreviewDirectionalLight? keyLight,
        PreviewDirectionalLight? fillLight) =>
        keyLight is null
            ? PreviewPresentationValidationResult<PreviewLighting>.Failure(
                PreviewPresentationValidationError.NullKeyLight)
            : fillLight is null
            ? PreviewPresentationValidationResult<PreviewLighting>.Failure(
                PreviewPresentationValidationError.NullFillLight)
            : PreviewPresentationValidationResult<PreviewLighting>.Success(
                new PreviewLighting(keyLight, fillLight));

    /// <inheritdoc />
    public bool Equals(PreviewLighting? other) =>
        other is not null && KeyLight.Equals(other.KeyLight) && FillLight.Equals(other.FillLight);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PreviewLighting other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StablePreviewHash.Create()
            .Add(KeyLight.GetHashCode())
            .Add(FillLight.GetHashCode())
            .ToHashCode();
}
