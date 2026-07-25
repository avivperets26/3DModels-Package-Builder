using System.Reflection;
using System.Text;
using System.Text.Json;
using Json.Schema;
using PackageBuilder.Contracts.Json;

namespace PackageBuilder.Contracts.Profiles;

internal static class ProfileJsonInfrastructure
{
    public const int MaximumInputCharacters = JsonInputSafeguards.MaximumInputCharacters;
    public const int MaximumDepth = JsonInputSafeguards.MaximumDepth;
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
        JsonInputError inputError = JsonInputSafeguards.TryParseObject(
            json,
            MaximumInputCharacters,
            out JsonDocument? document);
        if (inputError != JsonInputError.None)
        {
            return new(false, inputError.ToString());
        }

        using (document!)
        {
            EvaluationResults results = schema.Evaluate(
                document!.RootElement,
                new EvaluationOptions { OutputFormat = OutputFormat.List });
            return new(
                results.IsValid,
                results.IsValid ? null : JsonSerializer.Serialize(results));
        }
    }

    public static ProfileJsonError TryParse(
        string? json,
        JsonSchema schema,
        out JsonDocument? document)
    {
        JsonInputError inputError = JsonInputSafeguards.TryParseObject(
            json,
            MaximumInputCharacters,
            out document);
        ProfileJsonError error = inputError switch
        {
            JsonInputError.None => ProfileJsonError.None,
            JsonInputError.Null => ProfileJsonError.NullJson,
            JsonInputError.Empty => ProfileJsonError.EmptyJson,
            JsonInputError.TooLarge => ProfileJsonError.InputTooLarge,
            JsonInputError.Malformed => ProfileJsonError.MalformedJson,
            JsonInputError.RootMustBeObject => ProfileJsonError.RootMustBeObject,
            JsonInputError.DuplicateProperty => ProfileJsonError.DuplicateProperty,
            _ => ProfileJsonError.MalformedJson,
        };
        if (error != ProfileJsonError.None)
        {
            return error;
        }

        EvaluationResults schemaResult = schema.Evaluate(
            document!.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.Flag });
        if (!schemaResult.IsValid)
        {
            document.Dispose();
            document = null;
            return ProfileJsonError.SchemaViolation;
        }

        return ProfileJsonError.None;
    }

    private static BuildOptions CreateBuildOptions() =>
        new()
        {
            Dialect = Dialect.Draft202012,
            SchemaRegistry = new SchemaRegistry(),
        };
}
