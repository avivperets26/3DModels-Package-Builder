using System.Globalization;
using System.Reflection;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Domain.Tests.Validation;

[Trait("Task", "PB-0109")]
public sealed class ValidationFindingTests
{
    public static TheoryData<string?, ValidationFindingError> InvalidSources =>
        new()
        {
            { null, ValidationFindingError.NullSource },
            { string.Empty, ValidationFindingError.EmptySource },
            { " \t", ValidationFindingError.WhitespaceOnlySource },
            { "Unity", ValidationFindingError.MalformedSource },
            { "unity--worker", ValidationFindingError.MalformedSource },
            { "unity_worker", ValidationFindingError.MalformedSource },
            { "unity2", ValidationFindingError.MalformedSource },
            { "unity/worker", ValidationFindingError.MalformedSource },
            { "unity\u0001worker", ValidationFindingError.MalformedSource },
        };

    public static TheoryData<string?, ValidationFindingError> InvalidExplanations =>
        new()
        {
            { null, ValidationFindingError.NullExplanation },
            { string.Empty, ValidationFindingError.EmptyExplanation },
            { " \t", ValidationFindingError.WhitespaceOnlyExplanation },
            { " leading", ValidationFindingError.ExplanationEdgeWhitespace },
            { "trailing ", ValidationFindingError.ExplanationEdgeWhitespace },
            { "unsafe\u0001text", ValidationFindingError.ExplanationContainsControlCharacter },
        };

    public static TheoryData<string?, ValidationFindingError> InvalidActions =>
        new()
        {
            { null, ValidationFindingError.NullCorrectiveAction },
            { string.Empty, ValidationFindingError.EmptyCorrectiveAction },
            { " \t", ValidationFindingError.WhitespaceOnlyCorrectiveAction },
            { " leading", ValidationFindingError.CorrectiveActionEdgeWhitespace },
            { "trailing ", ValidationFindingError.CorrectiveActionEdgeWhitespace },
            { "unsafe\u0001text", ValidationFindingError.CorrectiveActionContainsControlCharacter },
        };

    public static TheoryData<int, bool> EverySeverityBlockingCombination =>
        new()
        {
            { 0, false },
            { 0, true },
            { 1, false },
            { 1, true },
            { 2, false },
            { 2, true },
            { 3, false },
            { 3, true },
        };

    [Theory]
    [InlineData("domain")]
    [InlineData("unity")]
    [InlineData("source-preflight")]
    [InlineData("marketplace-profile-validator")]
    public void SourceAcceptsExtensibleCanonicalIdentities(string value)
    {
        FindingSourceComponent source =
            FindingTestAssertions.Success(FindingSourceComponent.Create(value));

        Assert.Equal(value, source.Value);
        Assert.Equal(value, source.ToString());
        Assert.Equal(source, FindingTestAssertions.Success(FindingSourceComponent.Create(value)));
        Assert.True(source.Equals((object)FindingTestAssertions.Success(
            FindingSourceComponent.Create(value))));
        Assert.Equal(
            source.GetHashCode(),
            FindingTestAssertions.Success(FindingSourceComponent.Create(value)).GetHashCode());
        Assert.False(source.Equals((FindingSourceComponent?)null));
        Assert.False(source.Equals(new object()));
    }

    [Theory]
    [MemberData(nameof(InvalidSources))]
    public void SourceRejectsEveryMalformedCategory(
        string? value,
        ValidationFindingError error) =>
        FindingTestAssertions.Failure(FindingSourceComponent.Create(value), error);

    [Theory]
    [MemberData(nameof(InvalidExplanations))]
    public void ExplanationRejectsInvalidText(
        string? value,
        ValidationFindingError error) =>
        FindingTestAssertions.Failure(FindingExplanation.Create(value), error);

    [Theory]
    [MemberData(nameof(InvalidActions))]
    public void CorrectiveActionRejectsInvalidText(
        string? value,
        ValidationFindingError error) =>
        FindingTestAssertions.Failure(CorrectiveAction.Create(value), error);

    [Fact]
    public void HumanTextPreservesUnicodeHostilePunctuationAndUnusuallyLargeInput()
    {
        string explanationText =
            "\u6cd5\u7dda\u30de\u30c3\u30d7 \u2014 missing <asset> \"normal\"; ../../ remains prose.";
        string actionText = string.Concat(
            Enumerable.Repeat("\u914d\u7f6e\u3092\u78ba\u8a8d\u3059\u308b\u3002", 20_000));
        FindingExplanation explanation =
            FindingTestAssertions.Success(FindingExplanation.Create(explanationText));
        CorrectiveAction action =
            FindingTestAssertions.Success(CorrectiveAction.Create(actionText));

        Assert.Equal(explanationText, explanation.Value);
        Assert.Equal(explanationText, explanation.ToString());
        Assert.Equal(actionText, action.Value);
        Assert.Equal(actionText, action.ToString());
        Assert.Equal(
            explanation,
            FindingTestAssertions.Success(FindingExplanation.Create(explanationText)));
        Assert.True(explanation.Equals((object)FindingTestAssertions.Success(
            FindingExplanation.Create(explanationText))));
        Assert.Equal(
            explanation.GetHashCode(),
            FindingTestAssertions.Success(
                FindingExplanation.Create(explanationText)).GetHashCode());
        Assert.False(explanation.Equals((FindingExplanation?)null));
        Assert.False(explanation.Equals(new object()));
        Assert.Equal(action, FindingTestAssertions.Success(CorrectiveAction.Create(actionText)));
        Assert.True(action.Equals((object)FindingTestAssertions.Success(
            CorrectiveAction.Create(actionText))));
        Assert.Equal(
            action.GetHashCode(),
            FindingTestAssertions.Success(CorrectiveAction.Create(actionText)).GetHashCode());
        Assert.False(action.Equals((CorrectiveAction?)null));
        Assert.False(action.Equals(new object()));
    }

    [Theory]
    [MemberData(nameof(EverySeverityBlockingCombination))]
    public void EverySeverityMayBeBlockingOrNonBlocking(
        int severityIndex,
        bool blocksRelease)
    {
        FindingSeverity severity = FindingSeverity.All[severityIndex];
        ValidationFinding finding = FindingTestAssertions.Finding(
            severity,
            blocksRelease,
            FindingTestAssertions.ArtifactId(),
            FindingTestAssertions.Action());

        Assert.Same(severity, finding.Severity);
        Assert.Equal(blocksRelease, finding.BlocksRelease);
        Assert.Equal("Artifact-01", finding.RelatedArtifactId?.Value);
        Assert.Equal(
            "Assign the intended normal map.",
            finding.SuggestedAction?.Value);
    }

    [Fact]
    public void FindingAllowsDeliberatelyAbsentOptionalRelationships()
    {
        ValidationFinding finding = FindingTestAssertions.Finding(
            FindingSeverity.Warning,
            blocksRelease: false);

        Assert.Null(finding.RelatedArtifactId);
        Assert.Null(finding.SuggestedAction);
    }

    [Fact]
    public void FindingRejectsEachMissingRequiredTypedValueInStableOrder()
    {
        FindingCode code = FindingTestAssertions.Code();
        FindingExplanation explanation = FindingTestAssertions.Explanation();
        FindingSourceComponent source = FindingTestAssertions.Source();

        AssertFindingFailure(
            null,
            FindingSeverity.Error,
            explanation,
            source,
            ValidationFindingError.NullCode);
        AssertFindingFailure(
            code,
            null,
            explanation,
            source,
            ValidationFindingError.NullSeverity);
        AssertFindingFailure(
            code,
            FindingSeverity.Error,
            null,
            source,
            ValidationFindingError.NullExplanation);
        AssertFindingFailure(
            code,
            FindingSeverity.Error,
            explanation,
            null,
            ValidationFindingError.NullSource);
    }

    [Fact]
    public void FindingEqualityHashingAndOptionalValuesAreDeterministicAndOrdinal()
    {
        ValidationFinding first = FindingTestAssertions.Finding(
            FindingSeverity.Error,
            true,
            FindingTestAssertions.ArtifactId("\u00c4rtifact"),
            FindingTestAssertions.Action("R\u00e9assign the map."));
        ValidationFinding equal = FindingTestAssertions.Finding(
            FindingSeverity.Error,
            true,
            FindingTestAssertions.ArtifactId("\u00c4rtifact"),
            FindingTestAssertions.Action("R\u00e9assign the map."));

        Assert.Equal(first, equal);
        Assert.True(first.Equals((object)equal));
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(
            first,
            FindingTestAssertions.Finding(
                FindingSeverity.Error,
                false,
                FindingTestAssertions.ArtifactId("\u00c4rtifact"),
                FindingTestAssertions.Action("R\u00e9assign the map.")));
        Assert.NotEqual(first, FindingTestAssertions.Finding());
        Assert.False(first.Equals((ValidationFinding?)null));
        Assert.False(first.Equals(new object()));
    }

    [Fact]
    public void FindingHashingCoversEachIndependentOptionalValue()
    {
        ValidationFinding artifactOnly = FindingTestAssertions.Finding(
            artifactId: FindingTestAssertions.ArtifactId());
        ValidationFinding actionOnly = FindingTestAssertions.Finding(
            action: FindingTestAssertions.Action());

        Assert.NotEqual(FindingTestAssertions.Finding(), artifactOnly);
        Assert.NotEqual(FindingTestAssertions.Finding(), actionOnly);
        Assert.Equal(
            artifactOnly.GetHashCode(),
            FindingTestAssertions.Finding(
                artifactId: FindingTestAssertions.ArtifactId()).GetHashCode());
        Assert.Equal(
            actionOnly.GetHashCode(),
            FindingTestAssertions.Finding(
                action: FindingTestAssertions.Action()).GetHashCode());
    }

    [Fact]
    public void FindingIdentityAndHashingAreCultureIndependent()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            ValidationFinding first = FindingTestAssertions.Finding(
                FindingSeverity.Info,
                false,
                FindingTestAssertions.ArtifactId("Info"),
                FindingTestAssertions.Action("Inspect identity."));
            int hash = first.GetHashCode();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            ValidationFinding second = FindingTestAssertions.Finding(
                FindingSeverity.Info,
                false,
                FindingTestAssertions.ArtifactId("Info"),
                FindingTestAssertions.Action("Inspect identity."));

            Assert.Equal(first, second);
            Assert.Equal(hash, second.GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FindingDomainHasNoForbiddenDependencyOrHiddenSideEffect()
    {
        Assembly assembly = typeof(ValidationFinding).Assembly;
        string[] references =
        [
            .. assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty),
        ];
        Assert.DoesNotContain(references, name => name.Contains("Wpf", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Unity", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Unreal", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Blender", StringComparison.Ordinal));
        Assert.DoesNotContain(
            references,
            name => name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            references,
            name => name.Contains("Marketplaces", StringComparison.Ordinal));

        string repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string validationRoot = Path.Combine(
            repositoryRoot,
            "src",
            "PackageBuilder.Domain",
            "Validation");
        string productionText = string.Join(
            "\n",
            Directory.GetFiles(validationRoot, "*.cs")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        Assert.DoesNotContain("System.IO", productionText, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.UtcNow", productionText, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTimeOffset.UtcNow", productionText, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", productionText, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", productionText, StringComparison.Ordinal);
    }

    private static void AssertFindingFailure(
        FindingCode? code,
        FindingSeverity? severity,
        FindingExplanation? explanation,
        FindingSourceComponent? source,
        ValidationFindingError error) =>
        FindingTestAssertions.Failure(
            ValidationFinding.Create(
                code,
                severity,
                explanation,
                source,
                null,
                null,
                blocksRelease: false),
            error);
}
