namespace PackageBuilder.Targets.Portable;

/// <summary>Identifies one byte format that PB-0503 can validate and copy without re-encoding.</summary>
public sealed class PortableTextureFormat : IEquatable<PortableTextureFormat>
{
    private PortableTextureFormat(string identifier, PortableFileExtension outputExtension)
    {
        Identifier = identifier;
        OutputExtension = outputExtension;
    }

    public static PortableTextureFormat Png { get; } = new("png", PortableFileExtension.Png);

    public static PortableTextureFormat Jpeg { get; } = new("jpeg", PortableFileExtension.Jpg);

    public string Identifier { get; }

    public PortableFileExtension OutputExtension { get; }

    internal static PortableTextureFormat? FromExtension(PortableFileExtension extension) =>
        extension.Value switch
        {
            ".png" => Png,
            ".jpg" or ".jpeg" => Jpeg,
            _ => null,
        };

    public bool Equals(PortableTextureFormat? other) =>
        other is not null && string.Equals(Identifier, other.Identifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is PortableTextureFormat other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Identifier);

    public override string ToString() => Identifier;
}
