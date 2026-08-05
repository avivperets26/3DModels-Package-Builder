using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

public sealed class OfficialReleaseCatalogContractTests
{
    private static readonly Uri _source = new("https://example.invalid/releases");
    private static readonly DateTimeOffset _refreshedAt = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CatalogSnapshotsAndSortsCanonicalVersionsNewestFirst()
    {
        var releases = new List<OfficialToolRelease?>
        {
            Release("10.0.100", ToolReleaseSupport.LongTermSupport),
            Release("11.0.100-preview.2", ToolReleaseSupport.Standard),
            Release("10.0.302", ToolReleaseSupport.LongTermSupport),
        };

        ReleaseCatalogResult<OfficialReleaseCatalog> result = OfficialReleaseCatalog.Create(
            ToolKind.DotNet,
            _source,
            _refreshedAt,
            releases);
        releases.Clear();

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["11.0.100-preview.2", "10.0.302", "10.0.100"],
            result.Value!.Releases.Select(static release => release.Version.Value));
        Assert.Equal(_source, result.Value.Source);
        Assert.Equal(_refreshedAt, result.Value.RefreshedAtUtc);
    }

    [Theory]
    [InlineData("tool")]
    [InlineData("source")]
    [InlineData("timestamp")]
    [InlineData("null-releases")]
    [InlineData("empty")]
    [InlineData("wrong-tool")]
    [InlineData("duplicate")]
    [InlineData("support")]
    [InlineData("release-timestamp")]
    public void RejectsMalformedCatalogs(string scenario)
    {
        ToolKind tool = scenario == "tool" ? (ToolKind)99 : ToolKind.DotNet;
        Uri? source = scenario == "source" ? new Uri("http://example.invalid") : _source;
        DateTimeOffset timestamp = scenario == "timestamp"
            ? _refreshedAt.ToOffset(TimeSpan.FromHours(1))
            : _refreshedAt;
        IEnumerable<OfficialToolRelease?>? releases = scenario switch
        {
            "null-releases" => null,
            "empty" => [],
            "wrong-tool" => [BlenderRelease()],
            "duplicate" => [Release("10.0.302"), Release("10.0.302")],
            "support" => [Release("10.0.302", (ToolReleaseSupport)99)],
            "release-timestamp" =>
                [new OfficialToolRelease(Version(ToolKind.DotNet, "10.0.302"), ToolReleaseSupport.Standard, _refreshedAt.ToOffset(TimeSpan.FromHours(1)))],
            _ => [Release("10.0.302")],
        };

        ReleaseCatalogResult<OfficialReleaseCatalog> result = OfficialReleaseCatalog.Create(
            tool,
            source,
            timestamp,
            releases);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Null(result.Value);
    }

    private static OfficialToolRelease Release(
        string version,
        ToolReleaseSupport support = ToolReleaseSupport.Standard) =>
        new(Version(ToolKind.DotNet, version), support, _refreshedAt);

    private static OfficialToolRelease BlenderRelease() =>
        new(Version(ToolKind.Blender, "4.5.3"), ToolReleaseSupport.LongTermSupport, _refreshedAt);

    private static ToolVersion Version(ToolKind tool, string value)
    {
        ToolModelValidationResult<ToolVersion> result = ToolVersion.Create(tool, value);
        Assert.True(result.IsValid);
        return result.Value!;
    }
}
