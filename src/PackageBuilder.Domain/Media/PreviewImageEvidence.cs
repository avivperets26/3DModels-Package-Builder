namespace PackageBuilder.Domain.Media;

/// <summary>Renderer-provided coverage, projected bounds in normalized viewport coordinates,
/// and scene inspection counts. Coverage is measured with the studio/UI excluded, not inferred
/// from product color. All evidence must describe the same pose and camera as the final image.</summary>
public sealed class PreviewImageEvidence
{
    private readonly byte[] _coverage;

    public PreviewImageEvidence(ReadOnlySpan<byte> coverage, double left, double bottom,
        double right, double top, bool depthClipped, int missingMaterials, int visibleHelpers)
    {
        if (coverage.Length > PreviewRaster.MaximumPixels ||
            !double.IsFinite(left) || !double.IsFinite(bottom) || !double.IsFinite(right) ||
            !double.IsFinite(top) || right < left || top < bottom || missingMaterials < 0 || visibleHelpers < 0)
        {
            throw new ArgumentException("Invalid render evidence.", nameof(coverage));
        }
        _coverage = coverage.ToArray();
        Left = left;
        Bottom = bottom;
        Right = right;
        Top = top;
        DepthClipped = depthClipped;
        MissingMaterials = missingMaterials;
        VisibleHelpers = visibleHelpers;
    }

    public ReadOnlySpan<byte> Coverage => _coverage;
    public double Left { get; }
    public double Bottom { get; }
    public double Right { get; }
    public double Top { get; }
    public bool DepthClipped { get; }
    public int MissingMaterials { get; }
    public int VisibleHelpers { get; }
}

/// <summary>Version-one quality tolerances. Exposure is the fraction of foreground pixels
/// whose sRGB luminance lies outside the configured range; margins use the visible bounding box.</summary>
public sealed record PreviewImagePolicy(double MinimumCoverage = .01, double MaximumMargin = .35,
    double DarkLuminance = .02, double BrightLuminance = .98, double MaximumExposureFraction = .9)
{
    public bool IsValid => MinimumCoverage is > 0 and <= 1 && MaximumMargin is >= 0 and < .5 &&
        DarkLuminance is >= 0 and < 1 && BrightLuminance is > 0 and <= 1 &&
        DarkLuminance < BrightLuminance && MaximumExposureFraction is > 0 and <= 1;
}

/// <summary>Measured image fractions; no product/background classification is guessed.</summary>
public sealed record PreviewImageMetrics(double Coverage, double DarkFraction, double BrightFraction,
    double MaximumMargin);
