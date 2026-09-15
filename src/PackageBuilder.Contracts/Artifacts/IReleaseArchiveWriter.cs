using System.Collections.Immutable;

namespace PackageBuilder.Contracts.Artifacts;

/// <summary>A validated logical entry and a trusted opener. The opener supplies a new owned stream,
/// using existing containment checks for physical files. No directory discovery is permitted.</summary>
public sealed record ReleaseArchiveEntry(string Path, ArtifactContentIdentity Content,
    Func<CancellationToken, Task<Stream>> OpenReadAsync);

/// <summary>Writes only the supplied entry inventory to an empty private staging stream. Implementations
/// verify every byte identity, preserve caller stream ownership and clear partial output on failure.
/// The caller must discard staging if clearing fails, and use atomic artifact promotion after success.</summary>
public interface IReleaseArchiveWriter
{
    Task<ArtifactContentIdentity> WriteAsync(ImmutableArray<ReleaseArchiveEntry> entries, Stream destination,
        CancellationToken cancellationToken = default);
}
