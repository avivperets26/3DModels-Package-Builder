namespace PackageBuilder.Domain.Preview;

/// <summary>Tracks bounded camera orbit and distance without modifying product transforms.</summary>
public sealed record PreviewCameraState(double YawDegrees, double PitchDegrees, double DistanceMultiplier)
{
    /// <summary>Creates the neutral reset state.</summary>
    public static PreviewCameraState Reset() => new(0d, 0d, 1d);

    /// <summary>Applies orbit deltas and clamps pitch to the shared navigation policy.</summary>
    public PreviewCameraState Orbit(
        double yawDeltaDegrees,
        double pitchDeltaDegrees,
        PreviewNavigationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return !double.IsFinite(yawDeltaDegrees) || !double.IsFinite(pitchDeltaDegrees)
            ? this
            : this with
            {
                YawDegrees = CanonicalYaw(YawDegrees + yawDeltaDegrees),
                PitchDegrees = Math.Clamp(
                    PitchDegrees + pitchDeltaDegrees,
                    policy.MinimumPitchDegrees,
                    policy.MaximumPitchDegrees),
            };
    }

    /// <summary>
    /// Applies signed zoom steps to camera distance; positive steps zoom in and product scale is
    /// intentionally absent from this state.
    /// </summary>
    public PreviewCameraState Zoom(
        double signedSteps,
        double fractionalStep,
        PreviewNavigationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!double.IsFinite(signedSteps) || !double.IsFinite(fractionalStep) ||
            fractionalStep <= 0d || fractionalStep >= 1d)
        {
            return this;
        }

        double factor = Math.Pow(1d - fractionalStep, signedSteps);
        double distance = DistanceMultiplier * factor;
        return this with
        {
            DistanceMultiplier = Math.Clamp(
                distance,
                policy.MinimumDistanceMultiplier,
                policy.MaximumDistanceMultiplier),
        };
    }

    private static double CanonicalYaw(double yawDegrees)
    {
        double canonical = (yawDegrees + 180d) % 360d;
        if (canonical < 0d)
        {
            canonical += 360d;
        }

        return canonical - 180d;
    }
}
