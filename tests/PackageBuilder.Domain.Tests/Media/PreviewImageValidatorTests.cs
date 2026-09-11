using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Media;

namespace PackageBuilder.Domain.Tests.Media;

public sealed class PreviewImageValidatorTests
{
    [Theory]
    [InlineData("empty", "MEDIA_EMPTY")]
    [InlineData("tiny", "MEDIA_TINY")]
    [InlineData("margin", "MEDIA_EXCESSIVE_MARGIN")]
    [InlineData("dark", "MEDIA_UNDEREXPOSED")]
    [InlineData("bright", "MEDIA_OVEREXPOSED")]
    [InlineData("edge", "MEDIA_CLIPPED")]
    [InlineData("bounds", "MEDIA_CLIPPED")]
    [InlineData("depth", "MEDIA_CLIPPED")]
    [InlineData("material", "MEDIA_MISSING_MATERIAL")]
    [InlineData("helper", "MEDIA_HELPER_VISIBLE")]
    [InlineData("transparent", "MEDIA_EMPTY")]
    public void DetectsBadCaptures(string scenario, string expected)
    {
        (PreviewRaster image, PreviewImageEvidence evidence) = Fixture(scenario);
        PreviewImageValidation result = PreviewImageValidator.Validate(image, evidence, _id, new(), TestContext.Current.CancellationToken);
        Assert.Contains(result.Findings, f => f.Code.Value == expected && f.BlocksRelease && f.RelatedArtifactId == _id && f.SuggestedAction is not null);
    }

    [Fact]
    public void ValidProductOnBlackBackgroundPassesWithMeasuredCoverage()
    {
        (PreviewRaster image, PreviewImageEvidence evidence) = Fixture("valid");
        PreviewImageValidation result = PreviewImageValidator.Validate(image, evidence, _id, new(), TestContext.Current.CancellationToken);
        Assert.True(result.IsValid);
        Assert.InRange(result.Metrics!.Coverage, .24, .26);
        Assert.Equal(0, result.Metrics.DarkFraction);
    }

    [Fact]
    public void MissingEvidenceAndInvalidPolicyFailClosed()
    {
        (PreviewRaster image, PreviewImageEvidence evidence) = Fixture("valid");
        Assert.False(PreviewImageValidator.Validate(image, null, _id, new(), TestContext.Current.CancellationToken).IsValid);
        Assert.False(PreviewImageValidator.Validate(image, evidence, _id, new(MinimumCoverage: double.NaN), TestContext.Current.CancellationToken).IsValid);
        Assert.False(PreviewImageValidator.Validate(image, new([], 0, 0, 1, 1, false, 0, 0), _id, new(), TestContext.Current.CancellationToken).IsValid);
    }

    [Fact]
    public void BoundedOwnedBuffersAndCancellation()
    {
        byte[] pixels = [100, 100, 100, 255];
        var image = new PreviewRaster(1, 1, pixels);
        pixels[0] = 0;
        Assert.Equal(100, image.Pixels[0]);
        _ = Assert.Throws<ArgumentException>(() => new PreviewRaster(int.MaxValue, int.MaxValue, []));
        _ = Assert.Throws<ArgumentException>(() => new PreviewImageEvidence([], double.NaN, 0, 1, 1, false, 0, 0));
        _ = Assert.Throws<OperationCanceledException>(() => PreviewImageValidator.Validate(image, null, _id, new(), new CancellationToken(true)));
    }

    private static readonly BuildArtifactId _id = BuildArtifactId.Create("hero").Value!;

    private static (PreviewRaster, PreviewImageEvidence) Fixture(string scenario)
    {
        const int Width = 1920, Height = 1080;
        byte[] pixels = new byte[Width * Height * 4];
        byte[] mask = new byte[Width * Height];
        int left = scenario == "edge" ? 0 : Width / 4;
        int right = scenario is "tiny" or "margin" ? left + 8 : Width * 3 / 4;
        int top = Height / 4, bottom = scenario is "tiny" or "margin" ? top + 8 : Height * 3 / 4;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;
                pixels[i * 4 + 3] = scenario == "transparent" ? (byte)0 : (byte)255;
                if (scenario == "empty" || x < left || x >= right || y < top || y >= bottom)
                { continue; }
                mask[i] = 255;
                byte value = scenario == "dark" ? (byte)0 : scenario == "bright" ? (byte)255 : (byte)120;
                pixels[i * 4] = pixels[i * 4 + 1] = pixels[i * 4 + 2] = value;
            }
        }
        return (new(Width, Height, pixels), new(mask, scenario == "bounds" ? -.1 : .2, .2, .8, .8,
            scenario == "depth", scenario == "material" ? 1 : 0, scenario == "helper" ? 1 : 0));
    }
}
