using System.Globalization;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Domain.Tests.Validation;

[Trait("Task", "PB-0109")]
public sealed class FindingCodeSeverityTests
{
    public static TheoryData<string> ValidCodes =>
        [
            "A",
            "A0",
            "A_B",
            "Z9",
            "UNITY_MATERIAL_MISSING_NORMAL",
            "PORTABLE_FBX2_INVALID",
        ];

    public static TheoryData<string?, ValidationFindingError> InvalidCodes =>
        new()
        {
            { null, ValidationFindingError.NullCode },
            { string.Empty, ValidationFindingError.EmptyCode },
            { " \t", ValidationFindingError.WhitespaceOnlyCode },
            { "lowercase", ValidationFindingError.MalformedCode },
            { "Mixed_CASE", ValidationFindingError.MalformedCode },
            { "0", ValidationFindingError.MalformedCode },
            { "1CODE", ValidationFindingError.MalformedCode },
            { "CODE_1", ValidationFindingError.MalformedCode },
            { "_LEADING", ValidationFindingError.MalformedCode },
            { "TRAILING_", ValidationFindingError.MalformedCode },
            { "REPEATED__UNDERSCORE", ValidationFindingError.MalformedCode },
            { "HAS-HYPHEN", ValidationFindingError.MalformedCode },
            { "HAS SPACE", ValidationFindingError.MalformedCode },
            { "PATH/SEGMENT", ValidationFindingError.MalformedCode },
            { @"PATH\SEGMENT", ValidationFindingError.MalformedCode },
            { "C:PATH", ValidationFindingError.MalformedCode },
            { "../PATH", ValidationFindingError.MalformedCode },
            { "\u00c5", ValidationFindingError.MalformedCode },
            { "CONTROL\u0001", ValidationFindingError.MalformedCode },
        };

    [Theory]
    [MemberData(nameof(ValidCodes))]
    public void CodeAcceptsEveryGrammarBoundaryAndRepresentativePrefix(string value)
    {
        FindingCode code = FindingTestAssertions.Success(FindingCode.Create(value));

        Assert.Equal(value, code.Value);
        Assert.Equal(value, code.ToString());
    }

    [Fact]
    public void CodeHasNoArbitraryLengthLimit()
    {
        string unusuallyLarge = string.Join("_", Enumerable.Repeat("SEGMENT9", 20_000));

        Assert.Equal(
            unusuallyLarge,
            FindingTestAssertions.Success(FindingCode.Create(unusuallyLarge)).Value);
    }

    [Theory]
    [MemberData(nameof(InvalidCodes))]
    public void CodeRejectsEveryMalformedCategory(
        string? value,
        ValidationFindingError error) =>
        FindingTestAssertions.Failure(FindingCode.Create(value), error);

    [Fact]
    public void CodeUsesOrdinalStableValueSemantics()
    {
        FindingCode first = FindingTestAssertions.Code("UNITY_NORMAL_MISSING");
        FindingCode equal = FindingTestAssertions.Code("UNITY_NORMAL_MISSING");
        FindingCode other = FindingTestAssertions.Code("UNITY_NORMAL_INVALID");

        Assert.Equal(first, equal);
        Assert.True(first.Equals((object)equal));
        Assert.NotEqual(first, other);
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.False(first.Equals((FindingCode?)null));
        Assert.False(first.Equals(new object()));
    }

    [Fact]
    public void SeverityInventoryNamesAndTokensAreExactStableAndImmutable()
    {
        Assert.Equal(
            ["Info", "Warning", "Error", "Fatal"],
            FindingSeverity.All.Select(value => value.Name));
        Assert.Equal(
            ["info", "warning", "error", "fatal"],
            FindingSeverity.All.Select(value => value.SerializedToken));
        Assert.Equal("Info", FindingSeverity.Info.ToString());
        FindingTestAssertions.AssertImmutable(FindingSeverity.All, FindingSeverity.Info);
    }

    [Theory]
    [InlineData("info", 0)]
    [InlineData("warning", 1)]
    [InlineData("error", 2)]
    [InlineData("fatal", 3)]
    public void SeverityParsesOnlyStableTokens(string token, int index)
    {
        FindingSeverity value =
            FindingTestAssertions.Success(FindingSeverity.ParseToken(token));

        Assert.Same(FindingSeverity.All[index], value);
        Assert.Equal(value, FindingSeverity.All[index]);
        Assert.True(value.Equals((object)FindingSeverity.All[index]));
        Assert.Equal(value.GetHashCode(), FindingSeverity.All[index].GetHashCode());
        Assert.False(value.Equals((FindingSeverity?)null));
        Assert.False(value.Equals(new object()));
    }

    [Theory]
    [InlineData(null, ValidationFindingError.NullSeverity)]
    [InlineData("", ValidationFindingError.EmptySeverityToken)]
    [InlineData(" ", ValidationFindingError.WhitespaceOnlySeverityToken)]
    [InlineData("Warning", ValidationFindingError.UnknownSeverityToken)]
    [InlineData("critical", ValidationFindingError.UnknownSeverityToken)]
    public void SeverityRejectsNullMalformedAndUnknownTokens(
        string? token,
        ValidationFindingError error) =>
        FindingTestAssertions.Failure(FindingSeverity.ParseToken(token), error);

    [Fact]
    public void CodeAndSeverityAreCultureIndependent()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            FindingCode before = FindingTestAssertions.Code("INFO_IDENTITY");
            int codeHash = before.GetHashCode();
            int severityHash = FindingSeverity.Info.GetHashCode();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            Assert.Equal(before, FindingTestAssertions.Code("INFO_IDENTITY"));
            Assert.Equal(codeHash, FindingTestAssertions.Code("INFO_IDENTITY").GetHashCode());
            Assert.Same(
                FindingSeverity.Info,
                FindingTestAssertions.Success(FindingSeverity.ParseToken("info")));
            Assert.Equal(severityHash, FindingSeverity.Info.GetHashCode());
            FindingTestAssertions.Failure(
                FindingCode.Create("\u00c4NFO"),
                ValidationFindingError.MalformedCode);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
