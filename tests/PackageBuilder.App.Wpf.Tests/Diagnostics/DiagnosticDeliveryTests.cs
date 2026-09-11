using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PackageBuilder.App.Wpf.Media;
using PackageBuilder.Application.Diagnostics;
using PackageBuilder.Application.Documentation;
using PackageBuilder.Application.Media;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Contracts.Diagnostics;
using PackageBuilder.Contracts.Validation;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Media;
using PackageBuilder.Domain.Tools;
using PackageBuilder.Domain.Validation;
using PackageBuilder.Infrastructure.Diagnostics;

namespace PackageBuilder.App.Wpf.Tests.Diagnostics;

/// <summary>Exercises application report/support services with the real Windows image codec and ZIP
/// adapter. In-memory archives leave no package debris; optional evidence is synthetic HTML only.</summary>
[Trait("Task", "PB-0911")]
[Trait("Task", "PB-0912")]
public sealed class DiagnosticDeliveryTests
{
    private static readonly WindowsPreviewImageCodec _codec = new();
    private static readonly HtmlValidationReportGenerator _html = new(_codec);
    private static readonly ZipSupportBundleArchiveWriter _zip = new();
    private static readonly string[] _expectedEntries = ["bundle-inventory.json", "job.log.jsonl", "manifest-summary.json", "validation.html", "validation.json", "versions.json"];

    [Theory]
    [InlineData("completed", "passed")]
    [InlineData("failed", "failed")]
    [InlineData("cancelled", "cancelled")]
    [InlineData("validating", "incomplete")]
    public void HtmlAgreesWithCanonicalStatusAndPreservesExactMetrics(string state, string expected)
    {
        BuildJobState jobState = state switch
        {
            "completed" => BuildJobState.Completed,
            "failed" => BuildJobState.Failed,
            "cancelled" => BuildJobState.Cancelled,
            _ => BuildJobState.Validating
        };
        BuildValidationReport report = Report() with
        {
            JobState = jobState,
            Findings = [Finding("Check triangle count", false)]
        };
        string page = Render(report);
        Assert.Contains($"status {expected}", page, StringComparison.Ordinal);
        Assert.Contains("Warnings<strong>1", page, StringComparison.Ordinal);
        Assert.Contains("Does not block release", page, StringComparison.Ordinal);
        Assert.Contains("9007199254740993", page, StringComparison.Ordinal);
        Assert.Contains("Not measured", page, StringComparison.Ordinal);
        Assert.Contains("6000.3.10f1", page, StringComparison.Ordinal);
        Assert.Contains("No previews included", page, StringComparison.Ordinal);
        Assert.Contains("123.5", page, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlEncodesUntrustedTextWithoutExecutingTemplatesOrLeakingPrivateProse()
    {
        const string Attack = "<img src=x onerror=alert(1)> {{ 7 * 7 }} café 東京";
        string page = Render(Report() with { Findings = [Finding(Attack, true), Finding("password=\"hidden tail\"", true, "SECRET")] });
        Assert.Contains(WebUtility.HtmlEncode(Attack), page, StringComparison.Ordinal);
        Assert.DoesNotContain("<img src=x", page, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden", page, StringComparison.Ordinal);
        Assert.DoesNotContain("tail", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("default-src 'none'", page, StringComparison.Ordinal);
        Assert.Contains("status failed", page, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', page);
        Assert.False(Encoding.UTF8.GetBytes(page).AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
    }

    [Fact]
    public void HtmlRenderingIsCultureIndependentAndInvalidReportsFailClosed()
    {
        string normal = Render(Report());
        CultureInfo prior = CultureInfo.CurrentCulture;
        try
        { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); Assert.Equal(normal, Render(Report())); }
        finally { CultureInfo.CurrentCulture = prior; }
        Assert.False(_html.Generate(null, cancellationToken: TestContext.Current.CancellationToken).IsSuccess);
        BuildValidationReport report = Report();
        Assert.False(_html.Generate(report with { Artifacts = [report.Artifacts[0], report.Artifacts[0]] },
            cancellationToken: TestContext.Current.CancellationToken).IsSuccess);
    }

    [Fact]
    public void RealPreviewRoundTripProducesOfflineHtmlAndOptionalCompactEvidence()
    {
        byte[] pixels = new byte[1920 * 1080 * 4];
        for (int y = 0; y < 1080; y++)
        {
            for (int x = 0; x < 1920; x++)
            {
                int offset = (y * 1920 + x) * 4;
                bool shape = x is > 680 and < 1240 && y is > 250 and < 850;
                pixels[offset] = shape ? (byte)126 : (byte)25;
                pixels[offset + 1] = shape ? (byte)165 : (byte)37;
                pixels[offset + 2] = shape ? (byte)148 : (byte)44;
                pixels[offset + 3] = 255;
            }
        }
        byte[] jpeg = _codec.Encode(new(1920, 1080, pixels), PreviewImageFormat.Jpeg, 95);
        BuildValidationReport report = WithPreview(jpeg) with { Findings = [Finding("Material count exceeds the configured limit.", true)] };
        DocumentationResult<HtmlValidationReport> result = _html.Generate(report, [new(report.Artifacts[0].Id, jpeg)], TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Error);
        string page = result.Value!.Text;
        const string Marker = "data:image/png;base64,";
        int start = page.IndexOf(Marker, StringComparison.Ordinal) + Marker.Length;
        byte[] embedded = Convert.FromBase64String(page[start..page.IndexOf('"', start)]);
        PreviewRaster decoded = _codec.Decode(embedded);
        Assert.Equal(1920, decoded.Width);
        Assert.True(_codec.Decode(jpeg).Pixels.SequenceEqual(decoded.Pixels));
        string? evidenceRoot = Environment.GetEnvironmentVariable("PB_DIAGNOSTIC_EVIDENCE");
        if (evidenceRoot is not null)
        {
            string allowed = Path.Combine(ProjectRoot(), "artifacts", "PB-0911");
            Assert.Equal(allowed, Path.GetFullPath(evidenceRoot));
            _ = Directory.CreateDirectory(allowed);
            File.WriteAllBytes(Path.Combine(allowed, "validation.html"), result.Value.ToUtf8Bytes());
        }
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("size")]
    [InlineData("role")]
    [InlineData("duplicate")]
    [InlineData("malformed")]
    [InlineData("count")]
    [InlineData("oversize")]
    public void InvalidPreviewsNeverProducePartialHtml(string mutation)
    {
        byte[] bytes = mutation == "oversize" ? new byte[3_000_001] : new byte[32];
        BuildValidationReport report = WithPreview(bytes);
        var preview = new ValidationReportPreview(report.Artifacts[0].Id, bytes);
        if (mutation == "hash")
        { bytes[0] = 1; }
        if (mutation == "size")
        { report = report with { Artifacts = [report.Artifacts[0] with { SizeBytes = 31 }] }; }
        if (mutation == "role")
        { report = report with { Artifacts = [report.Artifacts[0] with { Role = "model" }] }; }
        ValidationReportPreview[] previews = mutation == "duplicate" ? [preview, preview] : mutation == "count" ? [.. Enumerable.Repeat(preview, 33)] : [preview];
        DocumentationResult<HtmlValidationReport> result = _html.Generate(report, previews, TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("static")]
    [InlineData("rigged")]
    [InlineData("rigged-animated")]
    [InlineData("item-set")]
    [InlineData("item-collection")]
    public void SupportZipRoundTripsOnlyReviewedDiagnosticsAndVerifiableInventory(string fixture)
    {
        SupportBundleRequest request = Request(fixture);
        var generator = new SupportBundleGenerator(_zip, _html);
        DocumentationResult<SupportBundle> result = generator.Generate(request, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Error);
        byte[] bytes = result.Value!.Bytes.ToArray();
        Assert.Equal(bytes, generator.Generate(request, TestContext.Current.CancellationToken).Value!.Bytes.ToArray());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), result.Value.Sha256);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Equal(_expectedEntries, archive.Entries.Select(e => e.FullName));
        var content = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            Assert.Equal(1980, entry.LastWriteTime.Year);
            using Stream source = entry.Open();
            using var copy = new MemoryStream();
            source.CopyTo(copy);
            content.Add(entry.FullName, copy.ToArray());
            string text = new UTF8Encoding(false, true).GetString(copy.ToArray());
            foreach (string forbidden in new[] { "private-secret", "private-person", "private-path", "private-tail", "StoneArch", "AvivPeretsFBX", ".fbx", ".png", "data:image", "sourceAssets" })
            { Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase); }
        }
        using var inventory = JsonDocument.Parse(content["bundle-inventory.json"]);
        foreach (JsonElement entry in inventory.RootElement.GetProperty("entries").EnumerateArray())
        {
            byte[] payload = content[entry.GetProperty("name").GetString()!];
            Assert.Equal(payload.Length, entry.GetProperty("sizeBytes").GetInt32());
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(payload)), entry.GetProperty("sha256").GetString());
        }
        Assert.True(BuildValidationReportJson.Deserialize(Encoding.UTF8.GetString(content["validation.json"])).IsSuccessful);
        Assert.True(BuildLockJson.Deserialize(Encoding.UTF8.GetString(content["versions.json"])).IsSuccessful);
        using var summary = JsonDocument.Parse(content["manifest-summary.json"]);
        Assert.Equal("support-manifest-summary", summary.RootElement.GetProperty("documentType").GetString());
        Assert.True(summary.RootElement.GetProperty("sourceAssetCount").GetInt32() > 0);
    }

    [Fact]
    public void InvalidInputsAndWriterFailureReturnNoBundleAndCancellationPropagates()
    {
        var generator = new SupportBundleGenerator(_zip, _html);
        SupportBundleRequest request = Request("static");
        foreach (SupportBundleRequest invalid in new[] { request with { ManifestJson = "{}" }, request with { JobLogJsonLines = "{}" }, request with { Report = null! } })
        { Assert.False(generator.Generate(invalid, TestContext.Current.CancellationToken).IsSuccess); }
        Assert.Equal("SUPPORT_ARCHIVE_FAILED", new SupportBundleGenerator(new FailingWriter(), _html).Generate(request, TestContext.Current.CancellationToken).Error);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _ = Assert.Throws<OperationCanceledException>(() => generator.Generate(request, cancelled.Token));
        _ = Assert.Throws<OperationCanceledException>(() => _html.Generate(request.Report, cancellationToken: cancelled.Token));
        _ = Assert.Throws<OperationCanceledException>(() => _zip.Write([], cancelled.Token));
    }

    [Fact]
    public void ArchiveBoundaryRejectsDuplicateInventoryAndOversizedOrInvalidUtf8Documents()
    {
        _ = Assert.Throws<InvalidDataException>(() => _zip.Write([], TestContext.Current.CancellationToken));
        SupportBundleDocument[] documents = [.. Enum.GetValues<SupportDocumentKind>().Select(k => new SupportBundleDocument(k, "{}"u8))];
        documents[0] = documents[1];
        _ = Assert.Throws<InvalidDataException>(() => _zip.Write(documents, TestContext.Current.CancellationToken));
        _ = Assert.Throws<ArgumentException>(() => new SupportBundleDocument(SupportDocumentKind.JobLog, new byte[8_000_001]));
        documents = [.. Enum.GetValues<SupportDocumentKind>().Select(k => new SupportBundleDocument(k, [255]))];
        _ = Assert.Throws<DecoderFallbackException>(() => _zip.Write(documents, TestContext.Current.CancellationToken));
    }

    private static string Render(BuildValidationReport report)
    {
        DocumentationResult<HtmlValidationReport> result = _html.Generate(report, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Error);
        return result.Value!.Text;
    }

    private static SupportBundleRequest Request(string fixture) => new(Report() with { Findings = [Finding("password=\"private-secret private-tail\"", true)] },
        File.ReadAllText(Path.Combine(ProjectRoot(), "tests", "fixtures", "manifests", "valid", fixture + ".json")),
        """{"timestampUtc":"2026-09-11T00:00:00Z","jobId":"job-support","correlationId":"correlation","component":"worker","severity":"warning","message":"password=[REDACTED] private-tail","properties":{"sourcePath":"C:\\Users\\private-person\\private-path","token":"private-secret"}}""");

    private static BuildValidationReport WithPreview(byte[] bytes)
    {
        BuildValidationReport report = Report();
        return report with { Artifacts = [report.Artifacts[0] with { Sha256 = Sha256Digest.Create(Convert.ToHexStringLower(SHA256.HashData(bytes))).Value!, SizeBytes = bytes.Length }] };
    }

    private static ValidationFinding Finding(string text, bool blocks, string code = "MATERIAL_LIMIT") => ValidationFinding.Create(
        FindingCode.Create(code).Value, FindingSeverity.Warning, FindingExplanation.Create(text).Value,
        FindingSourceComponent.Create("validator").Value, null, CorrectiveAction.Create("Review material assignments and rebuild.").Value, blocks).Value!;

    private static BuildValidationReport Report()
    {
        BuildJobId job = BuildJobId.Create("job-support").Value!;
        var versions = new BuildLock(job, "1.0.0", ToolVersion.Create(ToolKind.DotNet, "10.0.302").Value!,
            ToolVersion.Create(ToolKind.Blender, "5.0.0").Value!, ToolVersion.Create(ToolKind.Unity, "6000.3.10f1").Value!,
            ToolVersion.Create(ToolKind.Unreal, "5.8.0").Value!, 1, [new("unity-worker", "1.0.0")], [new("fab", "default", "1.0.0")]);
        BuildArtifactId id = BuildArtifactId.Create("hero").Value!;
        return new(job, versions, BuildJobState.Completed, [new(id, "preview", Sha256Digest.Create(new string('a', 64)).Value!, 9007199254740993)],
            new(1, [new("render", .5)], null, 42, 0, 42, 42), [new("triangles", "count", 123.5, id)], []);
    }

    private static string ProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PackageBuilder.sln")))
        { directory = directory.Parent; }
        return directory?.FullName ?? throw new InvalidOperationException("Project root missing.");
    }

    private sealed class FailingWriter : ISupportBundleArchiveWriter
    {
        public byte[] Write(IReadOnlyList<SupportBundleDocument> documents, CancellationToken cancellationToken = default) => throw new IOException("Synthetic failure");
    }
}
