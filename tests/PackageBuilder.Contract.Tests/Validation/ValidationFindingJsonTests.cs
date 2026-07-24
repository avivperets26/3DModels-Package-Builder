using System.Globalization;
using PackageBuilder.Contracts.Validation;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contract.Tests.Validation;

[Trait("Task", "PB-0109")]
public sealed class ValidationFindingJsonTests
{
    public static TheoryData<int, string> SeverityGoldenJson =>
        new()
        {
            {
                0,
                "{\"code\":\"UNITY_MATERIAL_MISSING_NORMAL\",\"severity\":\"info\",\"explanation\":\"The normal map is missing.\",\"source\":\"unity-material-validator\",\"relatedArtifactId\":\"Artifact-01\",\"suggestedAction\":\"Assign the intended normal map.\",\"blocksRelease\":true}"
            },
            {
                1,
                "{\"code\":\"UNITY_MATERIAL_MISSING_NORMAL\",\"severity\":\"warning\",\"explanation\":\"The normal map is missing.\",\"source\":\"unity-material-validator\",\"relatedArtifactId\":\"Artifact-01\",\"suggestedAction\":\"Assign the intended normal map.\",\"blocksRelease\":true}"
            },
            {
                2,
                "{\"code\":\"UNITY_MATERIAL_MISSING_NORMAL\",\"severity\":\"error\",\"explanation\":\"The normal map is missing.\",\"source\":\"unity-material-validator\",\"relatedArtifactId\":\"Artifact-01\",\"suggestedAction\":\"Assign the intended normal map.\",\"blocksRelease\":true}"
            },
            {
                3,
                "{\"code\":\"UNITY_MATERIAL_MISSING_NORMAL\",\"severity\":\"fatal\",\"explanation\":\"The normal map is missing.\",\"source\":\"unity-material-validator\",\"relatedArtifactId\":\"Artifact-01\",\"suggestedAction\":\"Assign the intended normal map.\",\"blocksRelease\":true}"
            },
        };

    public static TheoryData<string?, ValidationFindingJsonError> StructuralFailures =>
        new()
        {
            { null, ValidationFindingJsonError.NullJson },
            { string.Empty, ValidationFindingJsonError.EmptyJson },
            { " ", ValidationFindingJsonError.MalformedJson },
            { "{", ValidationFindingJsonError.MalformedJson },
            { "{\"code\":\"A\",}", ValidationFindingJsonError.MalformedJson },
            { "[]", ValidationFindingJsonError.RootMustBeObject },
            {
                "{\"code\":\"A\",\"severity\":\"info\",\"explanation\":\"x\",\"source\":\"domain\",\"blocksRelease\":false,\"extra\":1}",
                ValidationFindingJsonError.UnknownProperty
            },
            {
                "{\"code\":\"A\",\"code\":\"B\",\"severity\":\"info\",\"explanation\":\"x\",\"source\":\"domain\",\"blocksRelease\":false}",
                ValidationFindingJsonError.DuplicateProperty
            },
            {
                "{\"severity\":\"info\",\"explanation\":\"x\",\"source\":\"domain\",\"blocksRelease\":false}",
                ValidationFindingJsonError.MissingCode
            },
            {
                "{\"code\":1,\"severity\":\"info\",\"explanation\":\"x\",\"source\":\"domain\",\"blocksRelease\":false}",
                ValidationFindingJsonError.InvalidCode
            },
            {
                "{\"code\":\"A\",\"explanation\":\"x\",\"source\":\"domain\",\"blocksRelease\":false}",
                ValidationFindingJsonError.MissingSeverity
            },
            {
                "{\"code\":\"A\",\"severity\":1,\"explanation\":\"x\",\"source\":\"domain\",\"blocksRelease\":false}",
                ValidationFindingJsonError.InvalidSeverity
            },
            {
                "{\"code\":\"A\",\"severity\":\"info\",\"source\":\"domain\",\"blocksRelease\":false}",
                ValidationFindingJsonError.MissingExplanation
            },
            {
                "{\"code\":\"A\",\"severity\":\"info\",\"explanation\":1,\"source\":\"domain\",\"blocksRelease\":false}",
                ValidationFindingJsonError.InvalidExplanation
            },
            {
                "{\"code\":\"A\",\"severity\":\"info\",\"explanation\":\"x\",\"blocksRelease\":false}",
                ValidationFindingJsonError.MissingSource
            },
            {
                "{\"code\":\"A\",\"severity\":\"info\",\"explanation\":\"x\",\"source\":1,\"blocksRelease\":false}",
                ValidationFindingJsonError.InvalidSource
            },
            {
                "{\"code\":\"A\",\"severity\":\"info\",\"explanation\":\"x\",\"source\":\"domain\"}",
                ValidationFindingJsonError.MissingBlocksRelease
            },
            {
                "{\"code\":\"A\",\"severity\":\"info\",\"explanation\":\"x\",\"source\":\"domain\",\"blocksRelease\":\"false\"}",
                ValidationFindingJsonError.InvalidBlocksRelease
            },
            {
                "{\"code\":\"A\",\"severity\":\"info\",\"explanation\":\"x\",\"source\":\"domain\",\"relatedArtifactId\":null,\"blocksRelease\":false}",
                ValidationFindingJsonError.InvalidRelatedArtifactId
            },
            {
                "{\"code\":\"A\",\"severity\":\"info\",\"explanation\":\"x\",\"source\":\"domain\",\"suggestedAction\":false,\"blocksRelease\":false}",
                ValidationFindingJsonError.InvalidSuggestedAction
            },
        };

    [Theory]
    [MemberData(nameof(SeverityGoldenJson))]
    public void EverySeverityHasExactGoldenJson(
        int severityIndex,
        string expectedJson)
    {
        FindingSeverity severity = FindingSeverity.All[severityIndex];
        Domain.Validation.ValidationFinding finding =
            ValidationFindingJsonTestAssertions.Finding(severity);

        string json = ValidationFindingJsonTestAssertions.Serialize(finding);

        Assert.Equal(expectedJson, json);
        Assert.Equal(finding, ValidationFindingJsonTestAssertions.Deserialize(json));
    }

    [Fact]
    public void OptionalValuesAreOmittedRatherThanSerializedAsNull()
    {
        Domain.Validation.ValidationFinding finding =
            ValidationFindingJsonTestAssertions.Finding(
                FindingSeverity.Warning,
                blocksRelease: false,
                includeOptionalValues: false);

        string json = ValidationFindingJsonTestAssertions.Serialize(finding);

        Assert.Equal(
            "{\"code\":\"UNITY_MATERIAL_MISSING_NORMAL\",\"severity\":\"warning\",\"explanation\":\"The normal map is missing.\",\"source\":\"unity-material-validator\",\"blocksRelease\":false}",
            json);
        Domain.Validation.ValidationFinding roundTrip =
            ValidationFindingJsonTestAssertions.Deserialize(json);
        Assert.Equal(finding, roundTrip);
        Assert.Null(roundTrip.RelatedArtifactId);
        Assert.Null(roundTrip.SuggestedAction);
    }

    [Fact]
    public void SerializationRejectsNullFindingWithoutThrowing()
    {
        ValidationFindingSerializationResult result = ValidationFindingJson.Serialize(null);

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Json);
        Assert.Equal(ValidationFindingJsonError.NullFinding, result.Error);
    }

    [Theory]
    [MemberData(nameof(StructuralFailures))]
    public void MalformedContractsReturnStructuredFailures(
        string? json,
        ValidationFindingJsonError error) =>
        ValidationFindingJsonTestAssertions.Failure(json, error);

    [Theory]
    [InlineData(
        "lower",
        "info",
        "Valid explanation.",
        "domain",
        ValidationFindingJsonError.InvalidCode,
        ValidationFindingError.MalformedCode)]
    [InlineData(
        "VALID_CODE",
        "Info",
        "Valid explanation.",
        "domain",
        ValidationFindingJsonError.InvalidSeverity,
        ValidationFindingError.UnknownSeverityToken)]
    [InlineData(
        "VALID_CODE",
        "info",
        " ",
        "domain",
        ValidationFindingJsonError.InvalidExplanation,
        ValidationFindingError.WhitespaceOnlyExplanation)]
    [InlineData(
        "VALID_CODE",
        "info",
        "Valid explanation.",
        "Domain",
        ValidationFindingJsonError.InvalidSource,
        ValidationFindingError.MalformedSource)]
    public void InvalidDomainValuesRetainPreciseReason(
        string code,
        string severity,
        string explanation,
        string source,
        ValidationFindingJsonError contractError,
        ValidationFindingError domainError)
    {
        string json =
            $"{{\"code\":\"{code}\",\"severity\":\"{severity}\",\"explanation\":\"{explanation}\",\"source\":\"{source}\",\"blocksRelease\":false}}";

        ValidationFindingJsonTestAssertions.Failure(json, contractError, domainError);
    }

    [Fact]
    public void InvalidArtifactAndActionRetainPreciseReason()
    {
        ValidationFindingJsonTestAssertions.Failure(
            "{\"code\":\"A\",\"severity\":\"info\",\"explanation\":\"x\",\"source\":\"domain\",\"relatedArtifactId\":\" bad\",\"blocksRelease\":false}",
            ValidationFindingJsonError.InvalidRelatedArtifactId,
            artifactError: BuildModelValidationError.IdentityEdgeWhitespace);
        ValidationFindingJsonTestAssertions.Failure(
            "{\"code\":\"A\",\"severity\":\"info\",\"explanation\":\"x\",\"source\":\"domain\",\"suggestedAction\":\" bad\",\"blocksRelease\":false}",
            ValidationFindingJsonError.InvalidSuggestedAction,
            ValidationFindingError.CorrectiveActionEdgeWhitespace);
    }

    [Fact]
    public void UnicodeHostileAndUnusuallyLargeTextRoundTripsExactly()
    {
        string explanation = string.Concat(
            Enumerable.Repeat(
                "\u6cd5\u7dda <asset> \"normal\" ../../ \U0001f9ea. ",
                10_000)).TrimEnd();
        Domain.Validation.ValidationFinding finding =
            ValidationFindingJsonTestAssertions.Finding(
                FindingSeverity.Fatal,
                blocksRelease: true,
                includeOptionalValues: true,
                explanation);

        string json = ValidationFindingJsonTestAssertions.Serialize(finding);
        Domain.Validation.ValidationFinding roundTrip =
            ValidationFindingJsonTestAssertions.Deserialize(json);

        Assert.Equal(explanation, roundTrip.Explanation.Value);
        Assert.Equal(finding, roundTrip);
    }

    [Fact]
    public void RepeatedSerializationIsByteForByteDeterministicAndCultureIndependent()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            Domain.Validation.ValidationFinding finding =
                ValidationFindingJsonTestAssertions.Finding(FindingSeverity.Warning, false);
            string expected = ValidationFindingJsonTestAssertions.Serialize(finding);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            for (int index = 0; index < 20; index++)
            {
                Assert.Equal(
                    expected,
                    ValidationFindingJsonTestAssertions.Serialize(finding));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
