using System.Buffers;
using System.Text;
using System.Text.Json;
using Json.Schema;
using PackageBuilder.Contracts.Validation;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contracts.Workers;

public static class WorkerResultJson
{
    public const int CurrentProtocolVersion = 1;
    public const int MaximumInputCharacters = WorkerJsonInfrastructure.MaximumInputCharacters;
    public const int MaximumDepth = WorkerJsonInfrastructure.MaximumDepth;
    public const string SchemaIdentifier = "https://schemas.packagebuilder.dev/worker-result/v1";
    public const string SchemaDraft = WorkerJsonInfrastructure.SchemaDraft;

    private const string Resource = "PackageBuilder.Contracts.Schemas.worker-result.schema.json";
    private static readonly Lazy<string> _schemaText =
        new(() => WorkerJsonInfrastructure.ReadSchemaText(Resource));
    private static readonly Lazy<JsonSchema> _schema =
        new(() => WorkerJsonInfrastructure.BuildSchema(_schemaText.Value));

    public static string SchemaText => _schemaText.Value;

    public static WorkerJsonSchemaValidationResult ValidateSchemaDefinition() =>
        WorkerJsonInfrastructure.ValidateSchemaDefinition(SchemaText, SchemaIdentifier);

    public static WorkerJsonSchemaValidationResult ValidateSchemaDefinition(string? schemaText) =>
        WorkerJsonInfrastructure.ValidateSchemaDefinition(schemaText, SchemaIdentifier);

    public static WorkerJsonSchemaValidationResult ValidateJsonAgainstSchema(string? json) =>
        WorkerJsonInfrastructure.ValidateJson(json, _schema.Value, MaximumInputCharacters);

    public static WorkerJsonSerializationResult Serialize(WorkerResult? result)
    {
        if (result is null)
        {
            return WorkerJsonSerializationResult.Failure(WorkerJsonError.NullValue);
        }

        if (!IsSemanticallyValid(result, out _) ||
            result.Metrics.Any(metric => !WorkerJsonInfrastructure.IsFinite(metric.Value)))
        {
            return WorkerJsonSerializationResult.Failure(WorkerJsonError.SemanticViolation);
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("protocolVersion", result.ProtocolVersion);
            writer.WriteString("jobId", result.JobId.Value);
            writer.WriteString("status", WorkerJsonTokens.ResultStatus(result.Status));
            writer.WriteString("workerVersion", result.WorkerVersion);
            if (result.EngineVersion is not null)
            {
                writer.WriteString("engineVersion", result.EngineVersion);
            }

            writer.WriteBoolean("outputsPromoted", result.OutputsPromoted);
            writer.WritePropertyName("artifacts");
            writer.WriteStartArray();
            foreach (WorkerArtifact artifact in result.Artifacts)
            {
                WriteArtifact(writer, artifact);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("findings");
            writer.WriteStartArray();
            foreach (ValidationFinding finding in result.Findings)
            {
                _ = WorkerProgressEventJson.WriteFinding(writer, finding);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("metrics");
            writer.WriteStartArray();
            foreach (WorkerMetric metric in result.Metrics)
            {
                WorkerProgressEventJson.WriteMetric(writer, metric);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("logReferences");
            writer.WriteStartArray();
            foreach (string reference in result.LogReferences)
            {
                writer.WriteStringValue(reference);
            }

            writer.WriteEndArray();
            writer.WriteString("retrySafety", WorkerJsonTokens.RetrySafety(result.RetrySafety));
            if (result.Cancellation is not null)
            {
                writer.WritePropertyName("cancellation");
                writer.WriteStartObject();
                writer.WriteString(
                    "outcome",
                    WorkerJsonTokens.CancellationOutcome(result.Cancellation.Outcome));
                if (result.Cancellation.Details is not null)
                {
                    writer.WriteString("details", result.Cancellation.Details);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        string json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        return ValidateJsonAgainstSchema(json).IsValid
            ? WorkerJsonSerializationResult.Success(json)
            : WorkerJsonSerializationResult.Failure(WorkerJsonError.SemanticViolation);
    }

    public static WorkerJsonDeserializationResult<WorkerResult> Deserialize(string? json)
    {
        WorkerJsonError error = WorkerJsonInfrastructure.TryParse(
            json,
            _schema.Value,
            MaximumInputCharacters,
            out JsonDocument? document);
        if (error != WorkerJsonError.None)
        {
            return Failure(error);
        }

        using (document!)
        {
            JsonElement root = document!.RootElement;
            BuildModelValidationResult<BuildJobId> job =
                BuildJobId.Create(root.GetProperty("jobId").GetString());
            if (!job.IsValid)
            {
                return Failure(WorkerJsonError.DomainViolation, job.Error.ToString());
            }

            var artifacts = new List<WorkerArtifact>();
            foreach (JsonElement element in root.GetProperty("artifacts").EnumerateArray())
            {
                WorkerArtifact? artifact = ParseArtifact(element, out string? detail);
                if (artifact is null)
                {
                    return Failure(WorkerJsonError.DomainViolation, detail);
                }

                artifacts.Add(artifact);
            }

            var findings = new List<ValidationFinding>();
            foreach (JsonElement element in root.GetProperty("findings").EnumerateArray())
            {
                ValidationFindingDeserializationResult finding =
                    ValidationFindingJson.Deserialize(element.GetRawText());
                if (!finding.IsSuccessful)
                {
                    return Failure(WorkerJsonError.DomainViolation, finding.Error.ToString());
                }

                findings.Add(finding.Value!);
            }

            WorkerMetric[] metrics =
            [
                .. root.GetProperty("metrics").EnumerateArray()
                    .Select(WorkerProgressEventJson.ParseMetric),
            ];
            if (metrics.Any(metric => !WorkerJsonInfrastructure.IsFinite(metric.Value)))
            {
                return Failure(WorkerJsonError.SemanticViolation, "Metric must be finite.");
            }

            WorkerCancellation? cancellation =
                root.TryGetProperty("cancellation", out JsonElement cancellationElement)
                    ? new WorkerCancellation(
                        WorkerJsonTokens.ParseCancellationOutcome(
                            cancellationElement.GetProperty("outcome").GetString()!),
                        cancellationElement.TryGetProperty("details", out JsonElement details)
                            ? details.GetString()
                            : null)
                    : null;
            var result = new WorkerResult(
                root.GetProperty("protocolVersion").GetInt32(),
                job.Value!,
                WorkerJsonTokens.ParseResultStatus(root.GetProperty("status").GetString()!),
                root.GetProperty("workerVersion").GetString()!,
                root.GetProperty("outputsPromoted").GetBoolean(),
                artifacts,
                findings,
                metrics,
                root.GetProperty("logReferences").EnumerateArray()
                    .Select(element => element.GetString()!),
                WorkerJsonTokens.ParseRetrySafety(
                    root.GetProperty("retrySafety").GetString()!),
                root.TryGetProperty("engineVersion", out JsonElement engine)
                    ? engine.GetString()
                    : null,
                cancellation);
            return IsSemanticallyValid(result, out string? semanticDetail)
                ? WorkerJsonDeserializationResult<WorkerResult>.Success(result)
                : Failure(WorkerJsonError.SemanticViolation, semanticDetail);
        }
    }

    private static WorkerArtifact? ParseArtifact(JsonElement element, out string? detail)
    {
        BuildModelValidationResult<BuildArtifactId> id =
            BuildArtifactId.Create(element.GetProperty("artifactId").GetString());
        BuildModelValidationResult<BuildJobId> job =
            BuildJobId.Create(element.GetProperty("jobId").GetString());
        BuildModelValidationResult<BuildArtifactRole> role =
            BuildArtifactRole.Create(element.GetProperty("role").GetString());
        if (!id.IsValid || !job.IsValid || !role.IsValid)
        {
            detail = "Artifact identity, ownership, or role violates the Domain contract.";
            return null;
        }

        detail = null;
        return new WorkerArtifact(
            id.Value!,
            job.Value!,
            role.Value!,
            element.GetProperty("logicalReference").GetString()!,
            element.TryGetProperty("target", out JsonElement target)
                ? WorkerJsonTokens.ParseTarget(target.GetString()!)
                : null,
            element.TryGetProperty("sha256", out JsonElement hash)
                ? hash.GetString()
                : null,
            element.TryGetProperty("byteCount", out JsonElement bytes)
                ? bytes.GetInt64()
                : null);
    }

    private static void WriteArtifact(Utf8JsonWriter writer, WorkerArtifact artifact)
    {
        writer.WriteStartObject();
        writer.WriteString("artifactId", artifact.ArtifactId.Value);
        writer.WriteString("jobId", artifact.JobId.Value);
        writer.WriteString("role", artifact.Role.CanonicalIdentifier);
        writer.WriteString("logicalReference", artifact.LogicalReference);
        if (artifact.Target is not null)
        {
            writer.WriteString("target", artifact.Target.CanonicalIdentifier);
        }

        if (artifact.Sha256 is not null)
        {
            writer.WriteString("sha256", artifact.Sha256);
        }

        if (artifact.ByteCount.HasValue)
        {
            writer.WriteNumber("byteCount", artifact.ByteCount.Value);
        }

        writer.WriteEndObject();
    }

    private static bool IsSemanticallyValid(WorkerResult result, out string? details)
    {
        details = null;
        if (result.ProtocolVersion != CurrentProtocolVersion)
        {
            details = "Unknown protocol version.";
            return false;
        }

        if (result.Artifacts.Any(artifact => artifact is null) ||
            result.Findings.Any(finding => finding is null) ||
            result.Metrics.Any(metric => metric is null) ||
            result.LogReferences.Any(reference => reference is null))
        {
            details = "Result collections cannot contain null values.";
            return false;
        }

        bool statusValid = result.Status switch
        {
            WorkerResultStatus.Success =>
                result.RetrySafety == WorkerRetrySafety.Unsafe &&
                result.Cancellation is null &&
                result.Findings.All(finding => !finding.BlocksRelease),
            WorkerResultStatus.Failure =>
                !result.OutputsPromoted && result.Cancellation is null,
            WorkerResultStatus.Cancelled =>
                !result.OutputsPromoted &&
                result.Cancellation is not null &&
                result.RetrySafety != WorkerRetrySafety.Unsafe,
            _ => false,
        };
        if (!statusValid)
        {
            details = "Result status, findings, promotion, retry safety, or cancellation conflict.";
            return false;
        }

        var artifactIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkerArtifact artifact in result.Artifacts)
        {
            if (!artifactIds.Add(artifact.ArtifactId.Value) ||
                !artifact.JobId.Equals(result.JobId))
            {
                details = "Artifact identities must be unique and owned by the result job.";
                return false;
            }
        }

        var findingKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (ValidationFinding finding in result.Findings)
        {
            string key = $"{finding.Code.Value}\0{finding.RelatedArtifactId?.Value}";
            if (!findingKeys.Add(key) ||
                (finding.RelatedArtifactId is not null &&
                 !artifactIds.Contains(finding.RelatedArtifactId.Value)))
            {
                details = "Findings must be unique and reference a result artifact.";
                return false;
            }
        }

        if (result.Metrics.Select(metric => metric.MetricId)
            .Distinct(StringComparer.Ordinal).Count() != result.Metrics.Count ||
            result.LogReferences.Distinct(StringComparer.Ordinal).Count() !=
            result.LogReferences.Count)
        {
            details = "Metric identities and log references must be unique.";
            return false;
        }

        return true;
    }

    private static WorkerJsonDeserializationResult<WorkerResult> Failure(
        WorkerJsonError error,
        string? details = null) =>
        WorkerJsonDeserializationResult<WorkerResult>.Failure(error, details);
}
