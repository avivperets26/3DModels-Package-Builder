using System.Reflection;
using System.Text;
using System.Text.Json;
using Json.Schema;

namespace PackageBuilder.Contracts.Profiles;

internal static class ProfileJsonInfrastructure
{
    public const int MaximumInputCharacters = 1_048_576;
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
        JsonSchema.FromText(schemaText, CreateBuildOptions());

    public static ProfileJsonSchemaValidationResult ValidateSchemaDefinition(
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

    public static ProfileJsonSchemaValidationResult ValidateJsonAgainstSchema(
        string? json,
        JsonSchema schema)
    {
        if (json is null)
        {
            return new(false, "JSON is null.");
        }

        if (json.Length > MaximumInputCharacters)
        {
            return new(false, "JSON exceeds the approved 1 MiB character limit.");
        }

        try
        {
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions { MaxDepth = MaximumDepth });
            if (ContainsDuplicateProperty(document.RootElement))
            {
                return new(false, "JSON contains a duplicate property.");
            }

            EvaluationResults results = schema.Evaluate(
                document.RootElement,
                new EvaluationOptions { OutputFormat = OutputFormat.List });
            return new(
                results.IsValid,
                results.IsValid ? null : JsonSerializer.Serialize(results));
        }
        catch (JsonException exception)
        {
            return new(false, exception.Message);
        }
    }

    public static ProfileJsonError TryParse(
        string? json,
        JsonSchema schema,
        out JsonDocument? document)
    {
        document = null;
        if (json is null)
        {
            return ProfileJsonError.NullJson;
        }

        if (json.Length == 0)
        {
            return ProfileJsonError.EmptyJson;
        }

        if (json.Length > MaximumInputCharacters)
        {
            return ProfileJsonError.InputTooLarge;
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
            return ProfileJsonError.MalformedJson;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            document = null;
            return ProfileJsonError.RootMustBeObject;
        }

        if (ContainsDuplicateProperty(document.RootElement))
        {
            document.Dispose();
            document = null;
            return ProfileJsonError.DuplicateProperty;
        }

        EvaluationResults schemaResult = schema.Evaluate(
            document.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.Flag });
        if (!schemaResult.IsValid)
        {
            document.Dispose();
            document = null;
            return ProfileJsonError.SchemaViolation;
        }

        return ProfileJsonError.None;
    }

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

    private static BuildOptions CreateBuildOptions() =>
        new()
        {
            Dialect = Dialect.Draft202012,
            SchemaRegistry = new SchemaRegistry(),
        };
}
