namespace PackageBuilder.Domain.Preview;

/// <summary>Tracks preview-only key-light direction while leaving approved intensity and colour unchanged.</summary>
public sealed record PreviewLightState(double YawDegrees, double PitchDegrees)
{
    /// <summary>Creates state from the PB-0906 approved key light.</summary>
    public static PreviewLightState Reset(PreviewLighting lighting)
    {
        ArgumentNullException.ThrowIfNull(lighting);
        return new(lighting.KeyLight.YawDegrees, lighting.KeyLight.PitchDegrees);
    }

    /// <summary>Applies bounded direction deltas; non-finite input is a safe no-op.</summary>
    public PreviewLightState Adjust(
        double yawDeltaDegrees,
        double pitchDeltaDegrees,
        PreviewLightControlPolicy policy)
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
