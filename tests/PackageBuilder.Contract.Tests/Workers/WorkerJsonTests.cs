using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Workers;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contract.Tests.Workers;

[Trait("Task", "PB-0112")]
public sealed class WorkerJsonTests
{
    public static TheoryData<string> EventFixtures =>
        [
            "progress-0.json",
            "progress-intermediate.json",
            "progress-100.json",
            "progress-indeterminate.json",
            "finding-event.json",
            "metric-event.json",
        ];

    public static TheoryData<string> ResultFixtures =>
        [
            "result-success.json",
            "result-failure.json",
            "result-cancelled.json",
        ];

    [Fact]
    public void SchemasUseDraft202012AndExactVersionOneIdentifiers()
    {
        Assert.Equal(1, WorkerRequestJson.CurrentProtocolVersion);
        Assert.Equal(1, WorkerProgressEventJson.CurrentProtocolVersion);
        Assert.Equal(1, WorkerResultJson.CurrentProtocolVersion);
        Assert.Equal(
            "https://schemas.packagebuilder.dev/worker-request/v1",
            WorkerRequestJson.SchemaIdentifier);
        Assert.Equal(
            "https://schemas.packagebuilder.dev/worker-progress-event/v1",
            WorkerProgressEventJson.SchemaIdentifier);
        Assert.Equal(
            "https://schemas.packagebuilder.dev/worker-result/v1",
            WorkerResultJson.SchemaIdentifier);
        Assert.Equal(
            "https://json-schema.org/draft/2020-12/schema",
            WorkerRequestJson.SchemaDraft);
        Assert.Equal(WorkerRequestJson.SchemaDraft, WorkerProgressEventJson.SchemaDraft);
        Assert.Equal(WorkerRequestJson.SchemaDraft, WorkerResultJson.SchemaDraft);
        Assert.True(WorkerRequestJson.ValidateSchemaDefinition().IsValid);
        Assert.True(WorkerProgressEventJson.ValidateSchemaDefinition().IsValid);
        Assert.True(WorkerResultJson.ValidateSchemaDefinition().IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"$id\":\"https://schemas.packagebuilder.dev/worker-request/v1\"}")]
    [InlineData("{\"$schema\":\"https://json-schema.org/draft/2020-12/schema\"}")]
    [InlineData("{\"$id\":\"wrong\",\"$schema\":\"https://json-schema.org/draft/2020-12/schema\"}")]
    [InlineData("{\"$id\":\"https://schemas.packagebuilder.dev/worker-request/v1\",\"$schema\":\"wrong\"}")]
    [InlineData("{")]
    public void InvalidSchemaDefinitionsFailClosed(string? schema)
    {
        WorkerJsonSchemaValidationResult result =
            WorkerRequestJson.ValidateSchemaDefinition(schema);
        Assert.False(result.IsValid);
        Assert.NotNull(result.Details);
    }

    [Fact]
    public void RequestGoldenRoundTripsExactlyAndUsesExistingJobAndTargetIdentity()
    {
        string golden = ReadValid("request.json");
        WorkerJsonDeserializationResult<WorkerRequest> parsed =
            WorkerRequestJson.Deserialize(golden);

        Assert.True(parsed.IsSuccessful, parsed.Details);
        Assert.Equal("Job-01", parsed.Value!.JobId.Value);
        Assert.Equal("build-unity-target", parsed.Value.Operation);
        Assert.Equal("unity", parsed.Value.Target!.CanonicalIdentifier);
        Assert.Equal(golden, SerializeRequest(parsed.Value));
        Assert.Equal(golden, SerializeRequest(parsed.Value));
        Assert.True(WorkerRequestJson.ValidateJsonAgainstSchema(golden).IsValid);
    }

    [Fact]
    public void OptionalRequestValuesAreOmittedAndEveryTargetParses()
    {
        JsonObject node = JsonNode.Parse(ReadValid("request.json"))!.AsObject();
        _ = node.Remove("engineVersion");
        _ = node.Remove("target");
        WorkerRequest noOptionals = WorkerRequestJson.Deserialize(node.ToJsonString()).Value!;
        string serialized = SerializeRequest(noOptionals);
        Assert.DoesNotContain("\"engineVersion\":", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"target\":", serialized, StringComparison.Ordinal);

        foreach (string target in new[] { "portable", "unity", "unreal" })
        {
            node["target"] = target;
            Assert.Equal(
                target,
                WorkerRequestJson.Deserialize(node.ToJsonString())
                    .Value!.Target!.CanonicalIdentifier);
        }
    }

    [Theory]
    [MemberData(nameof(EventFixtures))]
    public void EveryEventGoldenIsOneCompactDeterministicRoundTrip(string fixture)
    {
        string golden = ReadValid(fixture);
        WorkerJsonDeserializationResult<WorkerProgressEvent> parsed =
            WorkerProgressEventJson.Deserialize(golden);

        Assert.True(parsed.IsSuccessful, parsed.Details);
        string first = SerializeEvent(parsed.Value!);
        string second = SerializeEvent(parsed.Value!);
        Assert.Equal(golden, first);
        Assert.Equal(first, second);
        Assert.DoesNotContain('\r', first);
        Assert.DoesNotContain('\n', first);
        Assert.True(WorkerProgressEventJson.ValidateJsonAgainstSchema(first).IsValid);
    }

    [Theory]
    [InlineData("milliseconds")]
    [InlineData("bytes")]
    [InlineData("count")]
    [InlineData("percent")]
    public void EveryMetricUnitRoundTrips(string unit)
    {
        JsonObject node = JsonNode.Parse(ReadValid("metric-event.json"))!.AsObject();
        node["metric"]!["unit"] = unit;
        WorkerProgressEvent parsed =
            WorkerProgressEventJson.Deserialize(node.ToJsonString()).Value!;
        Assert.Contains($"\"unit\":\"{unit}\"", SerializeEvent(parsed), StringComparison.Ordinal);
    }

    [Fact]
    public void ProgressSupportsZeroIntermediateHundredAndIndeterminate()
    {
        Assert.Equal(0, ParseEvent("progress-0.json").Percent);
        Assert.Equal(35.5, ParseEvent("progress-intermediate.json").Percent);
        Assert.Equal(100, ParseEvent("progress-100.json").Percent);
        Assert.Null(ParseEvent("progress-indeterminate.json").Percent);
    }

    [Fact]
    public void FindingEventPreservesPb0109FindingContractExactly()
    {
        WorkerProgressEvent value = ParseEvent("finding-event.json");

        Assert.Equal(WorkerEventKind.Finding, value.EventKind);
        Assert.Equal("UNITY_TEXTURE_ALPHA_UNUSED", value.Finding!.Code.Value);
        Assert.Equal(FindingSeverity.Warning, value.Finding.Severity);
        Assert.False(value.Finding.BlocksRelease);
    }

    [Theory]
    [MemberData(nameof(ResultFixtures))]
    public void EveryResultGoldenRoundTripsExactly(string fixture)
    {
        string golden = ReadValid(fixture);
        WorkerJsonDeserializationResult<WorkerResult> parsed =
            WorkerResultJson.Deserialize(golden);

        Assert.True(parsed.IsSuccessful, parsed.Details);
        Assert.Equal(golden, SerializeResult(parsed.Value!));
        Assert.Equal(golden, SerializeResult(parsed.Value!));
        Assert.True(WorkerResultJson.ValidateJsonAgainstSchema(golden).IsValid);
    }

    [Fact]
    public void SuccessArtifactReusesTypedIdentitiesAndOptionalHashAndBytes()
    {
        WorkerResult result = ParseResult("result-success.json");
        WorkerArtifact artifact = Assert.Single(result.Artifacts);

        Assert.Equal(WorkerResultStatus.Success, result.Status);
        Assert.Equal("Artifact-01", artifact.ArtifactId.Value);
        Assert.Equal(result.JobId, artifact.JobId);
        Assert.Equal("unity-package", artifact.Role.CanonicalIdentifier);
        Assert.Equal(
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            artifact.Sha256);
        Assert.Equal(1024, artifact.ByteCount);
        Assert.Equal("Artifact-01", Assert.Single(result.Findings).RelatedArtifactId!.Value);
    }

    [Fact]
    public void FailureAndCancellationAreDistinctFirstClassResults()
    {
        WorkerResult failed = ParseResult("result-failure.json");
        WorkerResult cancelled = ParseResult("result-cancelled.json");

        Assert.Equal(WorkerResultStatus.Failure, failed.Status);
        Assert.Null(failed.Cancellation);
        Assert.Equal(WorkerRetrySafety.Safe, failed.RetrySafety);
        Assert.Equal(WorkerResultStatus.Cancelled, cancelled.Status);
        Assert.Equal(WorkerRetrySafety.RequiresCleanup, cancelled.RetrySafety);
        Assert.Equal(WorkerCancellationOutcome.Partial, cancelled.Cancellation!.Outcome);
        Assert.False(cancelled.OutputsPromoted);
    }

    [Fact]
    public void BothCancellationOutcomeTokensRoundTripWithOptionalDetails()
    {
        JsonObject node = JsonNode.Parse(ReadValid("result-cancelled.json"))!.AsObject();
        node["retrySafety"] = "safe";
        node["cancellation"]!["outcome"] = "acknowledged";
        _ = node["cancellation"]!.AsObject().Remove("details");
        WorkerResult parsed = WorkerResultJson.Deserialize(node.ToJsonString()).Value!;
        Assert.Equal(WorkerCancellationOutcome.Acknowledged, parsed.Cancellation!.Outcome);
        Assert.Null(parsed.Cancellation.Details);
        Assert.DoesNotContain("details", SerializeResult(parsed), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("request-unknown-version.json", WorkerJsonError.SchemaViolation)]
    public void RetainedInvalidRequestFixturesFail(string fixture, WorkerJsonError error) =>
        Assert.Equal(error, WorkerRequestJson.Deserialize(ReadInvalid(fixture)).Error);

    [Theory]
    [InlineData("event-unknown-property.json", WorkerJsonError.SchemaViolation)]
    [InlineData("event-duplicate-property.json", WorkerJsonError.DuplicateProperty)]
    public void RetainedInvalidEventFixturesFail(string fixture, WorkerJsonError error) =>
        Assert.Equal(error, WorkerProgressEventJson.Deserialize(ReadInvalid(fixture)).Error);

    [Theory]
    [InlineData("result-invalid-hash.json", WorkerJsonError.SchemaViolation)]
    [InlineData("result-wrong-ownership.json", WorkerJsonError.SemanticViolation)]
    [InlineData("result-blocking-success.json", WorkerJsonError.SemanticViolation)]
    [InlineData("result-cancelled-promoted.json", WorkerJsonError.SchemaViolation)]
    public void RetainedInvalidResultFixturesFail(string fixture, WorkerJsonError error) =>
        Assert.Equal(error, WorkerResultJson.Deserialize(ReadInvalid(fixture)).Error);

    [Theory]
    [InlineData(null, WorkerJsonError.NullJson)]
    [InlineData("", WorkerJsonError.EmptyJson)]
    [InlineData("{", WorkerJsonError.MalformedJson)]
    [InlineData("[]", WorkerJsonError.RootMustBeObject)]
    public void StructuralFailuresAreNonThrowing(string? json, WorkerJsonError expected)
    {
        Assert.Equal(expected, WorkerRequestJson.Deserialize(json).Error);
        Assert.Equal(expected, WorkerProgressEventJson.Deserialize(json).Error);
        Assert.Equal(expected, WorkerResultJson.Deserialize(json).Error);
    }

    [Fact]
    public void NullModelsAndSchemaValidationFailuresReturnStructuredResults()
    {
        Assert.Equal(WorkerJsonError.NullValue, WorkerRequestJson.Serialize(null).Error);
        Assert.Equal(WorkerJsonError.NullValue, WorkerProgressEventJson.Serialize(null).Error);
        Assert.Equal(WorkerJsonError.NullValue, WorkerResultJson.Serialize(null).Error);
        Assert.False(WorkerRequestJson.ValidateJsonAgainstSchema(null).IsValid);
        Assert.False(WorkerProgressEventJson.ValidateJsonAgainstSchema("{}").IsValid);
        Assert.False(WorkerResultJson.ValidateJsonAgainstSchema("[]").IsValid);
    }

    [Fact]
    public void UnknownVersionsFailClosedForEveryStandaloneContract()
    {
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerRequestJson.Deserialize(
                ReadValid("request.json").Replace(
                    "\"protocolVersion\":1",
                    "\"protocolVersion\":2",
                    StringComparison.Ordinal)).Error);
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerProgressEventJson.Deserialize(
                ReadValid("progress-0.json").Replace(
                    "\"protocolVersion\":1",
                    "\"protocolVersion\":2",
                    StringComparison.Ordinal)).Error);
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerResultJson.Deserialize(
                ReadValid("result-success.json").Replace(
                    "\"protocolVersion\":1",
                    "\"protocolVersion\":2",
                    StringComparison.Ordinal)).Error);
    }

    [Theory]
    [InlineData("/protocolVersion")]
    [InlineData("/jobId")]
    [InlineData("/operation")]
    [InlineData("/productManifestReference")]
    [InlineData("/inputDirectoryReference")]
    [InlineData("/outputDirectoryReference")]
    [InlineData("/resultFileReference")]
    public void RequestRejectsEveryMissingRequiredProperty(string jsonPath)
    {
        JsonObject node = JsonNode.Parse(ReadValid("request.json"))!.AsObject();
        Remove(node, jsonPath);
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerRequestJson.Deserialize(node.ToJsonString()).Error);
    }

    [Theory]
    [InlineData("/protocolVersion")]
    [InlineData("/eventKind")]
    [InlineData("/jobId")]
    [InlineData("/finding")]
    [InlineData("/finding/code")]
    [InlineData("/finding/severity")]
    [InlineData("/finding/explanation")]
    [InlineData("/finding/source")]
    [InlineData("/finding/blocksRelease")]
    public void FindingEventRejectsEveryMissingRequiredProperty(string jsonPath)
    {
        JsonObject node = JsonNode.Parse(ReadValid("finding-event.json"))!.AsObject();
        Remove(node, jsonPath);
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerProgressEventJson.Deserialize(node.ToJsonString()).Error);
    }

    [Theory]
    [InlineData("/protocolVersion")]
    [InlineData("/jobId")]
    [InlineData("/status")]
    [InlineData("/workerVersion")]
    [InlineData("/outputsPromoted")]
    [InlineData("/artifacts")]
    [InlineData("/findings")]
    [InlineData("/metrics")]
    [InlineData("/logReferences")]
    [InlineData("/retrySafety")]
    [InlineData("/artifacts/0/artifactId")]
    [InlineData("/artifacts/0/jobId")]
    [InlineData("/artifacts/0/role")]
    [InlineData("/artifacts/0/logicalReference")]
    [InlineData("/metrics/0/metricId")]
    [InlineData("/metrics/0/value")]
    [InlineData("/metrics/0/unit")]
    public void ResultRejectsEveryMissingRequiredProperty(string jsonPath)
    {
        JsonObject node = JsonNode.Parse(ReadValid("result-success.json"))!.AsObject();
        Remove(node, jsonPath);
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerResultJson.Deserialize(node.ToJsonString()).Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(1)]
    [InlineData(new int[] { 1 })]
    public void RequiredStringsRejectForbiddenNullsAndWrongTypes(object? replacement)
    {
        JsonObject request = JsonNode.Parse(ReadValid("request.json"))!.AsObject();
        request["operation"] = JsonValue.Create(replacement);
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerRequestJson.Deserialize(request.ToJsonString()).Error);
    }

    [Theory]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef")]
    [InlineData("g123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void HashRejectsWrongCasingLengthAndCharacters(string hash)
    {
        JsonObject result = JsonNode.Parse(ReadValid("result-success.json"))!.AsObject();
        result["artifacts"]![0]!["sha256"] = hash;
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerResultJson.Deserialize(result.ToJsonString()).Error);
    }

    [Fact]
    public void ArtifactWithoutHashAndByteCountIsValid()
    {
        JsonObject result = JsonNode.Parse(ReadValid("result-success.json"))!.AsObject();
        JsonObject artifact = result["artifacts"]![0]!.AsObject();
        _ = artifact.Remove("target");
        _ = artifact.Remove("sha256");
        _ = artifact.Remove("byteCount");

        WorkerJsonDeserializationResult<WorkerResult> parsed =
            WorkerResultJson.Deserialize(result.ToJsonString());
        Assert.True(parsed.IsSuccessful, parsed.Details);
        Assert.Null(parsed.Value!.Artifacts[0].Sha256);
        Assert.Null(parsed.Value.Artifacts[0].ByteCount);
        string serialized = SerializeResult(parsed.Value);
        Assert.DoesNotContain("\"target\":", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"sha256\":", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"byteCount\":", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateArtifactFindingMetricAndLogIdentitiesAreRejected()
    {
        JsonObject result = JsonNode.Parse(ReadValid("result-success.json"))!.AsObject();
        result["artifacts"]!.AsArray().Add(result["artifacts"]![0]!.DeepClone());
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Deserialize(result.ToJsonString()).Error);

        result = JsonNode.Parse(ReadValid("result-success.json"))!.AsObject();
        result["findings"]!.AsArray().Add(result["findings"]![0]!.DeepClone());
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Deserialize(result.ToJsonString()).Error);

        result = JsonNode.Parse(ReadValid("result-success.json"))!.AsObject();
        result["metrics"]!.AsArray().Add(result["metrics"]![0]!.DeepClone());
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Deserialize(result.ToJsonString()).Error);

        result = JsonNode.Parse(ReadValid("result-success.json"))!.AsObject();
        result["logReferences"]!.AsArray().Add("logs/unity.log");
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerResultJson.Deserialize(result.ToJsonString()).Error);
    }

    [Theory]
    [InlineData("safe")]
    [InlineData("unsafe")]
    [InlineData("requires-cleanup")]
    public void FailedResultSupportsEveryRetrySafetyState(string retrySafety)
    {
        JsonObject result = JsonNode.Parse(ReadValid("result-failure.json"))!.AsObject();
        result["retrySafety"] = retrySafety;
        Assert.True(WorkerResultJson.Deserialize(result.ToJsonString()).IsSuccessful);
    }

    [Fact]
    public void RetrySafetyStatusAndCancellationContradictionsFail()
    {
        JsonObject success = JsonNode.Parse(ReadValid("result-success.json"))!.AsObject();
        success["retrySafety"] = "safe";
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerResultJson.Deserialize(success.ToJsonString()).Error);

        JsonObject cancelled = JsonNode.Parse(ReadValid("result-cancelled.json"))!.AsObject();
        cancelled["retrySafety"] = "unsafe";
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerResultJson.Deserialize(cancelled.ToJsonString()).Error);

        _ = cancelled.Remove("cancellation");
        cancelled["retrySafety"] = "safe";
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerResultJson.Deserialize(cancelled.ToJsonString()).Error);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void InvalidJsonNumericTokensAreRejected(string token)
    {
        string json = ReadValid("metric-event.json").Replace(
            "1250.5",
            token,
            StringComparison.Ordinal);
        Assert.Equal(
            WorkerJsonError.MalformedJson,
            WorkerProgressEventJson.Deserialize(json).Error);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteCSharpMetricsReturnStructuredSerializationFailure(double value)
    {
        WorkerProgressEvent template = ParseEvent("metric-event.json");
        var invalid = new WorkerProgressEvent(
            1,
            WorkerEventKind.Metric,
            template.JobId,
            metric: new WorkerMetric("elapsed-time", value, WorkerMetricUnit.Milliseconds));

        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerProgressEventJson.Serialize(invalid).Error);
    }

    [Fact]
    public void InvalidCSharpEventShapesReturnStructuredSerializationFailures()
    {
        BuildJobId job = BuildJobId.Create("Job-01").Value!;
        WorkerProgressEvent[] invalid =
        [
            new(1, WorkerEventKind.Progress, job),
            new(1, WorkerEventKind.Finding, job),
            new(1, WorkerEventKind.Metric, job),
            new(1, (WorkerEventKind)99, job),
            new(
                1,
                WorkerEventKind.Progress,
                job,
                "stage",
                percent: double.PositiveInfinity),
        ];

        Assert.All(
            invalid,
            value => Assert.Equal(
                WorkerJsonError.SemanticViolation,
                WorkerProgressEventJson.Serialize(value).Error));
    }

    [Fact]
    public void InvalidCSharpRequestAndResultShapesFailWithoutWritingContracts()
    {
        BuildJobId job = BuildJobId.Create("Job-01").Value!;
        var request = new WorkerRequest(
            2,
            job,
            "operation",
            "manifest.json",
            "input",
            "output",
            "result.json");
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerRequestJson.Serialize(request).Error);

        var invalidStatus = new WorkerResult(
            1,
            job,
            (WorkerResultStatus)99,
            "1.0.0",
            false,
            [],
            [],
            [],
            [],
            WorkerRetrySafety.Safe);
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Serialize(invalidStatus).Error);
    }

    [Fact]
    public void CSharpTokenEnumsRejectUnsupportedValuesThroughSchemaOrSemantics()
    {
        BuildJobId job = BuildJobId.Create("Job-01").Value!;
        var invalidMetric = new WorkerProgressEvent(
            1,
            WorkerEventKind.Metric,
            job,
            metric: new WorkerMetric("metric", 1, (WorkerMetricUnit)99));
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerProgressEventJson.Serialize(invalidMetric).Error);

        WorkerResult failure = ParseResult("result-failure.json");
        var invalidRetry = new WorkerResult(
            1,
            failure.JobId,
            failure.Status,
            failure.WorkerVersion,
            false,
            failure.Artifacts,
            failure.Findings,
            failure.Metrics,
            failure.LogReferences,
            (WorkerRetrySafety)99);
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Serialize(invalidRetry).Error);

        WorkerResult cancelled = ParseResult("result-cancelled.json");
        var invalidCancellation = new WorkerResult(
            1,
            cancelled.JobId,
            cancelled.Status,
            cancelled.WorkerVersion,
            false,
            cancelled.Artifacts,
            cancelled.Findings,
            cancelled.Metrics,
            cancelled.LogReferences,
            WorkerRetrySafety.Safe,
            cancellation: new WorkerCancellation((WorkerCancellationOutcome)99));
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Serialize(invalidCancellation).Error);
    }

    [Fact]
    public void CSharpResultRejectsNullCollectionMembersAndInvalidSerializableValues()
    {
        BuildJobId job = BuildJobId.Create("Job-01").Value!;
        var nullArtifact = new WorkerResult(
            1,
            job,
            WorkerResultStatus.Failure,
            "1.0.0",
            false,
            [null!],
            [],
            [],
            [],
            WorkerRetrySafety.Safe);
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Serialize(nullArtifact).Error);

        var nullFinding = new WorkerResult(
            1,
            job,
            WorkerResultStatus.Failure,
            "1.0.0",
            false,
            [],
            [null!],
            [],
            [],
            WorkerRetrySafety.Safe);
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Serialize(nullFinding).Error);

        var nullMetric = new WorkerResult(
            1,
            job,
            WorkerResultStatus.Failure,
            "1.0.0",
            false,
            [],
            [],
            [null!],
            [],
            WorkerRetrySafety.Safe);
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Serialize(nullMetric).Error);

        var nullLog = new WorkerResult(
            1,
            job,
            WorkerResultStatus.Failure,
            "1.0.0",
            false,
            [],
            [],
            [],
            [null!],
            WorkerRetrySafety.Safe);
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Serialize(nullLog).Error);

        var invalidVersion = new WorkerResult(
            1,
            job,
            WorkerResultStatus.Failure,
            "bad version",
            false,
            [],
            [],
            [],
            [],
            WorkerRetrySafety.Safe);
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Serialize(invalidVersion).Error);
    }

    [Fact]
    public void UnicodeControlCharactersReachDomainParityChecksAndFail()
    {
        string control = "\u0091";
        JsonObject request = JsonNode.Parse(ReadValid("request.json"))!.AsObject();
        request["jobId"] = $"Job{control}01";
        Assert.Equal(
            WorkerJsonError.DomainViolation,
            WorkerRequestJson.Deserialize(request.ToJsonString()).Error);

        JsonObject progress = JsonNode.Parse(ReadValid("progress-0.json"))!.AsObject();
        progress["jobId"] = $"Job{control}01";
        Assert.Equal(
            WorkerJsonError.DomainViolation,
            WorkerProgressEventJson.Deserialize(progress.ToJsonString()).Error);

        JsonObject finding = JsonNode.Parse(ReadValid("finding-event.json"))!.AsObject();
        finding["finding"]!["explanation"] = $"invalid{control}text";
        Assert.Equal(
            WorkerJsonError.DomainViolation,
            WorkerProgressEventJson.Deserialize(finding.ToJsonString()).Error);

        JsonObject result = JsonNode.Parse(ReadValid("result-success.json"))!.AsObject();
        result["jobId"] = $"Job{control}01";
        Assert.Equal(
            WorkerJsonError.DomainViolation,
            WorkerResultJson.Deserialize(result.ToJsonString()).Error);

        result = JsonNode.Parse(ReadValid("result-success.json"))!.AsObject();
        result["artifacts"]![0]!["artifactId"] = $"Artifact{control}01";
        Assert.Equal(
            WorkerJsonError.DomainViolation,
            WorkerResultJson.Deserialize(result.ToJsonString()).Error);

        result = JsonNode.Parse(ReadValid("result-success.json"))!.AsObject();
        result["findings"]![0]!["explanation"] = $"invalid{control}text";
        Assert.Equal(
            WorkerJsonError.DomainViolation,
            WorkerResultJson.Deserialize(result.ToJsonString()).Error);
    }

    [Fact]
    public void NestedDuplicatePropertyInsideArrayFailsBeforeSchemaEvaluation()
    {
        string json = ReadValid("result-success.json").Replace(
            "\"artifactId\":\"Artifact-01\"",
            "\"artifactId\":\"Artifact-01\",\"artifactId\":\"Artifact-02\"",
            StringComparison.Ordinal);
        Assert.Equal(
            WorkerJsonError.DuplicateProperty,
            WorkerResultJson.Deserialize(json).Error);
    }

    [Fact]
    public void ExtremeFiniteSyntaxThatOverflowsDoubleIsRejectedSemantically()
    {
        string eventJson = ReadValid("metric-event.json").Replace(
            "1250.5",
            "1e400",
            StringComparison.Ordinal);
        string resultJson = ReadValid("result-success.json").Replace(
            "1250.5",
            "1e400",
            StringComparison.Ordinal);

        Assert.NotEqual(
            WorkerJsonError.None,
            WorkerProgressEventJson.Deserialize(eventJson).Error);
        Assert.NotEqual(
            WorkerJsonError.None,
            WorkerResultJson.Deserialize(resultJson).Error);
    }

    [Fact]
    public void AdditionalSemanticContradictionsFail()
    {
        JsonObject result = JsonNode.Parse(ReadValid("result-success.json"))!.AsObject();
        result["findings"]![0]!["relatedArtifactId"] = "Artifact-Missing";
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Deserialize(result.ToJsonString()).Error);

        result = JsonNode.Parse(ReadValid("result-failure.json"))!.AsObject();
        result["outputsPromoted"] = true;
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerResultJson.Deserialize(result.ToJsonString()).Error);

        WorkerResult failure = ParseResult("result-failure.json");
        var duplicateMetrics = new WorkerResult(
            1,
            failure.JobId,
            failure.Status,
            failure.WorkerVersion,
            false,
            [],
            failure.Findings,
            [
                new WorkerMetric("same", 1, WorkerMetricUnit.Count),
                new WorkerMetric("same", 2, WorkerMetricUnit.Count),
            ],
            [],
            WorkerRetrySafety.Safe);
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Serialize(duplicateMetrics).Error);

        var duplicateLogs = new WorkerResult(
            1,
            failure.JobId,
            failure.Status,
            failure.WorkerVersion,
            false,
            [],
            failure.Findings,
            [],
            ["logs/a.log", "logs/a.log"],
            WorkerRetrySafety.Safe);
        Assert.Equal(
            WorkerJsonError.SemanticViolation,
            WorkerResultJson.Serialize(duplicateLogs).Error);
    }

    [Fact]
    public void CSharpStatusContradictionsExerciseEveryShortCircuitBoundary()
    {
        WorkerResult success = ParseResult("result-success.json");
        WorkerResult failure = ParseResult("result-failure.json");
        WorkerResult cancelled = ParseResult("result-cancelled.json");
        WorkerResult[] invalid =
        [
            Copy(success, retrySafety: WorkerRetrySafety.Safe),
            Copy(success, cancellation: new WorkerCancellation(WorkerCancellationOutcome.Partial)),
            Copy(failure, outputsPromoted: true),
            Copy(failure, cancellation: new WorkerCancellation(WorkerCancellationOutcome.Partial)),
            Copy(cancelled, outputsPromoted: true),
            Copy(cancelled, cancellation: null, replaceCancellation: true),
            Copy(cancelled, retrySafety: WorkerRetrySafety.Unsafe),
            Copy(failure, protocolVersion: 2),
        ];

        Assert.All(
            invalid,
            value => Assert.Equal(
                WorkerJsonError.SemanticViolation,
                WorkerResultJson.Serialize(value).Error));
    }

    [Fact]
    public void InternalResultTokenMappingRejectsUnknownEnumDeterministically()
    {
        Type tokens = typeof(WorkerResultJson).Assembly.GetType(
            "PackageBuilder.Contracts.Workers.WorkerJsonTokens",
            throwOnError: true)!;
        MethodInfo method = tokens.GetMethod(
            "ResultStatus",
            BindingFlags.Public | BindingFlags.Static)!;

        Assert.Equal(string.Empty, method.Invoke(null, [(WorkerResultStatus)99]));
    }

    [Fact]
    public void PhysicalNewlineAndControlCharacterInjectionFails()
    {
        string physicalNewline = ReadValid("progress-0.json").Replace(
            "source assets.",
            "source\nassets.",
            StringComparison.Ordinal);
        string escapedNewline = ReadValid("progress-0.json").Replace(
            "source assets.",
            "source\\nassets.",
            StringComparison.Ordinal);

        Assert.Equal(
            WorkerJsonError.MalformedJson,
            WorkerProgressEventJson.Deserialize(physicalNewline).Error);
        Assert.Equal(
            WorkerJsonError.SchemaViolation,
            WorkerProgressEventJson.Deserialize(escapedNewline).Error);
    }

    [Fact]
    public void OversizedAndDeepInputsFailWithinContractLimits()
    {
        string oversizedEvent = new(' ', WorkerProgressEventJson.MaximumInputCharacters + 1);
        string oversizedRequest = new(' ', WorkerRequestJson.MaximumInputCharacters + 1);
        string deep = new string('[', WorkerRequestJson.MaximumDepth + 1) + "0" +
            new string(']', WorkerRequestJson.MaximumDepth + 1);

        Assert.Equal(
            WorkerJsonError.LineTooLarge,
            WorkerProgressEventJson.Deserialize(oversizedEvent).Error);
        Assert.Equal(
            WorkerJsonError.InputTooLarge,
            WorkerRequestJson.Deserialize(oversizedRequest).Error);
        Assert.Equal(
            WorkerJsonError.MalformedJson,
            WorkerResultJson.Deserialize(deep).Error);
    }

    [Fact]
    public void RepeatedSerializationIsCultureIndependent()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            WorkerResult result = ParseResult("result-success.json");
            string expected = SerializeResult(result);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            for (int index = 0; index < 20; index++)
            {
                Assert.Equal(expected, SerializeResult(result));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ResultCollectionsAreImmutableSnapshots()
    {
        BuildJobId jobId = BuildJobId.Create("Job-01").Value!;
        var artifacts = new List<WorkerArtifact>();
        var findings = new List<ValidationFinding>();
        var metrics = new List<WorkerMetric>();
        var logs = new List<string>();
        var result = new WorkerResult(
            1,
            jobId,
            WorkerResultStatus.Failure,
            "1.0.0",
            false,
            artifacts,
            findings,
            metrics,
            logs,
            WorkerRetrySafety.Safe);

        artifacts.Add(ParseResult("result-success.json").Artifacts[0]);
        metrics.Add(new WorkerMetric("count", 1, WorkerMetricUnit.Count));
        logs.Add("logs/late.log");

        Assert.Empty(result.Artifacts);
        Assert.Empty(result.Metrics);
        Assert.Empty(result.LogReferences);
    }

    private static WorkerProgressEvent ParseEvent(string fixture)
    {
        WorkerJsonDeserializationResult<WorkerProgressEvent> result =
            WorkerProgressEventJson.Deserialize(ReadValid(fixture));
        Assert.True(result.IsSuccessful, result.Details);
        return result.Value!;
    }

    private static WorkerResult ParseResult(string fixture)
    {
        WorkerJsonDeserializationResult<WorkerResult> result =
            WorkerResultJson.Deserialize(ReadValid(fixture));
        Assert.True(result.IsSuccessful, result.Details);
        return result.Value!;
    }

    private static string SerializeRequest(WorkerRequest request)
    {
        WorkerJsonSerializationResult result = WorkerRequestJson.Serialize(request);
        Assert.True(result.IsSuccessful);
        return result.Json!;
    }

    private static string SerializeEvent(WorkerProgressEvent value)
    {
        WorkerJsonSerializationResult result = WorkerProgressEventJson.Serialize(value);
        Assert.True(result.IsSuccessful);
        return result.Json!;
    }

    private static string SerializeResult(WorkerResult value)
    {
        WorkerJsonSerializationResult result = WorkerResultJson.Serialize(value);
        Assert.True(result.IsSuccessful);
        return result.Json!;
    }

    private static WorkerResult Copy(
        WorkerResult value,
        int? protocolVersion = null,
        bool? outputsPromoted = null,
        WorkerRetrySafety? retrySafety = null,
        WorkerCancellation? cancellation = null,
        bool replaceCancellation = false) =>
        new(
            protocolVersion ?? value.ProtocolVersion,
            value.JobId,
            value.Status,
            value.WorkerVersion,
            outputsPromoted ?? value.OutputsPromoted,
            value.Artifacts,
            value.Findings,
            value.Metrics,
            value.LogReferences,
            retrySafety ?? value.RetrySafety,
            value.EngineVersion,
            replaceCancellation ? cancellation : cancellation ?? value.Cancellation);

    private static string ReadValid(string file) => ReadFixture("valid", file);
    private static string ReadInvalid(string file) => ReadFixture("invalid", file);

    private static string ReadFixture(string classification, string file) =>
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "fixtures",
                "workers",
                classification,
                file)).TrimEnd('\r', '\n');

    private static void Remove(JsonObject root, string jsonPath)
    {
        string[] segments = jsonPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        JsonNode current = root;
        for (int index = 0; index < segments.Length - 1; index++)
        {
            current = int.TryParse(segments[index], out int arrayIndex)
                ? current.AsArray()[arrayIndex]!
                : current[segments[index]]!;
        }

        if (current is JsonObject obj)
        {
            _ = obj.Remove(segments[^1]);
        }
        else
        {
            current.AsArray().RemoveAt(int.Parse(segments[^1], CultureInfo.InvariantCulture));
        }
    }
}
