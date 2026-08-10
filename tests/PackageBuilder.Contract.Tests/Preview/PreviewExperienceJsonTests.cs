using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Preview;
using PackageBuilder.Domain.Preview;

namespace PackageBuilder.Contract.Tests.Preview;

[Trait("Task", "PB-0913")]
public sealed class PreviewExperienceJsonTests
{
    [Fact]
    public void SchemaIsPinnedDraft202012VersionOne()
    {
        Assert.Equal(1, PreviewExperienceJson.CurrentSchemaVersion);
        Assert.Equal(
            "https://schemas.packagebuilder.dev/preview-experience/v1",
            PreviewExperienceJson.SchemaIdentifier);
        Assert.True(PreviewExperienceJson.ValidateSchemaDefinition().IsValid);
    }

    [Fact]
    public void DefaultContractRoundTripsToCanonicalDeterministicJson()
    {
        string first = PreviewExperienceJson.Serialize(PreviewExperienceDefaults.Contract).Json!;
        PreviewExperienceJsonResult parsed = PreviewExperienceJson.Deserialize(first);

        Assert.True(parsed.IsSuccessful);
        Assert.Equal(first, parsed.Json);
        Assert.Contains("\"contractVersion\":1", first, StringComparison.Ordinal);
        Assert.Contains("\"minimumPitchDegrees\":-80", first, StringComparison.Ordinal);
        Assert.Contains("\"restoreControlId\":\"show-controls\"", first, StringComparison.Ordinal);
        Assert.Contains("\"AnimationPlayPause\"", first, StringComparison.Ordinal);
        Assert.Contains("\"accessibleName\"", first, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, PreviewExperienceJsonError.NullJson)]
    [InlineData("", PreviewExperienceJsonError.EmptyJson)]
    [InlineData("[]", PreviewExperienceJsonError.RootMustBeObject)]
    [InlineData("{", PreviewExperienceJsonError.MalformedJson)]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1}", PreviewExperienceJsonError.DuplicateProperty)]
    [InlineData("{}", PreviewExperienceJsonError.SchemaViolation)]
    public void InvalidJsonFailsClosed(string? json, PreviewExperienceJsonError expected) =>
        Assert.Equal(expected, PreviewExperienceJson.Deserialize(json).Error);

    [Fact]
    public void UnknownPropertiesAndUnsupportedVersionsFailSchemaValidation()
    {
        JsonObject root = JsonNode.Parse(
            PreviewExperienceJson.Serialize(PreviewExperienceDefaults.Contract).Json!)!.AsObject();
        root["unknown"] = true;
        Assert.Equal(
            PreviewExperienceJsonError.SchemaViolation,
            PreviewExperienceJson.Deserialize(root.ToJsonString()).Error);

        _ = root.Remove("unknown");
        root["contractVersion"] = 2;
        Assert.Equal(
            PreviewExperienceJsonError.SchemaViolation,
            PreviewExperienceJson.Deserialize(root.ToJsonString()).Error);
    }

    [Fact]
    public void DuplicateFocusOrderPassesShapeButFailsDomainValidation()
    {
        JsonObject root = JsonNode.Parse(
            PreviewExperienceJson.Serialize(PreviewExperienceDefaults.Contract).Json!)!.AsObject();
        JsonArray controls = root["accessibility"]!["controls"]!.AsArray();
        controls[1]!["focusOrder"] = 0;
        Assert.Equal(
            PreviewExperienceJsonError.DomainViolation,
            PreviewExperienceJson.Deserialize(root.ToJsonString()).Error);
    }

    [Fact]
    public void NullValueAndOversizedInputFailExplicitly()
    {
        Assert.Equal(
            PreviewExperienceJsonError.NullValue,
            PreviewExperienceJson.Serialize(null).Error);
        Assert.Equal(
            PreviewExperienceJsonError.InputTooLarge,
            PreviewExperienceJson.Deserialize(
                new string('x', PreviewExperienceJson.MaximumInputCharacters + 1)).Error);
    }
}
