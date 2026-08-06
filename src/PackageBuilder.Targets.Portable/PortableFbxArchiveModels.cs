using System.Collections.ObjectModel;
using PackageBuilder.Contracts.Artifacts;

namespace PackageBuilder.Targets.Portable;

/// <summary>Identifies one expected deterministic portable archive failure.</summary>
public enum PortableFbxArchiveError
{
    None = 0,
    NullLayout,
    NullSourceCollection,
    NullSource,
    NullSourceStream,
    DuplicateSource,
    MissingSource,
    UnexpectedSource,
    InvalidSourceStream,
    InvalidDestinationStream,
    SourceLengthMismatch,
    SourceHashMismatch,
    Cancelled,
    IoFailure,
}

/// <summary>Pairs one planned artifact record with the caller-owned stream containing its bytes.</summary>
public sealed class PortableFbxArchiveSource(ArtifactStoreRecord record, Stream stream)
{
    public ArtifactStoreRecord Record { get; } = record ?? throw new ArgumentNullException(nameof(record));

    public Stream Stream { get; } = stream ?? throw new ArgumentNullException(nameof(stream));
}

/// <summary>Records one deterministic ZIP entry and the exact content identity written to it.</summary>
public sealed record PortableFbxArchiveEntryReceipt(
    string RelativePath,
    ArtifactStoreRecord SourceRecord,
    ArtifactContentIdentity ContentIdentity,
    DateTimeOffset TimestampUtc);

/// <summary>Records exact and logical identities for one completed deterministic FBX ZIP.</summary>
public sealed class PortableFbxArchiveReceipt
{
    private readonly ReadOnlyCollection<PortableFbxArchiveEntryReceipt> _entries;

    internal PortableFbxArchiveReceipt(
        string archiveFileName,
        IEnumerable<PortableFbxArchiveEntryReceipt> entries,
        ArtifactContentIdentity archiveIdentity,
        Sha256Digest logicalIdentity)
    {
        ArchiveFileName = archiveFileName;
        _entries = Array.AsReadOnly(entries.ToArray());
        ArchiveIdentity = archiveIdentity;
        LogicalIdentity = logicalIdentity;
    }

    public string ArchiveFileName { get; }

    public IReadOnlyList<PortableFbxArchiveEntryReceipt> Entries => _entries;

    public ArtifactContentIdentity ArchiveIdentity { get; }

    public Sha256Digest LogicalIdentity { get; }
}

/// <summary>Returns either one immutable archive receipt or one stable expected failure.</summary>
public sealed class PortableFbxArchiveResult
{
    private PortableFbxArchiveResult(
        bool isSuccess,
        PortableFbxArchiveReceipt? receipt,
        PortableFbxArchiveError error)
    {
        IsSuccess = isSuccess;
        Receipt = receipt;
        Error = error;
    }

    public bool IsSuccess { get; }

    public PortableFbxArchiveReceipt? Receipt { get; }

    public PortableFbxArchiveError Error { get; }

    internal static PortableFbxArchiveResult Success(PortableFbxArchiveReceipt receipt) =>
        new(true, receipt, PortableFbxArchiveError.None);

    internal static PortableFbxArchiveResult Failure(PortableFbxArchiveError error) =>
        new(false, null, error);
}
