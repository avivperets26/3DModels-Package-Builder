namespace PackageBuilder.Domain.Media;

/// <summary>Bounded, owned RGBA8 pixels in row order. Construction copies caller-owned memory.</summary>
public sealed class PreviewRaster
{
    public const int CaptureWidth = 1920;
    public const int CaptureHeight = 1080;
    public const int MaximumPixels = 8_388_608;
    private readonly byte[] _pixels;

    public PreviewRaster(int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (width <= 0 || height <= 0 || (long)width * height > MaximumPixels ||
            (long)width * height * 4 != rgba.Length)
        {
            throw new ArgumentException("Invalid or oversized RGBA raster.", nameof(rgba));
        }
        Width = width;
        Height = height;
        _pixels = rgba.ToArray();
    }

    public int Width { get; }
    public int Height { get; }
    public ReadOnlySpan<byte> Pixels => _pixels;
}
