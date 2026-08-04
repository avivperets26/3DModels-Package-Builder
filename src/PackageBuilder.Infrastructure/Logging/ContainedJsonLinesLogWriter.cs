using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PackageBuilder.Contracts.Configuration;
using PackageBuilder.Contracts.Logging;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Infrastructure.Configuration;

namespace PackageBuilder.Infrastructure.Logging;

/// <summary>Writes redacted JSON Lines logs only beneath the configured project root.</summary>
public sealed class ContainedJsonLinesLogWriter : IStructuredLogWriter, IDisposable
{
    private const int BufferSize = 16_384;
    private readonly string _projectRoot;
    private readonly string _logRoot;
    private readonly IReparsePointInspector _reparsePointInspector;
    private readonly IFileAttributeReader _fileAttributeReader;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private bool _disposed;

    public ContainedJsonLinesLogWriter(
        string projectRoot,
        IReparsePointInspector? reparsePointInspector = null,
        IFileAttributeReader? fileAttributeReader = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        if (!Path.IsPathFullyQualified(projectRoot) || !Directory.Exists(projectRoot))
        {
            throw new ArgumentException("The project root must be an existing absolute directory.", nameof(projectRoot));
        }

        _projectRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar);
        _logRoot = Path.Combine(_projectRoot, "logs");
        _reparsePointInspector = reparsePointInspector ?? new WindowsReparsePointInspector();
        _fileAttributeReader = fileAttributeReader ?? new WindowsFileAttributeReader();
        if ((_fileAttributeReader.GetAttributes(_projectRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new ArgumentException("The project root must not be a reparse point.", nameof(projectRoot));
        }
    }

    public Task<StructuredLogResult> WriteApplicationAsync(
        StructuredLogEvent logEvent,
        CancellationToken cancellationToken = default) =>
        WriteAsync(Path.Combine(_logRoot, "application.log"), null, logEvent, cancellationToken);

    public Task<StructuredLogResult> WriteJobAsync(
        BuildJobId jobId,
        StructuredLogEvent logEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobId);
        string jobDirectory = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(jobId.Value)));
        return WriteAsync(
            Path.Combine(_logRoot, "jobs", jobDirectory, "job.log"),
            jobId.Value,
            logEvent,
            cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writeGate.Dispose();
        _disposed = true;
    }

    private async Task<StructuredLogResult> WriteAsync(
        string path,
        string? jobId,
        StructuredLogEvent logEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (cancellationToken.IsCancellationRequested)
        {
            return StructuredLogResult.Failure("LOG_WRITE_CANCELLED", "The log write was cancelled before persistence.");
        }

        bool gateAcquired = false;
        try
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            gateAcquired = true;
            cancellationToken.ThrowIfCancellationRequested();
            string directory = Path.GetDirectoryName(path)!;
            ConfigurationFailure? unsafeExistingPath = _reparsePointInspector.Inspect(
                _projectRoot,
                directory,
                "logs");
            if (unsafeExistingPath is not null)
            {
                return UnsafePathFailure();
            }

            _ = Directory.CreateDirectory(directory);
            ConfigurationFailure? unsafePath = _reparsePointInspector.Inspect(_projectRoot, directory, "logs");
            if (unsafePath is not null)
            {
                return UnsafePathFailure();
            }

            if (File.Exists(path) && (_fileAttributeReader.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                return UnsafePathFailure();
            }

            byte[] payload = Serialize(logEvent, jobId);
            await using var stream = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return StructuredLogResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StructuredLogResult.Failure("LOG_WRITE_CANCELLED", "The log write was cancelled before completion.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return StructuredLogResult.Failure("LOG_WRITE_FAILED", "The contained log could not be written safely.");
        }
        finally
        {
            if (gateAcquired)
            {
                _ = _writeGate.Release();
            }
        }
    }

    private static StructuredLogResult UnsafePathFailure() =>
        StructuredLogResult.Failure(
            "LOG_PATH_UNSAFE",
            "The contained log path could not be verified safely.");

    private static byte[] Serialize(StructuredLogEvent logEvent, string? jobId)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("timestampUtc", logEvent.TimestampUtc);
            writer.WriteString("correlationId", logEvent.CorrelationId);
            writer.WriteString("component", logEvent.Component);
            writer.WriteString("step", logEvent.Step);

            writer.WriteString("severity", SeverityToken(logEvent.Severity));
            writer.WriteString("message", SensitiveLogValueRedactor.Redact("message", logEvent.Message));
            if (jobId is not null)
            {
                writer.WriteString("jobId", SensitiveLogValueRedactor.Redact("jobId", jobId));
            }

            writer.WriteStartObject("properties");
            foreach (StructuredLogProperty property in logEvent.Properties)
            {
                if (property.Value is null)
                {
                    writer.WriteNull(property.Name);
                }
                else
                {
                    writer.WriteString(property.Name, SensitiveLogValueRedactor.Redact(property.Name, property.Value));
                }
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        byte[] line = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(line);
        line[^1] = (byte)'\n';
        return line;
    }

    private static string SeverityToken(StructuredLogSeverity severity) =>
        severity.ToString().ToLowerInvariant();
}
