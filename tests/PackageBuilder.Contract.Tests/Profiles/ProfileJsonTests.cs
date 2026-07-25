using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Manifests;
using PackageBuilder.Contracts.Profiles;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Contract.Tests.Profiles;

public sealed class ProfileJsonTests
{
    private const string PublisherExample = "AvivPeretsFBX.example.json";
    private const string MarketplaceExample = "fab.identity.example.json";

    public static TheoryData<string, ProfileJsonError, bool> InvalidFixtures =>
        new()
        {
            {
                "publisher-unknown-property.json",
                ProfileJsonError.SchemaViolation,
                true
            },
            {
                "publisher-contradictory-contact.json",
                ProfileJsonError.DomainViolation,
                true
            },
            {
                "publisher-invalid-year-range.json",
                ProfileJsonError.DomainViolation,
                true
            },
            {
                "marketplace-unknown-version.json",
                ProfileJsonError.SchemaViolation,
                false
            },
        };

    [Fact]
    public void EmbeddedSchemasUseApprovedIdentityDialectAndVersion()
    {
        Assert.Equal(1, PublisherProfileJson.CurrentSchemaVersion);
        Assert.Equal(1, MarketplaceProfileJson.CurrentSchemaVersion);
        Assert.Equal(
            "https://schemas.packagebuilder.dev/publisher-profile/v1",
            PublisherProfileJson.SchemaIdentifier);
        Assert.Equal(
            "https://schemas.packagebuilder.dev/marketplace-profile/v1",
            MarketplaceProfileJson.SchemaIdentifier);
        Assert.Equal(
            "https://json-schema.org/draft/2020-12/schema",
            PublisherProfileJson.SchemaDraft);
        Assert.Equal(PublisherProfileJson.SchemaDraft, MarketplaceProfileJson.SchemaDraft);
        Assert.True(PublisherProfileJson.ValidateSchemaDefinition().IsValid);
        Assert.True(MarketplaceProfileJson.ValidateSchemaDefinition().IsValid);
    }

    [Theory]
    [InlineData(null, "Schema JSON is null.")]
    [InlineData("{}", "identifier")]
    [InlineData("{\"$id\":\"https://schemas.packagebuilder.dev/publisher-profile/v1\"}", "dialect")]
    [InlineData("{\"$id\":\"wrong\",\"$schema\":\"https://json-schema.org/draft/2020-12/schema\"}", "identifier")]
    [InlineData("{\"$id\":\"https://schemas.packagebuilder.dev/publisher-profile/v1\",\"$schema\":\"wrong\"}", "dialect")]
    [InlineData("{", null)]
    public void InvalidSchemaDefinitionsFailClosed(string? schema, string? detail)
    {
        ProfileJsonSchemaValidationResult result =
            PublisherProfileJson.ValidateSchemaDefinition(schema);
        Assert.False(result.IsValid);
        Assert.NotNull(result.Details);
        if (detail is not null)
        {
            Assert.Contains(detail, result.Details, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PublisherExampleIsExactGoldenAndRoundTripsWithoutChangingMeaning()
    {
        string golden = ReadPublisherExample();
        ProfileJsonDeserializationResult<PublisherProfile> parsed =
            PublisherProfileJson.Deserialize(golden);
        Assert.True(parsed.IsSuccessful);
        Assert.Equal("AvivPeretsFBX", parsed.Value!.Root.Value);
        Assert.Equal("AvivPeretsFBX", parsed.Value.DisplayName.Value);
        Assert.Equal(SupportContactKind.SecureUrl, parsed.Value.SupportContact.Kind);
        Assert.Equal(
            "https://example.com/package-builder-support",
            parsed.Value.SupportContact.Value);
        Assert.Equal(
            "Publisher Name (review before publication)",
            parsed.Value.Copyright.Holder.Value);
        Assert.Equal(
            CopyrightYearPolicyKind.PublicationYear,
            parsed.Value.Copyright.YearPolicy.Kind);
        Assert.Equal(2026, parsed.Value.Copyright.YearPolicy.Year);
        Assert.Equal(AiDisclosureState.Undeclared, parsed.Value.AiDisclosure.State);
        Assert.Null(parsed.Value.AiDisclosure.Text);
        Assert.Null(parsed.Value.Branding);

        ProfileJsonSerializationResult first = PublisherProfileJson.Serialize(parsed.Value);
        ProfileJsonSerializationResult second = PublisherProfileJson.Serialize(parsed.Value);
        Assert.True(first.IsSuccessful);
        Assert.Equal(golden, first.Json);
        Assert.Equal(first.Json, second.Json);
        Assert.Equal(parsed.Value, PublisherProfileJson.Deserialize(first.Json).Value);
    }

    [Fact]
    public void MarketplaceExampleIsExactGoldenIdentityOnlyRoundTrip()
    {
        string golden = ReadMarketplaceExample();
        ProfileJsonDeserializationResult<MarketplaceProfile> parsed =
            MarketplaceProfileJson.Deserialize(golden);
        Assert.True(parsed.IsSuccessful);
        Assert.Equal("fab", parsed.Value!.Marketplace.Value);
        Assert.Equal("default", parsed.Value.Identity.Value);

        ProfileJsonSerializationResult first = MarketplaceProfileJson.Serialize(parsed.Value);
        ProfileJsonSerializationResult second = MarketplaceProfileJson.Serialize(parsed.Value);
        Assert.True(first.IsSuccessful);
        Assert.Equal(golden, first.Json);
        Assert.Equal(first.Json, second.Json);
        Assert.Equal(parsed.Value, MarketplaceProfileJson.Deserialize(first.Json).Value);
    }

    [Fact]
    public void ProductManifestReferencesMatchCanonicalProfileIdentities()
    {
        PublisherProfile publisher =
            PublisherProfileJson.Deserialize(ReadPublisherExample()).Value!;
        MarketplaceProfile marketplace =
            MarketplaceProfileJson.Deserialize(ReadMarketplaceExample()).Value!;
        ProductManifestDeserializationResult manifest =
            ProductManifestJson.Deserialize(ReadManifestFixture("static.json"));

        Assert.True(manifest.IsSuccessful);
        Assert.Equal(publisher.Root, manifest.Value!.PublisherProfileReference);
        Assert.Equal(marketplace, manifest.Value.MarketplaceProfileReference);
    }

    [Theory]
    [MemberData(nameof(InvalidFixtures))]
    public void RetainedInvalidFixturesFailAsClassified(
        string fileName,
        ProfileJsonError expected,
        bool publisher)
    {
        string json = ReadInvalidFixture(fileName);
        ProfileJsonError actual = publisher
            ? PublisherProfileJson.Deserialize(json).Error
            : MarketplaceProfileJson.Deserialize(json).Error;
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("email", "support@example.com")]
    [InlineData("secure-url", "https://example.com/support")]
    public void EverySupportContactFormPreservesExplicitKind(string kind, string value)
    {
        JsonObject root = PublisherNode();
        root["supportContact"] = new JsonObject
        {
            ["kind"] = kind,
            ["value"] = value,
        };

        ProfileJsonDeserializationResult<PublisherProfile> parsed =
            PublisherProfileJson.Deserialize(root.ToJsonString());
        Assert.True(parsed.IsSuccessful);
        Assert.Equal(value, parsed.Value!.SupportContact.Value);
        Assert.Equal(
            kind == "email" ? SupportContactKind.Email : SupportContactKind.SecureUrl,
            parsed.Value.SupportContact.Kind);
        string serialized = PublisherProfileJson.Serialize(parsed.Value).Json!;
        Assert.Contains($"\"kind\":\"{kind}\"", serialized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("email", "https://example.com/support")]
    [InlineData("email", "invalid@example..com")]
    [InlineData("secure-url", "support@example.com")]
    [InlineData("secure-url", "http://example.com/support")]
    [InlineData("secure-url", "https://user@example.com/support")]
    public void ContradictoryOrUnsafeSupportContactsFailDomainValidation(
        string kind,
        string value)
    {
        JsonObject root = PublisherNode();
        root["supportContact"] = new JsonObject
        {
            ["kind"] = kind,
            ["value"] = value,
        };

        ProfileJsonDeserializationResult<PublisherProfile> result =
            PublisherProfileJson.Deserialize(root.ToJsonString());
        Assert.False(result.IsSuccessful);
        Assert.Equal(ProfileJsonError.DomainViolation, result.Error);
        Assert.Contains("Domain contract", result.Details, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("single-year", null, 2026)]
    [InlineData("year-range", 2024, 2026)]
    [InlineData("publication-year", null, 2026)]
    public void EveryCopyrightPolicyRoundTrips(
        string kind,
        int? startYear,
        int year)
    {
        JsonObject root = PublisherNode();
        var policy = new JsonObject
        {
            ["kind"] = kind,
            ["year"] = year,
        };
        if (startYear.HasValue)
        {
            policy["startYear"] = startYear.Value;
        }

        root["copyright"]!["yearPolicy"] = policy;
        ProfileJsonDeserializationResult<PublisherProfile> parsed =
            PublisherProfileJson.Deserialize(root.ToJsonString());
        Assert.True(parsed.IsSuccessful);
        string json = PublisherProfileJson.Serialize(parsed.Value).Json!;
        Assert.Equal(parsed.Value, PublisherProfileJson.Deserialize(json).Value);
        Assert.Equal(startYear, parsed.Value!.Copyright.YearPolicy.StartYear);
    }

    [Theory]
    [InlineData("single-year", true)]
    [InlineData("publication-year", true)]
    [InlineData("year-range", false)]
    public void CopyrightPolicyRejectsPropertiesFromAnotherPolicy(
        string kind,
        bool addStart)
    {
        JsonObject root = PublisherNode();
        var policy = new JsonObject
        {
            ["kind"] = kind,
            ["year"] = 2026,
        };
        if (addStart)
        {
            policy["startYear"] = 2024;
        }

        root["copyright"]!["yearPolicy"] = policy;
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            PublisherProfileJson.Deserialize(root.ToJsonString()).Error);
    }

    [Theory]
    [InlineData("undeclared", null)]
    [InlineData("no-ai-assistance", null)]
    [InlineData("no-ai-assistance", "User-reviewed disclosure text.")]
    [InlineData("ai-assisted", null)]
    [InlineData("ai-assisted", "User-reviewed disclosure text.")]
    public void EveryAiDisclosureStateAndOptionalTextRoundTrips(string state, string? text)
    {
        JsonObject root = PublisherNode();
        var disclosure = new JsonObject { ["state"] = state };
        if (text is not null)
        {
            disclosure["text"] = text;
        }

        root["aiDisclosure"] = disclosure;
        ProfileJsonDeserializationResult<PublisherProfile> parsed =
            PublisherProfileJson.Deserialize(root.ToJsonString());
        Assert.True(parsed.IsSuccessful);
        Assert.Equal(text, parsed.Value!.AiDisclosure.Text);
        Assert.Equal(
            PublisherProfileJson.Serialize(parsed.Value).Json,
            PublisherProfileJson.Serialize(parsed.Value).Json);
    }

    [Fact]
    public void UndeclaredDisclosureRejectsText()
    {
        JsonObject root = PublisherNode();
        root["aiDisclosure"] = new JsonObject
        {
            ["state"] = "undeclared",
            ["text"] = "A contradictory claim.",
        };
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            PublisherProfileJson.Deserialize(root.ToJsonString()).Error);
    }

    [Fact]
    public void InvalidDisclosureCopyrightAndBrandingBoundariesFailClosed()
    {
        var invalidProfiles = new List<JsonObject>();

        JsonObject missingYear = PublisherNode();
        missingYear["copyright"]!["yearPolicy"] = new JsonObject
        {
            ["kind"] = "single-year",
        };
        invalidProfiles.Add(missingYear);

        foreach (int year in new[] { 0, 10_000 })
        {
            JsonObject invalidYear = PublisherNode();
            invalidYear["copyright"]!["yearPolicy"]!["year"] = year;
            invalidProfiles.Add(invalidYear);
        }

        JsonObject missingStart = PublisherNode();
        missingStart["copyright"]!["yearPolicy"] = new JsonObject
        {
            ["kind"] = "year-range",
            ["year"] = 2026,
        };
        invalidProfiles.Add(missingStart);

        foreach ((int start, int end) in new[] { (2026, 2026), (2027, 2026) })
        {
            JsonObject invalidRange = PublisherNode();
            invalidRange["copyright"]!["yearPolicy"] = new JsonObject
            {
                ["kind"] = "year-range",
                ["startYear"] = start,
                ["year"] = end,
            };
            invalidProfiles.Add(invalidRange);
        }

        foreach (string text in new[] { string.Empty, " text ", new string('x', 4097) })
        {
            JsonObject invalidText = PublisherNode();
            invalidText["aiDisclosure"] = new JsonObject
            {
                ["state"] = "ai-assisted",
                ["text"] = text,
            };
            invalidProfiles.Add(invalidText);
        }

        JsonObject emptyBranding = PublisherNode();
        emptyBranding["branding"] = new JsonObject { ["images"] = new JsonArray() };
        invalidProfiles.Add(emptyBranding);

        JsonObject nullBrandingImage = PublisherNode();
        nullBrandingImage["branding"] = new JsonObject
        {
            ["images"] = new JsonArray((JsonNode?)null),
        };
        invalidProfiles.Add(nullBrandingImage);

        Assert.All(
            invalidProfiles,
            profile =>
            {
                ProfileJsonError error =
                    PublisherProfileJson.Deserialize(profile.ToJsonString()).Error;
                Assert.True(
                    error is ProfileJsonError.SchemaViolation or ProfileJsonError.DomainViolation);
            });
    }

    [Theory]
    [InlineData("")]
    [InlineData("logo")]
    [InlineData("watermark")]
    [InlineData("both")]
    public void BrandingAbsentAndEveryUniqueRoleCombinationRoundTrips(string selection)
    {
        JsonObject root = PublisherNode();
        if (selection.Length != 0)
        {
            var images = new JsonArray();
            if (selection is "logo" or "both")
            {
                images.Add(Branding("logo", "branding/logo.png", "logo.png"));
            }

            if (selection is "watermark" or "both")
            {
                images.Add(Branding("watermark", "branding/watermark.png", null));
            }

            root["branding"] = new JsonObject { ["images"] = images };
        }

        ProfileJsonDeserializationResult<PublisherProfile> parsed =
            PublisherProfileJson.Deserialize(root.ToJsonString());
        Assert.True(parsed.IsSuccessful);
        Assert.Equal(selection.Length == 0, parsed.Value!.Branding is null);
        string canonical = PublisherProfileJson.Serialize(parsed.Value).Json!;
        Assert.Equal(parsed.Value, PublisherProfileJson.Deserialize(canonical).Value);
    }

    [Fact]
    public void BrandingRejectsDuplicateRolesNonImageSourcesAndUnsafeReferences()
    {
        JsonObject duplicate = PublisherNode();
        duplicate["branding"] = new JsonObject
        {
            ["images"] = new JsonArray
            {
                Branding("logo", "branding/one.png", null),
                Branding("logo", "branding/two.png", null),
            },
        };
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            PublisherProfileJson.Deserialize(duplicate.ToJsonString()).Error);

        JsonObject nonImage = PublisherNode();
        JsonObject wrongSource = Branding("logo", "branding/logo.png", null);
        wrongSource["source"]!["kind"] = "fbx";
        nonImage["branding"] = new JsonObject { ["images"] = new JsonArray(wrongSource) };
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            PublisherProfileJson.Deserialize(nonImage.ToJsonString()).Error);

        JsonObject unsafeReference = PublisherNode();
        unsafeReference["branding"] = new JsonObject
        {
            ["images"] = new JsonArray(Branding("logo", "../logo.png", null)),
        };
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            PublisherProfileJson.Deserialize(unsafeReference.ToJsonString()).Error);

        JsonObject invalidOriginalName = PublisherNode();
        invalidOriginalName["branding"] = new JsonObject
        {
            ["images"] = new JsonArray(
                Branding("logo", "branding/logo.png", " logo.png")),
        };
        Assert.Equal(
            ProfileJsonError.DomainViolation,
            PublisherProfileJson.Deserialize(invalidOriginalName.ToJsonString()).Error);
    }

    [Fact]
    public void UnknownPropertiesAtEveryPublisherObjectLevelAreRejected()
    {
        JsonObject root = PublisherNode();
        root["branding"] = new JsonObject
        {
            ["images"] = new JsonArray(Branding("logo", "branding/logo.png", "logo.png")),
        };
        foreach (string path in GetObjectPaths(root).ToArray())
        {
            var mutation = (JsonObject)root.DeepClone();
            Resolve(mutation, path)!.AsObject()["unexpected"] = true;
            Assert.Equal(
                ProfileJsonError.SchemaViolation,
                PublisherProfileJson.Deserialize(mutation.ToJsonString()).Error);
        }
    }

    [Fact]
    public void MissingNullWrongTypeUnknownVersionAndUnknownTokensFailClosed()
    {
        JsonObject missing = PublisherNode();
        Assert.True(missing.Remove("root"));
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            PublisherProfileJson.Deserialize(missing.ToJsonString()).Error);

        JsonObject nullValue = PublisherNode();
        nullValue["displayName"] = null;
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            PublisherProfileJson.Deserialize(nullValue.ToJsonString()).Error);

        JsonObject wrongType = PublisherNode();
        wrongType["schemaVersion"] = "1";
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            PublisherProfileJson.Deserialize(wrongType.ToJsonString()).Error);

        JsonObject unknownVersion = PublisherNode();
        unknownVersion["schemaVersion"] = 2;
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            PublisherProfileJson.Deserialize(unknownVersion.ToJsonString()).Error);

        JsonObject unknownToken = PublisherNode();
        unknownToken["supportContact"]!["kind"] = "url";
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            PublisherProfileJson.Deserialize(unknownToken.ToJsonString()).Error);
    }

    [Fact]
    public void DuplicateMalformedNonObjectDeepAndOversizedInputsFailClosed()
    {
        string duplicatePublisher =
            ReadPublisherExample().Replace(
                "\"displayName\":\"AvivPeretsFBX\"",
                "\"displayName\":\"AvivPeretsFBX\",\"displayName\":\"Duplicate\"",
                StringComparison.Ordinal);
        Assert.Equal(
            ProfileJsonError.DuplicateProperty,
            PublisherProfileJson.Deserialize(duplicatePublisher).Error);
        Assert.False(PublisherProfileJson.ValidateJsonAgainstSchema(duplicatePublisher).IsValid);

        const string DuplicateInsideArray =
            "{\"schemaVersion\":1,\"root\":\"AvivPeretsFBX\",\"displayName\":\"AvivPeretsFBX\",\"supportContact\":{\"kind\":\"secure-url\",\"value\":\"https://example.com/support\"},\"copyright\":{\"holder\":\"Publisher Name\",\"yearPolicy\":{\"kind\":\"publication-year\",\"year\":2026}},\"aiDisclosure\":{\"state\":\"undeclared\"},\"branding\":{\"images\":[{\"role\":\"logo\",\"source\":{\"kind\":\"image\",\"kind\":\"image\",\"logicalReference\":\"branding/logo.png\"}}]}}";
        Assert.Equal(
            ProfileJsonError.DuplicateProperty,
            PublisherProfileJson.Deserialize(DuplicateInsideArray).Error);

        Assert.Equal(
            ProfileJsonError.MalformedJson,
            PublisherProfileJson.Deserialize("{").Error);
        Assert.Equal(
            ProfileJsonError.RootMustBeObject,
            PublisherProfileJson.Deserialize("[]").Error);
        Assert.Equal(
            ProfileJsonError.MalformedJson,
            PublisherProfileJson.Deserialize(
                "{\"schemaVersion\":1,\"x\":" + new string('[', 65) + "0" +
                new string(']', 65) + "}").Error);
        Assert.Equal(
            ProfileJsonError.InputTooLarge,
            PublisherProfileJson.Deserialize(
                new string('x', PublisherProfileJson.MaximumInputCharacters + 1)).Error);
        Assert.Equal(
            ProfileJsonError.EmptyJson,
            PublisherProfileJson.Deserialize(string.Empty).Error);
        Assert.Equal(
            ProfileJsonError.NullJson,
            PublisherProfileJson.Deserialize(null).Error);
    }

    [Fact]
    public void SchemaInstanceValidationCoversValidNullMalformedAndOversizedInput()
    {
        Assert.True(
            PublisherProfileJson.ValidateJsonAgainstSchema(ReadPublisherExample()).IsValid);
        Assert.True(
            MarketplaceProfileJson.ValidateJsonAgainstSchema(ReadMarketplaceExample()).IsValid);
        Assert.False(PublisherProfileJson.ValidateJsonAgainstSchema(null).IsValid);
        Assert.False(PublisherProfileJson.ValidateJsonAgainstSchema("{").IsValid);
        Assert.False(
            PublisherProfileJson.ValidateJsonAgainstSchema(
                new string('x', PublisherProfileJson.MaximumInputCharacters + 1)).IsValid);
        Assert.False(PublisherProfileJson.ValidateJsonAgainstSchema("{}").IsValid);
    }

    [Fact]
    public void NullProfilesDoNotSerialize()
    {
        Assert.Equal(
            ProfileJsonError.NullProfile,
            PublisherProfileJson.Serialize(null).Error);
        Assert.Equal(
            ProfileJsonError.NullProfile,
            MarketplaceProfileJson.Serialize(null).Error);
    }

    [Fact]
    public void MarketplaceContractRejectsMissingUnknownNullWrongTypeAndDuplicates()
    {
        JsonObject root = JsonNode.Parse(ReadMarketplaceExample())!.AsObject();
        foreach (string property in new[] { "schemaVersion", "marketplace", "profile" })
        {
            var missing = (JsonObject)root.DeepClone();
            Assert.True(missing.Remove(property));
            Assert.Equal(
                ProfileJsonError.SchemaViolation,
                MarketplaceProfileJson.Deserialize(missing.ToJsonString()).Error);
        }

        var unknown = (JsonObject)root.DeepClone();
        unknown["publisherRoot"] = "AvivPeretsFBX";
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            MarketplaceProfileJson.Deserialize(unknown.ToJsonString()).Error);

        var nullValue = (JsonObject)root.DeepClone();
        nullValue["profile"] = null;
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            MarketplaceProfileJson.Deserialize(nullValue.ToJsonString()).Error);

        var wrongType = (JsonObject)root.DeepClone();
        wrongType["marketplace"] = 1;
        Assert.Equal(
            ProfileJsonError.SchemaViolation,
            MarketplaceProfileJson.Deserialize(wrongType.ToJsonString()).Error);

        const string Duplicate =
            "{\"schemaVersion\":1,\"marketplace\":\"fab\",\"profile\":\"default\",\"profile\":\"other\"}";
        Assert.Equal(
            ProfileJsonError.DuplicateProperty,
            MarketplaceProfileJson.Deserialize(Duplicate).Error);
    }

    [Fact]
    public void UnicodeAndOrdinalIdentitiesAreCultureIndependent()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            JsonObject root = PublisherNode();
            root["displayName"] = "Ä°stanbul YayÄ±ncÄ± ä¸–ç•Œ";
            root["copyright"]!["holder"] = "Telif Sahibi ä¸–ç•Œ";
            ProfileJsonDeserializationResult<PublisherProfile> parsed =
                PublisherProfileJson.Deserialize(root.ToJsonString());
            Assert.True(parsed.IsSuccessful);
            Assert.Equal(
                "Ä°stanbul YayÄ±ncÄ± ä¸–ç•Œ",
                parsed.Value!.DisplayName.Value);
            Assert.Equal(
                PublisherProfileJson.Serialize(parsed.Value).Json,
                PublisherProfileJson.Serialize(parsed.Value).Json);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static JsonObject PublisherNode() =>
        JsonNode.Parse(ReadPublisherExample())!.AsObject();

    private static JsonObject Branding(string role, string reference, string? original)
    {
        var source = new JsonObject
        {
            ["kind"] = "image",
            ["logicalReference"] = reference,
        };
        if (original is not null)
        {
            source["originalFileName"] = original;
        }

        return new JsonObject
        {
            ["role"] = role,
            ["source"] = source,
        };
    }

    private static IEnumerable<string> GetObjectPaths(JsonNode node, string path = "")
    {
        if (node is JsonObject objectNode)
        {
            yield return path;
            foreach ((string name, JsonNode? child) in objectNode)
            {
                if (child is not null)
                {
                    foreach (string descendant in GetObjectPaths(child, $"{path}/{name}"))
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
                if (arrayNode[index] is not null)
                {
                    foreach (string descendant in GetObjectPaths(
                        arrayNode[index]!,
                        $"{path}/{index}"))
                    {
                        yield return descendant;
                    }
                }
            }
        }
    }

    private static JsonNode? Resolve(JsonNode root, string path)
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

    private static string ReadPublisherExample() =>
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "profiles",
                "publishers",
                PublisherExample)).Trim();

    private static string ReadMarketplaceExample() =>
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "profiles",
                "marketplaces",
                MarketplaceExample)).Trim();

    private static string ReadInvalidFixture(string fileName) =>
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "fixtures",
                "profiles",
                "invalid",
                fileName)).Trim();

    private static string ReadManifestFixture(string fileName) =>
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "fixtures",
                "manifests",
                "valid",
                fileName)).Trim();
}
