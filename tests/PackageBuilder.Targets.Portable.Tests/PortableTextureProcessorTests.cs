using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Portable.Tests;

public sealed class PortableTextureProcessorTests
{
    [Fact]
    public async Task CopiesValidatedPngWithCanonicalReferenceAndExactBytes()
    {
        byte[] bytes = PortableTestValues.Png(4, 2);
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new MemoryStream();

        PortableTextureCopyResult result = await Copy(bytes, source, destination);

        Assert.True(result.IsSuccess);
        PortableTextureCopyReceipt receipt = Assert.IsType<PortableTextureCopyReceipt>(result.Receipt);
        Assert.Equal(PortableTextureCopyError.None, result.Error);
        Assert.Equal("T_SilverwingTalonbow_Albedo.png", receipt.FileName);
        Assert.Equal("Silverwing_Talonbow_fbx/T_SilverwingTalonbow_Albedo.png", receipt.RelativeReference);
        Assert.Equal(PortableTextureFormat.Png, receipt.Format);
        Assert.Equal(TextureRole.Albedo, receipt.Role);
        Assert.Equal(ColourSpace.Srgb, receipt.ColourSpace);
        Assert.Null(receipt.NormalConvention);
        Assert.Equal(4, receipt.Width);
        Assert.Equal(2, receipt.Height);
        Assert.Equal(bytes.Length, receipt.ByteLength);
        Assert.Equal(bytes, destination.ToArray());
        Assert.Equal(0, source.Position);
    }

    [Fact]
    public async Task NormalizesJpegExtensionWithoutChangingBytes()
    {
        byte[] bytes = PortableTestValues.Jpeg();
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new MemoryStream();

        PortableTextureCopyResult result = await Copy(
            bytes,
            source,
            destination,
            sourceExtension: ".jpeg",
            destinationExtension: ".jpg");

        Assert.True(result.IsSuccess);
        Assert.Equal(PortableTextureFormat.Jpeg, result.Receipt!.Format);
        Assert.Equal("T_SilverwingTalonbow_Albedo.jpg", result.Receipt.FileName);
        Assert.Equal(bytes, destination.ToArray());
    }

    [Theory]
    [InlineData("albedo")]
    [InlineData("normal")]
    [InlineData("metallic")]
    [InlineData("roughness")]
    [InlineData("emission")]
    [InlineData("ambient-occlusion")]
    public async Task CopiesEveryCanonicalSeparateRole(string identifier)
    {
        TextureRole role = TextureRole.TryParse(identifier).Value!;
        byte[] bytes = PortableTestValues.Png();
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new MemoryStream();

        PortableTextureCopyResult result = await Copy(bytes, source, destination, role);

        Assert.True(result.IsSuccess);
        Assert.Equal(role, result.Receipt!.Role);
        Assert.Equal(role.RequiredColourSpace, result.Receipt.ColourSpace);
    }

    [Theory]
    [InlineData(0, PortableTextureCopyError.NullArtifactRecord)]
    [InlineData(1, PortableTextureCopyError.NullTextureAssignment)]
    [InlineData(2, PortableTextureCopyError.NullNamingProfile)]
    [InlineData(3, PortableTextureCopyError.NullSourceExtension)]
    [InlineData(4, PortableTextureCopyError.NullDestinationExtension)]
    [InlineData(5, PortableTextureCopyError.NullSourceStream)]
    [InlineData(6, PortableTextureCopyError.NullDestinationStream)]
    public async Task RejectsNullInputs(int index, PortableTextureCopyError expected)
    {
        byte[] bytes = PortableTestValues.Png();
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new MemoryStream();
        object?[] values =
        [
            PortableTestValues.RecordForBytes("texture", bytes),
            PortableTestValues.Texture(),
            PortableTestValues.Naming(),
            PortableFileExtension.Png,
            PortableFileExtension.Png,
            source,
            destination,
        ];
        values[index] = null;

        PortableTextureCopyResult result = await PortableTextureProcessor.CopyAsync(
            (PackageBuilder.Contracts.Artifacts.ArtifactStoreRecord?)values[0],
            (TextureAssignment?)values[1],
            (PortableNamingProfile?)values[2],
            (PortableFileExtension?)values[3],
            (PortableFileExtension?)values[4],
            (Stream?)values[5],
            (Stream?)values[6],
            TestContext.Current.CancellationToken);

        AssertFailure(result, expected);
    }

    [Theory]
    [InlineData("state", PortableTextureCopyError.ArtifactNotValidated)]
    [InlineData("target", PortableTextureCopyError.ArtifactTargetMismatch)]
    [InlineData("role", PortableTextureCopyError.ArtifactRoleMismatch)]
    [InlineData("texture-role", PortableTextureCopyError.UnsupportedTextureRole)]
    [InlineData("source-format", PortableTextureCopyError.UnsupportedSourceFormat)]
    [InlineData("destination-format", PortableTextureCopyError.UnsupportedDestinationFormat)]
    [InlineData("conversion", PortableTextureCopyError.ConversionRequiresReencoding)]
    public async Task RejectsInvalidPolicyInputs(string scenario, PortableTextureCopyError expected)
    {
        byte[] bytes = PortableTestValues.Png();
        ArtifactStoreRecord record = PortableTestValues.RecordForBytes(
            "texture",
            bytes,
            role: scenario == "role" ? "other" : "normalized-texture",
            state: scenario == "state" ? BuildArtifactLifecycleState.Staged : null,
            target: scenario == "target" ? BuildTarget.Unity : null);
        TextureAssignment assignment = PortableTestValues.Texture(
            scenario == "texture-role" ? TextureRole.Opacity : TextureRole.Albedo);
        PortableFileExtension sourceExtension = PortableFileExtension.Create(
            scenario == "source-format" ? ".tga" : ".png").Value!;
        PortableFileExtension destinationExtension = PortableFileExtension.Create(
            scenario == "destination-format" ? ".tga" : scenario == "conversion" ? ".jpg" : ".png").Value!;
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new MemoryStream();

        PortableTextureCopyResult result = await PortableTextureProcessor.CopyAsync(
            record,
            assignment,
            PortableTestValues.Naming(),
            sourceExtension,
            destinationExtension,
            source,
            destination,
            TestContext.Current.CancellationToken);

        AssertFailure(result, expected);
        Assert.Empty(destination.ToArray());
    }

    [Fact]
    public async Task RejectsLengthAndHashMismatches()
    {
        byte[] bytes = PortableTestValues.Png();
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new MemoryStream();
        ArtifactStoreRecord shortRecord = PortableTestValues.RecordForBytes("short", bytes[..^1]);

        AssertFailure(
            await PortableTextureProcessor.CopyAsync(
                shortRecord,
                PortableTestValues.Texture(),
                PortableTestValues.Naming(),
                PortableFileExtension.Png,
                PortableFileExtension.Png,
                source,
                destination,
                TestContext.Current.CancellationToken),
            PortableTextureCopyError.SourceLengthMismatch);

        byte[] different = [.. bytes];
        different[30] = 1;
        ArtifactStoreRecord hashRecord = PortableTestValues.RecordForBytes("hash", different);
        AssertFailure(
            await PortableTextureProcessor.CopyAsync(
                hashRecord,
                PortableTestValues.Texture(),
                PortableTestValues.Naming(),
                PortableFileExtension.Png,
                PortableFileExtension.Png,
                source,
                destination,
                TestContext.Current.CancellationToken),
            PortableTextureCopyError.SourceHashMismatch);
    }

    [Theory]
    [MemberData(nameof(InvalidImages))]
    public async Task RejectsInvalidImages(byte[] bytes, string extension, PortableTextureCopyError expected)
    {
        PortableFileExtension parsedExtension = PortableFileExtension.Create(extension).Value!;
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new MemoryStream();

        PortableTextureCopyResult result = await PortableTextureProcessor.CopyAsync(
            PortableTestValues.RecordForBytes("invalid", bytes),
            PortableTestValues.Texture(),
            PortableTestValues.Naming(),
            parsedExtension,
            parsedExtension.Equals(PortableFileExtension.Png) ? parsedExtension : PortableFileExtension.Jpg,
            source,
            destination,
            TestContext.Current.CancellationToken);

        AssertFailure(result, expected);
    }

    public static TheoryData<byte[], string, PortableTextureCopyError> InvalidImages
    {
        get
        {
            byte[] badPngSignature = PortableTestValues.Png();
            badPngSignature[0] = 0;
            byte[] noPngEnd = PortableTestValues.Png();
            noPngEnd[37] = 0;
            byte[] zeroPngWidth = PortableTestValues.Png(0, 2);
            byte[] hugePng = PortableTestValues.Png(32768, 32768);
            byte[] badJpegStart = PortableTestValues.Jpeg();
            badJpegStart[0] = 0;
            byte[] badJpegEnd = PortableTestValues.Jpeg();
            badJpegEnd[^1] = 0;
            byte[] noJpegFrame = [0xff, 0xd8, 0xff, 0xe0, 0x00, 0x02, 0xff, 0xd9];
            return new TheoryData<byte[], string, PortableTextureCopyError>
            {
                { [1, 2, 3], ".png", PortableTextureCopyError.InvalidImageFormat },
                { badPngSignature, ".png", PortableTextureCopyError.InvalidImageFormat },
                { noPngEnd, ".png", PortableTextureCopyError.InvalidImageFormat },
                { zeroPngWidth, ".png", PortableTextureCopyError.InvalidImageDimensions },
                { hugePng, ".png", PortableTextureCopyError.InvalidImageDimensions },
                { [0xff, 0xd8, 0xff], ".jpg", PortableTextureCopyError.InvalidImageFormat },
                { badJpegStart, ".jpg", PortableTextureCopyError.InvalidImageFormat },
                { badJpegEnd, ".jpg", PortableTextureCopyError.InvalidImageFormat },
                { noJpegFrame, ".jpg", PortableTextureCopyError.InvalidImageFormat },
            };
        }
    }

    [Fact]
    public async Task RejectsInvalidStreamsAndCancellation()
    {
        byte[] bytes = PortableTestValues.Png();
        using var validSource = new MemoryStream(bytes, writable: false);
        using var destination = new MemoryStream();
        using var nonSeekable = new NonSeekableReadStream(bytes);
        using var readOnlyDestination = new MemoryStream(bytes, writable: false);

        AssertFailure(await Copy(bytes, nonSeekable, destination), PortableTextureCopyError.SourceStreamInvalid);
        AssertFailure(await Copy(bytes, validSource, readOnlyDestination), PortableTextureCopyError.DestinationStreamInvalid);
        using var writableSharedStream = new MemoryStream(bytes, writable: true);
        writableSharedStream.SetLength(0);
        AssertFailure(
            await Copy(bytes, writableSharedStream, writableSharedStream),
            PortableTextureCopyError.DestinationStreamInvalid);
        using var occupiedDestination = new MemoryStream([1]);
        AssertFailure(
            await Copy(bytes, validSource, occupiedDestination),
            PortableTextureCopyError.DestinationStreamInvalid);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        PortableTextureCopyResult cancelled = await PortableTextureProcessor.CopyAsync(
            PortableTestValues.RecordForBytes("cancelled", bytes),
            PortableTestValues.Texture(),
            PortableTestValues.Naming(),
            PortableFileExtension.Png,
            PortableFileExtension.Png,
            validSource,
            destination,
            cancellation.Token);
        AssertFailure(cancelled, PortableTextureCopyError.Cancelled);
    }

    [Fact]
    public void TextureFormatsUseStableIdentitySemantics()
    {
        Assert.Equal("png", PortableTextureFormat.Png.Identifier);
        Assert.Equal(PortableFileExtension.Png, PortableTextureFormat.Png.OutputExtension);
        Assert.Equal(PortableTextureFormat.Png, PortableTextureFormat.Png);
        Assert.False(PortableTextureFormat.Png.Equals(PortableTextureFormat.Jpeg));
        Assert.False(PortableTextureFormat.Png.Equals(null));
        Assert.False(PortableTextureFormat.Png.Equals(new object()));
        Assert.True(PortableTextureFormat.Png.Equals((object)PortableTextureFormat.Png));
        Assert.Equal(PortableTextureFormat.Png.GetHashCode(), PortableTextureFormat.Png.GetHashCode());
        Assert.Equal("png", PortableTextureFormat.Png.ToString());
    }

    [Theory]
    [MemberData(nameof(AdditionalInvalidImages))]
    public async Task RejectsAdditionalMalformedImageStructures(
        byte[] bytes,
        string extension,
        PortableTextureCopyError expected)
    {
        PortableFileExtension parsedExtension = PortableFileExtension.Create(extension).Value!;
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new MemoryStream();

        PortableTextureCopyResult result = await PortableTextureProcessor.CopyAsync(
            PortableTestValues.RecordForBytes("malformed", bytes),
            PortableTestValues.Texture(),
            PortableTestValues.Naming(),
            parsedExtension,
            parsedExtension,
            source,
            destination,
            TestContext.Current.CancellationToken);

        AssertFailure(result, expected);
    }

    public static TheoryData<byte[], string, PortableTextureCopyError> AdditionalInvalidImages
    {
        get
        {
            byte[] wrongPngLength = PortableTestValues.Png();
            wrongPngLength[11] = 12;
            byte[] wrongPngChunk = PortableTestValues.Png();
            wrongPngChunk[12] = (byte)'X';
            byte[] nonzeroPngEndLength = PortableTestValues.Png();
            nonzeroPngEndLength[36] = 1;
            return new TheoryData<byte[], string, PortableTextureCopyError>
            {
                { wrongPngLength, ".png", PortableTextureCopyError.InvalidImageFormat },
                { wrongPngChunk, ".png", PortableTextureCopyError.InvalidImageFormat },
                { nonzeroPngEndLength, ".png", PortableTextureCopyError.InvalidImageFormat },
                { PortableTestValues.Png(2, 0), ".png", PortableTextureCopyError.InvalidImageDimensions },
                { PortableTestValues.Png(32769, 1), ".png", PortableTextureCopyError.InvalidImageDimensions },
                { PortableTestValues.Png(1, 32769), ".png", PortableTextureCopyError.InvalidImageDimensions },
                { PortableTestValues.Png(32768, 8193), ".png", PortableTextureCopyError.InvalidImageDimensions },
                { [0xff, 0xd8, 0x00, 0x00, 0xff, 0xd9], ".jpg", PortableTextureCopyError.InvalidImageFormat },
                { [0xff, 0xd8, 0xff, 0xe0, 0x00, 0x02, 0x00, 0xd9], ".jpg", PortableTextureCopyError.InvalidImageFormat },
                { [0xff, 0xd8, 0xff, 0xda, 0xff, 0xd9], ".jpg", PortableTextureCopyError.InvalidImageFormat },
                { [0xff, 0xd8, 0xff, 0xe0, 0x00, 0x01, 0xff, 0xd9], ".jpg", PortableTextureCopyError.InvalidImageFormat },
                { [0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10, 0xff, 0xd9], ".jpg", PortableTextureCopyError.InvalidImageFormat },
                { [0xff, 0xd8, 0xff, 0xc0, 0x00, 0x02, 0xff, 0xd9], ".jpg", PortableTextureCopyError.InvalidImageFormat },
                { PortableTestValues.Jpeg(0, 2), ".jpg", PortableTextureCopyError.InvalidImageDimensions },
                { PortableTestValues.Jpeg(2, 0), ".jpg", PortableTextureCopyError.InvalidImageDimensions },
            };
        }
    }

    [Theory]
    [InlineData(0x01)]
    [InlineData(0xd0)]
    [InlineData(0xc1)]
    [InlineData(0xc2)]
    [InlineData(0xc3)]
    [InlineData(0xc5)]
    [InlineData(0xc6)]
    [InlineData(0xc7)]
    [InlineData(0xc9)]
    [InlineData(0xca)]
    [InlineData(0xcb)]
    [InlineData(0xcd)]
    [InlineData(0xce)]
    [InlineData(0xcf)]
    public async Task AcceptsSupportedJpegMarkerPaths(int marker)
    {
        byte[] bytes = marker is 0x01 or 0xd0
            ? [0xff, 0xd8, 0xff, (byte)marker, .. PortableTestValues.Jpeg()[2..]]
            : PortableTestValues.JpegWithFrameMarker((byte)marker);
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new MemoryStream();

        PortableTextureCopyResult result = await Copy(
            bytes,
            source,
            destination,
            sourceExtension: ".jpg",
            destinationExtension: ".jpg");

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(0xbf)]
    [InlineData(0xc4)]
    [InlineData(0xc8)]
    [InlineData(0xcc)]
    [InlineData(0xd8)]
    public async Task SkipsNonFrameJpegSegmentsBeforeTheFrame(int marker)
    {
        byte[] bytes = [0xff, 0xd8, 0xff, (byte)marker, 0x00, 0x02, .. PortableTestValues.Jpeg()[2..]];
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new MemoryStream();

        PortableTextureCopyResult result = await Copy(
            bytes,
            source,
            destination,
            sourceExtension: ".jpg",
            destinationExtension: ".jpg");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ConvertsExpectedIoFailuresToStableErrorsAndRestoresSource()
    {
        byte[] bytes = PortableTestValues.Png();
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new ThrowingWriteStream();

        PortableTextureCopyResult result = await Copy(bytes, source, destination);

        AssertFailure(result, PortableTextureCopyError.IoFailure);
        Assert.Equal(0, source.Position);
    }

    [Theory]
    [InlineData("png-end")]
    [InlineData("jpeg-end")]
    [InlineData("jpeg-segment")]
    public async Task RejectsStreamsThatStopDuringStructuralInspection(string scenario)
    {
        byte[] bytes = scenario == "png-end" ? PortableTestValues.Png() : PortableTestValues.Jpeg();
        long failurePosition = scenario switch
        {
            "png-end" => bytes.Length - 12,
            "jpeg-end" => bytes.Length - 2,
            _ => 4,
        };
        PortableFileExtension extension = scenario == "png-end"
            ? PortableFileExtension.Png
            : PortableFileExtension.Jpg;
        using var source = new FailingInspectionStream(bytes, failurePosition);
        using var destination = new MemoryStream();

        PortableTextureCopyResult result = await PortableTextureProcessor.CopyAsync(
            PortableTestValues.RecordForBytes("short-read", bytes),
            PortableTestValues.Texture(),
            PortableTestValues.Naming(),
            extension,
            extension,
            source,
            destination,
            TestContext.Current.CancellationToken);

        AssertFailure(result, PortableTextureCopyError.InvalidImageFormat);
    }

    [Fact]
    public async Task RejectsAChangedSecondReadAndRemovesPartialOutput()
    {
        byte[] bytes = PortableTestValues.Png();
        using var source = new MutatingSecondCopyStream(bytes);
        using var destination = new MemoryStream();

        PortableTextureCopyResult result = await Copy(bytes, source, destination);

        AssertFailure(result, PortableTextureCopyError.IoFailure);
        Assert.Empty(destination.ToArray());
    }

    [Fact]
    public async Task SuppressesCleanupFailuresAfterAnExpectedIoFailure()
    {
        byte[] bytes = PortableTestValues.Png();
        using var source = new MemoryStream(bytes, writable: false);
        using var destination = new ThrowingWriteAndResetStream();

        PortableTextureCopyResult result = await Copy(bytes, source, destination);

        AssertFailure(result, PortableTextureCopyError.IoFailure);
    }

    private static async Task<PortableTextureCopyResult> Copy(
        byte[] bytes,
        Stream source,
        Stream destination,
        TextureRole? role = null,
        string sourceExtension = ".png",
        string destinationExtension = ".png") =>
        await PortableTextureProcessor.CopyAsync(
            PortableTestValues.RecordForBytes("texture", bytes),
            PortableTestValues.Texture(role),
            PortableTestValues.Naming(),
            PortableFileExtension.Create(sourceExtension).Value,
            PortableFileExtension.Create(destinationExtension).Value,
            source,
            destination,
            TestContext.Current.CancellationToken);

    private static void AssertFailure(PortableTextureCopyResult result, PortableTextureCopyError expected)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Receipt);
        Assert.Equal(expected, result.Error);
    }

    private sealed class NonSeekableReadStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public override bool CanSeek => false;
    }

    private class ThrowingWriteStream : MemoryStream
    {
        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("Synthetic write failure."));
    }

    private sealed class ThrowingWriteAndResetStream : ThrowingWriteStream
    {
        public override void SetLength(long value) => throw new IOException("Synthetic cleanup failure.");
    }

    private sealed class FailingInspectionStream(byte[] bytes, long failurePosition)
        : MemoryStream(bytes, writable: false)
    {
        public override int Read(Span<byte> buffer) =>
            Position == failurePosition ? 0 : base.Read(buffer);
    }

    private sealed class MutatingSecondCopyStream(byte[] bytes) : MemoryStream([.. bytes], writable: true)
    {
        private int _rewinds;

        public override long Position
        {
            get => base.Position;
            set
            {
                base.Position = value;
                if (value == 0 && ++_rewinds == 2)
                {
                    WriteByte(0);
                    base.Position = 0;
                }
            }
        }
    }
}
