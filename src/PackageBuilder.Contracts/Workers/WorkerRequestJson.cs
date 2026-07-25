using System.Buffers;
using System.Text;
using System.Text.Json;
using Json.Schema;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Contracts.Workers;

public static class WorkerRequestJson
{
    public const int CurrentProtocolVersion = 1;
    public const int MaximumInputCharacters = WorkerJsonInfrastructure.MaximumInputCharacters;
    public const int MaximumDepth = WorkerJsonInfrastructure.MaximumDepth;
    public const string SchemaIdentifier = "https://schemas.packagebuilder.dev/worker-request/v1";
    public const string SchemaDraft = WorkerJsonInfrastructure.SchemaDraft;

    private const string Resource = "PackageBuilder.Contracts.Schemas.worker-request.schema.json";
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

    public static WorkerJsonSerializationResult Serialize(WorkerRequest? request)
    {
        if (request is null)
        {
            return WorkerJsonSerializationResult.Failure(WorkerJsonError.NullValue);
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("protocolVersion", request.ProtocolVersion);
            writer.WriteString("jobId", request.JobId.Value);
            writer.WriteString("operation", request.Operation);
            writer.WriteString("productManifestReference", request.ProductManifestReference);
            writer.WriteString("inputDirectoryReference", request.InputDirectoryReference);
            writer.WriteString("outputDirectoryReference", request.OutputDirectoryReference);
            writer.WriteString("resultFileReference", request.ResultFileReference);
            if (request.EngineVersion is not null)
            {
                writer.WriteString("engineVersion", request.EngineVersion);
            }

            if (request.Target is not null)
            {
                writer.WriteString("target", request.Target.CanonicalIdentifier);
            }

            writer.WriteEndObject();
        }

        string json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        return ValidateJsonAgainstSchema(json).IsValid
            ? WorkerJsonSerializationResult.Success(json)
            : WorkerJsonSerializationResult.Failure(WorkerJsonError.SemanticViolation);
    }

    public static WorkerJsonDeserializationResult<WorkerRequest> Deserialize(string? json)
    {
        WorkerJsonError error = WorkerJsonInfrastructure.TryParse(
            json,
            _schema.Value,
            MaximumInputCharacters,
            out JsonDocument? document);
        if (error != WorkerJsonError.None)
        {
            return WorkerJsonDeserializationResult<WorkerRequest>.Failure(error);
        }

        using (document!)
        {
            JsonElement root = document!.RootElement;
            BuildModelValidationResult<BuildJobId> job =
                BuildJobId.Create(root.GetProperty("jobId").GetString());
            if (!job.IsValid)
            {
                return WorkerJsonDeserializationResult<WorkerRequest>.Failure(
                    WorkerJsonError.DomainViolation,
                    job.Error.ToString());
            }

            BuildTarget? target = root.TryGetProperty("target", out JsonElement targetElement)
                ? WorkerJsonTokens.ParseTarget(targetElement.GetString()!)
                : null;
            return WorkerJsonDeserializationResult<WorkerRequest>.Success(
                new WorkerRequest(
                    root.GetProperty("protocolVersion").GetInt32(),
                    job.Value!,
                    root.GetProperty("operation").GetString()!,
                    root.GetProperty("productManifestReference").GetString()!,
                    root.GetProperty("inputDirectoryReference").GetString()!,
                    root.GetProperty("outputDirectoryReference").GetString()!,
                    root.GetProperty("resultFileReference").GetString()!,
                    root.TryGetProperty("engineVersion", out JsonElement engine)
                        ? engine.GetString()
                        : null,
                    target));
        }
    }
}
