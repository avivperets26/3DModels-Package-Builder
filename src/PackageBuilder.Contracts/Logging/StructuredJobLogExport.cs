using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using PackageBuilder.Contracts.Json;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Contracts.Logging;

/// <summary>Strict per-job log export; rejects cross-job records, unknown/duplicate fields and
/// malformed lines. Input/output are bounded and free text is redacted as decoded string data.</summary>
public static class StructuredJobLogExport
{
    private static readonly HashSet<string> _fields = new(["timestampUtc", "correlationId", "component", "step", "severity", "message", "jobId", "properties"], StringComparer.Ordinal);

    /// <summary>Regenerates validated job records for sharing; an invalid line rejects the entire export.</summary>
    public static StructuredLogResult<string> Create(string? jsonLines, BuildJobId jobId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobId);
        cancellationToken.ThrowIfCancellationRequested();
        if (jsonLines is null || jsonLines.Length > 4_000_000)
        { return Failure(); }
        using var reader = new StringReader(jsonLines);
        var output = new StringBuilder();
        int count = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++count > 2048 || JsonInputSafeguards.TryParseObject(line, 600_000, out JsonDocument? document) != JsonInputError.None)
            { return Failure(); }
            using (document!)
            {
                try
                {
                    JsonElement root = document!.RootElement;
                    if (root.EnumerateObject().Any(p => !_fields.Contains(p.Name)) || root.GetProperty("jobId").GetString() != jobId.Value)
                    { return Failure(); }
                    string severity = root.GetProperty("severity").GetString()!;
                    if (!Enum.TryParse(severity, true, out StructuredLogSeverity level) || !severity.Equals(level.ToString().ToLowerInvariant(), StringComparison.Ordinal) ||
                        !DateTimeOffset.TryParse(root.GetProperty("timestampUtc").GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset timestamp))
                    { return Failure(); }
                    StructuredLogProperty[] properties = [.. root.GetProperty("properties").EnumerateObject().Select(p => new StructuredLogProperty(p.Name,
                        p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString()))];
                    StructuredLogResult<StructuredLogEvent> parsed = StructuredLogEvent.Create(timestamp,
                        root.GetProperty("correlationId").GetString(), root.GetProperty("component").GetString(),
                        root.TryGetProperty("step", out JsonElement step) ? step.GetString() : null, level, root.GetProperty("message").GetString(), properties);
                    if (!parsed.IsSuccess)
                    { return Failure(); }
                    _ = output.Append(Serialize(parsed.Value!, jobId.Value));
                    if (output.Length > 4_000_000)
                    { return Failure(); }
                }
                catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException) { return Failure(); }
            }
        }
        return StructuredLogResult.Success(output.ToString());
    }

    private static string Serialize(StructuredLogEvent entry, string jobId)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("timestampUtc", entry.TimestampUtc);
            Text("jobId", jobId);
            Text("correlationId", entry.CorrelationId);
            Text("component", entry.Component);
            if (entry.Step is not null)
            { Text("step", entry.Step); }
            writer.WriteString("severity", entry.Severity.ToString().ToLowerInvariant());
            Text("message", entry.Message);
            writer.WriteStartObject("properties");
            foreach (StructuredLogProperty property in entry.Properties)
            {
                if (property.Value is null)
                { writer.WriteNull(property.Name); }
                else
                { Text(property.Name, property.Value); }
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
            void Text(string name, string value) => writer.WriteString(name, SensitiveDiagnosticValueRedactor.RedactForExport(name, value));
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    private static StructuredLogResult<string> Failure() => StructuredLogResult.Failure<string>("SUPPORT_LOG_INVALID", "The selected job log is malformed, oversized, or belongs to another job.");
}
