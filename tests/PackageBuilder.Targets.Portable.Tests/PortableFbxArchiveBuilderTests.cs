using System.IO.Compression;

namespace PackageBuilder.Targets.Portable.Tests;

public sealed class PortableFbxArchiveBuilderTests
{
    [Fact]
    public async Task RepeatedBuildsAreByteAndLogicallyIdentical()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        using var first = new MemoryStream();
        using var second = new MemoryStream();
        PortableFbxArchiveSource[] firstSources = [.. scenario.Sources()];
        PortableFbxArchiveSource[] secondSources = [.. scenario.Sources(reverse: true)];

        PortableFbxArchiveResult firstResult = await PortableFbxArchiveBuilder.CreateAsync(
            scenario.Layout,
            firstSources,
            first,
            TestContext.Current.CancellationToken);
        PortableFbxArchiveResult secondResult = await PortableFbxArchiveBuilder.CreateAsync(
            scenario.Layout,
            secondSources,
            second,
            TestContext.Current.CancellationToken);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(first.ToArray(), second.ToArray());
        Assert.Equal(firstResult.Receipt!.ArchiveIdentity, secondResult.Receipt!.ArchiveIdentity);
        Assert.Equal(firstResult.Receipt.LogicalIdentity, secondResult.Receipt.LogicalIdentity);
        Assert.Equal("Silverwing_Talonbow_FBX.zip", firstResult.Receipt.ArchiveFileName);
        Assert.Equal(
            scenario.Layout.FlatFbxEntries.Select(entry => $"{scenario.Layout.FlatFbxFolderName}/{entry.RelativePath}"),
            firstResult.Receipt.Entries.Select(static entry => entry.RelativePath));
        Assert.All(firstResult.Receipt.Entries, entry => Assert.Equal(PortableFbxArchiveBuilder.EntryTimestampUtc, entry.TimestampUtc));

        first.Position = 0;
        using var archive = new ZipArchive(first, ZipArchiveMode.Read, leaveOpen: true);
        Assert.Equal(firstResult.Receipt.Entries.Select(static entry => entry.RelativePath), archive.Entries.Select(static entry => entry.FullName));
        Assert.All(archive.Entries, entry => Assert.Equal(PortableFbxArchiveBuilder.EntryTimestampUtc, entry.LastWriteTime));
        Dispose(firstSources);
        Dispose(secondSources);
    }

    [Theory]
    [InlineData("missing", PortableFbxArchiveError.MissingSource)]
    [InlineData("extra", PortableFbxArchiveError.UnexpectedSource)]
    [InlineData("duplicate", PortableFbxArchiveError.DuplicateSource)]
    [InlineData("null", PortableFbxArchiveError.NullSource)]
    public async Task RejectsSourceSetMismatch(string scenarioName, PortableFbxArchiveError expected)
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        List<PortableFbxArchiveSource?> sources = [.. scenario.Sources()];
        if (scenarioName == "missing")
        {
            sources.RemoveAt(0);
        }
        else if (scenarioName == "extra")
        {
            sources.Add(new PortableFbxArchiveSource(
                PortableTestValues.RecordForBytes("extra", [1]),
                new MemoryStream([1])));
        }
        else if (scenarioName == "duplicate")
        {
            sources.Add(new PortableFbxArchiveSource(sources[0]!.Record, new MemoryStream([1])));
        }
        else
        {
            sources.Add(null);
        }

        using var output = new MemoryStream();
        PortableFbxArchiveResult result = await PortableFbxArchiveBuilder.CreateAsync(
            scenario.Layout,
            sources,
            output,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Receipt);
        Assert.Equal(expected, result.Error);
        Dispose(sources.OfType<PortableFbxArchiveSource>());
    }

    [Fact]
    public async Task RejectsNullAndInvalidStreams()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        using var output = new MemoryStream();
        PortableFbxArchiveSource[] sources = [.. scenario.Sources()];

        Assert.Equal(PortableFbxArchiveError.NullLayout, (await PortableFbxArchiveBuilder.CreateAsync(null, [], output, TestContext.Current.CancellationToken)).Error);
        Assert.Equal(PortableFbxArchiveError.NullSourceCollection, (await PortableFbxArchiveBuilder.CreateAsync(scenario.Layout, null, output, TestContext.Current.CancellationToken)).Error);
        Assert.Equal(PortableFbxArchiveError.InvalidDestinationStream, (await PortableFbxArchiveBuilder.CreateAsync(scenario.Layout, sources, null, TestContext.Current.CancellationToken)).Error);
        using var nonempty = new MemoryStream([1]);
        Assert.Equal(PortableFbxArchiveError.InvalidDestinationStream, (await PortableFbxArchiveBuilder.CreateAsync(scenario.Layout, sources, nonempty, TestContext.Current.CancellationToken)).Error);
        sources[0].Stream.Dispose();
        Assert.Equal(PortableFbxArchiveError.InvalidSourceStream, (await PortableFbxArchiveBuilder.CreateAsync(scenario.Layout, sources, output, TestContext.Current.CancellationToken)).Error);
        Dispose(sources);
    }

    [Fact]
    public async Task RejectsAlteredLengthAndHashAndClearsOutput()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        PortableFbxArchiveSource[] shortSources = [.. scenario.Sources()];
        shortSources[0].Stream.Dispose();
        shortSources[0] = new PortableFbxArchiveSource(shortSources[0].Record, new MemoryStream([]));
        using var shortOutput = new MemoryStream();
        PortableFbxArchiveResult shortResult = await PortableFbxArchiveBuilder.CreateAsync(scenario.Layout, shortSources, shortOutput, TestContext.Current.CancellationToken);
        Assert.Equal(PortableFbxArchiveError.SourceLengthMismatch, shortResult.Error);
        Assert.Equal(0, shortOutput.Length);

        PortableFbxArchiveSource[] changedSources = [.. scenario.Sources()];
        changedSources[0].Stream.Dispose();
        byte[] sameLength = new byte[changedSources[0].Record.ContentIdentity.Bytes];
        changedSources[0] = new PortableFbxArchiveSource(changedSources[0].Record, new MemoryStream(sameLength));
        using var changedOutput = new MemoryStream();
        PortableFbxArchiveResult changedResult = await PortableFbxArchiveBuilder.CreateAsync(scenario.Layout, changedSources, changedOutput, TestContext.Current.CancellationToken);
        Assert.Equal(PortableFbxArchiveError.SourceHashMismatch, changedResult.Error);
        Assert.Equal(0, changedOutput.Length);
        Dispose(shortSources);
        Dispose(changedSources);
    }

    [Fact]
    public async Task RejectsSourceRecordThatDoesNotMatchThePlannedIdentity()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        PortableFbxArchiveSource[] sources = [.. scenario.Sources()];
        PortableFbxArchiveSource original = sources[0];
        var replacementRecord = new PackageBuilder.Contracts.Artifacts.ArtifactStoreRecord(
            original.Record.Artifact,
            PackageBuilder.Contracts.Artifacts.ArtifactContentIdentity.Create(
                original.Record.ContentIdentity.Bytes,
                PackageBuilder.Contracts.Artifacts.Sha256Digest.Create(new string('b', 64)).Value).Value!,
            original.Record.Layout);
        original.Stream.Dispose();
        sources[0] = new PortableFbxArchiveSource(replacementRecord, new MemoryStream(new byte[replacementRecord.ContentIdentity.Bytes]));
        using var output = new MemoryStream();

        PortableFbxArchiveResult result = await PortableFbxArchiveBuilder.CreateAsync(
            scenario.Layout,
            sources,
            output,
            TestContext.Current.CancellationToken);

        Assert.Equal(PortableFbxArchiveError.SourceHashMismatch, result.Error);
        Assert.Equal(0, output.Length);
        Dispose(sources);
    }

    [Fact]
    public async Task CancellationIsStructuredAndCallerStreamsRemainOwnedByCaller()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        PortableFbxArchiveSource[] sources = [.. scenario.Sources()];
        using var output = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        PortableFbxArchiveResult result = await PortableFbxArchiveBuilder.CreateAsync(
            scenario.Layout,
            sources,
            output,
            cancellation.Token);

        Assert.Equal(PortableFbxArchiveError.Cancelled, result.Error);
        Assert.Equal(0, output.Length);
        Assert.All(sources, source => Assert.True(source.Stream.CanRead));
        Dispose(sources);
    }

    [Fact]
    public async Task IoFailureIsStructuredAndOutputIsCleared()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        PortableFbxArchiveSource[] sources = [.. scenario.Sources()];
        PortableFbxArchiveSource original = sources[0];
        original.Stream.Dispose();
        sources[0] = new PortableFbxArchiveSource(
            original.Record,
            new ThrowingReadStream(original.Record.ContentIdentity.Bytes));
        using var output = new MemoryStream();

        PortableFbxArchiveResult result = await PortableFbxArchiveBuilder.CreateAsync(
            scenario.Layout,
            sources,
            output,
            TestContext.Current.CancellationToken);

        Assert.Equal(PortableFbxArchiveError.IoFailure, result.Error);
        Assert.Equal(0, output.Length);
        Dispose(sources);
    }

    [Fact]
    public async Task CleanupFailureDoesNotHideStructuredIoFailure()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        PortableFbxArchiveSource[] sources = [.. scenario.Sources()];
        using var output = new UncleanableDestinationStream();

        PortableFbxArchiveResult result = await PortableFbxArchiveBuilder.CreateAsync(
            scenario.Layout,
            sources,
            output,
            TestContext.Current.CancellationToken);

        Assert.Equal(PortableFbxArchiveError.IoFailure, result.Error);
        Dispose(sources);
    }

    [Fact]
    public void SourceRequiresNonNullMembers()
    {
        using var stream = new MemoryStream();
        _ = Assert.Throws<ArgumentNullException>(() => new PortableFbxArchiveSource(null!, stream));
        _ = Assert.Throws<ArgumentNullException>(() => new PortableFbxArchiveSource(PortableTestValues.Record("id", "normalized-fbx"), null!));
    }

    private static void Dispose(IEnumerable<PortableFbxArchiveSource> sources)
    {
        foreach (PortableFbxArchiveSource source in sources)
        {
            source.Stream.Dispose();
        }
    }

    private sealed class ThrowingReadStream(long length) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get; set; }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Synthetic read failure.");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Synthetic read failure."));
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class UncleanableDestinationStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }
        public override void Flush() => throw new IOException("Synthetic write failure.");
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override long Seek(long offset, SeekOrigin origin) => Position = offset;
        public override void SetLength(long value) => throw new NotSupportedException("Synthetic cleanup failure.");
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("Synthetic write failure.");
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("Synthetic write failure."));
    }
}
