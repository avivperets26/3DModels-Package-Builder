using System.IO.Compression;
using System.Text;
using PackageBuilder.Contracts.Diagnostics;

namespace PackageBuilder.Infrastructure.Diagnostics;

/// <summary>Encodes only the six fixed diagnostic documents in memory. Fixed ordering, timestamps
/// and attributes make repeated inputs repeatable; all streams dispose on success and failure.</summary>
public sealed class ZipSupportBundleArchiveWriter : ISupportBundleArchiveWriter
{
    public byte[] Write(IReadOnlyList<SupportBundleDocument> documents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        cancellationToken.ThrowIfCancellationRequested();
        if (documents.Count != Enum.GetValues<SupportDocumentKind>().Length || documents.Any(d => d is null) ||
            documents.Select(d => d.Kind).Distinct().Count() != documents.Count || documents.Sum(d => (long)d.Bytes.Length) > 16_000_000)
        { throw new InvalidDataException("Support document inventory is invalid or oversized."); }
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true, Encoding.UTF8))
        {
            foreach (SupportBundleDocument document in documents.OrderBy(d => d.EntryName, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = new UTF8Encoding(false, true).GetCharCount(document.Bytes);
                ZipArchiveEntry entry = zip.CreateEntry(document.EntryName, CompressionLevel.Optimal);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                entry.ExternalAttributes = 0;
                using Stream destination = entry.Open();
                destination.Write(document.Bytes);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return output.ToArray();
    }
}
