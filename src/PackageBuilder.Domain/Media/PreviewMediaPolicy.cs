namespace PackageBuilder.Domain.Media;

/// <summary>Versioned caller-supplied marketplace limits in bytes, exclusive at both boundaries.
/// Quality thresholds bound whole-image and worst 32x32-tile normalized RGB RMS error.</summary>
public sealed record PreviewMediaPolicy(string Version, long ImageByteLimit, long GalleryByteLimit,
    double MaximumRmsError = .02, double MaximumTileRmsError = .06)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Version) && Version.Length <= 64 &&
        ImageByteLimit is > 0 and <= 32_000_000 && GalleryByteLimit is > 0 and <= 256_000_000 &&
        MaximumRmsError is >= 0 and <= .1 && MaximumTileRmsError is >= 0 and <= .2;
}
