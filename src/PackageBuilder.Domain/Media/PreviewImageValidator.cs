using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Domain.Media;

/// <summary>Pure image-quality checks shared by engine adapters. Invalid/missing evidence blocks release.</summary>
public static class PreviewImageValidator
{
    public static PreviewImageValidation Validate(PreviewRaster image, PreviewImageEvidence? evidence,
        BuildArtifactId artifactId, PreviewImagePolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(artifactId);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();
        var findings = new List<ValidationFinding>();
        if (!policy.IsValid || evidence is null || evidence.Coverage.Length != image.Width * image.Height)
        {
            Add("MEDIA_EVIDENCE_INVALID", "Image evidence or quality policy is incomplete.", "Recapture with complete renderer evidence.");
            return new(null, findings.AsReadOnly());
        }
        if (image.Width != PreviewRaster.CaptureWidth || image.Height != PreviewRaster.CaptureHeight)
        {
            Add("MEDIA_DIMENSIONS_INVALID", "Preview dimensions must be 1920 by 1080.", "Render at the required resolution.");
        }
        int foreground = 0, dark = 0, bright = 0;
        int minX = image.Width, minY = image.Height, maxX = -1, maxY = -1;
        bool edge = false;
        for (int y = 0; y < image.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < image.Width; x++)
            {
                int pixel = y * image.Width + x;
                if (evidence.Coverage[pixel] < 16 || image.Pixels[pixel * 4 + 3] < 16)
                { continue; }
                foreground++;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
                edge |= x == 0 || y == 0 || x == image.Width - 1 || y == image.Height - 1;
                double luminance = (.2126 * image.Pixels[pixel * 4] + .7152 * image.Pixels[pixel * 4 + 1] +
                    .0722 * image.Pixels[pixel * 4 + 2]) / 255;
                if (luminance <= policy.DarkLuminance)
                { dark++; }
                if (luminance >= policy.BrightLuminance)
                { bright++; }
            }
        }
        double coverage = (double)foreground / (image.Width * image.Height);
        double darkFraction = foreground == 0 ? 0 : (double)dark / foreground;
        double brightFraction = foreground == 0 ? 0 : (double)bright / foreground;
        double margin = foreground == 0 ? 1 : Math.Max(
            Math.Max((double)minX / image.Width, (double)(image.Width - maxX - 1) / image.Width),
            Math.Max((double)minY / image.Height, (double)(image.Height - maxY - 1) / image.Height));
        if (foreground == 0)
        { Add("MEDIA_EMPTY", "No visible product pixels were rendered.", "Check product visibility and camera selection."); }
        else
        {
            if (coverage < policy.MinimumCoverage)
            { Add("MEDIA_TINY", "The product occupies too little of the image.", "Move the camera closer or reframe the product."); }
            if (margin > policy.MaximumMargin)
            { Add("MEDIA_EXCESSIVE_MARGIN", "The image has excessive empty or transparent margins.", "Center and reframe the product."); }
            if (darkFraction >= policy.MaximumExposureFraction)
            { Add("MEDIA_UNDEREXPOSED", "Most product pixels are underexposed.", "Adjust lighting or exposure and inspect the result."); }
            if (brightFraction >= policy.MaximumExposureFraction)
            { Add("MEDIA_OVEREXPOSED", "Most product pixels are overexposed.", "Reduce lighting or exposure and inspect the result."); }
        }
        if (edge || evidence.Left < 0 || evidence.Bottom < 0 || evidence.Right > 1 || evidence.Top > 1 || evidence.DepthClipped)
        { Add("MEDIA_CLIPPED", "Product bounds or visible pixels intersect the capture boundary.", "Fit the entire product inside the camera frustum."); }
        if (evidence.MissingMaterials > 0)
        { Add("MEDIA_MISSING_MATERIAL", "Renderer inspection found missing or unsupported materials.", "Repair materials before recapturing."); }
        if (evidence.VisibleHelpers > 0)
        { Add("MEDIA_HELPER_VISIBLE", "Renderer inspection found visible helper geometry or UI.", "Hide helpers and controls before recapturing."); }
        return new(new(coverage, darkFraction, brightFraction, margin), findings.AsReadOnly());

        void Add(string code, string explanation, string action) => findings.Add(ValidationFinding.Create(
            FindingCode.Create(code).Value, FindingSeverity.Error, FindingExplanation.Create(explanation).Value,
            FindingSourceComponent.Create("media-validator").Value, artifactId, CorrectiveAction.Create(action).Value, true).Value!);
    }
}

/// <summary>Measured values and release-blocking findings in stable check order.</summary>
public sealed record PreviewImageValidation(PreviewImageMetrics? Metrics, IReadOnlyList<ValidationFinding> Findings)
{
    public bool IsValid => !Findings.Any(finding => finding.BlocksRelease);
}
