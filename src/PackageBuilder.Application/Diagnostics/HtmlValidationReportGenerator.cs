using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PackageBuilder.Application.Documentation;
using PackageBuilder.Application.Media;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Contracts.Validation;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Media;
using Scriban;
using Scriban.Runtime;

namespace PackageBuilder.Application.Diagnostics;

/// <summary>One explicitly supplied preview. Bytes must match a preview artifact in the report;
/// the trusted codec re-encodes pixels so metadata or executable containers cannot reach HTML.</summary>
public sealed record ValidationReportPreview(BuildArtifactId ArtifactId, ReadOnlyMemory<byte> EncodedImage);

/// <summary>Self-contained, UTF-8 HTML with no scripts, remote requests or source-file references.</summary>
public sealed class HtmlValidationReport
{
    internal HtmlValidationReport(string text) { Text = text; }
    public string Text { get; }
    public byte[] ToUtf8Bytes() => new UTF8Encoding(false, true).GetBytes(Text);
}

/// <summary>Renders the canonical report through the approved template engine and one shared export projection.</summary>
public sealed class HtmlValidationReportGenerator(IPreviewImageCodec codec)
{
    private readonly IPreviewImageCodec _codec = codec ?? throw new ArgumentNullException(nameof(codec));
    private static readonly Lazy<Template> _template = new(() =>
    {
        using Stream stream = typeof(HtmlValidationReportGenerator).Assembly.GetManifestResourceStream(
            "PackageBuilder.Application.Diagnostics.Templates.validation-report.sbn")!;
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        var template = Template.Parse(reader.ReadToEnd().ReplaceLineEndings("\n"));
        return template.HasErrors ? throw new InvalidOperationException("Invalid embedded report template.") : template;
    });

    /// <summary>Validates and snapshots report data; optional previews are hash checked, decoded and
    /// re-encoded to PNG. Expected failures return no partial document; cancellation propagates.</summary>
    public DocumentationResult<HtmlValidationReport> Generate(BuildValidationReport? report,
        IReadOnlyList<ValidationReportPreview>? previews = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BuildValidationReportResult safe = DiagnosticReportProjection.Create(report);
        if (!safe.IsSuccessful)
        { return new(null, "HTML_REPORT_INVALID"); }
        BuildValidationReport value = safe.Value!;
        previews ??= [];
        if (previews.Count > 32 || previews.Any(p => p?.ArtifactId is null) || previews.Select(p => p.ArtifactId).Distinct().Count() != previews.Count)
        { return new(null, "HTML_PREVIEW_INVALID"); }
        var images = new ScriptArray();
        int totalBytes = 0;
        try
        {
            foreach (ValidationReportPreview preview in previews.OrderBy(p => p.ArtifactId.Value, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidationReportArtifact? artifact = value.Artifacts.SingleOrDefault(a => a.Id.Equals(preview.ArtifactId));
                if (artifact is null || artifact.Role != "preview" || preview.EncodedImage.Length is < 8 or > 3_000_000)
                { return new(null, "HTML_PREVIEW_MISMATCH"); }
                // Hash and decode the same owned bytes even if the caller later changes its buffer.
                byte[] encoded = preview.EncodedImage.ToArray();
                if (encoded.Length != artifact.SizeBytes || Convert.ToHexStringLower(SHA256.HashData(encoded)) != artifact.Sha256.Value)
                { return new(null, "HTML_PREVIEW_MISMATCH"); }
                PreviewRaster raster = _codec.Decode(encoded);
                if (raster.Width != PreviewRaster.CaptureWidth || raster.Height != PreviewRaster.CaptureHeight)
                { return new(null, "HTML_PREVIEW_INVALID"); }
                byte[] png = _codec.Encode(raster, PreviewImageFormat.Png, 100);
                if (png.Length > 8_000_000 - totalBytes)
                { return new(null, "HTML_PREVIEW_LIMIT"); }
                totalBytes += png.Length;
                images.Add(new ScriptObject { ["id"] = Escape(artifact.Id.Value), ["data"] = Convert.ToBase64String(png) });
            }
        }
        catch (InvalidDataException) { return new(null, "HTML_PREVIEW_INVALID"); }
        using var json = JsonDocument.Parse(safe.Json!);
        string status = json.RootElement.GetProperty("finalStatus").GetString()!;
        var sections = new ScriptArray
        {
            Table("Artifacts", ["Artifact", "Role", "Bytes", "SHA-256"], value.Artifacts.Select(a => new[] { a.Id.Value, a.Role, Count(a.SizeBytes), a.Sha256.Value })),
            Table("Technical metrics", ["Metric", "Artifact", "Value", "Unit"], value.Metrics.Select(m => new[] { m.Name, m.ArtifactId?.Value ?? "Job", Number(m.Value), m.Unit })),
            Table("Stage durations", ["Stage", "Seconds"], value.Resources.Stages.Select(s => new[] { s.Stage, Number(s.DurationSeconds) })),
            Table("Resource metrics", ["Measurement", "Value", "Unit"], ResourceRows(value.Resources)),
        };
        var globals = new ScriptObject
        {
            ["job"] = Escape(value.JobId.Value),
            ["state"] = Escape(value.JobState.CanonicalIdentifier),
            ["status"] = status,
            ["status_label"] = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(status),
            ["blocking_count"] = value.Findings.Count(f => f.BlocksRelease),
            ["warning_count"] = value.Findings.Count(f => f.Severity.SerializedToken == "warning"),
            ["artifact_count"] = value.Artifacts.Count,
            ["images"] = images,
            ["tables"] = sections,
            ["versions"] = Escape(BuildLockJson.Serialize(value.Versions).Json!),
            ["findings"] = new ScriptArray(value.Findings.Select(f => new ScriptObject
            {
                ["code"] = Escape(f.Code.Value),
                ["severity"] = Escape(f.Severity.SerializedToken),
                ["blocks"] = f.BlocksRelease ? "Blocks release" : "Does not block release",
                ["explanation"] = Escape(f.Explanation.Value),
                ["source"] = Escape(f.Source.Value),
                ["artifact"] = Escape(f.RelatedArtifactId?.Value ?? "Job"),
                ["action"] = Escape(f.SuggestedAction?.Value ?? "No suggested action supplied."),
            })),
        };
        DocumentationResult<string> rendered = ReviewedTemplateRenderer.Render(_template.Value, globals, 20_000_000, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        { cancellationToken.ThrowIfCancellationRequested(); }
        return rendered.IsSuccess ? new(new(rendered.Value!), null) : new(null, rendered.Error);
    }

    private static ScriptObject Table(string heading, string[] headers, IEnumerable<string[]> rows) => new()
    {
        ["heading"] = heading,
        ["headers"] = new ScriptArray(headers),
        ["rows"] = new ScriptArray(rows.Select(row => new ScriptArray(row.Select(Escape)))),
    };
    private static string Escape(string text) => WebUtility.HtmlEncode(text);
    private static string Number(double value) => value.ToString("G", CultureInfo.InvariantCulture);
    private static IEnumerable<string[]> ResourceRows(BuildResourceMetrics r) =>
    [
        ["Total duration", Number(r.TotalDurationSeconds), "seconds"],
        ["Peak process memory", Count(r.PeakProcessMemoryBytes), "bytes"],
        ["Peak owned disk", Count(r.PeakOwnedDiskBytes), "bytes"],
        ["Peak temporary space", Count(r.PeakTemporaryBytes), "bytes"],
        ["Bytes read", Count(r.BytesRead), "bytes"], ["Bytes written", Count(r.BytesWritten), "bytes"],
    ];
    private static string Count(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "Not measured";
}
