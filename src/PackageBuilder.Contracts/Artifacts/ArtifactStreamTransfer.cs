using System.Security.Cryptography;

namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Filesystem-neutral bounded streaming primitive shared by deterministic package writers.
/// Streams remain caller-owned; the byte limit is checked before writing each buffer.</summary>
public static class ArtifactStreamTransfer
{
    /// <summary>Copies at most the declared budget and hashes exactly the bytes copied; throws on excess input.</summary>
    public static async Task<(long Bytes, string Digest)> CopyAndHashAsync(Stream source, Stream destination,
        long maximumBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65_536];
        long bytes = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (read > maximumBytes - bytes)
            { throw new InvalidDataException("Artifact stream exceeded its declared byte budget."); }
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            hash.AppendData(buffer, 0, read);
            bytes += read;
        }
        return (bytes, Convert.ToHexStringLower(hash.GetHashAndReset()));
    }
}
