using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PackageBuilder.Contracts.Configuration;
using PackageBuilder.Contracts.Logging;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Infrastructure.Configuration;
using PackageBuilder.Infrastructure.Logging;

namespace PackageBuilder.Infrastructure.Tests.Logging;

public sealed class ContainedJsonLinesLogWriterTests
{
    private static DateTimeOffset Timestamp { get; } =
        new(2026, 8, 4, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ApplicationLogUsesDeterministicJsonLinesAndRedactsSensitiveContent()
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        using var writer = new ContainedJsonLinesLogWriter(workspace.Root);
        StructuredLogEvent logEvent = Event(
            "Authorization=Bearer raw-message-token at C:\\Users\\avivp\\source",
            [
                new("password", "raw-password"),
                new("safe", "token=raw-property-token"),
                new("nullable", null),
            ]);

        StructuredLogResult result = await writer.WriteApplicationAsync(
            logEvent,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        string path = Path.Combine(workspace.Root, "logs", "application.log");
        byte[] bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.NotEmpty(bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        string line = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("raw-message-token", line, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-password", line, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-property-token", line, StringComparison.Ordinal);
        Assert.DoesNotContain("avivp", line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", line, StringComparison.Ordinal);
        Assert.Contains("%USERPROFILE%", line, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(line);
        JsonElement root = document.RootElement;
        Assert.Equal("2026-08-04T12:30:00+00:00", root.GetProperty("timestampUtc").GetString());
        Assert.Equal("Correlation-01", root.GetProperty("correlationId").GetString());
        Assert.Equal("test-component", root.GetProperty("component").GetString());
        Assert.Equal("test-step", root.GetProperty("step").GetString());
        Assert.Equal("information", root.GetProperty("severity").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("properties").GetProperty("nullable").ValueKind);
    }

    [Fact]
    public async Task JobLogUsesHashedFolderAndKeepsTypedJobIdentityInRecord()
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        using var writer = new ContainedJsonLinesLogWriter(workspace.Root);
        BuildJobId jobId = BuildJobId.Create("../../unsafe/job-id").Value!;
        string expectedFolder = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(jobId.Value)));

        StructuredLogResult result = await writer.WriteJobAsync(
            jobId,
            Event("Job started."),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        string expectedPath = Path.Combine(workspace.Root, "logs", "jobs", expectedFolder, "job.log");
        Assert.True(File.Exists(expectedPath));
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "unsafe")));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            expectedPath,
            TestContext.Current.CancellationToken));
        Assert.Equal(jobId.Value, document.RootElement.GetProperty("jobId").GetString());
    }

    [Fact]
    public async Task ConcurrentWritesProduceCompleteParseableRecordsWithoutLoss()
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        using var writer = new ContainedJsonLinesLogWriter(workspace.Root);
        Task<StructuredLogResult>[] writes = [.. Enumerable.Range(0, 50)
            .Select(index => writer.WriteApplicationAsync(
                Event($"Message {index}.", [new("index", index.ToString(System.Globalization.CultureInfo.InvariantCulture))]),
                TestContext.Current.CancellationToken))];

        StructuredLogResult[] results = await Task.WhenAll(writes);

        Assert.All(results, result => Assert.True(result.IsSuccess));
        string[] lines = await File.ReadAllLinesAsync(
            Path.Combine(workspace.Root, "logs", "application.log"),
            TestContext.Current.CancellationToken);
        Assert.Equal(50, lines.Length);
        Assert.All(lines, line =>
        {
            using var document = JsonDocument.Parse(line);
            Assert.Equal("Correlation-01", document.RootElement.GetProperty("correlationId").GetString());
        });
    }

    [Fact]
    public async Task ExistingLogIsAppendedWithoutReplacingEarlierRecords()
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        using var writer = new ContainedJsonLinesLogWriter(workspace.Root);

        Assert.True((await writer.WriteApplicationAsync(Event("First."), TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await writer.WriteApplicationAsync(Event("Second."), TestContext.Current.CancellationToken)).IsSuccess);

        string[] lines = await File.ReadAllLinesAsync(
            Path.Combine(workspace.Root, "logs", "application.log"),
            TestContext.Current.CancellationToken);
        Assert.Equal(2, lines.Length);
        Assert.Contains("First.", lines[0], StringComparison.Ordinal);
        Assert.Contains("Second.", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreCancelledWriteCreatesNoLogState()
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        using var writer = new ContainedJsonLinesLogWriter(workspace.Root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        StructuredLogResult result = await writer.WriteApplicationAsync(Event("Cancelled."), cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOG_WRITE_CANCELLED", result.Error!.Code);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "logs")));
    }

    [Fact]
    public async Task UnsafeLogRootReturnsSanitizedFailure()
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        using var writer = new ContainedJsonLinesLogWriter(workspace.Root, new RejectingInspector());

        StructuredLogResult result = await writer.WriteApplicationAsync(
            Event("Rejected."),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOG_PATH_UNSAFE", result.Error!.Code);
        Assert.DoesNotContain(workspace.Root, result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(workspace.Root, "logs", "application.log")));
    }

    [Fact]
    public async Task ExistingFileAtLogRootIsRejectedAsUnsafe()
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        await File.WriteAllTextAsync(
            Path.Combine(workspace.Root, "logs"),
            "collision",
            TestContext.Current.CancellationToken);
        using var writer = new ContainedJsonLinesLogWriter(workspace.Root);

        StructuredLogResult result = await writer.WriteApplicationAsync(
            Event("Cannot write."),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOG_PATH_UNSAFE", result.Error!.Code);
        Assert.DoesNotContain(workspace.Root, result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LockedLogFileReturnsSanitizedWriteFailure()
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        string logDirectory = Path.Combine(workspace.Root, "logs");
        _ = Directory.CreateDirectory(logDirectory);
        string path = Path.Combine(logDirectory, "application.log");
        await File.WriteAllTextAsync(path, "existing\n", TestContext.Current.CancellationToken);
        await using FileStream exclusiveLock = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var writer = new ContainedJsonLinesLogWriter(workspace.Root);

        StructuredLogResult result = await writer.WriteApplicationAsync(
            Event("Cannot write."),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOG_WRITE_FAILED", result.Error!.Code);
        Assert.DoesNotContain(workspace.Root, result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancellationWhileWaitingForWriterReturnsStructuredFailure()
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        var inspector = new BlockingInspector();
        using var writer = new ContainedJsonLinesLogWriter(workspace.Root, inspector);
        Task<StructuredLogResult> first = Task.Run(
            () => writer.WriteApplicationAsync(Event("First."), CancellationToken.None));
        Assert.True(inspector.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        Task<StructuredLogResult> second = writer.WriteApplicationAsync(Event("Second."), cancellation.Token);
        cancellation.Cancel();
        inspector.Release.Set();

        StructuredLogResult[] results = await Task.WhenAll(first, second);

        Assert.True(results[0].IsSuccess);
        Assert.False(results[1].IsSuccess);
        Assert.Equal("LOG_WRITE_CANCELLED", results[1].Error!.Code);
    }

    [Fact]
    public async Task PostCreationInspectionFailureProducesNoLogRecord()
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        using var writer = new ContainedJsonLinesLogWriter(workspace.Root, new RejectingSecondInspection());

        StructuredLogResult result = await writer.WriteApplicationAsync(
            Event("Rejected after creation."),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOG_PATH_UNSAFE", result.Error!.Code);
        Assert.False(File.Exists(Path.Combine(workspace.Root, "logs", "application.log")));
    }

    [Fact]
    public async Task ExistingReparseLogFileIsRejectedBeforeOpening()
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        string logDirectory = Path.Combine(workspace.Root, "logs");
        _ = Directory.CreateDirectory(logDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(logDirectory, "application.log"),
            "existing\n",
            TestContext.Current.CancellationToken);
        using var writer = new ContainedJsonLinesLogWriter(
            workspace.Root,
            fileAttributeReader: new ReparseFileAttributeReader());

        StructuredLogResult result = await writer.WriteApplicationAsync(
            Event("Rejected file."),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("LOG_PATH_UNSAFE", result.Error!.Code);
    }

    [Theory]
    [InlineData(StructuredLogSeverity.Trace, "trace")]
    [InlineData(StructuredLogSeverity.Debug, "debug")]
    [InlineData(StructuredLogSeverity.Information, "information")]
    [InlineData(StructuredLogSeverity.Warning, "warning")]
    [InlineData(StructuredLogSeverity.Error, "error")]
    [InlineData(StructuredLogSeverity.Fatal, "fatal")]
    public async Task EverySeverityHasStableLowercaseToken(
        StructuredLogSeverity severity,
        string expected)
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        using var writer = new ContainedJsonLinesLogWriter(workspace.Root);
        StructuredLogEvent logEvent = StructuredLogEvent.Create(
            Timestamp,
            "Correlation-01",
            "test-component",
            null,
            severity,
            "Severity test.").Value!;

        Assert.True((await writer.WriteApplicationAsync(
            logEvent,
            TestContext.Current.CancellationToken)).IsSuccess);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(workspace.Root, "logs", "application.log"),
            TestContext.Current.CancellationToken));
        Assert.Equal(expected, document.RootElement.GetProperty("severity").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("step").ValueKind);
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("missing")]
    public void ConstructorRejectsInvalidProjectRoots(string scenario)
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        string root = scenario == "relative"
            ? "relative"
            : Path.Combine(workspace.Root, "missing");

        _ = Assert.Throws<ArgumentException>(() => new ContainedJsonLinesLogWriter(root));
    }

    [Fact]
    public void ConstructorRejectsReparseProjectRoot()
    {
        using var workspace = new StructuredLoggingTestWorkspace();

        _ = Assert.Throws<ArgumentException>(() => new ContainedJsonLinesLogWriter(
            workspace.Root,
            fileAttributeReader: new AlwaysReparseFileAttributeReader()));
    }

    [Fact]
    public async Task DisposedWriterRejectsFurtherUse()
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        var writer = new ContainedJsonLinesLogWriter(workspace.Root);
        writer.Dispose();
        writer.Dispose();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(
            () => writer.WriteApplicationAsync(Event("After disposal."), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Bearer abc.def.ghi", "Bearer [REDACTED]")]
    [InlineData("Basic dXNlcjpwYXNz", "Basic [REDACTED]")]
    [InlineData("api_key=secret-value", "api_key=[REDACTED]")]
    [InlineData("password: secret-value", "password: [REDACTED]")]
    public async Task CommonInlineCredentialFormsAreRedacted(string source, string expected)
    {
        using var workspace = new StructuredLoggingTestWorkspace();
        using var writer = new ContainedJsonLinesLogWriter(workspace.Root);

        Assert.True((await writer.WriteApplicationAsync(
            Event(source),
            TestContext.Current.CancellationToken)).IsSuccess);

        string line = await File.ReadAllTextAsync(
            Path.Combine(workspace.Root, "logs", "application.log"),
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(line);
        Assert.Equal(expected, document.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("secret-value", line, StringComparison.Ordinal);
    }

    private static StructuredLogEvent Event(
        string message,
        IEnumerable<StructuredLogProperty?>? properties = null) =>
        StructuredLogEvent.Create(
            Timestamp,
            "Correlation-01",
            "test-component",
            "test-step",
            StructuredLogSeverity.Information,
            message,
            properties).Value!;

    private sealed class RejectingInspector : IReparsePointInspector
    {
        public ConfigurationFailure? Inspect(string projectRoot, string configuredRoot, string logicalProperty) =>
            new("PATH_REPARSE_POINT", logicalProperty, "Unsafe path.");
    }

    private sealed class RejectingSecondInspection : IReparsePointInspector
    {
        private int _callCount;

        public ConfigurationFailure? Inspect(string projectRoot, string configuredRoot, string logicalProperty) =>
            Interlocked.Increment(ref _callCount) == 2
                ? new ConfigurationFailure("PATH_REPARSE_POINT", logicalProperty, "Unsafe path.")
                : null;
    }

    private sealed class BlockingInspector : IReparsePointInspector
    {
        private int _callCount;

        public ManualResetEventSlim Entered { get; } = new(false);

        public ManualResetEventSlim Release { get; } = new(false);

        public ConfigurationFailure? Inspect(string projectRoot, string configuredRoot, string logicalProperty)
        {
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                Entered.Set();
                _ = Release.Wait(TimeSpan.FromSeconds(10));
            }

            return null;
        }
    }

    private sealed class ReparseFileAttributeReader : IFileAttributeReader
    {
        public FileAttributes GetAttributes(string path) =>
            path.EndsWith("application.log", StringComparison.Ordinal)
                ? FileAttributes.ReparsePoint
                : FileAttributes.Directory;
    }

    private sealed class AlwaysReparseFileAttributeReader : IFileAttributeReader
    {
        public FileAttributes GetAttributes(string path) => FileAttributes.ReparsePoint;
    }
}
