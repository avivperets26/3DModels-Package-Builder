using System.Globalization;
using System.Text.Json.Nodes;
using PackageBuilder.Application.Items;
using PackageBuilder.Contracts.Manifests;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Application.Tests.Items;

/// <summary>Exercises reviewed ownership through application, domain and persisted manifest boundaries.</summary>
[Trait("Task", "PB-0801")]
public sealed class MultiItemSourceMapperTests
{
    [Theory]
    [InlineData("item-set")]
    [InlineData("item-collection")]
    public void ReviewedAssignmentsRoundTripWithoutChangingStableIdsOrItemOrder(string productCase)
    {
        ProductManifest draft = Draft(productCase);
        string original = ProductManifestJson.Serialize(draft).Json!;
        List<ItemSourceAssignment> supplied = [.. Assignments()];

        ProductManifestValidationResult mapped = MultiItemSourceMapper.Map(draft, supplied);
        Assert.True(mapped.IsValid);
        supplied.Clear();
        ProductManifest value = mapped.Value!;
        Assert.Equal(["Alpha", "Zed"], value.ItemSourceAssignments.Select(assignment => assignment.ItemId.Value));
        Assert.Equal(["Zed", "Alpha"], (value.ItemSet?.Items ?? value.ItemCollection!.Items).Select(item => item.Id.Value));
        Assert.Equal(2, value.ItemSourceAssignments.Count);
        Assert.Same(draft.SourceAssets[0], value.SourceAssets[0]);
        Assert.Equal(original, ProductManifestJson.Serialize(draft).Json);

        string json = ProductManifestJson.Serialize(value).Json!;
        ProductManifestDeserializationResult restored = ProductManifestJson.Deserialize(json);
        Assert.True(restored.IsSuccessful);
        Assert.Equal(json, ProductManifestJson.Serialize(restored.Value).Json);
        Assert.Equal(value.ItemSourceAssignments, restored.Value!.ItemSourceAssignments);
    }

    [Fact]
    public void AssignmentOrderAndCultureDoNotChangeCanonicalJson()
    {
        ProductManifest draft = Draft();
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            ProductManifest a = MultiItemSourceMapper.Map(draft, Assignments()).Value!;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            ProductManifest b = MultiItemSourceMapper.Map(draft, Assignments().Reverse()).Value!;
            Assert.Equal(ProductManifestJson.Serialize(a).Json, ProductManifestJson.Serialize(b).Json);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("null-entry", "MANIFEST_ITEM_SOURCE_INVALID")]
    [InlineData("null-id", "MANIFEST_ITEM_SOURCE_INVALID")]
    [InlineData("null-source", "MANIFEST_ITEM_SOURCE_INVALID")]
    [InlineData("unknown-item", "MANIFEST_ITEM_SOURCE_UNKNOWN")]
    [InlineData("wrong-item-case", "MANIFEST_ITEM_SOURCE_UNKNOWN")]
    [InlineData("unknown-source", "MANIFEST_ITEM_SOURCE_UNKNOWN")]
    [InlineData("wrong-source-case", "MANIFEST_ITEM_SOURCE_UNKNOWN")]
    [InlineData("traversal", "MANIFEST_ITEM_SOURCE_UNKNOWN")]
    [InlineData("duplicate", "MANIFEST_ITEM_SOURCE_DUPLICATE")]
    [InlineData("unmapped-item", "MANIFEST_ITEM_SOURCE_REVIEW_REQUIRED")]
    [InlineData("multiple-owners", "MANIFEST_ITEM_SOURCE_REVIEW_REQUIRED")]
    [InlineData("images-only", "MANIFEST_ITEM_SOURCE_REVIEW_REQUIRED")]
    [InlineData("empty", "MANIFEST_ITEM_SOURCE_REVIEW_REQUIRED")]
    public void InvalidOrAmbiguousMappingsProduceBlockingFindingsAndNoPartialManifest(string scenario, string code)
    {
        ProductManifest draft = Draft();
        List<ItemSourceAssignment?> assignments = [.. Assignments()];
        switch (scenario)
        {
            case "null-entry":
                assignments.Add(null);
                break;
            case "null-id":
                assignments.Add(new ItemSourceAssignment(null!, "models/one.glb"));
                break;
            case "null-source":
                assignments.Add(new ItemSourceAssignment(Id("Alpha"), null!));
                break;
            case "unknown-item":
                assignments.Add(new ItemSourceAssignment(Id("Other"), "models/one.glb"));
                break;
            case "wrong-item-case":
                assignments.Add(new ItemSourceAssignment(Id("alpha"), "models/one.glb"));
                break;
            case "unknown-source":
                assignments.Add(new ItemSourceAssignment(Id("Alpha"), "models/missing.glb"));
                break;
            case "wrong-source-case":
                assignments.Add(new ItemSourceAssignment(Id("Alpha"), "models/ONE.glb"));
                break;
            case "traversal":
                assignments.Add(new ItemSourceAssignment(Id("Alpha"), "../one.glb"));
                break;
            case "duplicate":
                assignments.Add(assignments[0]);
                break;
            case "unmapped-item":
                assignments.RemoveAt(0);
                break;
            case "multiple-owners":
                assignments.Add(new ItemSourceAssignment(Id("Alpha"), "models/one.glb"));
                break;
            case "images-only":
                assignments = [new ItemSourceAssignment(Id("Alpha"), "textures/shared.png")];
                break;
            case "empty":
                assignments.Clear();
                break;
        }

        ProductManifestValidationResult result = MultiItemSourceMapper.Map(draft, assignments);

        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Contains(result.Findings, finding => finding.Code.Value == code);
        Assert.All(result.Findings, finding => Assert.True(finding.BlocksRelease));
        Assert.Empty(draft.ItemSourceAssignments);
    }

    [Fact]
    public void EveryModelFileRequiresReviewEvenWhenEachItemAlreadyHasAFile()
    {
        JsonNode root = DraftJson();
        root["sourceAssets"]!.AsArray().Add(JsonNode.Parse("""{"kind":"fbx","logicalReference":"models/extra.fbx"}"""));
        ProductManifest draft = ProductManifestJson.Deserialize(root.ToJsonString()).Value!;
        ProductManifestValidationResult result = MultiItemSourceMapper.Map(draft, Assignments());
        Assert.False(result.IsValid);
        Assert.Contains(result.Findings, finding => finding.Code.Value == "MANIFEST_ITEM_SOURCE_REVIEW_REQUIRED");
    }

    [Fact]
    public void ExplicitImageSharingDoesNotDeduplicateOrChangeModelOwnership()
    {
        ProductManifestValidationResult result = MultiItemSourceMapper.Map(Draft(),
            [.. Assignments(), new(Id("Alpha"), "textures/shared.png"), new(Id("Zed"), "textures/shared.png")]);
        Assert.True(result.IsValid);
        Assert.Equal(4, result.Value!.ItemSourceAssignments.Count);
        Assert.Equal(3, result.Value.SourceAssets.Count);
    }

    [Theory]
    [InlineData("fbx", "models/renamed.fbx")]
    [InlineData("archive", "models/renamed.zip")]
    public void ReviewedSourceKindsAndRenamesRetainTheChosenItemId(string kind, string reference)
    {
        JsonNode root = DraftJson();
        root["sourceAssets"]![0]!["kind"] = kind;
        root["sourceAssets"]![0]!["logicalReference"] = reference;
        ProductManifest draft = ProductManifestJson.Deserialize(root.ToJsonString()).Value!;

        ProductManifestValidationResult result = MultiItemSourceMapper.Map(draft,
            [new(Id("Zed"), reference), new(Id("Alpha"), "models/two.glb")]);

        Assert.True(result.IsValid);
        Assert.Equal(reference, result.Value!.ItemSourceAssignments.Single(value => value.ItemId.Value == "Zed").SourceReference);
    }

    [Fact]
    public void OneItemCanOwnSeveralExplicitlyReviewedModelFiles()
    {
        JsonNode root = DraftJson();
        root["sourceAssets"]!.AsArray().Add(JsonNode.Parse("""{"kind":"fbx","logicalReference":"models/extra.fbx"}"""));
        ProductManifest draft = ProductManifestJson.Deserialize(root.ToJsonString()).Value!;
        ProductManifestValidationResult result = MultiItemSourceMapper.Map(draft,
            [.. Assignments(), new(Id("Alpha"), "models/extra.fbx")]);
        Assert.True(result.IsValid);
        Assert.Equal(2, result.Value!.ItemSourceAssignments.Count(value => value.ItemId.Value == "Alpha"));
    }

    [Fact]
    public void SingleProductCannotAcquireGroupingThroughAssignments()
    {
        JsonNode root = DraftJson();
        root["product"]!["case"] = "static";
        _ = root.AsObject().Remove("itemCollection");
        ProductManifest draft = ProductManifestJson.Deserialize(root.ToJsonString()).Value!;
        ProductManifestValidationResult result = MultiItemSourceMapper.Map(draft, Assignments());
        Assert.False(result.IsValid);
        Assert.Contains(result.Findings, finding => finding.Code.Value == "MANIFEST_ITEM_SOURCE_CASE_INVALID");
    }

    [Theory]
    [InlineData("[{\"itemId\":\"Other\",\"sourceReference\":\"models/one.glb\"}]", ProductManifestJsonError.DomainViolation)]
    [InlineData("[{\"itemId\":\"Alpha\",\"sourceReference\":\"models/missing.glb\"}]", ProductManifestJsonError.DomainViolation)]
    [InlineData("[{\"itemId\":\"Alpha\"}]", ProductManifestJsonError.SchemaViolation)]
    [InlineData("[{\"itemId\":\"Alpha\",\"sourceReference\":\"../escape.glb\"}]", ProductManifestJsonError.SchemaViolation)]
    [InlineData("[]", ProductManifestJsonError.SchemaViolation)]
    [InlineData("null", ProductManifestJsonError.SchemaViolation)]
    public void PersistedAssignmentsCannotBypassCanonicalValidation(string json, ProductManifestJsonError error)
    {
        JsonNode root = DraftJson();
        root["itemSourceAssignments"] = JsonNode.Parse(json);
        ProductManifestDeserializationResult result = ProductManifestJson.Deserialize(root.ToJsonString());
        Assert.False(result.IsSuccessful);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void LegacyDraftsRemainReadableWithoutInventingAssignments()
    {
        ProductManifest draft = Draft();
        Assert.Empty(draft.ItemSourceAssignments);
        string json = ProductManifestJson.Serialize(draft).Json!;
        Assert.DoesNotContain("itemSourceAssignments", json, StringComparison.Ordinal);
        Assert.True(ProductManifestJson.Deserialize(json).IsSuccessful);
    }

    private static ItemSourceAssignment[] Assignments() =>
        [new(Id("Zed"), "models/one.glb"), new(Id("Alpha"), "models/two.glb")];

    private static InternalAssetId Id(string value) => InternalAssetId.Create(value).Value!;

    private static ProductManifest Draft(string productCase = "item-collection")
    {
        JsonNode root = DraftJson();
        root["product"]!["case"] = productCase;
        if (productCase == "item-set")
        {
            JsonNode group = root["itemCollection"]!.DeepClone();
            _ = root.AsObject().Remove("itemCollection");
            root["itemSet"] = group;
        }

        ProductManifestDeserializationResult result = ProductManifestJson.Deserialize(root.ToJsonString());
        Assert.True(result.IsSuccessful);
        return result.Value!;
    }

    private static JsonNode DraftJson() => JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "publisherProfileReference": "ExamplePublisher",
          "product": {"displayName":"Example Collection","assetId":"ExampleCollection","folderName":"Example_Collection","case":"item-collection","version":"1.0.0"},
          "targets": ["portable"],
          "sourceAssets": [
            {"kind":"glb","logicalReference":"models/one.glb"},
            {"kind":"glb","logicalReference":"models/two.glb"},
            {"kind":"image","logicalReference":"textures/shared.png"}
          ],
          "materials": [], "animations": [],
          "itemCollection": {
            "items":[{"id":"Zed","categories":[],"sharedAssetReferences":[]},{"id":"Alpha","categories":[],"sharedAssetReferences":[]}],
            "relationships":[], "sharedAssets":[]
          }
        }
        """)!;
}
