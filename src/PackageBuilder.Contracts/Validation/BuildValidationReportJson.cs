using System.Buffers;
using System.Text;
using System.Text.Json;
using Json.Schema;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Contracts.Json;
using PackageBuilder.Contracts.Logging;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contracts.Validation;

/// <summary>Version-one bounded UTF-8 report contract. Reuses build-lock and finding contracts;
/// rejects unknown/duplicate fields and contradictory status. No filesystem access or remote schema fetches.</summary>
public static class BuildValidationReportJson
{
    public const int CurrentSchemaVersion = 1;
    public const string SchemaIdentifier = "https://schemas.packagebuilder.dev/validation-report/v1";
    private static readonly Lazy<string> _schemaText = new(() =>
    {
        using Stream stream = typeof(BuildValidationReportJson).Assembly.GetManifestResourceStream(
            "PackageBuilder.Contracts.Schemas.validation-report.schema.json")!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    });
    private static readonly Lazy<JsonSchema> _schema = new(() =>
    {
        var registry = new SchemaRegistry();
        registry.Register(JsonSchema.FromText(BuildLockJson.SchemaText, new BuildOptions { SchemaRegistry = registry }));
        return JsonSchema.FromText(SchemaText, new BuildOptions { Dialect = Dialect.Draft202012, SchemaRegistry = registry });
    });
    public static string SchemaText => _schemaText.Value;

    /// <summary>Canonicalizes collection order, redacts diagnostics, and validates the resulting schema.
    /// Published IDs use bounded ASCII identifiers so local paths cannot become artifact/job references.</summary>
    public static BuildValidationReportResult Serialize(BuildValidationReport? report)
    {
        if (!Valid(report))
        { return BuildValidationReportResult.Failure("REPORT_DOMAIN_INVALID"); }
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
            writer.WriteString("jobId", report!.JobId.Value);
            writer.WritePropertyName("versions");
            writer.WriteRawValue(BuildLockJson.Serialize(report.Versions).Json!);
            writer.WriteString("jobState", report.JobState.CanonicalIdentifier);
            writer.WriteString("finalStatus", Status(report));
            writer.WriteStartArray("artifacts");
            foreach (ValidationReportArtifact item in report.Artifacts.OrderBy(a => a.Id.Value, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", item.Id.Value);
                writer.WriteString("role", item.Role);
                writer.WriteString("sha256", item.Sha256.Value);
                writer.WriteNumber("sizeBytes", item.SizeBytes);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            WriteResources(writer, report.Resources);
            writer.WriteStartArray("metrics");
            foreach (ValidationReportMetric metric in report.Metrics.OrderBy(m => m.ArtifactId?.Value, StringComparer.Ordinal).ThenBy(m => m.Name, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("name", metric.Name);
                writer.WriteString("unit", metric.Unit);
                writer.WriteNumber("value", metric.Value == 0 ? 0 : metric.Value);
                if (metric.ArtifactId is not null)
                { writer.WriteString("artifactId", metric.ArtifactId.Value); }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("findings");
            foreach (string finding in report.Findings.Select(RedactedFindingJson).Order(StringComparer.Ordinal))
            { writer.WriteRawValue(finding); }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        string json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        if (json.Length > JsonInputSafeguards.MaximumInputCharacters)
        { return BuildValidationReportResult.Failure("REPORT_TOO_LARGE"); }
        using var document = JsonDocument.Parse(json);
        return MatchesSchema(document.RootElement) ? new(Read(document.RootElement), json, null) : BuildValidationReportResult.Failure("REPORT_SCHEMA_INVALID");
    }

    /// <summary>Validates structure and semantics, including finding references and derived status.</summary>
    public static BuildValidationReportResult Deserialize(string? json)
    {
        JsonInputError error = JsonInputSafeguards.TryParseObject(json, JsonInputSafeguards.MaximumInputCharacters, out JsonDocument? document);
        if (error != JsonInputError.None)
        { return BuildValidationReportResult.Failure("REPORT_INPUT_INVALID"); }
        using (document!)
        {
            JsonElement root = document!.RootElement;
            if (!MatchesSchema(root))
            { return BuildValidationReportResult.Failure("REPORT_SCHEMA_INVALID"); }
            BuildValidationReport? report;
            try
            { report = Read(root); }
            catch (FormatException) { return BuildValidationReportResult.Failure("REPORT_NUMBER_INVALID"); }
            return !Valid(report) || root.GetProperty("finalStatus").GetString() != Status(report!)
                ? BuildValidationReportResult.Failure("REPORT_DOMAIN_INVALID")
                : Serialize(report);
        }
    }

    private static bool MatchesSchema(JsonElement root) => _schema.Value.Evaluate(root, new EvaluationOptions { OutputFormat = OutputFormat.Flag }).IsValid;

    private static bool Valid(BuildValidationReport? r)
    {
        if (r?.JobId is null || !Identity(r.JobId.Value) || r.JobState is null || r.Versions is null ||
            !r.JobId.Equals(r.Versions.JobId) || !BuildLockJson.Serialize(r.Versions).IsSuccessful ||
            r.Artifacts is null or { Count: > 1024 } || r.Metrics is null or { Count: > 4096 } ||
            r.Findings is null or { Count: > 1024 } || r.Resources is null)
        { return false; }
        if (r.Artifacts.Any(a => a?.Id is null || !Identity(a.Id.Value) || !Token(a.Role) || a.Sha256 is null || a.SizeBytes < 0) ||
            r.Artifacts.Select(a => a.Id).Distinct().Count() != r.Artifacts.Count)
        { return false; }
        var ids = r.Artifacts.Select(a => a.Id).ToHashSet();
        if (r.Metrics.Any(m => m is null || !Token(m.Name) || !double.IsFinite(m.Value) || m.Value < 0 ||
            m.Unit is not ("seconds" or "bytes" or "count" or "ratio" or "meters") || (m.ArtifactId is not null && !ids.Contains(m.ArtifactId))) ||
            r.Metrics.Select(m => (m.ArtifactId, m.Name)).Distinct().Count() != r.Metrics.Count ||
            r.Findings.Any(f => f is null || f.Code.Value.Length > 128 || !Identity(f.Source.Value) || f.Explanation.Value.Length > 8192 ||
                f.SuggestedAction?.Value.Length > 8192 || (f.RelatedArtifactId is not null && !ids.Contains(f.RelatedArtifactId))))
        { return false; }
        BuildResourceMetrics resources = r.Resources;
        return double.IsFinite(resources.TotalDurationSeconds) && resources.TotalDurationSeconds >= 0 &&
            resources.Stages is { Count: <= 64 } && resources.Stages.All(s => s is not null && Token(s.Stage) &&
                double.IsFinite(s.DurationSeconds) && s.DurationSeconds >= 0 && s.DurationSeconds <= resources.TotalDurationSeconds) &&
            resources.Stages.Select(s => s.Stage).Distinct(StringComparer.Ordinal).Count() == resources.Stages.Count &&
            resources.PeakProcessMemoryBytes is null or >= 0 && resources.PeakOwnedDiskBytes is null or >= 0 &&
            resources.PeakTemporaryBytes is null or >= 0 && resources.BytesRead is null or >= 0 && resources.BytesWritten is null or >= 0;
    }

    private static bool Token(string? text) => text is { Length: <= 128 } && BuildLockValueValidator.IsIdentifier(text);
    private static bool Identity(string? text) => text is { Length: > 0 and <= 128 } && char.IsAsciiLetterOrDigit(text[0]) &&
        text.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    private static string Status(BuildValidationReport r) => r.Findings.Any(f => f.BlocksRelease) || r.JobState == BuildJobState.Failed ? "failed" :
        r.JobState == BuildJobState.Cancelled ? "cancelled" : r.JobState == BuildJobState.Completed ? "passed" : "incomplete";

    private static string RedactedFindingJson(ValidationFinding finding)
    {
        ValidationFinding safe = ValidationFinding.Create(finding.Code, finding.Severity,
            FindingExplanation.Create(SensitiveDiagnosticValueRedactor.Redact("explanation", finding.Explanation.Value)).Value,
            finding.Source, finding.RelatedArtifactId, finding.SuggestedAction is null ? null :
                CorrectiveAction.Create(SensitiveDiagnosticValueRedactor.Redact("action", finding.SuggestedAction.Value)).Value, finding.BlocksRelease).Value!;
        return ValidationFindingJson.Serialize(safe).Json!;
    }

    private static void WriteResources(Utf8JsonWriter writer, BuildResourceMetrics resources)
    {
        writer.WriteStartObject("resources");
        writer.WriteNumber("totalDurationSeconds", resources.TotalDurationSeconds == 0 ? 0 : resources.TotalDurationSeconds);
        writer.WriteStartArray("stages");
        foreach (BuildStageDuration stage in resources.Stages.OrderBy(s => s.Stage, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("stage", stage.Stage);
            writer.WriteNumber("durationSeconds", stage.DurationSeconds == 0 ? 0 : stage.DurationSeconds);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        Number("peakProcessMemoryBytes", resources.PeakProcessMemoryBytes);
        Number("peakOwnedDiskBytes", resources.PeakOwnedDiskBytes);
        Number("peakTemporaryBytes", resources.PeakTemporaryBytes);
        Number("bytesRead", resources.BytesRead);
        Number("bytesWritten", resources.BytesWritten);
        writer.WriteEndObject();
        void Number(string name, long? value)
        { if (value.HasValue) { writer.WriteNumber(name, value.Value); } else { writer.WriteNull(name); } }
    }

    private static BuildValidationReport? Read(JsonElement root)
    {
        BuildLock? versions = BuildLockJson.Deserialize(root.GetProperty("versions").GetRawText()).Value;
        ValidationFinding?[] findings = [.. root.GetProperty("findings").EnumerateArray().Select(f => ValidationFindingJson.Deserialize(f.GetRawText()).Value)];
        if (versions is null || findings.Any(f => f is null))
        { return null; }
        JsonElement resources = root.GetProperty("resources");
        return new(BuildJobId.Create(root.GetProperty("jobId").GetString()).Value!, versions,
            BuildJobState.All.First(s => s.CanonicalIdentifier == root.GetProperty("jobState").GetString()),
            Array.AsReadOnly(root.GetProperty("artifacts").EnumerateArray().Select(a => new ValidationReportArtifact(
                BuildArtifactId.Create(a.GetProperty("id").GetString()).Value!, a.GetProperty("role").GetString()!,
                Sha256Digest.Create(a.GetProperty("sha256").GetString()).Value!, a.GetProperty("sizeBytes").GetInt64())).ToArray()),
            new(resources.GetProperty("totalDurationSeconds").GetDouble(), Array.AsReadOnly(resources.GetProperty("stages").EnumerateArray().Select(s =>
                new BuildStageDuration(s.GetProperty("stage").GetString()!, s.GetProperty("durationSeconds").GetDouble())).ToArray()),
                Number("peakProcessMemoryBytes"), Number("peakOwnedDiskBytes"), Number("peakTemporaryBytes"), Number("bytesRead"), Number("bytesWritten")),
            Array.AsReadOnly(root.GetProperty("metrics").EnumerateArray().Select(m => new ValidationReportMetric(m.GetProperty("name").GetString()!,
                m.GetProperty("unit").GetString()!, m.GetProperty("value").GetDouble(), m.TryGetProperty("artifactId", out JsonElement id) ? BuildArtifactId.Create(id.GetString()).Value : null)).ToArray()),
            Array.AsReadOnly(findings.Select(f => f!).ToArray()));
        long? Number(string name) => resources.GetProperty(name).ValueKind == JsonValueKind.Null ? null : resources.GetProperty(name).GetInt64();
    }
}
