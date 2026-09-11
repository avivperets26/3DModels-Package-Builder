using System.Security.Cryptography;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Media;

namespace PackageBuilder.Application.Media;

/// <summary>Connects captured image/coverage bytes to the shared validator. Verifies both receipt
/// hashes before decoding; scene facts must originate from the same trusted worker capture.</summary>
public sealed class PreviewCaptureDecoder(IPreviewImageCodec codec)
{
    private readonly IPreviewImageCodec _codec = codec ?? throw new ArgumentNullException(nameof(codec));

    /// <summary>Rejects mismatched receipts before decoding and copies coverage alpha into owned evidence.</summary>
    public PreviewMediaInput Decode(BuildArtifactId artifactId, ReadOnlyMemory<byte> image, Sha256Digest imageHash,
        ReadOnlyMemory<byte> coverage, Sha256Digest coverageHash, double left, double bottom, double right, double top,
        bool depthClipped, int missingMaterials, int visibleHelpers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        ArgumentNullException.ThrowIfNull(imageHash);
        ArgumentNullException.ThrowIfNull(coverageHash);
        cancellationToken.ThrowIfCancellationRequested();
        if (image.Length > 32_000_000 || coverage.Length > 32_000_000 ||
            Convert.ToHexStringLower(SHA256.HashData(image.Span)) != imageHash.Value ||
            Convert.ToHexStringLower(SHA256.HashData(coverage.Span)) != coverageHash.Value)
        { throw new InvalidDataException("Capture receipt hashes do not match the supplied bytes."); }
        PreviewRaster raster = _codec.Decode(image);
        PreviewRaster mask = _codec.Decode(coverage);
        if (mask.Width != raster.Width || mask.Height != raster.Height)
        { throw new InvalidDataException("Coverage dimensions differ."); }
        byte[] alpha = new byte[mask.Width * mask.Height];
        for (int i = 0; i < alpha.Length; i++)
        { alpha[i] = mask.Pixels[i * 4 + 3]; }
        cancellationToken.ThrowIfCancellationRequested();
        return new(artifactId, raster, new(alpha, left, bottom, right, top, depthClipped, missingMaterials, visibleHelpers));
    }
}
