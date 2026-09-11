using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using PackageBuilder.App.Wpf.Media;
using PackageBuilder.Application.Media;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Contracts.Validation;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Media;
using PackageBuilder.Domain.Tools;
using PackageBuilder.Marketplaces.Fab;

namespace PackageBuilder.App.Wpf.Tests.Media;

[Trait("Task", "PB-0909")]
public sealed class PreviewMediaTests
{
    private static readonly WindowsPreviewImageCodec _codec = new();

    [Theory]
    [InlineData(PreviewImageFormat.Png)]
    [InlineData(PreviewImageFormat.Jpeg)]
    public void RealCodecRoundTripPreservesDimensionsAndChannels(PreviewImageFormat format)
    {
        PreviewMediaInput input = Fixture();
        byte[] encoded = _codec.Encode(input.Image, format, 95);
        PreviewRaster decoded = _codec.Decode(encoded);
        Assert.Equal(1920, decoded.Width);
        Assert.Equal(1080, decoded.Height);
        Assert.InRange(decoded.Pixels[(540 * 1920 + 960) * 4], 98, 102);
        Assert.Equal(encoded, _codec.Encode(input.Image, format, 95));
        if (format == PreviewImageFormat.Png)
        { Assert.True(input.Image.Pixels.SequenceEqual(decoded.Pixels)); }
    }

    [Fact]
    public void RealGalleryMeetsFabLimitsAndMeasuredQuality()
    {
        // The normal suite uses a deterministic synthetic capture. The Unity harness supplies
        // its disposable project to execute this same delivery pipeline against all five GPU views.
        string? project = Environment.GetEnvironmentVariable("PB_CAPTURE_TEST_PROJECT");
        PreviewMediaInput[] inputs = project is null ? [Fixture()] : ReadUnityCaptures(project);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        PreviewMediaResult result = new PreviewMediaOptimizer(_codec).Optimize(inputs, new(), FabPreviewMediaPolicy.Current, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccessful);
        Assert.Equal(inputs.Length, result.Images.Count);
        foreach (OptimizedPreviewImage image in result.Images)
        {
            Assert.InRange(image.Bytes.Length, 1, 2_999_999);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(image.Bytes)), image.Sha256);
            Assert.InRange(image.RmsError, 0, .02);
            Assert.InRange(image.TileRmsError, 0, .06);
        }
        BuildJobId job = BuildJobId.Create("media-integration").Value!;
        var versions = new BuildLock(job, "1.0.0", ToolVersion.Create(ToolKind.DotNet, "10.0.302").Value!,
            ToolVersion.Create(ToolKind.Blender, "5.0.0").Value!, ToolVersion.Create(ToolKind.Unity, "6000.3.10f1").Value!,
            ToolVersion.Create(ToolKind.Unreal, "5.8.0").Value!, 1, [new("unity-worker", "1.0.0")], [new("fab", "media", FabPreviewMediaPolicy.Current.Version)]);
        var report = new BuildValidationReport(job, versions, BuildJobState.Completed,
            result.Images.Select(i => new ValidationReportArtifact(i.ArtifactId, "preview", Sha256Digest.Create(i.Sha256).Value!, i.Bytes.Length)).ToArray(),
            new(timer.Elapsed.TotalSeconds, [], null, null, null, null, null),
            result.Images.SelectMany(i => new[] { new ValidationReportMetric("coverage", "ratio", i.Metrics.Coverage, i.ArtifactId),
                new("rms-error", "ratio", i.RmsError, i.ArtifactId), new("tile-rms-error", "ratio", i.TileRmsError, i.ArtifactId) }).ToArray(), result.Findings);
        BuildValidationReportResult serialized = BuildValidationReportJson.Serialize(report);
        Assert.True(serialized.IsSuccessful, serialized.Error);
        Assert.Equal(serialized.Json, BuildValidationReportJson.Deserialize(serialized.Json).Json);
        if (project is not null)
        { File.WriteAllText(Path.Combine(Path.GetDirectoryName(project)!, "media-validation-report.json"), serialized.Json); }
    }

    [Fact]
    public void TexturedImageUsesJpegWithinMeasuredQualityBudget()
    {
        PreviewMediaInput input = Fixture();
        byte[] pixels = input.Image.Pixels.ToArray();
        var random = new Random(901);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            for (int c = 0; c < 3; c++)
            { pixels[i + c] = (byte)(pixels[i + c] + random.Next(-4, 5)); }
        }
        input = input with { Image = new(1920, 1080, pixels) };
        PreviewMediaResult result = new PreviewMediaOptimizer(_codec).Optimize([input], new(), FabPreviewMediaPolicy.Current, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccessful);
        Assert.Equal(PreviewImageFormat.Jpeg, result.Images[0].Format);
        Assert.InRange(result.Images[0].RmsError, 0, .02);
        Assert.InRange(result.Images[0].TileRmsError, 0, .06);
    }

    [Fact]
    public void TruncatedEncodedImagesAreRejected()
    {
        PreviewMediaInput input = Fixture();
        foreach (PreviewImageFormat format in Enum.GetValues<PreviewImageFormat>())
        {
            byte[] bytes = _codec.Encode(input.Image, format, 95);
            _ = Assert.Throws<InvalidDataException>(() => _codec.Decode(bytes.AsMemory(0, bytes.Length - 12)));
        }
    }

    [Fact]
    public void StrictSizeBoundariesAndAtomicGalleryFailure()
    {
        PreviewMediaInput input = Fixture();
        var optimizer = new PreviewMediaOptimizer(new PredictableCodec(input.Image));
        Assert.True(optimizer.Optimize([input], new(), new("test", 101, 101), TestContext.Current.CancellationToken).IsSuccessful);
        Assert.Empty(optimizer.Optimize([input], new(), new("test", 100, 101), TestContext.Current.CancellationToken).Images);
        PreviewMediaResult total = optimizer.Optimize([input, input with { ArtifactId = BuildArtifactId.Create("front").Value! }],
            new(), new("test", 101, 200), TestContext.Current.CancellationToken);
        Assert.Empty(total.Images);
        Assert.Contains(total.Findings, f => f.Code.Value == "MEDIA_GALLERY_LIMIT");
    }

    [Fact]
    public void CorruptWrongSizeOversizedUnsupportedAndTransparencyAreRejected()
    {
        _ = Assert.Throws<InvalidDataException>(() => _codec.Decode(new byte[8]));
        _ = Assert.Throws<InvalidDataException>(() => _codec.Decode(new byte[32_000_001]));
        _ = Assert.Throws<InvalidDataException>(() => _codec.Decode(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        _ = Assert.Throws<InvalidDataException>(() => _codec.Encode(new(1, 1, [1, 2, 3, 255]), PreviewImageFormat.Png, 95));
        PreviewMediaInput transparent = Fixture(true);
        _ = Assert.Throws<InvalidDataException>(() => _codec.Encode(transparent.Image, PreviewImageFormat.Jpeg, 95));
        PreviewMediaResult result = new PreviewMediaOptimizer(_codec).Optimize([transparent], new(), FabPreviewMediaPolicy.Current, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccessful);
        Assert.Equal(PreviewImageFormat.Png, result.Images[0].Format);
        Assert.True(transparent.Image.Pixels.SequenceEqual(_codec.Decode(result.Images[0].Bytes.ToArray()).Pixels));
    }

    [Fact]
    public void RejectsLocalizedDamageEvenWhenWholeImageErrorIsSmall()
    {
        PreviewMediaInput input = Fixture();
        byte[] damaged = input.Image.Pixels.ToArray();
        for (int y = 500; y < 516; y++)
        { for (int x = 950; x < 966; x++) { damaged[(y * 1920 + x) * 4] = 255; } }
        var codec = new PredictableCodec(new(1920, 1080, damaged));
        PreviewMediaResult result = new PreviewMediaOptimizer(codec).Optimize([input], new(), new("test", 101, 1000), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccessful);
        Assert.Empty(result.Images);
    }

    [Fact]
    public void ReceiptHashesAndCoverageBridgeIntoTheValidator()
    {
        PreviewMediaInput input = Fixture();
        byte[] image = _codec.Encode(input.Image, PreviewImageFormat.Png, 100);
        byte[] maskPixels = input.Image.Pixels.ToArray();
        for (int i = 0; i < input.Evidence.Coverage.Length; i++)
        { maskPixels[i * 4 + 3] = input.Evidence.Coverage[i]; }
        byte[] mask = _codec.Encode(new(1920, 1080, maskPixels), PreviewImageFormat.Png, 100);
        var decoder = new PreviewCaptureDecoder(_codec);
        PreviewMediaInput decoded = decoder.Decode(input.ArtifactId, image, Hash(image), mask, Hash(mask), .25, .25, .75, .75, false, 0, 0, TestContext.Current.CancellationToken);
        Assert.True(PreviewImageValidator.Validate(decoded.Image, decoded.Evidence, input.ArtifactId, new(), TestContext.Current.CancellationToken).IsValid);
        _ = Assert.Throws<InvalidDataException>(() => decoder.Decode(input.ArtifactId, image, Hash(mask), mask, Hash(mask), .25, .25, .75, .75, false, 0, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CancellationAndInvalidGalleryReturnNoPartialOutput()
    {
        PreviewMediaInput input = Fixture();
        var optimizer = new PreviewMediaOptimizer(_codec);
        _ = Assert.Throws<OperationCanceledException>(() => optimizer.Optimize([input], new(), FabPreviewMediaPolicy.Current, new CancellationToken(true)));
        Assert.False(optimizer.Optimize([input, input], new(), FabPreviewMediaPolicy.Current, TestContext.Current.CancellationToken).IsSuccessful);
        Assert.False(optimizer.Optimize([], new(), FabPreviewMediaPolicy.Current, TestContext.Current.CancellationToken).IsSuccessful);
    }

    private static Sha256Digest Hash(byte[] bytes) => Sha256Digest.Create(Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!;

    private static PreviewMediaInput[] ReadUnityCaptures(string project)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(project)!, "capture-receipts.json")));
        var decoder = new PreviewCaptureDecoder(_codec);
        return [.. json.RootElement.GetProperty("images").EnumerateArray().Select(receipt => decoder.Decode(
            BuildArtifactId.Create(receipt.GetProperty("role").GetString()).Value!,
            File.ReadAllBytes(Path.Combine(project, receipt.GetProperty("file").GetString()!)), Sha256Digest.Create(receipt.GetProperty("sha256").GetString()).Value!,
            File.ReadAllBytes(Path.Combine(project, receipt.GetProperty("coverageFile").GetString()!)), Sha256Digest.Create(receipt.GetProperty("coverageSha256").GetString()).Value!,
            receipt.GetProperty("left").GetDouble(), receipt.GetProperty("bottom").GetDouble(), receipt.GetProperty("right").GetDouble(), receipt.GetProperty("top").GetDouble(),
            receipt.GetProperty("depthClipped").GetBoolean(), receipt.GetProperty("missingMaterials").GetInt32(), receipt.GetProperty("visibleHelpers").GetInt32(), TestContext.Current.CancellationToken))];
    }

    private static PreviewMediaInput Fixture(bool transparent = false)
    {
        byte[] rgba = new byte[1920 * 1080 * 4];
        byte[] coverage = new byte[1920 * 1080];
        for (int y = 0; y < 1080; y++)
        {
            for (int x = 0; x < 1920; x++)
            {
                int i = y * 1920 + x;
                bool product = x >= 480 && x < 1440 && y >= 270 && y < 810;
                rgba[i * 4] = product ? (byte)100 : (byte)25;
                rgba[i * 4 + 1] = product ? (byte)160 : (byte)25;
                rgba[i * 4 + 2] = product ? (byte)200 : (byte)25;
                rgba[i * 4 + 3] = transparent && !product ? (byte)0 : (byte)255;
                coverage[i] = product ? (byte)255 : (byte)0;
            }
        }
        return new(BuildArtifactId.Create("hero").Value!, new(1920, 1080, rgba), new(coverage, .25, .25, .75, .75, false, 0, 0));
    }

    // Deliberately controlled byte size / decoded damage isolates policy tests from codec heuristics.
    private sealed class PredictableCodec(PreviewRaster decoded) : IPreviewImageCodec
    {
        public PreviewRaster Decode(ReadOnlyMemory<byte> encoded) => decoded;
        public byte[] Encode(PreviewRaster image, PreviewImageFormat format, int jpegQuality) => new byte[100];
    }
}
