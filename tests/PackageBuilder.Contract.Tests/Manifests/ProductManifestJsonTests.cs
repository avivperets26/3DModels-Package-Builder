using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Manifests;

namespace PackageBuilder.Contract.Tests.Manifests;

public sealed class ProductManifestJsonTests
{
    public static TheoryData<string> ValidCases =>
        [
            "static.json",
            "rigged.json",
            "rigged-animated.json",
            "item-set.json",
            "item-collection.json",
        ];

    public static TheoryData<string, ProductManifestJsonError> InvalidFixtures =>
        new()
        {
            { "unknown-property.json", ProductManifestJsonError.SchemaViolation },
            { "null-required.json", ProductManifestJsonError.SchemaViolation },
            { "wrong-type.json", ProductManifestJsonError.SchemaViolation },
            { "duplicate-source.json", ProductManifestJsonError.DomainViolation },
            { "case-contradiction.json", ProductManifestJsonError.SchemaViolation },
        };

    public static TheoryData<string, string> RequiredPropertyCases =>
        new()
        {
            { "static.json", "/schemaVersion" },
            { "static.json", "/publisherProfileReference" },
            { "static.json", "/product" },
            { "static.json", "/targets" },
            { "static.json", "/sourceAssets" },
            { "static.json", "/materials" },
            { "static.json", "/animations" },
            { "static.json", "/product/displayName" },
            { "static.json", "/product/assetId" },
            { "static.json", "/product/folderName" },
            { "static.json", "/product/case" },
            { "static.json", "/product/version" },
            { "static.json", "/sourceAssets/0/kind" },
            { "static.json", "/sourceAssets/0/logicalReference" },
            { "static.json", "/materials/0/id" },
            { "static.json", "/materials/0/metallicFactor" },
            { "static.json", "/materials/0/roughnessFactor" },
            { "static.json", "/materials/0/normalScale" },
            { "static.json", "/materials/0/emission" },
            { "static.json", "/materials/0/ambientOcclusionStrength" },
            { "static.json", "/materials/0/heightScale" },
            { "static.json", "/materials/0/opacity" },
            { "static.json", "/materials/0/surfaceMode" },
            { "static.json", "/materials/0/uvTransform" },
            { "static.json", "/materials/0/doubleSided" },
            { "static.json", "/materials/0/textureAssignments" },
            { "static.json", "/materials/0/emission/red" },
            { "static.json", "/materials/0/emission/green" },
            { "static.json", "/materials/0/emission/blue" },
            { "static.json", "/materials/0/emission/intensity" },
            { "static.json", "/materials/0/uvTransform/scaleU" },
            { "static.json", "/materials/0/uvTransform/scaleV" },
            { "static.json", "/materials/0/uvTransform/offsetU" },
            { "static.json", "/materials/0/uvTransform/offsetV" },
            { "static.json", "/materials/0/textureAssignments/0/source" },
            { "static.json", "/materials/0/textureAssignments/0/role" },
            { "static.json", "/materials/0/textureAssignments/0/colourSpace" },
            { "static.json", "/materials/0/textureAssignments/0/source/kind" },
            { "static.json", "/materials/0/textureAssignments/0/source/logicalReference" },
            { "rigged-animated.json", "/rig/type" },
            { "rigged-animated.json", "/rig/bones" },
            { "rigged-animated.json", "/rig/referencePose" },
            { "rigged-animated.json", "/rig/bones/0/identity" },
            { "rigged-animated.json", "/rig/referencePose/0/boneIdentity" },
            { "rigged-animated.json", "/rig/referencePose/0/transform" },
            { "rigged-animated.json", "/rig/referencePose/0/transform/translationX" },
            { "rigged-animated.json", "/rig/referencePose/0/transform/translationY" },
            { "rigged-animated.json", "/rig/referencePose/0/transform/translationZ" },
            { "rigged-animated.json", "/rig/referencePose/0/transform/rotationX" },
            { "rigged-animated.json", "/rig/referencePose/0/transform/rotationY" },
            { "rigged-animated.json", "/rig/referencePose/0/transform/rotationZ" },
            { "rigged-animated.json", "/rig/referencePose/0/transform/rotationW" },
            { "rigged-animated.json", "/rig/referencePose/0/transform/scaleX" },
            { "rigged-animated.json", "/rig/referencePose/0/transform/scaleY" },
            { "rigged-animated.json", "/rig/referencePose/0/transform/scaleZ" },
            { "rigged-animated.json", "/animations/0/name" },
            { "rigged-animated.json", "/animations/0/startFrame" },
            { "rigged-animated.json", "/animations/0/endFrame" },
            { "rigged-animated.json", "/animations/0/framesPerSecond" },
            { "rigged-animated.json", "/animations/0/loopBehavior" },
            { "rigged-animated.json", "/animations/0/rootMotionStatus" },
            { "item-set.json", "/itemSet/items" },
            { "item-set.json", "/itemSet/relationships" },
            { "item-set.json", "/itemSet/sharedAssets" },
            { "item-set.json", "/itemSet/items/0/id" },
            { "item-set.json", "/itemSet/items/0/categories" },
            { "item-set.json", "/itemSet/items/0/sharedAssetReferences" },
            { "item-set.json", "/itemSet/relationships/0/firstItemId" },
            { "item-set.json", "/itemSet/relationships/0/secondItemId" },
            { "item-set.json", "/itemSet/sharedAssets/0/id" },
            { "item-set.json", "/itemSet/sharedAssets/0/source" },
            { "item-set.json", "/itemSet/sharedAssets/0/source/kind" },
            { "item-set.json", "/itemSet/sharedAssets/0/source/logicalReference" },
            { "item-set.json", "/itemSet/assembledSetRules/members" },
            { "item-set.json", "/itemSet/assembledSetRules/requireUniqueAttachmentSlots" },
            { "item-set.json", "/itemSet/assembledSetRules/compatibilityMetadata" },
            { "item-set.json", "/itemSet/assembledSetRules/members/0/itemId" },
            { "item-set.json", "/itemSet/assembledSetRules/compatibilityMetadata/0/key" },
            { "item-set.json", "/itemSet/assembledSetRules/compatibilityMetadata/0/value" },
            { "item-collection.json", "/itemCollection/items" },
            { "item-collection.json", "/itemCollection/relationships" },
            { "item-collection.json", "/itemCollection/sharedAssets" },
            { "item-collection.json", "/itemCollection/items/0/id" },
            { "item-collection.json", "/itemCollection/items/0/categories" },
            { "item-collection.json", "/itemCollection/items/0/sharedAssetReferences" },
            { "static.json", "/marketplaceProfileReference/marketplace" },
            { "static.json", "/marketplaceProfileReference/profile" },
        };

    [Fact]
    public void EmbeddedSchemaUsesApprovedIdentityAndDialect()
    {
        ProductManifestSchemaValidationResult result =
            ProductManifestJson.ValidateSchemaDefinition();

        Assert.True(result.IsValid, result.Details);
        using var schema = JsonDocument.Parse(ProductManifestJson.SchemaText);
        Assert.Equal(
            ProductManifestJson.SchemaIdentifier,
            schema.RootElement.GetProperty("$id").GetString());
        Assert.Equal(
            ProductManifestJson.SchemaDraft,
            schema.RootElement.GetProperty("$schema").GetString());
    }

    [Fact]
    public void SchemaDefinitionValidationReturnsStructuredFailures()
    {
        string schema = ProductManifestJson.SchemaText;
        JsonObject schemaObject = JsonNode.Parse(schema)!.AsObject();

        Assert.False(ProductManifestJson.ValidateSchemaDefinition(null).IsValid);
        Assert.False(ProductManifestJson.ValidateSchemaDefinition("{").IsValid);

        var missingId = (JsonObject)schemaObject.DeepClone();
        _ = missingId.Remove("$id");
        Assert.False(
            ProductManifestJson.ValidateSchemaDefinition(missingId.ToJsonString()).IsValid);

        var missingDraft = (JsonObject)schemaObject.DeepClone();
        _ = missingDraft.Remove("$schema");
        Assert.False(
            ProductManifestJson.ValidateSchemaDefinition(missingDraft.ToJsonString()).IsValid);

        var wrongId = (JsonObject)schemaObject.DeepClone();
        wrongId["$id"] = "https://schemas.packagebuilder.dev/product-manifest/unknown";
        Assert.False(
            ProductManifestJson.ValidateSchemaDefinition(wrongId.ToJsonString()).IsValid);

        var wrongDraft = (JsonObject)schemaObject.DeepClone();
        wrongDraft["$schema"] = "https://json-schema.org/draft/2019-09/schema";
        Assert.False(
            ProductManifestJson.ValidateSchemaDefinition(wrongDraft.ToJsonString()).IsValid);
    }

    [Theory]
    [MemberData(nameof(ValidCases))]
    public void ValidFixturesRoundTripToExactDeterministicGoldenJson(string fixtureName)
    {
        string golden = ReadFixture(fixtureName);

        ProductManifestDeserializationResult parsed = ProductManifestJson.Deserialize(golden);
        Assert.True(parsed.IsSuccessful);
        Assert.NotNull(parsed.Value);

        ProductManifestSerializationResult first = ProductManifestJson.Serialize(parsed.Value);
        ProductManifestSerializationResult second = ProductManifestJson.Serialize(parsed.Value);
        Assert.True(first.IsSuccessful);
        Assert.Equal(golden, first.Json);
        Assert.Equal(first.Json, second.Json);
        Assert.True(ProductManifestJson.ValidateJsonAgainstSchema(first.Json).IsValid);

        ProductManifestDeserializationResult reparsed =
            ProductManifestJson.Deserialize(first.Json);
        Assert.True(reparsed.IsSuccessful);
        Assert.Equal(parsed.Value!.ProductCase, reparsed.Value!.ProductCase);
    }

    [Theory]
    [InlineData(null, ProductManifestJsonError.NullJson)]
    [InlineData("", ProductManifestJsonError.EmptyJson)]
    [InlineData("{", ProductManifestJsonError.MalformedJson)]
    [InlineData("[]", ProductManifestJsonError.RootMustBeObject)]
    public void InvalidJsonHasStructuredExpectedError(
        string? json,
        ProductManifestJsonError expected)
    {
        ProductManifestDeserializationResult result = ProductManifestJson.Deserialize(json);

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Value);
        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public void NullManifestAndOversizedInputAreRejected()
    {
        ProductManifestSerializationResult serialization =
            ProductManifestJson.Serialize(null);
        string oversized = new(' ', ProductManifestJson.MaximumInputCharacters + 1);

        Assert.False(serialization.IsSuccessful);
        Assert.Equal(ProductManifestJsonError.NullManifest, serialization.Error);
        Assert.Equal(
            ProductManifestJsonError.InputTooLarge,
            ProductManifestJson.Deserialize(oversized).Error);
    }

    [Fact]
    public void ExcessiveNestingIsRejectedAsMalformedInput()
    {
        string overDeep =
            new string('[', ProductManifestJson.MaximumDepth + 1) +
            "0" +
            new string(']', ProductManifestJson.MaximumDepth + 1);

        Assert.Equal(
            ProductManifestJsonError.MalformedJson,
            ProductManifestJson.Deserialize(overDeep).Error);
    }

    [Fact]
    public void DuplicatePropertiesAreRejectedBeforeSchemaEvaluation()
    {
        string json = ReadFixture("static.json").Replace(
            "\"schemaVersion\":1,",
            "\"schemaVersion\":1,\"schemaVersion\":1,",
            StringComparison.Ordinal);

        Assert.Equal(
            ProductManifestJsonError.DuplicateProperty,
            ProductManifestJson.Deserialize(json).Error);
    }

    [Fact]
    public void DuplicatePropertiesNestedInsideArraysAreRejected()
    {
        string json = ReadFixture("static.json").Replace(
            "\"kind\":\"fbx\",\"logicalReference\"",
            "\"kind\":\"fbx\",\"kind\":\"fbx\",\"logicalReference\"",
            StringComparison.Ordinal);

        Assert.Equal(
            ProductManifestJsonError.DuplicateProperty,
            ProductManifestJson.Deserialize(json).Error);
    }

    [Theory]
    [InlineData("unknownTopLevel")]
    [InlineData("unknownNested")]
    [InlineData("nullRequired")]
    [InlineData("wrongType")]
    [InlineData("unknownVersion")]
    [InlineData("contradictoryCase")]
    public void SchemaRejectsStrictContractViolations(string mutation)
    {
        JsonObject root = ParseFixture("static.json");
        switch (mutation)
        {
            case "unknownTopLevel":
                root["unexpected"] = true;
                break;
            case "unknownNested":
                root["product"]!["unexpected"] = true;
                break;
            case "nullRequired":
                root["product"]!["assetId"] = null;
                break;
            case "wrongType":
                root["targets"] = "portable";
                break;
            case "unknownVersion":
                root["schemaVersion"] = 2;
                break;
            case "contradictoryCase":
                root["rig"] = ParseFixture("rigged.json")["rig"]!.DeepClone();
                break;
        }

        string json = root.ToJsonString();
        Assert.False(ProductManifestJson.ValidateJsonAgainstSchema(json).IsValid);
        Assert.Equal(
            ProductManifestJsonError.SchemaViolation,
            ProductManifestJson.Deserialize(json).Error);
    }

    [Theory]
    [InlineData("duplicateSource")]
    [InlineData("unknownTextureSource")]
    public void SemanticViolationsReturnDomainFindings(string mutation)
    {
        JsonObject root = ParseFixture("static.json");
        if (mutation == "duplicateSource")
        {
            JsonArray sources = root["sourceAssets"]!.AsArray();
            sources.Add(sources[0]!.DeepClone());
        }
        else
        {
            root["materials"]![0]!["textureAssignments"]![0]!["source"]![
                "logicalReference"] = "textures/undeclared.png";
        }

        ProductManifestDeserializationResult result =
            ProductManifestJson.Deserialize(root.ToJsonString());

        Assert.False(result.IsSuccessful);
        Assert.Equal(ProductManifestJsonError.DomainViolation, result.Error);
        Assert.NotEmpty(result.Findings);
        Assert.All(result.Findings, finding => Assert.True(finding.BlocksRelease));
    }

    [Fact]
    public void StandaloneSchemaValidationHandlesNullAndMalformedJson()
    {
        Assert.False(ProductManifestJson.ValidateJsonAgainstSchema(null).IsValid);
        Assert.False(ProductManifestJson.ValidateJsonAgainstSchema("{").IsValid);
    }

    [Theory]
    [MemberData(nameof(InvalidFixtures))]
    public void InvalidFixturesRemainRejected(
        string fixtureName,
        ProductManifestJsonError expected)
    {
        ProductManifestDeserializationResult result =
            ProductManifestJson.Deserialize(ReadFixture(fixtureName, "invalid"));

        Assert.False(result.IsSuccessful);
        Assert.Equal(expected, result.Error);
    }

    [Theory]
    [MemberData(nameof(RequiredPropertyCases))]
    public void EveryRequiredPropertyLevelRejectsOmission(string fixtureName, string path)
    {
        JsonObject root = ParseFixture(fixtureName);
        RemoveProperty(root, path);

        Assert.False(ProductManifestJson.ValidateJsonAgainstSchema(root.ToJsonString()).IsValid);
        Assert.Equal(
            ProductManifestJsonError.SchemaViolation,
            ProductManifestJson.Deserialize(root.ToJsonString()).Error);
    }

    [Theory]
    [MemberData(nameof(ValidCases))]
    public void EveryOwnedObjectRejectsUnknownProperties(string fixtureName)
    {
        JsonObject original = ParseFixture(fixtureName);
        foreach (string path in GetObjectPaths(original))
        {
            var mutated = (JsonObject)original.DeepClone();
            ResolveNode(mutated, path)!.AsObject()["unexpected"] = true;

            Assert.False(
                ProductManifestJson.ValidateJsonAgainstSchema(mutated.ToJsonString()).IsValid);
        }
    }

    [Theory]
    [MemberData(nameof(ValidCases))]
    public void EveryPresentPropertyRejectsNullAndWrongJsonType(string fixtureName)
    {
        JsonObject original = ParseFixture(fixtureName);
        foreach (string path in GetPropertyPaths(original))
        {
            var nullMutation = (JsonObject)original.DeepClone();
            ReplaceProperty(nullMutation, path, null);
            Assert.False(
                ProductManifestJson.ValidateJsonAgainstSchema(nullMutation.ToJsonString()).IsValid);

            var typeMutation = (JsonObject)original.DeepClone();
            JsonNode current = ResolveNode(typeMutation, path)!;
            JsonNode wrongType = current is JsonObject or JsonArray
                ? JsonValue.Create("wrong")!
                : current.GetValueKind() == System.Text.Json.JsonValueKind.String
                    ? JsonValue.Create(true)!
                    : JsonValue.Create("wrong")!;
            ReplaceProperty(typeMutation, path, wrongType);
            Assert.False(
                ProductManifestJson.ValidateJsonAgainstSchema(typeMutation.ToJsonString()).IsValid);
        }
    }

    [Theory]
    [InlineData("static.json", "/product/case", "unknown")]
    [InlineData("static.json", "/targets/0", "fbx")]
    [InlineData("static.json", "/sourceAssets/0/kind", "obj")]
    [InlineData("static.json", "/materials/0/surfaceMode", "blend")]
    [InlineData("static.json", "/materials/0/textureAssignments/0/role", "diffuse")]
    [InlineData("rigged.json", "/rig/type", "automatic")]
    [InlineData("rigged-animated.json", "/animations/0/loopBehavior", "ping-pong")]
    [InlineData("rigged-animated.json", "/animations/0/rootMotionStatus", "enabled")]
    public void UnknownCanonicalTokensAreRejected(
        string fixtureName,
        string path,
        string token)
    {
        JsonObject root = ParseFixture(fixtureName);
        ReplaceProperty(root, path, JsonValue.Create(token));

        Assert.Equal(
            ProductManifestJsonError.SchemaViolation,
            ProductManifestJson.Deserialize(root.ToJsonString()).Error);
    }

    [Fact]
    public void EmptyAndDuplicateTargetsAreRejected()
    {
        JsonObject empty = ParseFixture("static.json");
        empty["targets"] = new JsonArray();
        JsonObject duplicate = ParseFixture("static.json");
        duplicate["targets"]!.AsArray().Add("portable");

        Assert.Equal(
            ProductManifestJsonError.SchemaViolation,
            ProductManifestJson.Deserialize(empty.ToJsonString()).Error);
        Assert.Equal(
            ProductManifestJsonError.SchemaViolation,
            ProductManifestJson.Deserialize(duplicate.ToJsonString()).Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public void OlderAndFutureSchemaVersionsFailClearly(int version)
    {
        JsonObject root = ParseFixture("static.json");
        root["schemaVersion"] = version;

        Assert.Equal(
            ProductManifestJsonError.SchemaViolation,
            ProductManifestJson.Deserialize(root.ToJsonString()).Error);
    }

    [Fact]
    public void SchemaValidDomainValuesStillFailWithoutNormalization()
    {
        JsonObject root = ParseFixture("static.json");
        root["product"]!["folderName"] = "CON";
        root["publisherProfileReference"] = "CON";

        ProductManifestDeserializationResult result =
            ProductManifestJson.Deserialize(root.ToJsonString());

        Assert.False(result.IsSuccessful);
        Assert.Equal(ProductManifestJsonError.DomainViolation, result.Error);
        _ = Assert.Single(result.Findings);
        Assert.Equal("MANIFEST_VALUE_INVALID", result.Findings[0].Code.Value);
    }

    [Fact]
    public void EveryCaseSpecificContradictionIsRejected()
    {
        JsonObject rigged = ParseFixture("rigged.json");
        JsonObject animated = ParseFixture("rigged-animated.json");
        JsonObject itemSet = ParseFixture("item-set.json");
        JsonObject collection = ParseFixture("item-collection.json");
        var contradictions = new List<JsonObject>();

        AddSectionContradictions(
            contradictions,
            ParseFixture("static.json"),
            ("rig", rigged["rig"]!),
            ("animations", animated["animations"]!),
            ("itemSet", itemSet["itemSet"]!),
            ("itemCollection", collection["itemCollection"]!));

        var riggedMissingRig = (JsonObject)rigged.DeepClone();
        _ = riggedMissingRig.Remove("rig");
        contradictions.Add(riggedMissingRig);
        AddSectionContradictions(
            contradictions,
            rigged,
            ("animations", animated["animations"]!),
            ("itemSet", itemSet["itemSet"]!),
            ("itemCollection", collection["itemCollection"]!));

        var animatedMissingRig = (JsonObject)animated.DeepClone();
        _ = animatedMissingRig.Remove("rig");
        contradictions.Add(animatedMissingRig);
        var animatedMissingClips = (JsonObject)animated.DeepClone();
        animatedMissingClips["animations"] = new JsonArray();
        contradictions.Add(animatedMissingClips);
        AddSectionContradictions(
            contradictions,
            animated,
            ("itemSet", itemSet["itemSet"]!),
            ("itemCollection", collection["itemCollection"]!));

        var setMissingSection = (JsonObject)itemSet.DeepClone();
        _ = setMissingSection.Remove("itemSet");
        contradictions.Add(setMissingSection);
        AddSectionContradictions(
            contradictions,
            itemSet,
            ("rig", rigged["rig"]!),
            ("animations", animated["animations"]!),
            ("itemCollection", collection["itemCollection"]!));

        var collectionMissingSection = (JsonObject)collection.DeepClone();
        _ = collectionMissingSection.Remove("itemCollection");
        contradictions.Add(collectionMissingSection);
        AddSectionContradictions(
            contradictions,
            collection,
            ("rig", rigged["rig"]!),
            ("animations", animated["animations"]!),
            ("itemSet", itemSet["itemSet"]!));

        Assert.All(
            contradictions,
            manifest =>
                Assert.Equal(
                    ProductManifestJsonError.SchemaViolation,
                    ProductManifestJson.Deserialize(manifest.ToJsonString()).Error));
    }

    [Theory]
    [InlineData("unsafeSource")]
    [InlineData("invalidMaterial")]
    [InlineData("invalidRig")]
    [InlineData("invalidClip")]
    [InlineData("invalidSet")]
    [InlineData("invalidCollection")]
    public void MalformedNestedDomainSectionsAreRejected(string mutation)
    {
        string fixtureName = mutation switch
        {
            "invalidRig" or "invalidClip" => "rigged-animated.json",
            "invalidSet" => "item-set.json",
            "invalidCollection" => "item-collection.json",
            _ => "static.json",
        };
        JsonObject root = ParseFixture(fixtureName);
        ProductManifestJsonError expected = ProductManifestJsonError.SchemaViolation;
        switch (mutation)
        {
            case "unsafeSource":
                root["sourceAssets"]![0]!["logicalReference"] = "../escape.fbx";
                break;
            case "invalidMaterial":
                root["materials"]![0]!["metallicFactor"] = 2;
                break;
            case "invalidRig":
                root["rig"]!["bones"]!.AsArray().Add(
                    root["rig"]!["bones"]![0]!.DeepClone());
                expected = ProductManifestJsonError.DomainViolation;
                break;
            case "invalidClip":
                root["animations"]![0]!["endFrame"] = -1;
                expected = ProductManifestJsonError.DomainViolation;
                break;
            case "invalidSet":
                root["itemSet"]!["items"]!.AsArray().Add(
                    root["itemSet"]!["items"]![0]!.DeepClone());
                expected = ProductManifestJsonError.DomainViolation;
                break;
            case "invalidCollection":
                root["itemCollection"]!["assembledSetRules"] =
                    ParseFixture("item-set.json")["itemSet"]![
                        "assembledSetRules"]!.DeepClone();
                break;
        }

        ProductManifestDeserializationResult result =
            ProductManifestJson.Deserialize(root.ToJsonString());
        Assert.False(result.IsSuccessful);
        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public void OptionalNestedBranchesRemainDeterministic()
    {
        JsonObject materialWithoutCutoff = ParseFixture("static.json");
        materialWithoutCutoff["materials"]![0]!["surfaceMode"] = "opaque";
        _ = materialWithoutCutoff["materials"]![0]!.AsObject().Remove("alphaCutoff");
        ProductManifestDeserializationResult materialResult =
            ProductManifestJson.Deserialize(materialWithoutCutoff.ToJsonString());
        Assert.True(materialResult.IsSuccessful);
        Assert.True(ProductManifestJson.Serialize(materialResult.Value).IsSuccessful);

        JsonObject setWithoutRules = ParseFixture("item-set.json");
        _ = setWithoutRules["itemSet"]!.AsObject().Remove("assembledSetRules");
        ProductManifestDeserializationResult setResult =
            ProductManifestJson.Deserialize(setWithoutRules.ToJsonString());
        Assert.True(setResult.IsSuccessful);
        Assert.True(ProductManifestJson.Serialize(setResult.Value).IsSuccessful);

        JsonObject animationWithoutRootMotion = ParseFixture("rigged-animated.json");
        animationWithoutRootMotion["animations"]![0]!["rootMotionStatus"] = "none";
        _ = animationWithoutRootMotion["animations"]![0]!.AsObject().Remove(
            "rootMotionBoneIdentity");
        ProductManifestDeserializationResult animationResult =
            ProductManifestJson.Deserialize(animationWithoutRootMotion.ToJsonString());
        Assert.True(animationResult.IsSuccessful);
        Assert.True(ProductManifestJson.Serialize(animationResult.Value).IsSuccessful);

        JsonObject memberWithoutSlot = ParseFixture("item-set.json");
        _ = memberWithoutSlot["itemSet"]!["items"]![0]!.AsObject().Remove(
            "attachmentSlot");
        _ = memberWithoutSlot["itemSet"]!["assembledSetRules"]!["members"]![0]!
            .AsObject().Remove("attachmentSlot");
        ProductManifestDeserializationResult memberResult =
            ProductManifestJson.Deserialize(memberWithoutSlot.ToJsonString());
        Assert.True(memberResult.IsSuccessful);
        Assert.True(ProductManifestJson.Serialize(memberResult.Value).IsSuccessful);
    }

    [Theory]
    [InlineData("material")]
    [InlineData("animation")]
    [InlineData("assembledMember")]
    public void SchemaValidInvalidNestedValuesReturnDomainViolation(string section)
    {
        string fixtureName = section switch
        {
            "animation" => "rigged-animated.json",
            "assembledMember" => "item-set.json",
            _ => "static.json",
        };
        JsonObject root = ParseFixture(fixtureName);
        switch (section)
        {
            case "material":
                _ = root["materials"]![0]!.AsObject().Remove("alphaCutoff");
                break;
            case "animation":
                root["animations"]![0]!["rootMotionBoneIdentity"] = "Unknown";
                break;
            case "assembledMember":
                root["itemSet"]!["assembledSetRules"]!["members"]![0]!["itemId"] = "CON";
                break;
        }

        Assert.Equal(
            ProductManifestJsonError.DomainViolation,
            ProductManifestJson.Deserialize(root.ToJsonString()).Error);
    }

    [Fact]
    public void UnicodeAndLargeBoundedTextRoundTripCultureIndependently()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            JsonObject root = ParseFixture("static.json");
            root["product"]!["displayName"] = "Taş Kemer 世界 " + new string('x', 100_000);

            ProductManifestDeserializationResult parsed =
                ProductManifestJson.Deserialize(root.ToJsonString());
            Assert.True(parsed.IsSuccessful);
            ProductManifestSerializationResult serialized =
                ProductManifestJson.Serialize(parsed.Value);
            Assert.True(serialized.IsSuccessful);
            ProductManifestDeserializationResult reparsed =
                ProductManifestJson.Deserialize(serialized.Json);
            Assert.True(reparsed.IsSuccessful);
            Assert.StartsWith(
                "Taş Kemer 世界",
                reparsed.Value!.DisplayName.Value,
                StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static JsonObject ParseFixture(string fixtureName) =>
        JsonNode.Parse(ReadFixture(fixtureName))!.AsObject();

    private static void AddSectionContradictions(
        List<JsonObject> contradictions,
        JsonObject source,
        params (string Name, JsonNode Value)[] sections)
    {
        foreach ((string name, JsonNode value) in sections)
        {
            var mutation = (JsonObject)source.DeepClone();
            mutation[name] = value.DeepClone();
            contradictions.Add(mutation);
        }
    }

    private static IEnumerable<string> GetObjectPaths(JsonNode node, string path = "")
    {
        if (node is JsonObject objectNode)
        {
            yield return path;
            foreach ((string name, JsonNode? child) in objectNode)
            {
                if (child is null)
                {
                    continue;
                }

                foreach (string childPath in GetObjectPaths(child, $"{path}/{name}"))
                {
                    yield return childPath;
                }
            }
        }
        else if (node is JsonArray arrayNode)
        {
            for (int index = 0; index < arrayNode.Count; index++)
            {
                if (arrayNode[index] is not null)
                {
                    foreach (string childPath in GetObjectPaths(
                        arrayNode[index]!,
                        $"{path}/{index}"))
                    {
                        yield return childPath;
                    }
                }
            }
        }
    }

    private static IEnumerable<string> GetPropertyPaths(JsonNode node, string path = "")
    {
        if (node is JsonObject objectNode)
        {
            foreach ((string name, JsonNode? child) in objectNode)
            {
                string childPath = $"{path}/{name}";
                yield return childPath;
                if (child is JsonObject or JsonArray)
                {
                    foreach (string descendant in GetPropertyPaths(child, childPath))
                    {
                        yield return descendant;
                    }
                }
            }
        }
        else if (node is JsonArray arrayNode)
        {
            for (int index = 0; index < arrayNode.Count; index++)
            {
                if (arrayNode[index] is JsonObject or JsonArray)
                {
                    foreach (string descendant in GetPropertyPaths(
                        arrayNode[index]!,
                        $"{path}/{index}"))
                    {
                        yield return descendant;
                    }
                }
            }
        }
    }

    private static JsonNode? ResolveNode(JsonNode root, string path)
    {
        JsonNode? current = root;
        foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current is JsonArray array
                ? array[int.Parse(segment, CultureInfo.InvariantCulture)]
                : current![segment];
        }

        return current;
    }

    private static void RemoveProperty(JsonObject root, string path)
    {
        (JsonNode parent, string property) = ResolveParent(root, path);
        Assert.True(parent.AsObject().Remove(property));
    }

    private static void ReplaceProperty(JsonObject root, string path, JsonNode? replacement)
    {
        (JsonNode parent, string property) = ResolveParent(root, path);
        if (parent is JsonArray array)
        {
            array[int.Parse(property, CultureInfo.InvariantCulture)] = replacement;
        }
        else
        {
            parent[property] = replacement;
        }
    }

    private static (JsonNode Parent, string Property) ResolveParent(
        JsonObject root,
        string path)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string parentPath = string.Join('/', segments[..^1]);
        return (ResolveNode(root, parentPath)!, segments[^1]);
    }

    private static string ReadFixture(
        string fixtureName,
        string classification = "valid") =>
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "fixtures",
                "manifests",
                classification,
                fixtureName)).Trim();
}
