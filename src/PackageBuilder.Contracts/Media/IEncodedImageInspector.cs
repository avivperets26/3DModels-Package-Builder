namespace PackageBuilder.Contracts.Media;

/// <summary>Container formats supported by bounded gallery inspection.</summary>
public enum EncodedImageFormat { Png, Jpeg }

/// <summary>Dimensions and container format measured after complete bounded image decoding.</summary>
public sealed record EncodedImageInfo(int Width, int Height, EncodedImageFormat Format);

/// <summary>Trusted codec boundary. Decode exactly one PNG/JPEG frame with bounded bytes/pixels,
/// no external paths or network access. Reject malformed/unsupported data with InvalidDataException.</summary>
public interface IEncodedImageInspector
{
    /// <summary>Fully decodes the supplied bytes without filesystem access before returning measured metadata.</summary>
    EncodedImageInfo Inspect(ReadOnlyMemory<byte> encoded);
}
