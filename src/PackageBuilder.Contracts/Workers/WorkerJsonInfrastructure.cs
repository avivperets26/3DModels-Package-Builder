using System.Reflection;
using System.Text;
using System.Text.Json;
using Json.Schema;

namespace PackageBuilder.Contracts.Workers;

internal static class WorkerJsonInfrastructure
{
    public const int MaximumInputCharacters = 1_048_576;
    public const int MaximumEventCharacters = 65_536;
    public const int MaximumDepth = 64;
    public const string SchemaDraft = "https://json-schema.org/draft/2020-12/schema";

    public static string ReadSchemaText(string resourceName)
    {
        using Stream stream =
            Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    public static JsonSchema BuildSchema(string schemaText) =>
        JsonSchema.FromText(
            schemaText,
            new BuildOptions
            {
                Dialect = Dialect.Draft202012,
                SchemaRegistry = new SchemaRegistry(),
            });

    public static WorkerJsonSchemaValidationResult ValidateSchemaDefinition(
        string? schemaText,
        string expectedIdentifier)
    {
        if (schemaText is null)
        {
            return new(false, "Schema JSON is null.");
        }

        try
        {
            using var document = JsonDocument.Parse(
                schemaText,
                new JsonDocumentOptions { MaxDepth = MaximumDepth });
            if (!document.RootElement.TryGetProperty("$id", out JsonElement id))
            {
                return new(false, "The schema identifier is missing.");
            }

            if (!document.RootElement.TryGetProperty("$schema", out JsonElement draft))
            {
                return new(false, "The schema dialect is missing.");
            }

            if (!string.Equals(id.GetString(), expectedIdentifier, StringComparison.Ordinal))
            {
                return new(false, "The schema identifier is not approved.");
            }

            if (!string.Equals(draft.GetString(), SchemaDraft, StringComparison.Ordinal))
            {
                return new(false, "The schema dialect is not approved.");
            }

            _ = BuildSchema(schemaText);
            return new(true, null);
        }
        catch (JsonException exception)
        {
            return new(false, exception.Message);
        }
    }

    public static WorkerJsonSchemaValidationResult ValidateJson(
        string? json,
        JsonSchema schema,
        int maximumCharacters)
    {
        WorkerJsonError error = TryParse(json, schema, maximumCharacters, out JsonDocument? doc);
        doc?.Dispose();
        return new(error == WorkerJsonError.None, error == WorkerJsonError.None ? null : error.ToString());
    }

    public static WorkerJsonError TryParse(
        string? json,
        JsonSchema schema,
        int maximumCharacters,
        out JsonDocument? document)
    {
        document = null;
        if (json is null)
        {
            return WorkerJsonError.NullJson;
        }

        if (json.Length == 0)
        {
            return WorkerJsonError.EmptyJson;
        }

        if (json.Length > maximumCharacters)
        {
            return maximumCharacters == MaximumEventCharacters
                ? WorkerJsonError.LineTooLarge
                : WorkerJsonError.InputTooLarge;
        }

        try
        {
            document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaximumDepth,
                });
        }
        catch (JsonException)
        {
            return WorkerJsonError.MalformedJson;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            document = null;
            return WorkerJsonError.RootMustBeObject;
        }

        if (ContainsDuplicateProperty(document.RootElement))
        {
            document.Dispose();
            document = null;
            return WorkerJsonError.DuplicateProperty;
        }

        EvaluationResults result = schema.Evaluate(
            document.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.Flag });
        if (!result.IsValid)
        {
            document.Dispose();
            document = null;
            return WorkerJsonError.SchemaViolation;
        }

        return WorkerJsonError.None;
    }

    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool ContainsDuplicateProperty(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || ContainsDuplicateProperty(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (ContainsDuplicateProperty(item))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
