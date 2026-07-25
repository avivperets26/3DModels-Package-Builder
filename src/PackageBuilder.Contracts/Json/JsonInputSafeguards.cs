using System.Text.Json;

namespace PackageBuilder.Contracts.Json;

internal enum JsonInputError
{
    None = 0,
    Null,
    Empty,
    TooLarge,
    Malformed,
    RootMustBeObject,
    DuplicateProperty,
}

internal static class JsonInputSafeguards
{
    public const int MaximumInputCharacters = 1_048_576;
    public const int MaximumDepth = 64;

    public static JsonInputError TryParseObject(
        string? json,
        int maximumCharacters,
        out JsonDocument? document)
    {
        document = null;
        if (json is null)
        {
            return JsonInputError.Null;
        }

        if (json.Length == 0)
        {
            return JsonInputError.Empty;
        }

        if (json.Length > maximumCharacters)
        {
            return JsonInputError.TooLarge;
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
            return JsonInputError.Malformed;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            document = null;
            return JsonInputError.RootMustBeObject;
        }

        if (ContainsDuplicateProperty(document.RootElement))
        {
            document.Dispose();
            document = null;
            return JsonInputError.DuplicateProperty;
        }

        return JsonInputError.None;
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
}
