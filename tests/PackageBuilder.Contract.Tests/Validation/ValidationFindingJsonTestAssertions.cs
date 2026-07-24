using PackageBuilder.Contracts.Validation;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contract.Tests.Validation;

internal static class ValidationFindingJsonTestAssertions
{
    public static T DomainSuccess<T>(ValidationFindingResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid);
        return Assert.IsType<T>(result.Value);
    }

    public static BuildArtifactId ArtifactId(string value = "Artifact-01")
    {
        BuildModelValidationResult<BuildArtifactId> result = BuildArtifactId.Create(value);
        Assert.True(result.IsValid);
        return Assert.IsType<BuildArtifactId>(result.Value);
    }

    public static Domain.Validation.ValidationFinding Finding(
        FindingSeverity? severity = null,
        bool blocksRelease = true,
        bool includeOptionalValues = true,
        string explanation = "The normal map is missing.")
    {
        ValidationFindingResult<Domain.Validation.ValidationFinding> result =
            Domain.Validation.ValidationFinding.Create(
                DomainSuccess(FindingCode.Create("UNITY_MATERIAL_MISSING_NORMAL")),
                severity ?? FindingSeverity.Error,
                DomainSuccess(FindingExplanation.Create(explanation)),
                DomainSuccess(FindingSourceComponent.Create("unity-material-validator")),
                includeOptionalValues ? ArtifactId() : null,
                includeOptionalValues
                    ? DomainSuccess(CorrectiveAction.Create("Assign the intended normal map."))
                    : null,
                blocksRelease);
        return DomainSuccess(result);
    }

    public static string Serialize(Domain.Validation.ValidationFinding finding)
    {
        ValidationFindingSerializationResult result = ValidationFindingJson.Serialize(finding);
        Assert.True(result.IsSuccessful);
        Assert.Equal(ValidationFindingJsonError.None, result.Error);
        return Assert.IsType<string>(result.Json);
    }

    public static Domain.Validation.ValidationFinding Deserialize(string json)
    {
        ValidationFindingDeserializationResult result = ValidationFindingJson.Deserialize(json);
        Assert.True(result.IsSuccessful);
        Assert.Equal(ValidationFindingJsonError.None, result.Error);
        Assert.Equal(ValidationFindingError.None, result.DomainError);
        Assert.Equal(BuildModelValidationError.None, result.ArtifactIdError);
        return Assert.IsType<Domain.Validation.ValidationFinding>(result.Value);
    }

    public static void Failure(
        string? json,
        ValidationFindingJsonError error,
        ValidationFindingError domainError = ValidationFindingError.None,
        BuildModelValidationError artifactError = BuildModelValidationError.None)
    {
        ValidationFindingDeserializationResult result = ValidationFindingJson.Deserialize(json);
        Assert.False(result.IsSuccessful);
        Assert.Null(result.Value);
        Assert.Equal(error, result.Error);
        Assert.Equal(domainError, result.DomainError);
        Assert.Equal(artifactError, result.ArtifactIdError);
    }
}
