namespace PackageBuilder.Contracts.Diagnostics;

/// <summary>Closed archive inventory; caller-supplied paths and arbitrary attachments are not supported.</summary>
public enum SupportDocumentKind { ManifestSummary, Versions, JobLog, ValidationJson, ValidationHtml, Inventory }

/// <summary>Owned diagnostic payload with a fixed archive entry name.</summary>
public sealed class SupportBundleDocument
{
    private readonly byte[] _bytes;
    public SupportBundleDocument(SupportDocumentKind kind, ReadOnlySpan<byte> bytes)
    {
        if (!Enum.IsDefined(kind) || bytes.Length > 8_000_000)
        { throw new ArgumentException("Invalid diagnostic document.", nameof(bytes)); }
        Kind = kind;
        _bytes = bytes.ToArray();
    }
    public SupportDocumentKind Kind { get; }
    public ReadOnlySpan<byte> Bytes => _bytes;
    public string EntryName => Kind switch
    {
        SupportDocumentKind.ManifestSummary => "manifest-summary.json",
        SupportDocumentKind.Versions => "versions.json",
        SupportDocumentKind.JobLog => "job.log.jsonl",
        SupportDocumentKind.ValidationJson => "validation.json",
        SupportDocumentKind.ValidationHtml => "validation.html",
        SupportDocumentKind.Inventory => "bundle-inventory.json",
        _ => throw new InvalidOperationException("Unsupported diagnostic document."),
    };
}

/// <summary>Encodes the exact reviewed diagnostic inventory. Implementations must not read files,
/// follow references, upload data or expose partial output; cancellation propagates.</summary>
public interface ISupportBundleArchiveWriter
{
    byte[] Write(IReadOnlyList<SupportBundleDocument> documents, CancellationToken cancellationToken = default);
}
