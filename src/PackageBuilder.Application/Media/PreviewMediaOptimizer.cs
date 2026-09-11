using System.Security.Cryptography;
using PackageBuilder.Domain.Media;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Application.Media;

/// <summary>Validates captures, chooses the smallest quality-approved encoding, then enforces the
/// gallery budget atomically. No resizing, alpha flattening, file writes, or source mutations.</summary>
public sealed class PreviewMediaOptimizer(IPreviewImageCodec codec)
{
    private readonly IPreviewImageCodec _codec = codec ?? throw new ArgumentNullException(nameof(codec));
    private static readonly int[] _jpegQualities = [95, 90, 85, 80, 75];

    /// <summary>Processes one bounded gallery in input order; cancellation propagates and blockers suppress all outputs.</summary>
    public PreviewMediaResult Optimize(IReadOnlyList<PreviewMediaInput> captures, PreviewImagePolicy imagePolicy,
        PreviewMediaPolicy mediaPolicy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(captures);
        ArgumentNullException.ThrowIfNull(imagePolicy);
        ArgumentNullException.ThrowIfNull(mediaPolicy);
        cancellationToken.ThrowIfCancellationRequested();
        var findings = new List<ValidationFinding>();
        var images = new List<OptimizedPreviewImage>();
        if (!mediaPolicy.IsValid || captures.Count is < 1 or > 32 || captures.Any(c => c is null || c.ArtifactId is null || c.Image is null) ||
            captures.Select(c => c.ArtifactId).Distinct().Count() != captures.Count)
        {
            Fail("MEDIA_GALLERY_INPUT_INVALID", "Gallery input or media policy is invalid.");
            return Result();
        }
        foreach (PreviewMediaInput capture in captures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreviewImageValidation validation = PreviewImageValidator.Validate(capture.Image, capture.Evidence,
                capture.ArtifactId, imagePolicy, cancellationToken);
            findings.AddRange(validation.Findings);
            if (!validation.IsValid)
            { continue; }
            OptimizedPreviewImage? best = null;
            try
            {
                TryCandidate(PreviewImageFormat.Png, 100);
                if (IsOpaque(capture.Image))
                {
                    foreach (int quality in _jpegQualities)
                    { TryCandidate(PreviewImageFormat.Jpeg, quality); }
                }
            }
            catch (InvalidDataException)
            {
                Fail("MEDIA_CODEC_FAILED", "An image could not be safely encoded and decoded.");
                continue;
            }
            if (best is null)
            { Fail("MEDIA_IMAGE_LIMIT", "No encoding meets both image size and visual quality limits."); }
            else
            { images.Add(best); }

            void TryCandidate(PreviewImageFormat format, int quality)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] bytes = _codec.Encode(capture.Image, format, quality);
                if (bytes.Length == 0 || bytes.LongLength >= mediaPolicy.ImageByteLimit ||
                    (best is not null && bytes.Length >= best.Bytes.Length))
                { return; }
                PreviewRaster decoded = _codec.Decode(bytes);
                if (decoded.Width != capture.Image.Width || decoded.Height != capture.Image.Height)
                { return; }
                (double rms, double tileRms, bool alphaEqual) = Measure(capture.Image, decoded, cancellationToken);
                if (!alphaEqual || rms > mediaPolicy.MaximumRmsError || tileRms > mediaPolicy.MaximumTileRmsError)
                { return; }
                // Revalidate the actual delivery pixels; compression cannot introduce a passing-to-failing image.
                PreviewImageValidation deliveredValidation = PreviewImageValidator.Validate(decoded, capture.Evidence, capture.ArtifactId, imagePolicy, cancellationToken);
                if (!deliveredValidation.IsValid)
                { return; }
                best = new(capture.ArtifactId, format, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)),
                    rms, tileRms, deliveredValidation.Metrics!);
            }
        }
        if (images.Sum(image => (long)image.Bytes.Length) >= mediaPolicy.GalleryByteLimit)
        { Fail("MEDIA_GALLERY_LIMIT", "The combined gallery exceeds its configured byte limit."); }
        return Result();

        void Fail(string code, string text) => findings.Add(ValidationFinding.Create(FindingCode.Create(code).Value,
            FindingSeverity.Error, FindingExplanation.Create(text).Value, FindingSourceComponent.Create("media-optimizer").Value,
            null, CorrectiveAction.Create("Review the capture and configured limits, then regenerate the gallery.").Value, true).Value!);
        PreviewMediaResult Result() => new(findings.Any(f => f.BlocksRelease) ? Array.Empty<OptimizedPreviewImage>() : images.AsReadOnly(), findings.AsReadOnly());
    }

    private static bool IsOpaque(PreviewRaster image)
    {
        for (int i = 3; i < image.Pixels.Length; i += 4)
        { if (image.Pixels[i] != 255) { return false; } }
        return true;
    }

    private static (double Rms, double TileRms, bool AlphaEqual) Measure(PreviewRaster original, PreviewRaster candidate, CancellationToken token)
    {
        double total = 0, worst = 0;
        bool alphaEqual = true;
        for (int top = 0; top < original.Height; top += 32)
        {
            token.ThrowIfCancellationRequested();
            for (int left = 0; left < original.Width; left += 32)
            {
                double sum = 0;
                int samples = 0;
                for (int y = top; y < Math.Min(top + 32, original.Height); y++)
                {
                    for (int x = left; x < Math.Min(left + 32, original.Width); x++)
                    {
                        int i = (y * original.Width + x) * 4;
                        alphaEqual &= original.Pixels[i + 3] == candidate.Pixels[i + 3];
                        for (int channel = 0; channel < 3; channel++)
                        {
                            int delta = original.Pixels[i + channel] - candidate.Pixels[i + channel];
                            sum += delta * delta;
                            samples++;
                        }
                    }
                }
                total += sum;
                worst = Math.Max(worst, Math.Sqrt(sum / samples) / 255);
            }
        }
        return (Math.Sqrt(total / (original.Width * original.Height * 3L)) / 255, worst, alphaEqual);
    }
}
