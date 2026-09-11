using System.Globalization;
using System.Net;
using System.Text;
using PackageBuilder.Application.Documentation;
using PackageBuilder.Contracts.Manifests;
using PackageBuilder.Contracts.Profiles;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Profiles;
using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Application.Tests.Documentation;

public sealed class DocumentationTests
{
    internal const string ProfileJson = """
        {"schemaVersion":1,"root":"ExampleStudio","displayName":"Élan Studio 🪶","supportContact":{"kind":"email","value":"support@example.com"},"copyright":{"holder":"Élan Studio","yearPolicy":{"kind":"year-range","year":2026,"startYear":2024}},"aiDisclosure":{"state":"ai-assisted","text":"Concept only."},"branding":{"images":[{"role":"logo","source":{"kind":"image","logicalReference":"branding/logo.png"}}]}}
        """;

    internal static PublisherProfile Publisher => PublisherProfileJson.Deserialize(ProfileJson).Value!;
    internal static ProductManifest Manifest => ProductManifestJson.Deserialize("""
        {"schemaVersion":1,"publisherProfileReference":"ExampleStudio","product":{"displayName":"Stone Arch","assetId":"StoneArch","folderName":"Stone_Arch","case":"static","version":"1.2.3"},"targets":["portable","unity","unreal"],"sourceAssets":[{"kind":"fbx","logicalReference":"models/arch.fbx"}],"materials":[],"animations":[]}
        """).Value!;
    internal static ReadmeMeasurements Metrics => new(1.25, 2.5, 0.75, 456, 1, 1, 1, "+Y", "-Z", "Base centre");
    internal static ReadmeBuildData Build(BuildTarget? target = null, ReadmeEngine? engine = null, string? usage = null) =>
        ReadmeBuildData.Create(target ?? BuildTarget.Portable, [new("StoneArch.fbx", "FBX", "7.4")], Metrics,
            engine, [], [usage ?? "Import the FBX and assign the supplied textures."]).Value!;

    [Fact]
    public void SharedSectionsContainActualDataWithDeterministicStrictUtf8()
    {
        DocumentationResult<ReadmeDocument> result = Render(Manifest, Publisher, Build());
        Assert.True(result.IsSuccess, result.Error);
        ReadmeDocument document = result.Value!;
        string visible = WebUtility.HtmlDecode(document.Text);
        Assert.Contains("Publisher: Élan Studio 🪶 (ExampleStudio)", visible);
        Assert.Contains("Version: 1.2.3", visible);
        Assert.Contains("StoneArch.fbx — FBX 7.4", visible);
        Assert.Contains("Triangles: 456", visible);
        Assert.Contains("Materials: 1", visible);
        Assert.Contains("Textures: 1", visible);
        Assert.Contains("Width: 1.25 m", visible);
        Assert.Contains("Scale: 1 m per unit", visible);
        Assert.Contains("Up axis: +Y", visible);
        Assert.Contains("Forward axis: -Z", visible);
        Assert.Contains("Pivot: Base centre", visible);
        Assert.Contains("None required.", visible);
        Assert.Contains("AI-assisted: Concept only.", visible);
        Assert.Contains("support@example.com", visible);
        Assert.Contains("Copyright © 2024-2026 Élan Studio", visible);
        Assert.DoesNotContain("AvivPeretsFBX", visible);
        Assert.DoesNotContain('\r', document.Text);
        Assert.Equal(new UTF8Encoding(false, true).GetBytes(document.Text), document.Utf8Bytes);
        Assert.Equal(document.Text, Render(Manifest, Publisher, Build()).Value!.Text);
    }

    [Theory]
    [InlineData("{{ 7 * 7 }}")]
    [InlineData("{{ include '/private' }}")]
    [InlineData("{{ while true }} x {{ end }}")]
    [InlineData("[click](javascript:alert(1)) <script>alert(1)</script>")]
    [InlineData("![remote](https://example.com/track.png)")]
    public void UntrustedProseIsLiteralAndCannotBecomeCodeMarkupOrRemoteMedia(string input)
    {
        DocumentationResult<ReadmeDocument> result = Render(Manifest, Publisher, Build(usage: input));
        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains(input, WebUtility.HtmlDecode(result.Value!.Text));
        Assert.DoesNotContain("{{", result.Value.Text);
        Assert.DoesNotContain("<script>", result.Value.Text);
        Assert.DoesNotContain("](javascript:", result.Value.Text);
        Assert.DoesNotContain("![", result.Value.Text);
    }

    [Fact]
    public void IsIndependentFromCurrentCultureAndConcurrentRequests()
    {
        string expected = Render(Manifest, Publisher, Build()).Value!.Text;
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            _ = Parallel.For(0, 16, _ => Assert.Equal(expected, Render(Manifest, Publisher, Build()).Value!.Text));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("unity", "Unity", "6000.3.10f1", "URP", "17.3.0")]
    [InlineData("unreal", "Unreal", "5.8.0", null, null)]
    public void EngineVersionsComeOnlyFromSuppliedTargetResults(string target, string name, string version, string? pipeline, string? pipelineVersion)
    {
        var engine = new ReadmeEngine(name, version, pipeline, pipelineVersion);
        DocumentationResult<ReadmeBuildData> build = ReadmeBuildData.Create(BuildTarget.TryParse(target).Value,
            [new("product.asset", name, version)], Metrics, engine, [new("Dependency", "2.1")], ["Open the overview scene."]);
        Assert.True(build.IsSuccess, build.Error);
        string text = WebUtility.HtmlDecode(Render(Manifest, Publisher, build.Value).Value!.Text);
        Assert.Contains($"{name} {version}", text);
        Assert.Contains("Dependency 2.1", text);
        Assert.Contains(pipeline is null ? "No render pipeline dependency." : $"{pipeline} {pipelineVersion}", text);
    }

    [Fact]
    public void MissingOrMismatchedInputsFailInsteadOfInventingValues()
    {
        Assert.Equal("DOC_REQUIRED_DATA_MISSING", Render(Manifest, Publisher, null).Error);
        PublisherProfile? other = PublisherProfileJson.Deserialize(ProfileJson.Replace("ExampleStudio", "OtherStudio", StringComparison.Ordinal)).Value;
        Assert.Equal("DOC_PUBLISHER_MISMATCH", Render(Manifest, other, Build()).Error);
        Assert.Equal("DOC_CANCELLED", SharedReadmeGenerator.Generate(Manifest, Publisher, Build(), new CancellationToken(true)).Error);
        Assert.Equal("DOC_ENGINE_INVALID", ReadmeBuildData.Create(BuildTarget.Unity, [new("x.asset", "Unity", "1")], Metrics, null, [], ["Use it."]).Error);
        Assert.Equal("DOC_ENGINE_INVALID", ReadmeBuildData.Create(BuildTarget.Portable, [new("x.fbx", "FBX", "7.4")], Metrics, new("Unity", "1", null, null), [], ["Use it."]).Error);
    }

    [Theory]
    [InlineData("../x.fbx")]
    [InlineData("/x.fbx")]
    [InlineData("C:/x.fbx")]
    [InlineData("x\\y.fbx")]
    public void RejectsUnsafeDeliveredPaths(string path) =>
        Assert.Equal("DOC_FILES_INVALID", ReadmeBuildData.Create(BuildTarget.Portable, [new(path, "FBX", "7.4")], Metrics, null, [], ["Use it."]).Error);

    [Fact]
    public void RejectsInvalidMetricsAndDuplicateFilesAndSnapshotsBoundedCollections()
    {
        foreach (ReadmeMeasurements metrics in new[] { Metrics with { Width = double.NaN }, Metrics with { Triangles = 0 },
            Metrics with { Materials = -1 }, Metrics with { Textures = -1 }, Metrics with { MetresPerUnit = double.PositiveInfinity },
            Metrics with { ForwardAxis = "-Y" }, Metrics with { Pivot = "" } })
        {
            Assert.Equal("DOC_METRICS_INVALID", ReadmeBuildData.Create(BuildTarget.Portable, [new("x.fbx", "FBX", "7.4")], metrics, null, [], ["Use it."]).Error);
        }

        var files = new List<ReadmeFile> { new("x.fbx", "FBX", "7.4") };
        ReadmeBuildData build = ReadmeBuildData.Create(BuildTarget.Portable, files, Metrics, null, [], ["Use it."]).Value!;
        files.Add(new("X.fbx", "FBX", "7.4"));
        _ = Assert.Single(build.Files);
        Assert.Equal("DOC_FILES_INVALID", ReadmeBuildData.Create(BuildTarget.Portable, files, Metrics, null, [], ["Use it."]).Error);
        Assert.Equal("DOC_COLLECTION_LIMIT", ReadmeBuildData.Create(BuildTarget.Portable,
            Enumerable.Repeat(files[0], 1025), Metrics, null, [], ["Use it."]).Error);
        Assert.Equal("DOC_USAGE_OR_DEPENDENCIES_INVALID", ReadmeBuildData.Create(BuildTarget.Portable,
            [files[0]], Metrics, null, [], ["bad\ud800"]).Error);
    }
    private static DocumentationResult<ReadmeDocument> Render(ProductManifest? manifest, PublisherProfile? publisher, ReadmeBuildData? build) =>
        SharedReadmeGenerator.Generate(manifest, publisher, build, TestContext.Current.CancellationToken);
}
