using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Tools;
using PackageBuilder.Marketplaces.Fab;

namespace PackageBuilder.App.Wpf.Tests;

public sealed class FabRequirementsProfileTests
{
    [Fact]
    public void ExactProfilePinRoundTripsThroughExistingBuildLockSchema()
    {
        BuildLockMarketplaceProfile pin = FabRequirementsBaseline.Profile.BuildLockIdentity;
        BuildLock buildLock = new(BuildJobId.Create("Job-1001").Value!, "1.0.0",
            ToolVersion.Create(ToolKind.DotNet, "10.0.302").Value!,
            ToolVersion.Create(ToolKind.Blender, "5.0.0").Value!,
            ToolVersion.Create(ToolKind.Unity, "6000.3.10f1").Value!,
            ToolVersion.Create(ToolKind.Unreal, "5.8.0").Value!, 1, [new("worker", "1.0.0")], [pin]);
        BuildLockJsonResult serialized = BuildLockJson.Serialize(buildLock);
        Assert.True(serialized.IsSuccessful);
        BuildLockJsonResult parsed = BuildLockJson.Deserialize(serialized.Json);
        Assert.True(parsed.IsSuccessful);
        Assert.Equal(serialized.Json, parsed.Json);
        Assert.Equal(pin, Assert.Single(parsed.Value!.MarketplaceProfiles));
    }

    [Fact]
    public void ReviewedBaselineHasAllSectionsDatesSourcesAndExplicitUnknowns()
    {
        FabRequirementsProfile profile = FabRequirementsBaseline.Profile;
        Assert.Equal("2026-09-12.1", profile.Document.Version);
        Assert.Equal(new DateOnly(2026, 9, 12), profile.Document.EffectiveOn);
        Assert.Equal(6, profile.Document.Rules.Select(rule => rule.Section).Distinct().Count());
        Assert.All(profile.Document.Rules, rule => Assert.Contains(profile.Document.Sources, source => source.Id == rule.Source));
        Assert.Equal("unresolved", profile.Rule("other-format-bytes").Status);
        Assert.Null(profile.Rule("other-format-bytes").Limit);
        Assert.Equal("package-builder-policy", profile.Rule("byte-units").Origin);
        Assert.Equal(3_000_000, FabPreviewMediaPolicy.Current.ImageByteLimit);
        Assert.Equal(25_000_000, FabPreviewMediaPolicy.Current.GalleryByteLimit);
        Assert.True(FabPreviewMediaPolicy.Current.IsValid);
        Assert.Contains(profile.Sha256, profile.BuildLockIdentity.Version, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{broken}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":2}")]
    public void HostileJsonReturnsExpectedFailure(string? json) => Assert.False(FabRequirementsProfileJson.Load(json).IsSuccess);

    [Theory]
    [InlineData("unknown")]
    [InlineData("schema")]
    [InlineData("marketplace")]
    [InlineData("version")]
    [InlineData("date")]
    [InlineData("missing-field")]
    [InlineData("null-rules")]
    [InlineData("null-rule")]
    [InlineData("duplicate-rule")]
    [InlineData("missing-section")]
    [InlineData("source-url")]
    [InlineData("source-credentials")]
    [InlineData("source-query")]
    [InlineData("source-date")]
    [InlineData("source-reference")]
    [InlineData("missing-image")]
    [InlineData("negative-limit")]
    [InlineData("overflow-limit")]
    [InlineData("comparison")]
    [InlineData("unit")]
    [InlineData("unknown-limit")]
    [InlineData("duplicate-category")]
    [InlineData("unknown-format")]
    [InlineData("null-format")]
    [InlineData("duplicate-format")]
    [InlineData("origin")]
    public void InvalidProfileMutationFailsClosed(string mutation)
    {
        JsonObject root = BaselineNode();
        JsonArray rules = root["rules"]!.AsArray();
        JsonNode image = rules.Single(rule => rule!["id"]!.GetValue<string>() == "image-bytes")!;
        JsonNode source = root["sources"]![0]!;
        switch (mutation)
        {
            case "unknown":
                root["surprise"] = true;
                break;
            case "schema":
                root["schemaVersion"] = 2;
                break;
            case "marketplace":
                root["marketplace"] = "other";
                break;
            case "version":
                root["version"] = "latest";
                break;
            case "date":
                root["effectiveOn"] = "2020-01-01";
                break;
            case "missing-field":
                _ = root.Remove("effectiveDateBasis");
                break;
            case "null-rules":
                root["rules"] = null;
                break;
            case "null-rule":
                rules[0] = null;
                break;
            case "duplicate-rule":
                rules.Add(rules[0]!.DeepClone());
                break;
            case "missing-section":
                foreach (JsonNode rule in rules.Where(rule => rule!["section"]!.GetValue<string>() == "unity").ToArray()!)
                { _ = rules.Remove(rule); }
                break;
            case "source-url":
                source["url"] = "https://example.invalid/requirements";
                break;
            case "source-credentials":
                source["url"] = "https://example@dev.epicgames.com/documentation/en-us/fab/rules";
                break;
            case "source-query":
                source["url"] = source["url"]!.GetValue<string>() + "?key=fixture";
                break;
            case "source-date":
                source["updatedOn"] = "2030-01-01";
                break;
            case "source-reference":
                image["source"] = "absent";
                break;
            case "missing-image":
                _ = rules.Remove(image);
                break;
            case "negative-limit":
                image["limit"] = -1;
                break;
            case "overflow-limit":
                image["limit"] = long.MaxValue;
                break;
            case "comparison":
                image["comparison"] = "lte";
                break;
            case "unit":
                image["unit"] = "megabytes";
                break;
            case "unknown-limit":
                image["status"] = "unresolved";
                break;
            case "duplicate-category":
                root["listings"]![1]!["category"] = root["listings"]![0]!["category"]!.DeepClone();
                break;
            case "unknown-format":
                root["listings"]![0]!["formats"]![0] = "executable";
                break;
            case "null-format":
                root["listings"]![0]!["formats"]![0] = null;
                break;
            case "duplicate-format":
                root["listings"]![0]!["formats"]!.AsArray().Add(root["listings"]![0]!["formats"]![0]!.DeepClone());
                break;
            case "origin":
                image["origin"] = "assumed-official";
                break;
        }
        Assert.False(FabRequirementsProfileJson.Load(root.ToJsonString()).IsSuccess);
    }

    [Fact]
    public void BoundsDuplicatesAndCommentsAreRejectedBeforeBinding()
    {
        Assert.False(FabRequirementsProfileJson.Load(new string(' ', 262_145)).IsSuccess);
        string json = FabRequirementsBaseline.Profile.CanonicalJson;
        Assert.False(FabRequirementsProfileJson.Load(json.Replace("\"image-bytes\"", "\"image-bytes\",\"id\":\"image-bytes\"", StringComparison.Ordinal)).IsSuccess);
        Assert.False(FabRequirementsProfileJson.Load("/*comment*/" + json).IsSuccess);
        Assert.False(FabRequirementsProfileJson.Load(json[..^1] + ",}").IsSuccess);
    }

    [Fact]
    public void CanonicalHashIgnoresInputOrderWhitespaceAndCultureButTracksEveryChange()
    {
        JsonObject root = BaselineNode();
        root["rules"] = new JsonArray(root["rules"]!.AsArray().Reverse().Select(node => node!.DeepClone()).ToArray());
        FabRequirementsProfile reordered = FabRequirementsProfileJson.Load(root.ToJsonString()).Value!;
        Assert.Equal(FabRequirementsBaseline.Profile.Sha256, reordered.Sha256);
        Assert.Empty(FabRequirementsProfileUpdater.Compare(FabRequirementsBaseline.Profile, reordered));
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            root["version"] = "2026-09-12.2";
            root["rules"]![0]!["summary"] = "Updated reviewed assertion.";
            FabRequirementsProfile updated = FabRequirementsProfileJson.Load(root.ToJsonString()).Value!;
            Assert.NotEqual(reordered.Sha256, updated.Sha256);
            Assert.Equal(["/rules", "/version"], FabRequirementsProfileUpdater.Compare(reordered, updated).Select(change => change.Path));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    public static IEnumerable<TheoryDataRow<string, string>> CasesAndFormats => ProductCase.All.SelectMany(productCase =>
        _formats.Select(format => new TheoryDataRow<string, string>(productCase.CanonicalIdentifier, format)));

    private static readonly string[] _formats = ["fbx", "glb", "unity", "unreal"];

    [Theory]
    [MemberData(nameof(CasesAndFormats))]
    public void AllFiveCasesResolveOnlySelectedEngineAndPortableFormats(string productCase, string format)
    {
        FabRequiredTargets result = FabRequiredTargetResolver.Resolve(FabRequirementsBaseline.Profile,
            new FabListingConfiguration(ProductCase.TryParse(productCase).Value!, "3d-model", [format])).Value!;
        string target = format is "fbx" or "glb" ? "portable" : format;
        Assert.Equal(target, Assert.Single(result.BuildTargets).CanonicalIdentifier);
        Assert.Contains(FabOutput.Documentation, result.Required);
        Assert.Contains(FabOutput.Media, result.Required);
        Assert.Equal(format == "glb", result.Required.Contains(FabOutput.Glb));
        Assert.Empty(result.Required.Intersect(result.Optional));
        Assert.Contains(result.UnresolvedRules, rule => rule.Id == "technical-review");
        Assert.Equal(target == "portable", result.UnresolvedRules.Any(rule => rule.Id == "other-format-bytes"));
    }

    [Theory]
    [InlineData("animation", "fbx")]
    [InlineData("animation", "glb")]
    [InlineData("unknown", "unity")]
    [InlineData("3d-model", "unknown")]
    public void UnsupportedCategoryFormatsAreNotQuietlySubstituted(string category, string format) =>
        Assert.False(FabRequiredTargetResolver.Resolve(FabRequirementsBaseline.Profile,
            new FabListingConfiguration(ProductCase.RiggedAnimated, category, [format])).IsSuccess);

    [Fact]
    public void EmptyDuplicateAndNullSelectionsFailAndCombinedTargetsAreDeterministic()
    {
        FabRequirementsProfile profile = FabRequirementsBaseline.Profile;
        Assert.False(FabRequiredTargetResolver.Resolve(profile, null).IsSuccess);
        Assert.False(FabRequiredTargetResolver.Resolve(profile, new(ProductCase.Static, "3d-model", [])).IsSuccess);
        Assert.False(FabRequiredTargetResolver.Resolve(profile, new(ProductCase.Static, "3d-model", ["unity", "unity"])).IsSuccess);
        FabRequiredTargets combined = FabRequiredTargetResolver.Resolve(profile,
            new(ProductCase.ItemCollection, "3d-model", ["unreal", "glb", "unity", "fbx"])).Value!;
        Assert.Equal(BuildTarget.All, combined.BuildTargets);
        Assert.Equal(Enum.GetValues<FabOutput>(), combined.Required);
        Assert.Empty(combined.Optional);
        Assert.True(FabRequiredTargetResolver.Resolve(profile, new(ProductCase.RiggedAnimated, "animation", ["unity"])).IsSuccess);
    }

    [Fact]
    public void CompanionChoicesComeFromSelectedProfileAndListing()
    {
        JsonObject root = BaselineNode();
        JsonNode model = root["listings"]!.AsArray().Single(node => node!["category"]!.GetValue<string>() == "3d-model")!;
        model["documentationRequired"] = false;
        model["mediaRequired"] = false;
        FabRequirementsProfile profile = FabRequirementsProfileJson.Load(root.ToJsonString()).Value!;
        FabListingConfiguration listing = new(ProductCase.Static, "3d-model", ["fbx"]);
        FabRequiredTargets optional = FabRequiredTargetResolver.Resolve(profile, listing).Value!;
        Assert.Equal([FabOutput.Portable], optional.Required);
        Assert.Contains(FabOutput.Documentation, optional.Optional);
        Assert.Contains(FabOutput.Media, optional.Optional);
        FabRequiredTargets selected = FabRequiredTargetResolver.Resolve(profile, listing with { IncludeDocumentation = true, IncludeMedia = true }).Value!;
        Assert.Contains(FabOutput.Documentation, selected.Required);
        Assert.Contains(FabOutput.Media, selected.Required);
    }

    private static JsonObject BaselineNode() => JsonNode.Parse(FabRequirementsBaseline.Profile.CanonicalJson)!.AsObject();
}
