using System.Buffers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Json.Schema;
using PackageBuilder.Contracts.Json;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Contracts.BuildLocks;

/// <summary>Strict, canonical JSON contract for deterministic build-lock schema version 1.</summary>
public static class BuildLockJson
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumInputCharacters = JsonInputSafeguards.MaximumInputCharacters;
    public const string SchemaIdentifier = "https://schemas.packagebuilder.dev/build-lock/v1";
    private const string SchemaResource = "PackageBuilder.Contracts.Schemas.build-lock.schema.json";
    private static readonly Lazy<string> _schemaText = new(ReadSchemaText);
    private static readonly Lazy<JsonSchema> _schema = new(() => BuildSchema(_schemaText.Value));

    public static string SchemaText => _schemaText.Value;

    public static BuildLockSchemaValidationResult ValidateSchemaDefinition()
    {
        try
        {
            using var document = JsonDocument.Parse(SchemaText);
            JsonElement root = document.RootElement;
            bool valid = root.GetProperty("$id").GetString() == SchemaIdentifier
                && root.GetProperty("$schema").GetString() == "https://json-schema.org/draft/2020-12/schema";
            _ = BuildSchema(SchemaText);
            return new(valid, valid ? null : "The schema identifier or dialect is not approved.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return new(false, exception.Message);
        }
    }

    public static BuildLockJsonResult Serialize(BuildLock? value)
    {
        if (!TryValidate(value))
        {
            return BuildLockJsonResult.Failure(value is null ? BuildLockJsonError.NullValue : BuildLockJsonError.DomainViolation);
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(writer, value!);
        }

        string json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        return BuildLockJsonResult.Success(value!, json);
    }

    public static BuildLockJsonResult Deserialize(string? json)
    {
        JsonInputError inputError = JsonInputSafeguards.TryParseObject(json, MaximumInputCharacters, out JsonDocument? document);
        BuildLockJsonError error = inputError switch
        {
            JsonInputError.None => BuildLockJsonError.None,
            JsonInputError.Null => BuildLockJsonError.NullJson,
            JsonInputError.Empty => BuildLockJsonError.EmptyJson,
            JsonInputError.TooLarge => BuildLockJsonError.InputTooLarge,
            JsonInputError.Malformed => BuildLockJsonError.MalformedJson,
            JsonInputError.RootMustBeObject => BuildLockJsonError.RootMustBeObject,
            JsonInputError.DuplicateProperty => BuildLockJsonError.DuplicateProperty,
            _ => BuildLockJsonError.MalformedJson,
        };
        if (error != BuildLockJsonError.None)
        {
            return BuildLockJsonResult.Failure(error);
        }

        using (document!)
        {
            EvaluationResults evaluated = _schema.Value.Evaluate(document!.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Flag });
            if (!evaluated.IsValid)
            {
                return BuildLockJsonResult.Failure(BuildLockJsonError.SchemaViolation);
            }

            BuildLock? value = Read(document.RootElement);
            BuildLockJsonResult serialized = Serialize(value);
            return serialized.IsSuccessful
                ? serialized
                : BuildLockJsonResult.Failure(BuildLockJsonError.DomainViolation);
        }
    }

    private static BuildLock? Read(JsonElement root)
    {
        BuildJobId? jobId = BuildJobId.Create(root.GetProperty("jobId").GetString()).Value;
        ToolVersion? dotnet = ToolVersion.Create(ToolKind.DotNet, root.GetProperty("dotnetSdk").GetString()).Value;
        ToolVersion? blender = ToolVersion.Create(ToolKind.Blender, root.GetProperty("blender").GetString()).Value;
        ToolVersion? unity = ToolVersion.Create(ToolKind.Unity, root.GetProperty("unity").GetString()).Value;
        ToolVersion? unreal = ToolVersion.Create(ToolKind.Unreal, root.GetProperty("unreal").GetString()).Value;
        if (jobId is null || dotnet is null || blender is null || unity is null || unreal is null)
        {
            return null;
        }

        BuildLockWorker[] workers = [.. root.GetProperty("workers").EnumerateArray().Select(item => new BuildLockWorker(item.GetProperty("id").GetString()!, item.GetProperty("version").GetString()!))];
        BuildLockMarketplaceProfile[] profiles = [.. root.GetProperty("marketplaceProfiles").EnumerateArray().Select(item => new BuildLockMarketplaceProfile(item.GetProperty("marketplace").GetString()!, item.GetProperty("profile").GetString()!, item.GetProperty("version").GetString()!))];
        return new BuildLock(jobId, root.GetProperty("packageBuilderVersion").GetString()!, dotnet, blender, unity, unreal,
            root.GetProperty("manifestSchemaVersion").GetInt32(), workers, profiles);
    }

    private static bool TryValidate(BuildLock? value) => value is not null
        && value.JobId is not null
        && BuildLockValueValidator.IsVersion(value.PackageBuilderVersion)
        && value.DotNetSdk?.Tool == ToolKind.DotNet
        && value.Blender?.Tool == ToolKind.Blender
        && value.Unity?.Tool == ToolKind.Unity
        && value.Unreal?.Tool == ToolKind.Unreal
        && value.ManifestSchemaVersion > 0
        && value.Workers is { Count: > 0 and <= 64 }
        && value.MarketplaceProfiles is { Count: > 0 and <= 64 }
        && value.Workers.All(item => item is not null && BuildLockValueValidator.IsIdentifier(item.Id) && BuildLockValueValidator.IsVersion(item.Version))
        && value.MarketplaceProfiles.All(item => item is not null && BuildLockValueValidator.IsIdentifier(item.Marketplace) && BuildLockValueValidator.IsIdentifier(item.Profile) && BuildLockValueValidator.IsVersion(item.Version))
        && value.Workers.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == value.Workers.Count
        && value.MarketplaceProfiles.Select(item => (item.Marketplace, item.Profile)).Distinct().Count() == value.MarketplaceProfiles.Count;

    private static void Write(Utf8JsonWriter writer, BuildLock value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
        writer.WriteString("jobId", value.JobId.Value);
        writer.WriteString("packageBuilderVersion", value.PackageBuilderVersion);
        writer.WriteString("dotnetSdk", value.DotNetSdk.Value);
        writer.WriteString("blender", value.Blender.Value);
        writer.WriteString("unity", value.Unity.Value);
        writer.WriteString("unreal", value.Unreal.Value);
        writer.WriteNumber("manifestSchemaVersion", value.ManifestSchemaVersion);
        writer.WriteStartArray("workers");
        foreach (BuildLockWorker item in value.Workers.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("id", item.Id);
            writer.WriteString("version", item.Version);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteStartArray("marketplaceProfiles");
        foreach (BuildLockMarketplaceProfile item in value.MarketplaceProfiles.OrderBy(item => item.Marketplace, StringComparer.Ordinal).ThenBy(item => item.Profile, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("marketplace", item.Marketplace);
            writer.WriteString("profile", item.Profile);
            writer.WriteString("version", item.Version);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static string ReadSchemaText()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SchemaResource)!;
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    private static JsonSchema BuildSchema(string schemaText) =>
        JsonSchema.FromText(
            schemaText,
            new BuildOptions
            {
                Dialect = Dialect.Draft202012,
                SchemaRegistry = new SchemaRegistry(),
            });
}
