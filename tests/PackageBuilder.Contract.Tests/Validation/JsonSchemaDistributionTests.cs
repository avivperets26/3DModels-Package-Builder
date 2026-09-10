using System.Text.Json;
using Json.Schema;

namespace PackageBuilder.Contract.Tests.Validation;

/// <summary>Guards the Draft 2020-12 and offline behavior of the independently compiled validator.</summary>
public sealed class JsonSchemaDistributionTests
{
    [Theory]
    [InlineData("{\"name\":\"oak\",\"values\":[1,\"green\"]}", true)]
    [InlineData("{\"name\":\"oak\",\"values\":[1]}", true)]
    [InlineData("{\"name\":\"\",\"values\":[1]}", false)]
    [InlineData("{\"name\":\"oak\",\"values\":[\"wrong\"]}", false)]
    [InlineData("{\"name\":\"oak\",\"values\":[1,2]}", false)]
    [InlineData("{\"name\":\"oak\",\"values\":[1,\"green\",3]}", false)]
    [InlineData("{\"name\":\"oak\",\"values\":[1],\"extra\":true}", false)]
    [InlineData("{\"name\":\"oak\"}", false)]
    public void Draft202012ResolvesLocalDefinitionsAndTracksEvaluatedProperties(string instance, bool expected)
    {
        const string SchemaText = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$defs": { "name": { "type": "string", "minLength": 1 } },
              "type": "object",
              "allOf": [{ "properties": { "name": { "$ref": "#/$defs/name" } } }],
              "properties": {
                "values": {
                  "type": "array",
                  "prefixItems": [{ "type": "integer" }, { "type": "string" }],
                  "items": false,
                  "minItems": 1
                }
              },
              "required": ["name", "values"],
              "unevaluatedProperties": false
            }
            """;
        var schema = JsonSchema.FromText(SchemaText, OfflineOptions());
        using var document = JsonDocument.Parse(instance);

        EvaluationResults result = schema.Evaluate(
            document.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.Equal(expected, result.IsValid);
        if (!expected)
        {
            // Exercises the embedded English resource and diagnostic serialization path.
            string diagnostics = JsonSerializer.Serialize(result);
            Assert.Contains("errors", diagnostics, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DefaultRegistryDoesNotFetchAnUnknownRemoteReference()
    {
        BuildOptions options = OfflineOptions();
        Assert.Null(options.SchemaRegistry.Fetch(new Uri("https://example.invalid/schema"), options.SchemaRegistry));
        var schema = JsonSchema.FromText(
            """{"$ref":"https://example.invalid/pb0015-unregistered"}""",
            options);
        using var document = JsonDocument.Parse("{}");
        _ = Assert.Throws<RefResolutionException>(() => schema.Evaluate(document.RootElement));
    }

    private static BuildOptions OfflineOptions() =>
        new() { Dialect = Dialect.Draft202012, SchemaRegistry = new SchemaRegistry() };
}
