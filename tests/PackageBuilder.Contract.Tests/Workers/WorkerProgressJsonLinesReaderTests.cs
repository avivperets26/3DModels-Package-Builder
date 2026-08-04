using System.Runtime.CompilerServices;
using PackageBuilder.Contracts.Workers;

namespace PackageBuilder.Contract.Tests.Workers;

[Trait("Task", "PB-0209")]
public sealed class WorkerProgressJsonLinesReaderTests
{
    private const string Progress =
        "{\"protocolVersion\":1,\"eventKind\":\"progress\",\"jobId\":\"Job-01\",\"stage\":\"importing\",\"percent\":35}";
    private const string Finding =
        "{\"protocolVersion\":1,\"eventKind\":\"finding\",\"jobId\":\"Job-01\",\"finding\":{\"code\":\"UNITY_TEXTURE_ALPHA_UNUSED\",\"severity\":\"warning\",\"explanation\":\"The texture alpha channel is not used.\",\"source\":\"unity-worker\",\"blocksRelease\":false}}";
    private const string Metric =
        "{\"protocolVersion\":1,\"eventKind\":\"metric\",\"jobId\":\"Job-01\",\"metric\":{\"metricId\":\"textures-imported\",\"value\":12,\"unit\":\"count\"}}";

    [Fact]
    public async Task ReadsProgressFindingAndMetricRecordsInPhysicalOrder()
    {
        IReadOnlyList<WorkerProgressJsonLineReadResult> results = await ReadAllAsync(
            string.Join('\n', Progress, Finding, Metric) + "\n");

        Assert.Equal(3, results.Count);
        Assert.Equal([1L, 2L, 3L], results.Select(result => result.LineNumber));
        Assert.All(results, result => Assert.True(result.IsSuccessful, result.Details));
        Assert.Equal(WorkerEventKind.Progress, results[0].Value!.EventKind);
        Assert.Equal(35, results[0].Value!.Percent);
        Assert.Equal(WorkerEventKind.Finding, results[1].Value!.EventKind);
        Assert.Equal(
            "UNITY_TEXTURE_ALPHA_UNUSED",
            results[1].Value!.Finding!.Code.Value);
        Assert.Equal(WorkerEventKind.Metric, results[2].Value!.EventKind);
        Assert.Equal("textures-imported", results[2].Value!.Metric!.MetricId);
        Assert.Equal(12, results[2].Value!.Metric!.Value);
    }

    [Fact]
    public async Task SupportsCrLfAndFinalUnterminatedRecord()
    {
        IReadOnlyList<WorkerProgressJsonLineReadResult> results = await ReadAllAsync(
            Progress + "\r\n" + Metric);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.True(result.IsSuccessful, result.Details));
        Assert.Equal(WorkerEventKind.Progress, results[0].Value!.EventKind);
        Assert.Equal(WorkerEventKind.Metric, results[1].Value!.EventKind);
    }

    [Fact]
    public async Task ReportsMalformedRecordAndRecoversAtNextLine()
    {
        const string SecretLikeContent = "{\"access_token\":\"must-not-be-repeated\"";
        IReadOnlyList<WorkerProgressJsonLineReadResult> results = await ReadAllAsync(
            Progress + "\n" + SecretLikeContent + "\n" + Metric + "\n");

        Assert.Equal(3, results.Count);
        Assert.True(results[0].IsSuccessful);
        Assert.False(results[1].IsSuccessful);
        Assert.Equal(2, results[1].LineNumber);
        Assert.Equal(WorkerJsonError.MalformedJson, results[1].Error);
        Assert.Null(results[1].Value);
        Assert.DoesNotContain(
            "must-not-be-repeated",
            results[1].Details ?? string.Empty,
            StringComparison.Ordinal);
        Assert.True(results[2].IsSuccessful);
        Assert.Equal(WorkerEventKind.Metric, results[2].Value!.EventKind);
    }

    [Theory]
    [InlineData("", WorkerJsonError.EmptyJson)]
    [InlineData("   ", WorkerJsonError.MalformedJson)]
    [InlineData("[]", WorkerJsonError.RootMustBeObject)]
    [InlineData("{}", WorkerJsonError.SchemaViolation)]
    [InlineData("{\"protocolVersion\":1,\"protocolVersion\":1}", WorkerJsonError.DuplicateProperty)]
    public async Task ReturnsSpecificFailureForEveryMalformedPhysicalLine(
        string malformed,
        WorkerJsonError expected)
    {
        IReadOnlyList<WorkerProgressJsonLineReadResult> results = await ReadAllAsync(
            malformed + "\n" + Progress + "\n");

        Assert.Equal(2, results.Count);
        Assert.False(results[0].IsSuccessful);
        Assert.Equal(expected, results[0].Error);
        Assert.True(results[1].IsSuccessful);
    }

    [Fact]
    public async Task DiscardsOversizedRecordAndRecoversWithoutRetainingItsContent()
    {
        string oversized = new('x', WorkerProgressEventJson.MaximumInputCharacters + 32);
        IReadOnlyList<WorkerProgressJsonLineReadResult> results = await ReadAllAsync(
            oversized + "\n" + Finding + "\n");

        Assert.Equal(2, results.Count);
        Assert.False(results[0].IsSuccessful);
        Assert.Equal(WorkerJsonError.LineTooLarge, results[0].Error);
        Assert.Null(results[0].Details);
        Assert.True(results[1].IsSuccessful);
        Assert.Equal(WorkerEventKind.Finding, results[1].Value!.EventKind);
    }

    [Fact]
    public async Task AcceptsExactMaximumLengthWithLfOrCrLfFraming()
    {
        string exactMaximum = Progress.PadRight(
            WorkerProgressEventJson.MaximumInputCharacters,
            ' ');

        IReadOnlyList<WorkerProgressJsonLineReadResult> lf = await ReadAllAsync(
            exactMaximum + "\n");
        IReadOnlyList<WorkerProgressJsonLineReadResult> crlf = await ReadAllAsync(
            exactMaximum + "\r\n");

        Assert.True(Assert.Single(lf).IsSuccessful);
        Assert.True(Assert.Single(crlf).IsSuccessful);
    }

    [Fact]
    public async Task RejectsMaximumPlusOneWithoutMistakingItForCrLf()
    {
        string maximumPlusOne = Progress.PadRight(
            WorkerProgressEventJson.MaximumInputCharacters + 1,
            ' ');

        WorkerProgressJsonLineReadResult result = Assert.Single(
            await ReadAllAsync(maximumPlusOne + "\n"));

        Assert.False(result.IsSuccessful);
        Assert.Equal(WorkerJsonError.LineTooLarge, result.Error);
    }

    [Fact]
    public async Task EmptyStreamProducesNoSyntheticRecord() =>
        Assert.Empty(await ReadAllAsync(string.Empty));

    [Fact]
    public async Task CancellationStopsIncrementalConsumption()
    {
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (WorkerProgressJsonLineReadResult _ in
                WorkerProgressJsonLinesReader.ReadAsync(
                    new CancellingTextReader(),
                    TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Fact]
    public async Task NullReaderIsRejectedAsProgrammingError()
    {
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (WorkerProgressJsonLineReadResult _ in
                WorkerProgressJsonLinesReader.ReadAsync(
                    null!,
                    TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Fact]
    public async Task HandlesRecordsSplitAcrossSmallAsynchronousReads()
    {
        using var reader = new ChunkedTextReader(Progress + "\n" + Metric, 3);

        IReadOnlyList<WorkerProgressJsonLineReadResult> results = await ReadAllAsync(reader);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.True(result.IsSuccessful, result.Details));
    }

    [Fact]
    public async Task CallerCanStopAfterOneRecordAndDisposeEnumeration()
    {
        await using IAsyncEnumerator<WorkerProgressJsonLineReadResult> enumerator =
            WorkerProgressJsonLinesReader.ReadAsync(
                new StringReader(Progress + "\n" + Metric + "\n"),
                TestContext.Current.CancellationToken).GetAsyncEnumerator(
                    TestContext.Current.CancellationToken);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(WorkerEventKind.Progress, enumerator.Current.Value!.EventKind);
    }

    [Fact]
    public async Task CallerCanDisposeBeforeStartingEnumeration()
    {
        IAsyncEnumerator<WorkerProgressJsonLineReadResult> enumerator =
            WorkerProgressJsonLinesReader.ReadAsync(
                new StringReader(Progress),
                TestContext.Current.CancellationToken).GetAsyncEnumerator(
                    TestContext.Current.CancellationToken);

        await enumerator.DisposeAsync();
    }

    [Fact]
    public async Task PhysicalReadFailurePropagatesAfterDeliveredRecord()
    {
        await using IAsyncEnumerator<WorkerProgressJsonLineReadResult> enumerator =
            WorkerProgressJsonLinesReader.ReadAsync(
                new FailingAfterFirstReadTextReader(Progress + "\n"),
                TestContext.Current.CancellationToken).GetAsyncEnumerator(
                    TestContext.Current.CancellationToken);

        Assert.True(await enumerator.MoveNextAsync());
        _ = await Assert.ThrowsAsync<IOException>(async () =>
            _ = await enumerator.MoveNextAsync());
    }

    private static Task<IReadOnlyList<WorkerProgressJsonLineReadResult>> ReadAllAsync(
        string content) =>
        ReadAllAsync(new StringReader(content));

    private static async Task<IReadOnlyList<WorkerProgressJsonLineReadResult>> ReadAllAsync(
        TextReader reader)
    {
        var results = new List<WorkerProgressJsonLineReadResult>();
        await foreach (WorkerProgressJsonLineReadResult result in
            WorkerProgressJsonLinesReader.ReadAsync(reader))
        {
            results.Add(result);
        }

        return results;
    }

    private sealed class ChunkedTextReader(string value, int maximumChunk) : TextReader
    {
        private int _position;

        public override ValueTask<int> ReadAsync(
            Memory<char> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = Math.Min(
                Math.Min(maximumChunk, buffer.Length),
                value.Length - _position);
            if (count == 0)
            {
                return ValueTask.FromResult(0);
            }

            value.AsMemory(_position, count).CopyTo(buffer);
            _position += count;
            return ValueTask.FromResult(count);
        }
    }

    private sealed class CancellingTextReader : TextReader
    {
        public override ValueTask<int> ReadAsync(
            Memory<char> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromCanceled<int>(new CancellationToken(canceled: true));
    }

    private sealed class FailingAfterFirstReadTextReader(string firstRead) : TextReader
    {
        private bool _returnedFirstRead;

        public override ValueTask<int> ReadAsync(
            Memory<char> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_returnedFirstRead)
            {
                return ValueTask.FromException<int>(new IOException("Synthetic read failure."));
            }

            firstRead.AsMemory().CopyTo(buffer);
            _returnedFirstRead = true;
            return ValueTask.FromResult(firstRead.Length);
        }
    }
}
