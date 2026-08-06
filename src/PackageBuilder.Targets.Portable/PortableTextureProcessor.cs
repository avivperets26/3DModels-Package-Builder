using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Portable;

/// <summary>Validates and streams one exact portable texture copy without modifying its source.</summary>
public static class PortableTextureProcessor
{
    private const int BufferSize = 65_536;
    private const int MaximumDimension = 32_768;
    private const long MaximumPixels = 268_435_456;
    private static readonly byte[] _pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>
    /// Copies a validated normalized texture. A different source/output byte format is rejected
    /// because changing only a suffix would misrepresent the bytes and lossy re-encoding is not approved.
    /// </summary>
    public static async Task<PortableTextureCopyResult> CopyAsync(
        ArtifactStoreRecord? sourceRecord,
        TextureAssignment? assignment,
        PortableNamingProfile? naming,
        PortableFileExtension? sourceExtension,
        PortableFileExtension? destinationExtension,
        Stream? source,
        Stream? destination,
        CancellationToken cancellationToken = default)
    {
        PortableTextureCopyError inputError = ValidateInputs(
            sourceRecord,
            assignment,
            naming,
            sourceExtension,
            destinationExtension,
            source,
            destination,
            out PortableTextureFormat? sourceFormat,
            out PortableTextureFormat? destinationFormat);
        if (inputError != PortableTextureCopyError.None)
        {
            return PortableTextureCopyResult.Failure(inputError);
        }

        long sourcePosition = source!.Position;
        long destinationPosition = destination!.Position;
        try
        {
            if (source.Length - sourcePosition != sourceRecord!.ContentIdentity.Bytes)
            {
                return PortableTextureCopyResult.Failure(PortableTextureCopyError.SourceLengthMismatch);
            }

            Sha256Digest measured = await HashAsync(source, cancellationToken).ConfigureAwait(false);
            if (!measured.Equals(sourceRecord.ContentIdentity.Sha256))
            {
                return PortableTextureCopyResult.Failure(PortableTextureCopyError.SourceHashMismatch);
            }

            source.Position = sourcePosition;
            ImageInspectionResult inspection = Inspect(source, sourceFormat!);
            if (!inspection.IsValid)
            {
                return PortableTextureCopyResult.Failure(inspection.Error);
            }

            source.Position = sourcePosition;
            string fileName = naming!.GetTextureFileName(assignment!.Role, destinationExtension).Value!;
            string relativeReference = $"{naming.FlatFbxFolderName}/{fileName}";
            Sha256Digest copiedHash = await CopyAndHashAsync(source, destination, cancellationToken)
                .ConfigureAwait(false);
            if (!copiedHash.Equals(measured))
            {
                ResetDestination(destination, destinationPosition);
                return PortableTextureCopyResult.Failure(PortableTextureCopyError.IoFailure);
            }

            return PortableTextureCopyResult.Success(
                new PortableTextureCopyReceipt(
                    sourceRecord,
                    assignment,
                    destinationFormat!,
                    fileName,
                    relativeReference,
                    inspection.Width,
                    inspection.Height,
                    sourceRecord.ContentIdentity.Bytes,
                    copiedHash));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ResetDestination(destination, destinationPosition);
            return PortableTextureCopyResult.Failure(PortableTextureCopyError.Cancelled);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ResetDestination(destination, destinationPosition);
            return PortableTextureCopyResult.Failure(PortableTextureCopyError.IoFailure);
        }
        finally
        {
            if (source.CanSeek)
            {
                source.Position = sourcePosition;
            }
        }
    }

    private static PortableTextureCopyError ValidateInputs(
        ArtifactStoreRecord? record,
        TextureAssignment? assignment,
        PortableNamingProfile? naming,
        PortableFileExtension? sourceExtension,
        PortableFileExtension? destinationExtension,
        Stream? source,
        Stream? destination,
        out PortableTextureFormat? sourceFormat,
        out PortableTextureFormat? destinationFormat)
    {
        sourceFormat = sourceExtension is null ? null : PortableTextureFormat.FromExtension(sourceExtension);
        destinationFormat = destinationExtension is null ? null : PortableTextureFormat.FromExtension(destinationExtension);
        return record is null
            ? PortableTextureCopyError.NullArtifactRecord
            : assignment is null
            ? PortableTextureCopyError.NullTextureAssignment
            : naming is null
            ? PortableTextureCopyError.NullNamingProfile
            : sourceExtension is null
            ? PortableTextureCopyError.NullSourceExtension
            : destinationExtension is null
            ? PortableTextureCopyError.NullDestinationExtension
            : source is null
            ? PortableTextureCopyError.NullSourceStream
            : destination is null
            ? PortableTextureCopyError.NullDestinationStream
            : !record.Artifact.LifecycleState.Equals(BuildArtifactLifecycleState.Validated)
            ? PortableTextureCopyError.ArtifactNotValidated
            : record.Artifact.Target is null || !record.Artifact.Target.Equals(BuildTarget.Portable)
            ? PortableTextureCopyError.ArtifactTargetMismatch
            : !string.Equals(record.Artifact.Role.CanonicalIdentifier, "normalized-texture", StringComparison.Ordinal)
            ? PortableTextureCopyError.ArtifactRoleMismatch
            : !IsSupportedRole(assignment.Role)
            ? PortableTextureCopyError.UnsupportedTextureRole
            : sourceFormat is null
            ? PortableTextureCopyError.UnsupportedSourceFormat
            : destinationFormat is null
            ? PortableTextureCopyError.UnsupportedDestinationFormat
            : !sourceFormat.Equals(destinationFormat)
            ? PortableTextureCopyError.ConversionRequiresReencoding
            : !source.CanRead || !source.CanSeek
            ? PortableTextureCopyError.SourceStreamInvalid
            : !destination.CanWrite || !destination.CanSeek || destination.Length != 0 || destination.Position != 0
            ? PortableTextureCopyError.DestinationStreamInvalid
            : ReferenceEquals(source, destination)
            ? PortableTextureCopyError.DestinationStreamInvalid
            : PortableTextureCopyError.None;
    }

    private static bool IsSupportedRole(TextureRole role) =>
        role.Equals(TextureRole.Albedo) ||
        role.Equals(TextureRole.Normal) ||
        role.Equals(TextureRole.Metallic) ||
        role.Equals(TextureRole.Roughness) ||
        role.Equals(TextureRole.Emission) ||
        role.Equals(TextureRole.AmbientOcclusion);

    private static ImageInspectionResult Inspect(Stream source, PortableTextureFormat format) =>
        format.Equals(PortableTextureFormat.Png) ? InspectPng(source) : InspectJpeg(source);

    private static ImageInspectionResult InspectPng(Stream source)
    {
        if (source.Length - source.Position < 45)
        {
            return ImageInspectionResult.InvalidFormat();
        }

        Span<byte> header = stackalloc byte[24];
        if (!ReadExactly(source, header) ||
            !header[..8].SequenceEqual(_pngSignature) ||
            BinaryPrimitives.ReadUInt32BigEndian(header[8..12]) != 13 ||
            !header[12..16].SequenceEqual("IHDR"u8))
        {
            return ImageInspectionResult.InvalidFormat();
        }

        int width = BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
        int height = BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
        long current = source.Position;
        source.Position = source.Length - 12;
        Span<byte> end = stackalloc byte[12];
        bool validEnd = ReadExactly(source, end) &&
            BinaryPrimitives.ReadUInt32BigEndian(end[..4]) == 0 &&
            end[4..8].SequenceEqual("IEND"u8);
        source.Position = current;
        return !validEnd
            ? ImageInspectionResult.InvalidFormat()
            : ValidateDimensions(width, height);
    }

    private static ImageInspectionResult InspectJpeg(Stream source)
    {
        if (source.Length - source.Position < 4)
        {
            return ImageInspectionResult.InvalidFormat();
        }

        Span<byte> pair = stackalloc byte[2];
        if (!ReadExactly(source, pair) || pair[0] != 0xff || pair[1] != 0xd8)
        {
            return ImageInspectionResult.InvalidFormat();
        }

        long afterStart = source.Position;
        source.Position = source.Length - 2;
        bool validEnd = ReadExactly(source, pair) && pair[0] == 0xff && pair[1] == 0xd9;
        source.Position = afterStart;
        if (!validEnd)
        {
            return ImageInspectionResult.InvalidFormat();
        }

        while (source.Position < source.Length - 2)
        {
            int prefix = source.ReadByte();
            if (prefix != 0xff)
            {
                return ImageInspectionResult.InvalidFormat();
            }

            int marker;
            do
            {
                marker = source.ReadByte();
            }
            while (marker == 0xff);

            if (marker is < 0 or 0xd9 or 0xda)
            {
                return ImageInspectionResult.InvalidFormat();
            }

            if (marker == 0x01 || marker is >= 0xd0 and <= 0xd7)
            {
                continue;
            }

            if (!ReadExactly(source, pair))
            {
                return ImageInspectionResult.InvalidFormat();
            }

            int segmentLength = BinaryPrimitives.ReadUInt16BigEndian(pair);
            if (segmentLength < 2 || source.Position + segmentLength - 2 > source.Length)
            {
                return ImageInspectionResult.InvalidFormat();
            }

            if (IsStartOfFrame(marker))
            {
                Span<byte> frame = stackalloc byte[5];
                return segmentLength < 7 || !ReadExactly(source, frame)
                    ? ImageInspectionResult.InvalidFormat()
                    : ValidateDimensions(
                    BinaryPrimitives.ReadUInt16BigEndian(frame[3..5]),
                    BinaryPrimitives.ReadUInt16BigEndian(frame[1..3]));
            }

            source.Position += segmentLength - 2;
        }

        return ImageInspectionResult.InvalidFormat();
    }

    private static bool IsStartOfFrame(int marker) =>
        marker is >= 0xc0 and <= 0xc3 or >= 0xc5 and <= 0xc7 or >= 0xc9 and <= 0xcb or >= 0xcd and <= 0xcf;

    private static ImageInspectionResult ValidateDimensions(int width, int height) =>
        width <= 0 || height <= 0 || width > MaximumDimension || height > MaximumDimension ||
        (long)width * height > MaximumPixels
            ? ImageInspectionResult.InvalidDimensions()
            : ImageInspectionResult.Valid(width, height);

    private static bool ReadExactly(Stream source, Span<byte> destination)
    {
        int offset = 0;
        while (offset < destination.Length)
        {
            int read = source.Read(destination[offset..]);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    private static async Task<Sha256Digest> HashAsync(Stream source, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        byte[] hash = await sha256.ComputeHashAsync(source, cancellationToken).ConfigureAwait(false);
        return Sha256Digest.Create(Convert.ToHexString(hash).ToLowerInvariant()).Value!;
    }

    private static async Task<Sha256Digest> CopyAndHashAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            while (true)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
            }

            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            return Sha256Digest.Create(Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()).Value!;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static void ResetDestination(Stream destination, long originalPosition)
    {
        try
        {
            destination.SetLength(originalPosition);
            destination.Position = originalPosition;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or ObjectDisposedException)
        {
            // The caller owns stream disposal and physical partial-output cleanup.
        }
    }

    private readonly record struct ImageInspectionResult(
        bool IsValid,
        int Width,
        int Height,
        PortableTextureCopyError Error)
    {
        public static ImageInspectionResult Valid(int width, int height) =>
            new(true, width, height, PortableTextureCopyError.None);

        public static ImageInspectionResult InvalidFormat() =>
            new(false, 0, 0, PortableTextureCopyError.InvalidImageFormat);

        public static ImageInspectionResult InvalidDimensions() =>
            new(false, 0, 0, PortableTextureCopyError.InvalidImageDimensions);
    }
}
