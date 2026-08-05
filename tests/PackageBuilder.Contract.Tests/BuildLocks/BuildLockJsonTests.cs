using System.Text.Json.Nodes;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Contract.Tests.BuildLocks;

[Trait("Task", "PB-0308")]
public sealed class BuildLockJsonTests
{
    [Fact]
    public void SchemaIsPinnedDraft202012VersionOne()
    {
        Assert.Equal(1, BuildLockJson.CurrentSchemaVersion);
        Assert.Equal("https://schemas.packagebuilder.dev/build-lock/v1", BuildLockJson.SchemaIdentifier);
        Assert.True(BuildLockJson.ValidateSchemaDefinition().IsValid);
    }

    [Fact]
    public void CanonicalRoundTripRecordsEveryRequiredVersionAndSortsCollections()
    {
        BuildLock value = ValidLock(
            [new("unity-worker", "2.0.0"), new("blender-worker", "1.0.0")],
            [new("unity-asset-store", "default", "3.0.0"), new("fab", "default", "2.0.0")]);

        string first = BuildLockJson.Serialize(value).Json!;
        BuildLockJsonResult parsed = BuildLockJson.Deserialize(first);

        Assert.True(parsed.IsSuccessful);
        Assert.Equal(first, parsed.Json);
        Assert.Contains("\"dotnetSdk\":\"10.0.302\"", first, StringComparison.Ordinal);
        Assert.Contains("\"blender\":\"5.0.0\"", first, StringComparison.Ordinal);
        Assert.Contains("\"unity\":\"6000.3.10f1\"", first, StringComparison.Ordinal);
        Assert.Contains("\"unreal\":\"5.8.0\"", first, StringComparison.Ordinal);
        Assert.True(first.IndexOf("blender-worker", StringComparison.Ordinal) < first.IndexOf("unity-worker", StringComparison.Ordinal));
        Assert.True(first.IndexOf("\"fab\"", StringComparison.Ordinal) < first.IndexOf("unity-asset-store", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, BuildLockJsonError.NullJson)]
    [InlineData("", BuildLockJsonError.EmptyJson)]
    [InlineData("[]", BuildLockJsonError.RootMustBeObject)]
    [InlineData("{", BuildLockJsonError.MalformedJson)]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1}", BuildLockJsonError.DuplicateProperty)]
    [InlineData("{}", BuildLockJsonError.SchemaViolation)]
    public void InvalidJsonFailsClosed(string? json, BuildLockJsonError expected) => Assert.Equal(expected, BuildLockJson.Deserialize(json).Error);

    [Fact]
    public void OversizedInputFailsBeforeParsing()
    {
        string json = new('x', BuildLockJson.MaximumInputCharacters + 1);
        Assert.Equal(BuildLockJsonError.InputTooLarge, BuildLockJson.Deserialize(json).Error);
    }

    [Fact]
    public void UnknownPropertiesAndVendorInvalidVersionsFailClosed()
    {
        JsonObject node = JsonNode.Parse(BuildLockJson.Serialize(ValidLock()).Json!)!.AsObject();
        node["unknown"] = true;
        Assert.Equal(BuildLockJsonError.SchemaViolation, BuildLockJson.Deserialize(node.ToJsonString()).Error);

        _ = node.Remove("unknown");
        node["unity"] = "5.8.0";
        Assert.Equal(BuildLockJsonError.DomainViolation, BuildLockJson.Deserialize(node.ToJsonString()).Error);
    }

    [Fact]
    public void DuplicateWorkerAndMarketplaceIdentitiesAreDomainViolations()
    {
        BuildLock duplicateWorkers = ValidLock([new("worker", "1.0.0"), new("worker", "2.0.0")]);
        Assert.Equal(BuildLockJsonError.DomainViolation, BuildLockJson.Serialize(duplicateWorkers).Error);

        BuildLock duplicateProfiles = ValidLock(
            profiles: [new("fab", "default", "1.0.0"), new("fab", "default", "2.0.0")]);
        Assert.Equal(BuildLockJsonError.DomainViolation, BuildLockJson.Serialize(duplicateProfiles).Error);
    }

    private static BuildLock ValidLock(
        IReadOnlyList<BuildLockWorker>? workers = null,
        IReadOnlyList<BuildLockMarketplaceProfile>? profiles = null) => new(
            BuildJobId.Create("Job-0308").Value!,
            "1.2.3",
            Version(ToolKind.DotNet, "10.0.302"),
            Version(ToolKind.Blender, "5.0.0"),
            Version(ToolKind.Unity, "6000.3.10f1"),
            Version(ToolKind.Unreal, "5.8.0"),
            1,
            workers ?? [new("worker", "1.0.0")],
            profiles ?? [new("fab", "default", "1.0.0")]);

    private static ToolVersion Version(ToolKind tool, string value) => ToolVersion.Create(tool, value).Value!;
}
