using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Domain.Tests.Validation;

internal static class FindingTestAssertions
{
    public static T Success<T>(ValidationFindingResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid);
        Assert.Equal(ValidationFindingError.None, result.Error);
        return Assert.IsType<T>(result.Value);
    }

    public static void Failure<T>(
        ValidationFindingResult<T> result,
        ValidationFindingError error)
        where T : class
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static FindingCode Code(string value = "UNITY_MATERIAL_MISSING_NORMAL") =>
        Success(FindingCode.Create(value));

    public static FindingSourceComponent Source(string value = "unity-material-validator") =>
        Success(FindingSourceComponent.Create(value));

    public static FindingExplanation Explanation(string value = "The normal map is missing.") =>
        Success(FindingExplanation.Create(value));

    public static CorrectiveAction Action(string value = "Assign the intended normal map.") =>
        Success(CorrectiveAction.Create(value));

    public static BuildArtifactId ArtifactId(string value = "Artifact-01")
    {
        BuildModelValidationResult<BuildArtifactId> result = BuildArtifactId.Create(value);
        Assert.True(result.IsValid);
        return Assert.IsType<BuildArtifactId>(result.Value);
    }

    public static ValidationFinding Finding(
        FindingSeverity? severity = null,
        bool blocksRelease = true,
        BuildArtifactId? artifactId = null,
        CorrectiveAction? action = null) =>
        Success(
            ValidationFinding.Create(
                Code(),
                severity ?? FindingSeverity.Error,
                Explanation(),
                Source(),
                artifactId,
                action,
                blocksRelease));

    public static void AssertImmutable<T>(IReadOnlyList<T> values, T newValue)
    {
        IList<T> list = Assert.IsType<IList<T>>(values, exactMatch: false);
        Assert.True(list.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => list.Add(newValue));
    }
}
