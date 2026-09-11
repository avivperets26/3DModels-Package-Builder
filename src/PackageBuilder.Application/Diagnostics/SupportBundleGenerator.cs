using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PackageBuilder.Application.Documentation;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Contracts.Diagnostics;
using PackageBuilder.Contracts.Logging;
using PackageBuilder.Contracts.Manifests;
using PackageBuilder.Contracts.Validation;
using PackageBuilder.Domain.Manifests;

namespace PackageBuilder.Application.Diagnostics;

/// <summary>Explicit diagnostic inputs for one job; no directory, attachment, model or texture inputs.</summary>
public sealed record SupportBundleRequest(BuildValidationReport Report, string ManifestJson, string JobLogJsonLines);

/// <summary>Owned ZIP bytes and an inspectable inventory prepared before any user-selected file write.</summary>
public sealed class SupportBundle
{
    private readonly byte[] _bytes;
    internal SupportBundle(byte[] bytes, IReadOnlyList<string> entries)
    { _bytes = bytes; Entries = entries; Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)); }
    public ReadOnlySpan<byte> Bytes => _bytes;
    public IReadOnlyList<string> Entries { get; }
    public string Sha256 { get; }
}

/// <summary>Creates a bounded, redacted support ZIP from an allowlist of regenerated diagnostics.
/// Manifests become labelled summaries; raw source references, branding and asset names are omitted.</summary>
public sealed class SupportBundleGenerator(ISupportBundleArchiveWriter writer, HtmlValidationReportGenerator html)
{
    private readonly ISupportBundleArchiveWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    private readonly HtmlValidationReportGenerator _html = html ?? throw new ArgumentNullException(nameof(html));
    private static readonly string[] _omitted = ["source-assets", "textures", "source-references", "asset-names", "publisher-details", "private-marketplace-data"];

    /// <summary>Validates every input before encoding the fixed diagnostic inventory. Never reads source files.</summary>
    public DocumentationResult<SupportBundle> Generate(SupportBundleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        BuildValidationReportResult safe = DiagnosticReportProjection.Create(request.Report);
        if (!safe.IsSuccessful)
        { return new(null, "SUPPORT_REPORT_INVALID"); }
        ProductManifest? manifest = ProductManifestJson.Deserialize(request.ManifestJson).Value;
        if (manifest is null || manifest.SchemaVersion != safe.Value!.Versions.ManifestSchemaVersion)
        { return new(null, "SUPPORT_MANIFEST_INVALID"); }
        StructuredLogResult<string> log = StructuredJobLogExport.Create(request.JobLogJsonLines, safe.Value.JobId, cancellationToken);
        if (!log.IsSuccess)
        { return new(null, "SUPPORT_LOG_INVALID"); }
        DocumentationResult<HtmlValidationReport> page = _html.Generate(safe.Value, cancellationToken: cancellationToken);
        if (!page.IsSuccess)
        { return new(null, page.Error); }
        try
        {
            var documents = new List<SupportBundleDocument>
        {
            Document(SupportDocumentKind.ManifestSummary, ManifestSummary(manifest)),
            Document(SupportDocumentKind.Versions, BuildLockJson.Serialize(safe.Value.Versions).Json!),
            Document(SupportDocumentKind.JobLog, log.Value!),
            Document(SupportDocumentKind.ValidationJson, safe.Json!),
            new(SupportDocumentKind.ValidationHtml, page.Value!.ToUtf8Bytes()),
        };
            documents.Add(new(SupportDocumentKind.Inventory, Inventory(documents)));
            cancellationToken.ThrowIfCancellationRequested();
            byte[] archive = _writer.Write(documents.AsReadOnly(), cancellationToken);
            return new(new(archive, Array.AsReadOnly(documents.Select(d => d.EntryName).Order(StringComparer.Ordinal).ToArray())), null);
        }
        catch (IOException) { return new(null, "SUPPORT_ARCHIVE_FAILED"); }
        catch (ArgumentException) { return new(null, "SUPPORT_DOCUMENT_LIMIT"); }
    }

    private static SupportBundleDocument Document(SupportDocumentKind kind, string text) => new(kind, new UTF8Encoding(false, true).GetBytes(text));

    private static string ManifestSummary(ProductManifest manifest) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        documentType = "support-manifest-summary",
        sourceSchemaVersion = manifest.SchemaVersion,
        productCase = manifest.ProductCase.CanonicalIdentifier,
        targets = manifest.Targets.Select(t => t.CanonicalIdentifier).Order(StringComparer.Ordinal).ToArray(),
        sourceAssetCount = manifest.SourceAssets.Count,
        materialCount = manifest.Materials.Count,
        animationCount = manifest.Animations.Count,
        itemCount = manifest.ItemSet?.Items.Count ?? manifest.ItemCollection?.Items.Count ?? 1,
        rigPresent = manifest.Rig is not null,
        omitted = _omitted,
    });

    private static byte[] Inventory(IEnumerable<SupportBundleDocument> documents)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("schemaVersion", 1);
            json.WriteString("policy", "diagnostic-export-v1");
            json.WriteString("scope", "Regenerated diagnostics only; no source assets, previews, raw manifest or arbitrary attachments.");
            json.WriteStartArray("entries");
            foreach (SupportBundleDocument document in documents.OrderBy(d => d.EntryName, StringComparer.Ordinal))
            {
                json.WriteStartObject();
                json.WriteString("name", document.EntryName);
                json.WriteNumber("sizeBytes", document.Bytes.Length);
                json.WriteString("sha256", Convert.ToHexStringLower(SHA256.HashData(document.Bytes)));
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }
}
