using PackageBuilder.Domain.Media;

namespace PackageBuilder.Targets.Unreal;

/// <summary>Packs decoded linear data without gamma conversion, filtering or source mutation.</summary>
public static class UnrealOrmPacker
{
    /// <summary>Uses the red channel of each separate map; absent AO is white. Dimensions must match.</summary>
    public static PreviewRaster Pack(PreviewRaster? ambientOcclusion, PreviewRaster roughness, PreviewRaster metallic)
    {
        ArgumentNullException.ThrowIfNull(roughness);
        ArgumentNullException.ThrowIfNull(metallic);
        if (roughness.Width != metallic.Width || roughness.Height != metallic.Height ||
            (ambientOcclusion is not null && (ambientOcclusion.Width != roughness.Width || ambientOcclusion.Height != roughness.Height)))
        { throw new ArgumentException("ORM inputs must have identical dimensions; implicit resampling is forbidden."); }
        byte[] pixels = new byte[roughness.Pixels.Length];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = ambientOcclusion is null ? byte.MaxValue : ambientOcclusion.Pixels[i];
            pixels[i + 1] = roughness.Pixels[i];
            pixels[i + 2] = metallic.Pixels[i];
            pixels[i + 3] = byte.MaxValue;
        }
        return new(roughness.Width, roughness.Height, pixels);
    }
}
