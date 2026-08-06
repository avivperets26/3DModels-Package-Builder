using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using PackageBuilder.Contracts.Artifacts;

namespace PackageBuilder.Targets.Portable;

/// <summary>Creates a byte-reproducible FBX ZIP from the exact manifest-derived flat layout.</summary>
public static class PortableFbxArchiveBuilder
{
    public static DateTimeOffset EntryTimestampUtc { get; } =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Writes sorted entries with fixed metadata while verifying every source identity.</summary>
    public static async Task<PortableFbxArchiveResult> CreateAsync(
        PortableFolderLayout? layout,
        IEnumerable<PortableFbxArchiveSource?>? sources,
        Stream? destination,
        CancellationToken cancellationToken = default)
    {
        if (layout is null)
        {
            return Failure(PortableFbxArchiveError.NullLayout);
        }

        if (sources is null)
        {
            return Failure(PortableFbxArchiveError.NullSourceCollection);
        }

        if (destination is null || !destination.CanRead || !destination.CanWrite || !destination.CanSeek ||
            destination.Position != 0 || destination.Length != 0)
        {
            return Failure(PortableFbxArchiveError.InvalidDestinationStream);
        }

        PortableFbxArchiveSource?[] candidates = [.. sources];
        if (candidates.Any(static source => source is null))
        {
            return Failure(PortableFbxArchiveError.NullSource);
        }

        PortableFbxArchiveSource[] values = [.. candidates.Cast<PortableFbxArchiveSource>()];
        if (values.Any(static source => !source.Stream.CanRead))
        {
            return Failure(PortableFbxArchiveError.InvalidSourceStream);
        }

        var byId = new Dictionary<string, PortableFbxArchiveSource>(StringComparer.Ordinal);
        foreach (PortableFbxArchiveSource source in values)
        {
            if (!byId.TryAdd(source.Record.Artifact.Id.Value, source))
            {
                return Failure(PortableFbxArchiveError.DuplicateSource);
            }
        }

        string[] expectedIds =
        [
            .. layout.FlatFbxEntries.Select(static entry => entry.Source.Artifact.Id.Value),
        ];
        if (expectedIds.Any(id => !byId.ContainsKey(id)))
        {
            return Failure(PortableFbxArchiveError.MissingSource);
        }

        if (byId.Keys.Any(id => !expectedIds.Contains(id, StringComparer.Ordinal)))
        {
            return Failure(PortableFbxArchiveError.UnexpectedSource);
        }

        var receipts = new List<PortableFbxArchiveEntryReceipt>(layout.FlatFbxEntries.Count);
        try
        {
            PortableFbxArchiveError writeError = PortableFbxArchiveError.None;
            using (var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
            {
                foreach (PortableFolderEntry planned in layout.FlatFbxEntries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PortableFbxArchiveSource source = byId[planned.Source.Artifact.Id.Value];
                    if (!source.Record.ContentIdentity.Equals(planned.Source.ContentIdentity))
                    {
                        writeError = PortableFbxArchiveError.SourceHashMismatch;
                        break;
                    }

                    ZipArchiveEntry entry = archive.CreateEntry(
                        $"{layout.FlatFbxFolderName}/{planned.RelativePath}",
                        CompressionLevel.NoCompression);
                    entry.LastWriteTime = EntryTimestampUtc;
                    entry.ExternalAttributes = 0;

                    using Stream entryStream = entry.Open();
                    (long bytes, string digest) = await CopyAndHashAsync(
                        source.Stream,
                        entryStream,
                        cancellationToken).ConfigureAwait(false);
                    if (bytes != planned.Source.ContentIdentity.Bytes)
                    {
                        writeError = PortableFbxArchiveError.SourceLengthMismatch;
                        break;
                    }

                    if (!string.Equals(
                            digest,
                            planned.Source.ContentIdentity.Sha256.Value,
                            StringComparison.Ordinal))
                    {
                        writeError = PortableFbxArchiveError.SourceHashMismatch;
                        break;
                    }

                    receipts.Add(new PortableFbxArchiveEntryReceipt(
                        entry.FullName,
                        planned.Source,
                        planned.Source.ContentIdentity,
                        EntryTimestampUtc));
                }
            }

            if (writeError != PortableFbxArchiveError.None)
            {
                return ResetAndFail(destination, writeError);
            }

            destination.Position = 0;
            (long archiveBytes, string archiveDigest) = await HashAsync(destination, cancellationToken)
                .ConfigureAwait(false);
            ArtifactContentIdentity archiveIdentity = ArtifactContentIdentity.Create(
                archiveBytes,
                Sha256Digest.Create(archiveDigest).Value).Value!;
            Sha256Digest logicalIdentity = CreateLogicalIdentity(receipts);
            destination.Position = 0;
            return PortableFbxArchiveResult.Success(
                new PortableFbxArchiveReceipt(
                    $"{layout.ProductFolderName}_FBX.zip",
                    receipts,
                    archiveIdentity,
                    logicalIdentity));
        }
        catch (OperationCanceledException)
        {
            return ResetAndFail(destination, PortableFbxArchiveError.Cancelled);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return ResetAndFail(destination, PortableFbxArchiveError.IoFailure);
        }
    }

    private static async Task<(long Bytes, string Digest)> CopyAndHashAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        long bytes = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            hash.AppendData(buffer, 0, read);
            bytes = checked(bytes + read);
        }

        return (bytes, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private static async Task<(long Bytes, string Digest)> HashAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        source.Position = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        long bytes = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            hash.AppendData(buffer, 0, read);
            bytes = checked(bytes + read);
        }

        return (bytes, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private static Sha256Digest CreateLogicalIdentity(
        IEnumerable<PortableFbxArchiveEntryReceipt> entries)
    {
        var text = new StringBuilder();
        foreach (PortableFbxArchiveEntryReceipt entry in entries)
        {
            _ = text.Append(entry.RelativePath).Append('\0')
                .Append(entry.ContentIdentity.Bytes).Append('\0')
                .Append(entry.ContentIdentity.Sha256.Value).Append('\n');
        }

        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))
            .ToLowerInvariant();
        return Sha256Digest.Create(digest).Value!;
    }

    private static PortableFbxArchiveResult ResetAndFail(
        Stream destination,
        PortableFbxArchiveError error)
    {
        try
        {
            destination.SetLength(0);
            destination.Position = 0;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or ObjectDisposedException)
        {
            // The caller owns the destination and any physical cleanup that its stream cannot perform.
        }

        return Failure(error);
    }

    private static PortableFbxArchiveResult Failure(PortableFbxArchiveError error) =>
        PortableFbxArchiveResult.Failure(error);
}
