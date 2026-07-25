using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Migrations;

namespace PackageBuilder.Contract.Tests.Migrations;

public sealed class ManifestProfileMigrationTests
{
    public static TheoryData<string, MigrationDocumentFamily> CurrentDocuments =>
        new()
        {
            { ManifestFixture("static.json"), MigrationDocumentFamily.ProductManifest },
            { ManifestFixture("rigged.json"), MigrationDocumentFamily.ProductManifest },
            { ManifestFixture("rigged-animated.json"), MigrationDocumentFamily.ProductManifest },
            { ManifestFixture("item-set.json"), MigrationDocumentFamily.ProductManifest },
            { ManifestFixture("item-collection.json"), MigrationDocumentFamily.ProductManifest },
            { PublisherFixture(), MigrationDocumentFamily.PublisherProfile },
            { MarketplaceFixture(), MigrationDocumentFamily.MarketplaceProfile },
        };

    public static TheoryData<string?> InvalidVersionDocuments =>
        new()
        {
            { "{\"product\":{}}" },
            { "{\"schemaVersion\":null,\"product\":{}}" },
            { "{\"schemaVersion\":\"1\",\"product\":{}}" },
            { "{\"schemaVersion\":1.5,\"product\":{}}" },
            { "{\"schemaVersion\":0,\"product\":{}}" },
            { "{\"schemaVersion\":-1,\"product\":{}}" },
            { "{\"schemaVersion\":2147483648,\"product\":{}}" },
            { "{\"schemaVersion\":999999999999999999999999,\"product\":{}}" },
        };

    [Theory]
    [MemberData(nameof(CurrentDocuments))]
    public void CurrentVersionIsDetectedTypedValidatedAndCanonical(
        string json,
        MigrationDocumentFamily family)
    {
        MigrationResult inspected = ManifestProfileMigration.Inspect(json);
        MigrationResult migrated = ManifestProfileMigration.Migrate(json);

        Assert.Equal(MigrationStatus.CurrentDocument, inspected.Status);
        Assert.Equal(MigrationStatus.CurrentDocument, migrated.Status);
        Assert.Equal(family, inspected.Family);
        Assert.Equal(1, inspected.SourceVersion!.Value);
        Assert.Equal(1, inspected.TargetVersion!.Value);
        Assert.Equal(json, inspected.OriginalJson);
        Assert.Equal(json, inspected.FinalDocument!.CanonicalJson);
        Assert.Empty(inspected.Changes);
        Assert.Equal("MIGRATION_NOT_REQUIRED", inspected.DiagnosticCode);
        AssertTypedFamily(inspected);
    }

    [Theory]
    [MemberData(nameof(InvalidVersionDocuments))]
    public void MissingNullWrongMalformedAndOutOfRangeVersionsAreInvalid(string? json)
    {
        MigrationResult result = ManifestProfileMigration.Inspect(json);

        Assert.Equal(MigrationStatus.InvalidDocument, result.Status);
        Assert.Null(result.SourceVersion);
        Assert.Null(result.FinalDocument);
        Assert.StartsWith("MIGRATION_", result.DiagnosticCode, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":1,\"unknown\":true}")]
    [InlineData("{\"schemaVersion\":1,\"product\":{},\"root\":\"Publisher\"}")]
    public void InvalidJsonAndUnknownOrAmbiguousFamiliesFailClosed(string? json)
    {
        MigrationResult result = ManifestProfileMigration.Migrate(json);

        Assert.Equal(MigrationStatus.InvalidDocument, result.Status);
        Assert.Null(result.FinalDocument);
    }

    [Fact]
    public void DuplicateVersionAtEveryRepresentativeNestingLevelIsRejected()
    {
        string[] documents =
        [
            "{\"schemaVersion\":1,\"schemaVersion\":1,\"product\":{}}",
            "{\"schemaVersion\":1,\"product\":{\"name\":\"a\",\"name\":\"b\"}}",
            "{\"schemaVersion\":1,\"product\":{\"items\":[{\"name\":\"a\",\"name\":\"b\"}]}}",
        ];

        Assert.All(
            documents,
            json =>
            {
                MigrationResult result = ManifestProfileMigration.Inspect(json);
                Assert.Equal(MigrationStatus.InvalidDocument, result.Status);
                Assert.Equal("MIGRATION_JSON_DUPLICATEPROPERTY", result.DiagnosticCode);
            });
    }

    [Fact]
    public void ProductionVersionOneDocumentsNeverInventALegacyMigration()
    {
        MigrationResult current = ManifestProfileMigration.Inspect(ManifestFixture("static.json"));
        JsonObject future = JsonNode.Parse(ManifestFixture("static.json"))!.AsObject();
        future["schemaVersion"] = 2;
        MigrationResult unsupportedFuture =
            ManifestProfileMigration.Inspect(future.ToJsonString());

        Assert.Equal(MigrationStatus.CurrentDocument, current.Status);
        Assert.Equal(MigrationStatus.UnsupportedNewerVersion, unsupportedFuture.Status);
        Assert.Equal("MIGRATION_VERSION_NEWER_UNSUPPORTED", unsupportedFuture.DiagnosticCode);
    }

    [Fact]
    public void ControlledRegistryDistinguishesUnsupportedOlderAndMissingStep()
    {
        MigrationRegistryResult empty = MigrationRegistry.Create([], Versions(product: 3));
        ManifestProfileMigrationEngine noHistory = Engine(empty);
        Assert.Equal(
            MigrationStatus.UnsupportedOlderVersion,
            noHistory.Inspect(Representative(1, "\"oldName\":\"Asset\"")).Status);

        MigrationRegistryResult partial = MigrationRegistry.Create(
            [StepTwoToThree()],
            Versions(product: 3));
        ManifestProfileMigrationEngine missing = Engine(partial);
        Assert.Equal(
            MigrationStatus.MissingMigrationStep,
            missing.Inspect(Representative(1, "\"oldName\":\"Asset\"")).Status);
    }

    [Fact]
    public void InspectReportsAvailableWithoutExecutingAndMigrationRunsSingleOrMultipleSteps()
    {
        MigrationRegistryResult registry = RepresentativeRegistry();
        ManifestProfileMigrationEngine engine = Engine(registry);
        string versionOne = MigrationFixture("representative-v1.json");
        string versionTwo = MigrationFixture("representative-v2.json");

        MigrationResult available = engine.Inspect(versionOne);
        MigrationResult multi = engine.Migrate(versionOne);
        MigrationResult single = engine.Migrate(versionTwo);

        Assert.Equal(MigrationStatus.MigrationAvailable, available.Status);
        Assert.Null(available.FinalDocument);
        Assert.Empty(available.Changes);
        Assert.Equal(MigrationStatus.SuccessfullyMigrated, multi.Status);
        Assert.Equal(MigrationStatus.SuccessfullyMigrated, single.Status);
        Assert.Equal(6, multi.Changes.Count);
        Assert.Equal(4, single.Changes.Count);
        Assert.Equal(
            MigrationFixture("representative-v3.json"),
            multi.FinalDocument!.CanonicalJson);
        Assert.Equal(multi.FinalDocument.CanonicalJson, single.FinalDocument!.CanonicalJson);
        Assert.Equal(versionOne, multi.OriginalJson);
        Assert.Equal(1, multi.SourceVersion!.Value);
        Assert.Equal(3, multi.TargetVersion!.Value);
    }

    [Fact]
    public void ChangeLedgerExplicitlyRecordsEverySupportedChangeCategory()
    {
        MigrationResult result = Engine(RepresentativeRegistry()).Migrate(
            Representative(1, "\"oldName\":\"Asset\""));

        Assert.Contains(
            result.Changes,
            change => change.Kind == MigrationChangeKind.Rename &&
                      change.SourcePointer == "/oldName" &&
                      change.TargetPointer == "/name");
        Assert.Contains(
            result.Changes,
            change => change.Kind == MigrationChangeKind.Default &&
                      change.TargetPointer == "/enabled");
        Assert.Contains(
            result.Changes,
            change => change.Kind == MigrationChangeKind.Conversion &&
                      change.SourcePointer == "/schemaVersion");
        Assert.Contains(result.Changes, change => change.Kind == MigrationChangeKind.Warning);
        Assert.Contains(
            result.Changes,
            change => change.Kind == MigrationChangeKind.ReviewRequired);
        Assert.All(result.Changes, change => Assert.NotEmpty(change.Description));
    }

    [Fact]
    public void RegistryRejectsDuplicatesGapsCyclesDowngradesAndAmbiguousPaths()
    {
        IJsonMigrationStep oneToTwo = StepOneToTwo();
        AssertRegistryError(
            MigrationRegistryError.DuplicateRegistration,
            [oneToTwo, StepOneToTwo()],
            productCurrent: 2);
        AssertRegistryError(
            MigrationRegistryError.Gap,
            [Step(1, 3, source => SuccessfulOutput(source, 3, []))],
            productCurrent: 3);
        AssertRegistryError(
            MigrationRegistryError.Cycle,
            [
                oneToTwo,
                Step(2, 1, source => SuccessfulOutput(source, 1, [])),
            ],
            productCurrent: 3);
        AssertRegistryError(
            MigrationRegistryError.Downgrade,
            [Step(2, 1, source => SuccessfulOutput(source, 1, []))],
            productCurrent: 2);
        AssertRegistryError(
            MigrationRegistryError.AmbiguousPath,
            [
                oneToTwo,
                Step(1, 3, source => SuccessfulOutput(source, 3, [])),
            ],
            productCurrent: 3);
    }

    [Fact]
    public void InvalidRegistryIsReportedWithoutSelectingAPath()
    {
        MigrationRegistryResult ambiguous = MigrationRegistry.Create(
            [
                StepOneToTwo(),
                Step(1, 3, source => SuccessfulOutput(source, 3, [])),
            ],
            Versions(product: 3));
        MigrationResult result = Engine(ambiguous).Inspect(
            Representative(1, "\"oldName\":\"Asset\""));

        Assert.Equal(MigrationStatus.AmbiguousMigrationPath, result.Status);
        Assert.Equal("MIGRATION_REGISTRY_AMBIGUOUSPATH", result.DiagnosticCode);
    }

    [Fact]
    public void StepFailureExceptionMalformedOutputAndDuplicateOutputFailClosed()
    {
        IJsonMigrationStep[] badSteps =
        [
            Step(
                1,
                2,
                _ => MigrationStepResult.Failure("CONTROLLED_STEP_FAILURE")),
            Step(
                1,
                2,
                _ => throw new InvalidOperationException("Sensitive raw input must not escape.")),
            Step(
                1,
                2,
                _ => MigrationStepResult.Success("{", [])),
            Step(
                1,
                2,
                _ => MigrationStepResult.Success(
                    "{\"schemaVersion\":2,\"schemaVersion\":2,\"product\":{}}",
                    [])),
            Step(1, 2, _ => MigrationStepResult.Success(null!, [])),
            Step(
                1,
                2,
                source => SuccessfulOutput(source, 2, [null!])),
            Step(1, 2, _ => MigrationStepResult.Failure(null!)),
        ];

        foreach (IJsonMigrationStep step in badSteps)
        {
            MigrationRegistryResult registry = MigrationRegistry.Create(
                [step],
                Versions(product: 2));
            MigrationResult result = Engine(registry).Migrate(
                Representative(1, "\"oldName\":\"SensitiveValue\""));
            Assert.Equal(MigrationStatus.MigrationFailure, result.Status);
            Assert.DoesNotContain(
                "SensitiveValue",
                result.DiagnosticCode,
                StringComparison.Ordinal);
            Assert.Null(result.FinalDocument);
        }
    }

    [Fact]
    public void RemovedRenamedOrTransformedDataCannotDisappearWithoutLedgerEvidence()
    {
        IJsonMigrationStep silentRemoval = Step(
            1,
            2,
            source =>
            {
                JsonObject output = JsonNode.Parse(source.GetRawText())!.AsObject();
                string convertedValue = output["oldName"]!.GetValue<string>();
                output["schemaVersion"] = 2;
                _ = output.Remove("oldName");
                output["converted"] = convertedValue;
                return MigrationStepResult.Success(
                    output.ToJsonString(),
                    [
                        Change(
                            MigrationChangeKind.Warning,
                            "/oldName",
                            null,
                            "A warning cannot authorize removal of the matching source."),
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance the controlled schema version."),
                    ]);
            });
        MigrationRegistryResult registry = MigrationRegistry.Create(
            [silentRemoval],
            Versions(product: 2));

        MigrationResult result = Engine(registry).Migrate(
            Representative(1, "\"oldName\":\"RetainMe\""));

        Assert.Equal(MigrationStatus.MigrationFailure, result.Status);
        Assert.Equal("MIGRATION_CHANGE_LEDGER_INCOMPLETE", result.DiagnosticCode);
        Assert.Equal(Representative(1, "\"oldName\":\"RetainMe\""), result.OriginalJson);

        IJsonMigrationStep auditedConversion = Step(
            1,
            2,
            source =>
            {
                JsonObject output = JsonNode.Parse(source.GetRawText())!.AsObject();
                output["schemaVersion"] = 2;
                _ = output.Remove("oldName");
                return MigrationStepResult.Success(
                    output.ToJsonString(),
                    [
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance the controlled schema version."),
                        Change(
                            MigrationChangeKind.Conversion,
                            "/oldName",
                            "/converted",
                            "Explicitly audit a controlled conversion."),
                    ]);
            });
        ManifestProfileMigrationEngine conversionEngine = new(
            MigrationRegistry.Create([auditedConversion], Versions(product: 2)),
            (_, json) => MigrationFinalizationResult.Success(
                MigrationFinalDocument.Representative(json)));

        MigrationResult converted = conversionEngine.Migrate(
            Representative(1, "\"oldName\":\"RetainMe\""));

        Assert.Equal(MigrationStatus.SuccessfullyMigrated, converted.Status);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    public void EmptyContainersCannotBeAddedOrRemovedWithoutLedgerEvidence(
        string emptyContainer)
    {
        IJsonMigrationStep silentRemoval = Step(
            1,
            2,
            source =>
            {
                JsonObject output = JsonNode.Parse(source.GetRawText())!.AsObject();
                output["schemaVersion"] = 2;
                _ = output.Remove("empty");
                return MigrationStepResult.Success(
                    output.ToJsonString(),
                    [
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance the controlled schema version."),
                    ]);
            });
        IJsonMigrationStep silentAddition = Step(
            1,
            2,
            source =>
            {
                JsonObject output = JsonNode.Parse(source.GetRawText())!.AsObject();
                output["schemaVersion"] = 2;
                output["empty"] = JsonNode.Parse(emptyContainer);
                return MigrationStepResult.Success(
                    output.ToJsonString(),
                    [
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance the controlled schema version."),
                    ]);
            });

        MigrationResult removal = Engine(
            MigrationRegistry.Create([silentRemoval], Versions(product: 2))).Migrate(
            Representative(1, $"\"empty\":{emptyContainer}"));
        MigrationResult addition = Engine(
            MigrationRegistry.Create([silentAddition], Versions(product: 2))).Migrate(
            Representative(1, "\"oldName\":\"Asset\""));

        Assert.Equal(MigrationStatus.MigrationFailure, removal.Status);
        Assert.Equal("MIGRATION_CHANGE_LEDGER_INCOMPLETE", removal.DiagnosticCode);
        Assert.Equal(MigrationStatus.MigrationFailure, addition.Status);
        Assert.Equal("MIGRATION_CHANGE_LEDGER_INCOMPLETE", addition.DiagnosticCode);
    }

    [Fact]
    public void InvalidChangeShapeAndWrongFamilyOrVersionOutputAreRejected()
    {
        IJsonMigrationStep[] steps =
        [
            Step(
                1,
                2,
                source => SuccessfulOutput(
                    source,
                    2,
                    [new MigrationChange(MigrationChangeKind.Removal, null, null, string.Empty)])),
            Step(
                1,
                2,
                _ => MigrationStepResult.Success(
                    "{\"schemaVersion\":2,\"root\":\"Publisher\"}",
                    [
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance the controlled schema version."),
                    ])),
            Step(
                1,
                2,
                source => SuccessfulOutput(
                    source,
                    3,
                    [
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance to the wrong version."),
                    ])),
        ];

        Assert.All(
            steps,
            step =>
            {
                MigrationResult result = Engine(
                    MigrationRegistry.Create([step], Versions(product: 2))).Migrate(
                    Representative(1, "\"oldName\":\"Asset\""));
                Assert.Equal(MigrationStatus.MigrationFailure, result.Status);
            });
    }

    [Fact]
    public void FinalSchemaAndSemanticValidationFailureRejectsMigratedOutput()
    {
        ManifestProfileMigrationEngine engine = new(
            RepresentativeRegistry(),
            (_, _) => MigrationFinalizationResult.Failure(
                "MIGRATION_FINAL_SCHEMA_OR_DOMAIN_INVALID"));

        MigrationResult result = engine.Migrate(
            Representative(1, "\"oldName\":\"Asset\""));

        Assert.Equal(MigrationStatus.MigrationFailure, result.Status);
        Assert.Equal("MIGRATION_FINAL_SCHEMA_OR_DOMAIN_INVALID", result.DiagnosticCode);
        Assert.Null(result.FinalDocument);
        Assert.NotEmpty(result.Changes);

        ManifestProfileMigrationEngine fallbackDiagnosticEngine = new(
            RepresentativeRegistry(),
            (_, _) => MigrationFinalizationResult.Failure(null!));
        MigrationResult fallback = fallbackDiagnosticEngine.Migrate(
            Representative(1, "\"oldName\":\"Asset\""));
        Assert.Equal("MIGRATION_FINAL_DOCUMENT_INVALID", fallback.DiagnosticCode);

        ManifestProfileMigrationEngine nullDocumentEngine = new(
            RepresentativeRegistry(),
            (_, _) => MigrationFinalizationResult.Success(null!));
        MigrationResult nullDocument = nullDocumentEngine.Migrate(
            Representative(1, "\"oldName\":\"Asset\""));
        Assert.Equal(MigrationStatus.MigrationFailure, nullDocument.Status);
        Assert.Equal("MIGRATION_FINAL_DOCUMENT_INVALID", nullDocument.DiagnosticCode);
    }

    [Fact]
    public void CurrentProductionDocumentMustPassStrictSchemaAndSemanticValidation()
    {
        JsonObject schemaInvalid = JsonNode.Parse(ManifestFixture("static.json"))!.AsObject();
        schemaInvalid["unexpected"] = true;
        JsonObject semanticInvalid = JsonNode.Parse(ManifestFixture("static.json"))!.AsObject();
        semanticInvalid["product"]!["folderName"] = "CON";

        Assert.Equal(
            MigrationStatus.InvalidDocument,
            ManifestProfileMigration.Inspect(schemaInvalid.ToJsonString()).Status);
        Assert.Equal(
            MigrationStatus.InvalidDocument,
            ManifestProfileMigration.Inspect(semanticInvalid.ToJsonString()).Status);

        ManifestProfileMigrationEngine fallbackDiagnosticEngine = new(
            MigrationRegistry.Create([], Versions()),
            (_, _) => MigrationFinalizationResult.Failure(null!));
        MigrationResult fallback = fallbackDiagnosticEngine.Inspect(
            Representative(1, "\"oldName\":\"Asset\""));
        Assert.Equal("MIGRATION_CURRENT_DOCUMENT_INVALID", fallback.DiagnosticCode);

        ManifestProfileMigrationEngine nullDocumentEngine = new(
            MigrationRegistry.Create([], Versions()),
            (_, _) => MigrationFinalizationResult.Success(null!));
        MigrationResult nullDocument = nullDocumentEngine.Inspect(
            Representative(1, "\"oldName\":\"Asset\""));
        Assert.Equal(MigrationStatus.InvalidDocument, nullDocument.Status);
        Assert.Equal(
            "MIGRATION_CURRENT_DOCUMENT_INVALID",
            nullDocument.DiagnosticCode);
    }

    [Fact]
    public void CallerInputAndOriginalAuditEvidenceRemainUnchanged()
    {
        string input = Representative(1, "\"oldName\":\"Asset\"");
        string copy = string.Concat(input);

        MigrationResult result = Engine(RepresentativeRegistry()).Migrate(input);

        Assert.Equal(copy, input);
        Assert.Equal(copy, result.OriginalJson);
        Assert.NotSame(result.Changes, result.Changes.ToArray());
        _ = Assert.Throws<NotSupportedException>(
            () => ((IList<MigrationChange>)result.Changes).Add(
                Change(
                    MigrationChangeKind.Warning,
                    "/name",
                    null,
                    "Cannot mutate the ledger.")));
    }

    [Fact]
    public void MaximumInputSizeAndDepthBoundariesAreEnforced()
    {
        ManifestProfileMigrationEngine engine = CurrentRepresentativeEngine();
        const string Prefix = "{\"schemaVersion\":1,\"product\":{},\"padding\":\"";
        const string Suffix = "\"}";
        string atLimit = Prefix +
                         new string(
                             'x',
                             1_048_576 - Prefix.Length - Suffix.Length) +
                         Suffix;
        string aboveLimit = atLimit + " ";

        Assert.Equal(MigrationStatus.CurrentDocument, engine.Inspect(atLimit).Status);
        Assert.Equal(MigrationStatus.InvalidDocument, engine.Inspect(aboveLimit).Status);

        string acceptedDepth = NestedDocument(60);
        string rejectedDepth = NestedDocument(70);
        Assert.Equal(MigrationStatus.CurrentDocument, engine.Inspect(acceptedDepth).Status);
        Assert.Equal(MigrationStatus.InvalidDocument, engine.Inspect(rejectedDepth).Status);
    }

    [Fact]
    public void OutputAndDiagnosticsRemainDeterministicAndCultureIndependent()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            ManifestProfileMigrationEngine engine = Engine(RepresentativeRegistry());
            string input = Representative(1, "\"oldName\":\"\u0130stanbul \u4E16\u754C\"");

            MigrationResult first = engine.Migrate(input);
            MigrationResult second = engine.Migrate(input);

            Assert.Equal(first.Status, second.Status);
            Assert.Equal(first.DiagnosticCode, second.DiagnosticCode);
            Assert.Equal(first.FinalDocument!.CanonicalJson, second.FinalDocument!.CanonicalJson);
            Assert.Equal(first.Changes, second.Changes);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void SchemaVersionValueHasValidatedOrdinalValueSemantics()
    {
        SchemaVersion one = Version(1);
        SchemaVersion anotherOne = Version(1);
        SchemaVersion two = Version(2);

        Assert.True(SchemaVersion.Create(1).IsValid);
        Assert.False(SchemaVersion.Create(0).IsValid);
        Assert.False(SchemaVersion.Create(-1).IsValid);
        Assert.False(SchemaVersion.Create((long)int.MaxValue + 1).IsValid);
        Assert.Equal(one, anotherOne);
        Assert.Equal(one.GetHashCode(), anotherOne.GetHashCode());
        Assert.True(one == anotherOne);
        Assert.True(one != two);
        Assert.True(one < two);
        Assert.True(one <= anotherOne);
        Assert.True(two > one);
        Assert.True(two >= one);
        Assert.Equal("1", one.ToString());
        Assert.Equal(1, one.CompareTo(null));
        Assert.False(one.Equals((SchemaVersion?)null));
        Assert.False(one.Equals(two));
        Assert.True(one.Equals((object)anotherOne));
        Assert.False(one.Equals((object)two));
        Assert.False(one.Equals("1"));
    }

    [Fact]
    public void InvalidRegistryInputsFailWithoutThrowing()
    {
        Assert.Equal(
            MigrationRegistryError.InvalidRegistration,
            MigrationRegistry.Create(null, Versions()).Error);
        Assert.Equal(
            MigrationRegistryError.InvalidRegistration,
            MigrationRegistry.Create([null], Versions()).Error);
        Assert.Equal(
            MigrationRegistryError.InvalidRegistration,
            MigrationRegistry.Create([], null).Error);

        var missingFamily = new Dictionary<MigrationDocumentFamily, SchemaVersion>
        {
            [MigrationDocumentFamily.ProductManifest] = Version(1),
        };
        Assert.Equal(
            MigrationRegistryError.InvalidRegistration,
            MigrationRegistry.Create([], missingFamily).Error);

        IJsonMigrationStep[] invalidSteps =
        [
            new InvalidMigrationStep(
                (MigrationDocumentFamily)999,
                Version(1),
                Version(2)),
            new InvalidMigrationStep(
                MigrationDocumentFamily.ProductManifest,
                null!,
                Version(2)),
            new InvalidMigrationStep(
                MigrationDocumentFamily.ProductManifest,
                Version(1),
                null!),
        ];
        Assert.All(
            invalidSteps,
            step => Assert.Equal(
                MigrationRegistryError.InvalidRegistration,
                MigrationRegistry.Create([step], Versions(product: 2)).Error));
    }

    [Fact]
    public void UntrustedDiagnosticTextFallsBackToStableNonSensitiveCodes()
    {
        IJsonMigrationStep leakingStep = Step(
            1,
            2,
            _ => MigrationStepResult.Failure(
                "{\"secret\":\"SensitiveValue\"}"));
        MigrationResult stepResult = Engine(
            MigrationRegistry.Create([leakingStep], Versions(product: 2))).Migrate(
            Representative(1, "\"oldName\":\"SensitiveValue\""));

        ManifestProfileMigrationEngine leakingFinalizer = new(
            RepresentativeRegistry(),
            (_, _) => MigrationFinalizationResult.Failure(
                "{\"secret\":\"SensitiveValue\"}"));
        MigrationResult finalizerResult = leakingFinalizer.Migrate(
            Representative(1, "\"oldName\":\"SensitiveValue\""));

        Assert.Equal("MIGRATION_STEP_FAILED", stepResult.DiagnosticCode);
        Assert.Equal(
            "MIGRATION_FINAL_DOCUMENT_INVALID",
            finalizerResult.DiagnosticCode);
        Assert.DoesNotContain(
            "SensitiveValue",
            stepResult.DiagnosticCode,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SensitiveValue",
            finalizerResult.DiagnosticCode,
            StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidRegistryOutcomesRemainStructuredForEveryRegistryFailureKind()
    {
        var missingCurrentVersion = new Dictionary<MigrationDocumentFamily, SchemaVersion>
        {
            [MigrationDocumentFamily.ProductManifest] = Version(1),
        };
        MigrationResult missingCurrent = Engine(
            MigrationRegistry.Create([], missingCurrentVersion)).Inspect(PublisherFixture());
        Assert.Equal(MigrationStatus.MigrationFailure, missingCurrent.Status);
        Assert.Equal("MIGRATION_REGISTRY_INVALID", missingCurrent.DiagnosticCode);

        MigrationRegistryResult gap = MigrationRegistry.Create(
            [
                StepOneToTwo(),
                Step(3, 4, source => SuccessfulOutput(source, 4, [])),
            ],
            Versions(product: 4));
        MigrationResult gapResult = Engine(gap).Inspect(
            Representative(1, "\"oldName\":\"Asset\""));
        Assert.Equal(MigrationStatus.MissingMigrationStep, gapResult.Status);
        Assert.Equal("MIGRATION_REGISTRY_GAP", gapResult.DiagnosticCode);

        MigrationRegistryResult duplicate = MigrationRegistry.Create(
            [StepOneToTwo(), StepOneToTwo()],
            Versions(product: 2));
        MigrationResult duplicateResult = Engine(duplicate).Inspect(
            Representative(1, "\"oldName\":\"Asset\""));
        Assert.Equal(MigrationStatus.MigrationFailure, duplicateResult.Status);
        Assert.Equal("MIGRATION_REGISTRY_DUPLICATEREGISTRATION", duplicateResult.DiagnosticCode);
    }

    [Fact]
    public void RegistrySnapshotsVersionsAndRejectsInvalidFamilyOrNullVersion()
    {
        Dictionary<MigrationDocumentFamily, SchemaVersion> versions = Versions();
        MigrationRegistryResult valid = MigrationRegistry.Create([], versions);
        versions[MigrationDocumentFamily.ProductManifest] = Version(2);

        Assert.Equal(
            1,
            valid.Registry!.CurrentVersions[MigrationDocumentFamily.ProductManifest].Value);

        var invalidFamily = new Dictionary<MigrationDocumentFamily, SchemaVersion>
        {
            [(MigrationDocumentFamily)999] = Version(1),
            [MigrationDocumentFamily.PublisherProfile] = Version(1),
            [MigrationDocumentFamily.MarketplaceProfile] = Version(1),
        };
        Assert.Equal(
            MigrationRegistryError.InvalidRegistration,
            MigrationRegistry.Create([], invalidFamily).Error);

        var nullVersion = new Dictionary<MigrationDocumentFamily, SchemaVersion>
        {
            [MigrationDocumentFamily.ProductManifest] = null!,
            [MigrationDocumentFamily.PublisherProfile] = Version(1),
            [MigrationDocumentFamily.MarketplaceProfile] = Version(1),
        };
        Assert.Equal(
            MigrationRegistryError.InvalidRegistration,
            MigrationRegistry.Create([], nullVersion).Error);
    }

    [Fact]
    public void EveryChangeShapeAndPointerInvariantIsEnforced()
    {
        MigrationChange[] invalidChanges =
        [
            new(MigrationChangeKind.Addition, "/source", "/target", "Invalid addition."),
            new(MigrationChangeKind.Addition, null, null, "Invalid addition."),
            new(MigrationChangeKind.Removal, null, null, "Invalid removal."),
            new(MigrationChangeKind.Removal, "/source", "/target", "Invalid removal."),
            new(MigrationChangeKind.Rename, null, "/target", "Invalid rename."),
            new(MigrationChangeKind.Rename, "/source", null, "Invalid rename."),
            new(MigrationChangeKind.Rename, "/same", "/same", "Invalid rename."),
            new(MigrationChangeKind.Conversion, null, "/target", "Invalid conversion."),
            new(MigrationChangeKind.Conversion, "/source", null, "Invalid conversion."),
            new(MigrationChangeKind.Warning, null, null, "Invalid warning."),
            new(MigrationChangeKind.ReviewRequired, null, null, "Invalid review."),
            new((MigrationChangeKind)999, "/source", "/target", "Invalid kind."),
            new(MigrationChangeKind.Warning, "not-a-pointer", null, "Invalid pointer."),
            new(MigrationChangeKind.Warning, "/control\u0001", null, "Invalid pointer."),
            new(MigrationChangeKind.Warning, "/source", null, string.Empty),
        ];

        foreach (MigrationChange invalidChange in invalidChanges)
        {
            IJsonMigrationStep step = Step(
                1,
                2,
                source => SuccessfulOutput(
                    source,
                    2,
                    [
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance the controlled schema version."),
                        invalidChange,
                    ]));
            MigrationResult result = Engine(
                MigrationRegistry.Create([step], Versions(product: 2))).Migrate(
                Representative(1, "\"oldName\":\"Asset\""));

            Assert.Equal(MigrationStatus.MigrationFailure, result.Status);
        }
    }

    [Fact]
    public void LedgerRequiresConversionsAndAdditionsAndHandlesArraysAndEscapedPointers()
    {
        IJsonMigrationStep missingConversion = Step(
            1,
            2,
            source => SuccessfulOutput(
                source,
                2,
                [
                    Change(
                        MigrationChangeKind.Warning,
                        "/schemaVersion",
                        null,
                        "A warning cannot authorize conversion."),
                ]));
        Assert.Equal(
            MigrationStatus.MigrationFailure,
            Engine(MigrationRegistry.Create([missingConversion], Versions(product: 2)))
                .Migrate(Representative(1, "\"oldName\":\"Asset\""))
                .Status);

        IJsonMigrationStep missingAddition = Step(
            1,
            2,
            source =>
            {
                JsonObject output = JsonNode.Parse(source.GetRawText())!.AsObject();
                output["schemaVersion"] = 2;
                output["added"] = true;
                return MigrationStepResult.Success(
                    output.ToJsonString(),
                    [
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance the controlled schema version."),
                    ]);
            });
        Assert.Equal(
            MigrationStatus.MigrationFailure,
            Engine(MigrationRegistry.Create([missingAddition], Versions(product: 2)))
                .Migrate(Representative(1, "\"oldName\":\"Asset\""))
                .Status);

        IJsonMigrationStep wrongRemovalKind = Step(
            1,
            2,
            source =>
            {
                JsonObject output = JsonNode.Parse(source.GetRawText())!.AsObject();
                output["schemaVersion"] = 2;
                _ = output.Remove("oldName");
                return MigrationStepResult.Success(
                    output.ToJsonString(),
                    [
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance the controlled schema version."),
                        Change(
                            MigrationChangeKind.Warning,
                            "/oldName",
                            null,
                            "A warning cannot authorize removal."),
                    ]);
            });
        Assert.Equal(
            MigrationStatus.MigrationFailure,
            Engine(MigrationRegistry.Create([wrongRemovalKind], Versions(product: 2)))
                .Migrate(Representative(1, "\"oldName\":\"Asset\""))
                .Status);

        IJsonMigrationStep wrongAdditionKind = Step(
            1,
            2,
            source =>
            {
                JsonObject output = JsonNode.Parse(source.GetRawText())!.AsObject();
                output["schemaVersion"] = 2;
                output["added"] = true;
                return MigrationStepResult.Success(
                    output.ToJsonString(),
                    [
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance the controlled schema version."),
                        Change(
                            MigrationChangeKind.Warning,
                            null,
                            "/added",
                            "A warning cannot authorize an addition."),
                    ]);
            });
        Assert.Equal(
            MigrationStatus.MigrationFailure,
            Engine(MigrationRegistry.Create([wrongAdditionKind], Versions(product: 2)))
                .Migrate(Representative(1, "\"oldName\":\"Asset\""))
                .Status);

        const string ComplexInput =
            "{\"schemaVersion\":1,\"product\":{\"items\":[1,null]},\"old/name~\":\"remove\"}";
        IJsonMigrationStep complete = Step(
            1,
            2,
            source =>
            {
                JsonObject output = JsonNode.Parse(source.GetRawText())!.AsObject();
                output["schemaVersion"] = 2;
                _ = output.Remove("old/name~");
                output["new/name~"] = true;
                return MigrationStepResult.Success(
                    output.ToJsonString(),
                    [
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance the controlled schema version."),
                        Change(
                            MigrationChangeKind.Removal,
                            "/old~1name~0",
                            null,
                            "Remove the explicitly audited representative value."),
                        Change(
                            MigrationChangeKind.Addition,
                            null,
                            "/new~1name~0",
                            "Add the explicitly audited representative value."),
                    ]);
            });
        ManifestProfileMigrationEngine engine = new(
            MigrationRegistry.Create([complete], Versions(product: 2)),
            (_, json) => MigrationFinalizationResult.Success(
                MigrationFinalDocument.Representative(json)));

        MigrationResult result = engine.Migrate(ComplexInput);

        Assert.Equal(MigrationStatus.SuccessfullyMigrated, result.Status);
        Assert.Contains(result.Changes, change => change.Kind == MigrationChangeKind.Removal);
        Assert.Contains(result.Changes, change => change.Kind == MigrationChangeKind.Addition);
    }

    [Fact]
    public void InvalidCurrentPublisherAndMarketplaceDocumentsFailFinalValidation()
    {
        JsonObject publisher = JsonNode.Parse(PublisherFixture())!.AsObject();
        publisher["unexpected"] = true;
        JsonObject marketplace = JsonNode.Parse(MarketplaceFixture())!.AsObject();
        marketplace["unexpected"] = true;

        Assert.Equal(
            MigrationStatus.InvalidDocument,
            ManifestProfileMigration.Inspect(publisher.ToJsonString()).Status);
        Assert.Equal(
            MigrationStatus.InvalidDocument,
            ManifestProfileMigration.Inspect(marketplace.ToJsonString()).Status);
    }

    private static ManifestProfileMigrationEngine Engine(MigrationRegistryResult registry) =>
        new(registry, RepresentativeFinalizer);

    private static ManifestProfileMigrationEngine CurrentRepresentativeEngine() =>
        Engine(MigrationRegistry.Create([], Versions()));

    private static MigrationFinalizationResult RepresentativeFinalizer(
        MigrationDocumentFamily family,
        string json)
    {
        if (family != MigrationDocumentFamily.ProductManifest)
        {
            return MigrationFinalizationResult.Failure("REPRESENTATIVE_FAMILY_INVALID");
        }

        JsonObject root = JsonNode.Parse(json)!.AsObject();
        int version = root["schemaVersion"]!.GetValue<int>();
        if (version == 1)
        {
            return MigrationFinalizationResult.Success(
                MigrationFinalDocument.Representative(json));
        }

        if (version != 3 ||
            root["name"] is null ||
            root["enabled"]?.GetValue<bool>() != true)
        {
            return MigrationFinalizationResult.Failure(
                "MIGRATION_FINAL_SCHEMA_OR_DOMAIN_INVALID");
        }

        string canonical = new JsonObject
        {
            ["schemaVersion"] = 3,
            ["product"] = new JsonObject(),
            ["name"] = root["name"]!.GetValue<string>(),
            ["enabled"] = true,
        }.ToJsonString();
        return MigrationFinalizationResult.Success(
            MigrationFinalDocument.Representative(canonical));
    }

    private static MigrationRegistryResult RepresentativeRegistry() =>
        MigrationRegistry.Create(
            [StepOneToTwo(), StepTwoToThree()],
            Versions(product: 3));

    private static DelegateJsonMigrationStep StepOneToTwo() =>
        Step(
            1,
            2,
            source =>
            {
                JsonObject output = JsonNode.Parse(source.GetRawText())!.AsObject();
                string value = output["oldName"]!.GetValue<string>();
                output["schemaVersion"] = 2;
                Assert.True(output.Remove("oldName"));
                output["name"] = value;
                return MigrationStepResult.Success(
                    output.ToJsonString(),
                    [
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance the controlled schema version from 1 to 2."),
                        Change(
                            MigrationChangeKind.Rename,
                            "/oldName",
                            "/name",
                            "Rename the representative property without changing its value."),
                    ]);
            });

    private static DelegateJsonMigrationStep StepTwoToThree() =>
        Step(
            2,
            3,
            source =>
            {
                JsonObject output = JsonNode.Parse(source.GetRawText())!.AsObject();
                output["schemaVersion"] = 3;
                output["enabled"] = true;
                return MigrationStepResult.Success(
                    output.ToJsonString(),
                    [
                        Change(
                            MigrationChangeKind.Conversion,
                            "/schemaVersion",
                            "/schemaVersion",
                            "Advance the controlled schema version from 2 to 3."),
                        Change(
                            MigrationChangeKind.Default,
                            null,
                            "/enabled",
                            "Add the controlled representative default."),
                        Change(
                            MigrationChangeKind.Warning,
                            "/enabled",
                            null,
                            "The caller should verify the representative default."),
                        Change(
                            MigrationChangeKind.ReviewRequired,
                            "/enabled",
                            null,
                            "Review is explicitly required for the representative default."),
                    ]);
            });

    private static DelegateJsonMigrationStep Step(
        int from,
        int to,
        Func<JsonElement, MigrationStepResult> migration) =>
        new(
            MigrationDocumentFamily.ProductManifest,
            Version(from),
            Version(to),
            migration);

    private static MigrationStepResult SuccessfulOutput(
        JsonElement source,
        int version,
        IEnumerable<MigrationChange> changes)
    {
        JsonObject output = JsonNode.Parse(source.GetRawText())!.AsObject();
        output["schemaVersion"] = version;
        return MigrationStepResult.Success(output.ToJsonString(), changes);
    }

    private static MigrationChange Change(
        MigrationChangeKind kind,
        string? source,
        string? target,
        string description) =>
        new(kind, source, target, description);

    private static SchemaVersion Version(int value) => SchemaVersion.Create(value).Value!;

    private static Dictionary<MigrationDocumentFamily, SchemaVersion> Versions(
        int product = 1,
        int publisher = 1,
        int marketplace = 1) =>
        new()
        {
            [MigrationDocumentFamily.ProductManifest] = Version(product),
            [MigrationDocumentFamily.PublisherProfile] = Version(publisher),
            [MigrationDocumentFamily.MarketplaceProfile] = Version(marketplace),
        };

    private static void AssertRegistryError(
        MigrationRegistryError expected,
        IEnumerable<IJsonMigrationStep> steps,
        int productCurrent)
    {
        MigrationRegistryResult result = MigrationRegistry.Create(
            steps,
            Versions(product: productCurrent));
        Assert.False(result.IsValid);
        Assert.Null(result.Registry);
        Assert.Equal(expected, result.Error);
    }

    private static void AssertTypedFamily(MigrationResult result)
    {
        Assert.NotNull(result.FinalDocument);
        Assert.Equal(
            result.Family == MigrationDocumentFamily.ProductManifest,
            result.FinalDocument!.ProductManifest is not null);
        Assert.Equal(
            result.Family == MigrationDocumentFamily.PublisherProfile,
            result.FinalDocument.PublisherProfile is not null);
        Assert.Equal(
            result.Family == MigrationDocumentFamily.MarketplaceProfile,
            result.FinalDocument.MarketplaceProfile is not null);
    }

    private static string Representative(int version, string additionalProperty) =>
        $"{{\"schemaVersion\":{version},\"product\":{{}},{additionalProperty}}}";

    private static string NestedDocument(int nesting) =>
        "{\"schemaVersion\":1,\"product\":{\"nested\":" +
        new string('[', nesting) +
        "0" +
        new string(']', nesting) +
        "}}";

    private static string ManifestFixture(string name) =>
        Read(
            "fixtures",
            "manifests",
            "valid",
            name);

    private static string PublisherFixture() =>
        Read("profiles", "publishers", "AvivPeretsFBX.example.json");

    private static string MarketplaceFixture() =>
        Read("profiles", "marketplaces", "fab.identity.example.json");

    private static string MigrationFixture(string name) =>
        Read("fixtures", "migrations", "internal", name);

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([AppContext.BaseDirectory, .. parts])).Trim();

    private sealed class InvalidMigrationStep(
        MigrationDocumentFamily family,
        SchemaVersion fromVersion,
        SchemaVersion toVersion) : IJsonMigrationStep
    {
        public MigrationDocumentFamily Family { get; } = family;

        public SchemaVersion FromVersion { get; } = fromVersion;

        public SchemaVersion ToVersion { get; } = toVersion;

        public MigrationStepResult Migrate(JsonElement source) =>
            MigrationStepResult.Failure("INVALID_REGISTRATION_MUST_NOT_EXECUTE");
    }
}
