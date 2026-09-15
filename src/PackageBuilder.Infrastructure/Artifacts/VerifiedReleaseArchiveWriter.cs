using System.Collections.Immutable;
using System.IO.Compression;
using System.Text;
using PackageBuilder.Contracts.Artifacts;

namespace PackageBuilder.Infrastructure.Artifacts;

/// <summary>Deterministic streaming release envelope with exact entry hashes and no filesystem discovery.
/// This envelope is a local handoff; customers upload its selected inner deliveries individually.</summary>
public sealed class VerifiedReleaseArchiveWriter : IReleaseArchiveWriter
{
    /// <summary>Uses fixed ZIP metadata, sorted safe paths and bounded reads; never overwrites an existing stream.</summary>
    public async Task<ArtifactContentIdentity> WriteAsync(ImmutableArray<ReleaseArchiveEntry> entries, Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanRead || !destination.CanWrite || !destination.CanSeek || destination.Length != 0 || destination.Position != 0)
        { throw new ArgumentException("An empty readable, writable, seekable staging stream is required.", nameof(destination)); }
        if (entries.IsDefaultOrEmpty || entries.Length > 1024 || entries.Any(entry => entry is null || entry.Content is null
            || entry.OpenReadAsync is null || !DeliveryPath.IsValid(entry.Path))
            || entries.Select(entry => entry.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length
            || entries.Any(entry => entries.Any(other => other.Path.StartsWith(entry.Path + "/", StringComparison.OrdinalIgnoreCase))))
        { throw new ArgumentException("Invalid or colliding archive inventory.", nameof(entries)); }
        try
        {
            using (var zip = new ZipArchive(destination, ZipArchiveMode.Create, true, Encoding.UTF8))
            {
                foreach (ReleaseArchiveEntry item in entries.OrderBy(entry => entry.Path, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using Stream source = await item.OpenReadAsync(cancellationToken).ConfigureAwait(false);
                    if (ReferenceEquals(source, destination))
                    { throw new InvalidDataException("Source aliases destination."); }
                    ZipArchiveEntry entry = zip.CreateEntry(item.Path, CompressionLevel.NoCompression);
                    entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    entry.ExternalAttributes = 0;
                    using Stream target = entry.Open();
                    (long bytes, string digest) = await ArtifactStreamTransfer.CopyAndHashAsync(source, target, item.Content.Bytes, cancellationToken).ConfigureAwait(false);
                    if (bytes != item.Content.Bytes || digest != item.Content.Sha256.Value)
                    { throw new InvalidDataException("Release source differs from validated content."); }
                }
            }
            destination.Position = 0;
            (long length, string sha) = await ArtifactStreamTransfer.CopyAndHashAsync(destination, Stream.Null, destination.Length, cancellationToken).ConfigureAwait(false);
            destination.Position = 0;
            return ArtifactContentIdentity.Create(length, Sha256Digest.Create(sha).Value).Value!;
        }
        catch
        {
            // A reset error propagates too: callers must never publish a failed staging stream.
            destination.SetLength(0);
            destination.Position = 0;
            throw;
        }
    }
}
