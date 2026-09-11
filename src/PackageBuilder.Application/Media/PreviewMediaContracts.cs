using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Media;

namespace PackageBuilder.Application.Media;

public enum PreviewImageFormat { Png, Jpeg }

/// <summary>Codec boundary: only bounded JPEG/PNG, one 1920x1080 frame, no file or network access.
/// Implementations throw InvalidDataException for unsupported or malformed input.</summary>
public interface IPreviewImageCodec
{
    PreviewRaster Decode(ReadOnlyMemory<byte> encoded);
    byte[] Encode(PreviewRaster image, PreviewImageFormat format, int jpegQuality);
}

/// <summary>One final capture and matching renderer evidence, identified without local paths.</summary>
public sealed record PreviewMediaInput(BuildArtifactId ArtifactId, PreviewRaster Image, PreviewImageEvidence Evidence);

/// <summary>Owned optimized bytes and independently decoded quality measurements.</summary>
public sealed class OptimizedPreviewImage
{
    private readonly byte[] _bytes;
    internal OptimizedPreviewImage(BuildArtifactId artifactId, PreviewImageFormat format, byte[] bytes,
        string sha256, double rmsError, double tileRmsError, PreviewImageMetrics metrics)
    {
        ArtifactId = artifactId;
        Format = format;
        _bytes = bytes;
        Sha256 = sha256;
        RmsError = rmsError;
        TileRmsError = tileRmsError;
        Metrics = metrics;
    }
    public BuildArtifactId ArtifactId { get; }
    public PreviewImageFormat Format { get; }
    public ReadOnlySpan<byte> Bytes => _bytes;
    public string Sha256 { get; }
    public double RmsError { get; }
    public double TileRmsError { get; }
    public PreviewImageMetrics Metrics { get; }
}

/// <summary>Atomic gallery result: failures never expose a partial publishable image collection.</summary>
public sealed record PreviewMediaResult(IReadOnlyList<OptimizedPreviewImage> Images,
    IReadOnlyList<PackageBuilder.Domain.Validation.ValidationFinding> Findings)
{
    public bool IsSuccessful => !Findings.Any(finding => finding.BlocksRelease);
}
