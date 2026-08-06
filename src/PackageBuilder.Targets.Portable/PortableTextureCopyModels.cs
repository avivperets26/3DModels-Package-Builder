using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Portable;

/// <summary>Identifies one expected portable texture-copy failure without exposing raw I/O details.</summary>
public enum PortableTextureCopyError
{
    None = 0,
    NullArtifactRecord,
    NullTextureAssignment,
    NullNamingProfile,
    NullSourceExtension,
    NullDestinationExtension,
    NullSourceStream,
    NullDestinationStream,
    ArtifactNotValidated,
    ArtifactTargetMismatch,
    ArtifactRoleMismatch,
    UnsupportedTextureRole,
    UnsupportedSourceFormat,
    UnsupportedDestinationFormat,
    ConversionRequiresReencoding,
    SourceStreamInvalid,
    DestinationStreamInvalid,
    SourceLengthMismatch,
    SourceHashMismatch,
    InvalidImageFormat,
    InvalidImageDimensions,
    Cancelled,
    IoFailure,
}

/// <summary>Contains measured metadata and the canonical relative reference for one exact texture copy.</summary>
public sealed class PortableTextureCopyReceipt
{
    internal PortableTextureCopyReceipt(
        ArtifactStoreRecord sourceRecord,
        TextureAssignment assignment,
        PortableTextureFormat format,
        string fileName,
        string relativeReference,
        int width,
        int height,
        long byteLength,
        Sha256Digest sha256)
    {
        SourceRecord = sourceRecord;
        Assignment = assignment;
        Format = format;
        FileName = fileName;
        RelativeReference = relativeReference;
        Width = width;
        Height = height;
        ByteLength = byteLength;
        Sha256 = sha256;
    }

    public ArtifactStoreRecord SourceRecord { get; }

    public TextureAssignment Assignment { get; }

    public TextureRole Role => Assignment.Role;

    public ColourSpace ColourSpace => Assignment.ColourSpace;

    public NormalConvention? NormalConvention => Assignment.NormalConvention;

    public PortableTextureFormat Format { get; }

    public string FileName { get; }

    public string RelativeReference { get; }

    public int Width { get; }

    public int Height { get; }

    public long ByteLength { get; }

    public Sha256Digest Sha256 { get; }
}

/// <summary>Returns either one immutable texture-copy receipt or one stable expected failure.</summary>
public sealed class PortableTextureCopyResult
{
    private PortableTextureCopyResult(
        bool isSuccess,
        PortableTextureCopyReceipt? receipt,
        PortableTextureCopyError error)
    {
        IsSuccess = isSuccess;
        Receipt = receipt;
        Error = error;
    }

    public bool IsSuccess { get; }

    public PortableTextureCopyReceipt? Receipt { get; }

    public PortableTextureCopyError Error { get; }

    internal static PortableTextureCopyResult Success(PortableTextureCopyReceipt receipt) =>
        new(true, receipt, PortableTextureCopyError.None);

    internal static PortableTextureCopyResult Failure(PortableTextureCopyError error) =>
        new(false, null, error);
}
