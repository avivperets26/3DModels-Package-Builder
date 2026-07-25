using System.Buffers;
using System.Text;
using System.Text.Json;
using Json.Schema;
using PackageBuilder.Contracts.Validation;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contracts.Workers;

public static class WorkerProgressEventJson
{
    public const int CurrentProtocolVersion = 1;
    public const int MaximumInputCharacters = WorkerJsonInfrastructure.MaximumEventCharacters;
    public const int MaximumDepth = WorkerJsonInfrastructure.MaximumDepth;
    public const string SchemaIdentifier =
        "https://schemas.packagebuilder.dev/worker-progress-event/v1";
    public const string SchemaDraft = WorkerJsonInfrastructure.SchemaDraft;

    private const string Resource =
        "PackageBuilder.Contracts.Schemas.worker-progress-event.schema.json";
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

    public static WorkerJsonSerializationResult Serialize(WorkerProgressEvent? value)
    {
        if (value is null)
        {
            return WorkerJsonSerializationResult.Failure(WorkerJsonError.NullValue);
        }

        if ((value.Percent.HasValue && !WorkerJsonInfrastructure.IsFinite(value.Percent.Value)) ||
            (value.Metric is not null && !WorkerJsonInfrastructure.IsFinite(value.Metric.Value)))
        {
            return WorkerJsonSerializationResult.Failure(WorkerJsonError.SemanticViolation);
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("protocolVersion", value.ProtocolVersion);
            writer.WriteString("eventKind", WorkerJsonTokens.EventKind(value.EventKind));
            writer.WriteString("jobId", value.JobId.Value);
            switch (value.EventKind)
            {
                case WorkerEventKind.Progress:
                    writer.WriteString("stage", value.Stage);
                    if (value.Message is not null)
                    {
                        writer.WriteString("message", value.Message);
                    }

                    if (value.Percent.HasValue)
                    {
                        writer.WriteNumber("percent", value.Percent.Value);
                    }

                    break;
                case WorkerEventKind.Finding:
                    writer.WritePropertyName("finding");
                    if (!WriteFinding(writer, value.Finding))
                    {
                        return WorkerJsonSerializationResult.Failure(
                            WorkerJsonError.SemanticViolation);
                    }

                    break;
                case WorkerEventKind.Metric:
                    writer.WritePropertyName("metric");
                    WriteMetric(writer, value.Metric);
                    break;
                default:
                    return WorkerJsonSerializationResult.Failure(
                        WorkerJsonError.SemanticViolation);
            }

            writer.WriteEndObject();
        }

        string json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        return ValidateJsonAgainstSchema(json).IsValid
            ? WorkerJsonSerializationResult.Success(json)
            : WorkerJsonSerializationResult.Failure(WorkerJsonError.SemanticViolation);
    }

    public static WorkerJsonDeserializationResult<WorkerProgressEvent> Deserialize(string? json)
    {
        WorkerJsonError error = WorkerJsonInfrastructure.TryParse(
            json,
            _schema.Value,
            MaximumInputCharacters,
            out JsonDocument? document);
        if (error != WorkerJsonError.None)
        {
            return WorkerJsonDeserializationResult<WorkerProgressEvent>.Failure(error);
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

            WorkerEventKind kind =
                WorkerJsonTokens.ParseEventKind(root.GetProperty("eventKind").GetString()!);
            ValidationFinding? finding = null;
            WorkerMetric? metric = null;
            if (kind == WorkerEventKind.Finding)
            {
                ValidationFindingDeserializationResult parsed = ValidationFindingJson.Deserialize(
                    root.GetProperty("finding").GetRawText());
                if (!parsed.IsSuccessful)
                {
                    return Failure(
                        WorkerJsonError.DomainViolation,
                        parsed.Error.ToString());
                }

                finding = parsed.Value;
            }
            else if (kind == WorkerEventKind.Metric)
            {
                metric = ParseMetric(root.GetProperty("metric"));
                if (!WorkerJsonInfrastructure.IsFinite(metric.Value))
                {
                    return Failure(WorkerJsonError.SemanticViolation, "Metric must be finite.");
                }
            }

            return WorkerJsonDeserializationResult<WorkerProgressEvent>.Success(
                new WorkerProgressEvent(
                    root.GetProperty("protocolVersion").GetInt32(),
                    kind,
                    job.Value!,
                    root.TryGetProperty("stage", out JsonElement stage)
                        ? stage.GetString()
                        : null,
                    root.TryGetProperty("message", out JsonElement message)
                        ? message.GetString()
                        : null,
                    root.TryGetProperty("percent", out JsonElement percent)
                        ? percent.GetDouble()
                        : null,
                    finding,
                    metric));
        }
    }

    internal static void WriteMetric(Utf8JsonWriter writer, WorkerMetric? metric)
    {
        writer.WriteStartObject();
        if (metric is not null)
        {
            writer.WriteString("metricId", metric.MetricId);
            writer.WriteNumber("value", metric.Value);
            writer.WriteString("unit", WorkerJsonTokens.MetricUnit(metric.Unit));
        }

        writer.WriteEndObject();
    }

    internal static WorkerMetric ParseMetric(JsonElement element) =>
        new(
            element.GetProperty("metricId").GetString()!,
            element.GetProperty("value").GetDouble(),
            WorkerJsonTokens.ParseMetricUnit(element.GetProperty("unit").GetString()!));

    internal static bool WriteFinding(Utf8JsonWriter writer, ValidationFinding? finding)
    {
        ValidationFindingSerializationResult serialized = ValidationFindingJson.Serialize(finding);
        if (!serialized.IsSuccessful)
        {
            return false;
        }

        using var document = JsonDocument.Parse(serialized.Json!);
        document.RootElement.WriteTo(writer);
        return true;
    }

    private static WorkerJsonDeserializationResult<WorkerProgressEvent> Failure(
        WorkerJsonError error,
        string? details = null) =>
        WorkerJsonDeserializationResult<WorkerProgressEvent>.Failure(error, details);
}
