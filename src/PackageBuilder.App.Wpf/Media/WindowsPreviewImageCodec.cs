using System.Buffers.Binary;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PackageBuilder.Application.Media;
using PackageBuilder.Domain.Media;

namespace PackageBuilder.App.Wpf.Media;

/// <summary>Windows Imaging Component adapter using the existing WPF runtime. Only PNG/JPEG
/// containers are accepted; dimensions are checked before pixel allocation. Encoding discards metadata.</summary>
public sealed class WindowsPreviewImageCodec : IPreviewImageCodec
{
    /// <summary>Decodes a single bounded delivery-sized frame into owned RGBA pixels while discarding metadata.</summary>
    public PreviewRaster Decode(ReadOnlyMemory<byte> encoded)
    {
        if (encoded.Length is < 8 or > 32_000_000)
        { throw new InvalidDataException("Image input size is invalid."); }
        try
        {
            using var stream = new MemoryStream(encoded.ToArray(), false);
            BitmapDecoder decoder;
            if (encoded.Span[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            {
                ValidatePngContainer(encoded.Span);
                decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            }
            else if (encoded.Span[0] == 255 && encoded.Span[1] == 216)
            {
                if (encoded.Span[^2] != 255 || encoded.Span[^1] != 217)
                { throw new InvalidDataException("JPEG end marker is missing."); }
                decoder = new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            }
            else
            { throw new InvalidDataException("Only PNG and JPEG are accepted."); }
            if (decoder.Frames.Count != 1)
            { throw new InvalidDataException("Exactly one image frame is required."); }
            BitmapFrame frame = decoder.Frames[0];
            if (frame.PixelWidth != PreviewRaster.CaptureWidth || frame.PixelHeight != PreviewRaster.CaptureHeight)
            { throw new InvalidDataException("Image dimensions must be 1920 by 1080."); }
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            byte[] pixels = new byte[frame.PixelWidth * frame.PixelHeight * 4];
            converted.CopyPixels(pixels, frame.PixelWidth * 4, 0);
            SwapRedBlue(pixels);
            return new(frame.PixelWidth, frame.PixelHeight, pixels);
        }
        catch (Exception exception) when (exception is NotSupportedException or System.Runtime.InteropServices.COMException or ArgumentException or FileFormatException)
        { throw new InvalidDataException("Image decoding failed.", exception); }
    }

    /// <summary>Encodes fixed-resolution pixels with no metadata, preserving alpha for PNG and rejecting it for JPEG.</summary>
    public byte[] Encode(PreviewRaster image, PreviewImageFormat format, int jpegQuality)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width != PreviewRaster.CaptureWidth || image.Height != PreviewRaster.CaptureHeight ||
            !Enum.IsDefined(format) || jpegQuality is < 1 or > 100)
        { throw new InvalidDataException("Invalid encoding options."); }
        byte[] pixels = image.Pixels.ToArray();
        if (format == PreviewImageFormat.Jpeg)
        {
            for (int i = 3; i < pixels.Length; i += 4)
            { if (pixels[i] != 255) { throw new InvalidDataException("JPEG cannot preserve transparency."); } }
        }
        SwapRedBlue(pixels);
        var source = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, pixels, image.Width * 4);
        BitmapEncoder encoder = format == PreviewImageFormat.Png ? new PngBitmapEncoder() : new JpegBitmapEncoder { QualityLevel = jpegQuality };
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    // WIC can accept partial PNGs or silently read only APNG's default frame. Fail closed
    // on truncated chunks, animation and trailing content before handing bytes to WIC.
    private static void ValidatePngContainer(ReadOnlySpan<byte> bytes)
    {
        int offset = 8;
        bool dataSeen = false;
        while (offset <= bytes.Length - 12)
        {
            uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes[offset..]);
            if (length > bytes.Length - offset - 12)
            { break; }
            ReadOnlySpan<byte> type = bytes.Slice(offset + 4, 4);
            if (type.SequenceEqual("acTL"u8))
            { throw new InvalidDataException("Animated PNG is unsupported."); }
            if (type.SequenceEqual("IDAT"u8))
            { dataSeen = true; }
            if (type.SequenceEqual("IEND"u8))
            {
                if (length == 0 && offset + 12 == bytes.Length && dataSeen)
                { return; }
                break;
            }
            offset += (int)length + 12;
        }
        throw new InvalidDataException("PNG container is incomplete.");
    }

    private static void SwapRedBlue(byte[] pixels)
    {
        for (int i = 0; i < pixels.Length; i += 4)
        { (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]); }
    }
}
