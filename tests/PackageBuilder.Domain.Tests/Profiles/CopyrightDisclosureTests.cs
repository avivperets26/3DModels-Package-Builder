using PackageBuilder.Domain.Identifiers;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Domain.Tests.Profiles;

public sealed class CopyrightDisclosureTests
{
    [Fact]
    public void CopyrightHolderPreservesValidatedTextAndBoundaries()
    {
        CopyrightHolder holder = ProfileTestAssertions.Holder("Élan Studio");
        CopyrightHolder boundary = ProfileTestAssertions.Holder(
            new string('H', CopyrightHolder.MaximumLength));

        Assert.Equal("Élan Studio", holder.Value);
        Assert.Equal("Élan Studio", holder.ToString());
        Assert.Equal(CopyrightHolder.MaximumLength, boundary.Value.Length);
        Assert.Equal(holder, ProfileTestAssertions.Holder("Élan Studio"));
        Assert.NotEqual(holder, ProfileTestAssertions.Holder("élan Studio"));
        Assert.False(holder.Equals((object)ProfileTestAssertions.Holder("Other")));
        Assert.Equal(holder.GetHashCode(), ProfileTestAssertions.Holder("Élan Studio").GetHashCode());
        Assert.False(holder.Equals((CopyrightHolder?)null));
        Assert.False(holder.Equals(new object()));
    }

    [Theory]
    [InlineData(null, ProfileValidationError.NullCopyrightHolder)]
    [InlineData("", ProfileValidationError.EmptyCopyrightHolder)]
    [InlineData(" ", ProfileValidationError.WhitespaceOnlyCopyrightHolder)]
    [InlineData(" holder", ProfileValidationError.CopyrightHolderEdgeWhitespace)]
    [InlineData("holder ", ProfileValidationError.CopyrightHolderEdgeWhitespace)]
    [InlineData("bad\u0000holder", ProfileValidationError.CopyrightHolderContainsControlCharacter)]
    public void CopyrightHolderRejectsInvalidText(
        string? value,
        ProfileValidationError error) =>
        ProfileTestAssertions.Failure(CopyrightHolder.Create(value), error);

    [Fact]
    public void CopyrightHolderRejectsUnusuallyLargeInput() =>
        ProfileTestAssertions.Failure(
            CopyrightHolder.Create(new string('H', CopyrightHolder.MaximumLength + 1)),
            ProfileValidationError.CopyrightHolderTooLong);

    [Fact]
    public void CopyrightYearPolicyKindsHaveStableOrderAndExactParsing()
    {
        Assert.Equal(
            ["single-year", "year-range", "publication-year"],
            CopyrightYearPolicyKind.All.Select(value => value.CanonicalIdentifier));
        foreach (CopyrightYearPolicyKind kind in CopyrightYearPolicyKind.All)
        {
            CanonicalIdentifierParseResult<CopyrightYearPolicyKind> parsed = CopyrightYearPolicyKind.TryParse(kind.CanonicalIdentifier);
            Assert.True(parsed.IsValid);
            Assert.Same(kind, parsed.Value);
            Assert.Equal(CanonicalIdentifierParseError.None, parsed.Error);
            Assert.Equal(kind.CanonicalIdentifier, kind.ToString());
            Assert.Equal(kind, parsed.Value);
            Assert.NotEqual(
                kind,
                CopyrightYearPolicyKind.All.First(value => !value.Equals(kind)));
            Assert.False(kind.Equals(
                (object)CopyrightYearPolicyKind.All.First(value => !value.Equals(kind))));
            Assert.Equal(kind.GetHashCode(), parsed.Value!.GetHashCode());
            Assert.False(kind.Equals((CopyrightYearPolicyKind?)null));
            Assert.False(kind.Equals(new object()));
        }

        Assert.Equal(
            CanonicalIdentifierParseError.Null,
            CopyrightYearPolicyKind.TryParse(null).Error);
        Assert.Equal(
            CanonicalIdentifierParseError.Unknown,
            CopyrightYearPolicyKind.TryParse("other-policy").Error);
        Assert.Equal(
            CanonicalIdentifierParseError.Malformed,
            CopyrightYearPolicyKind.TryParse("Single-Year").Error);
    }

    [Fact]
    public void EveryCopyrightYearPolicyStateIsExplicitAndClockIndependent()
    {
        CopyrightYearPolicy single = ProfileTestAssertions.Year(
            CopyrightYearPolicyKind.SingleYear,
            2026);
        CopyrightYearPolicy range = ProfileTestAssertions.Year(
            CopyrightYearPolicyKind.YearRange,
            2026,
            2020);
        CopyrightYearPolicy publication = ProfileTestAssertions.Year(
            CopyrightYearPolicyKind.PublicationYear,
            2040);

        Assert.Equal(2026, single.Year);
        Assert.Null(single.StartYear);
        Assert.Equal(2020, range.StartYear);
        Assert.Equal(2026, range.Year);
        Assert.Equal(2040, publication.Year);
        Assert.Null(publication.StartYear);
        Assert.NotEqual(single, range);
        Assert.NotEqual(
            single,
            ProfileTestAssertions.Year(CopyrightYearPolicyKind.SingleYear, 2027));
        Assert.NotEqual(
            range,
            ProfileTestAssertions.Year(CopyrightYearPolicyKind.YearRange, 2026, 2019));
        Assert.Equal(single, ProfileTestAssertions.Year(
            CopyrightYearPolicyKind.SingleYear,
            2026));
        Assert.False(single.Equals((object)range));
        Assert.Equal(
            single.GetHashCode(),
            ProfileTestAssertions.Year(CopyrightYearPolicyKind.SingleYear, 2026).GetHashCode());
        Assert.False(single.Equals((CopyrightYearPolicy?)null));
        Assert.False(single.Equals(new object()));
    }

    [Fact]
    public void CopyrightYearPolicyRejectsMissingInvalidAndContradictoryCombinations()
    {
        ProfileTestAssertions.Failure(
            CopyrightYearPolicy.Create(null, 2026),
            ProfileValidationError.NullCopyrightYearPolicyKind);
        ProfileTestAssertions.Failure(
            CopyrightYearPolicy.Create(CopyrightYearPolicyKind.SingleYear, null),
            ProfileValidationError.MissingCopyrightYear);
        ProfileTestAssertions.Failure(
            CopyrightYearPolicy.Create(CopyrightYearPolicyKind.SingleYear, 0),
            ProfileValidationError.InvalidCopyrightYear);
        ProfileTestAssertions.Failure(
            CopyrightYearPolicy.Create(CopyrightYearPolicyKind.SingleYear, 10000),
            ProfileValidationError.InvalidCopyrightYear);
        ProfileTestAssertions.Failure(
            CopyrightYearPolicy.Create(CopyrightYearPolicyKind.YearRange, 2026),
            ProfileValidationError.MissingCopyrightStartYear);
        ProfileTestAssertions.Failure(
            CopyrightYearPolicy.Create(CopyrightYearPolicyKind.YearRange, 2026, 0),
            ProfileValidationError.InvalidCopyrightYearRange);
        ProfileTestAssertions.Failure(
            CopyrightYearPolicy.Create(CopyrightYearPolicyKind.YearRange, 2026, 2026),
            ProfileValidationError.InvalidCopyrightYearRange);
        ProfileTestAssertions.Failure(
            CopyrightYearPolicy.Create(CopyrightYearPolicyKind.YearRange, 2026, 2027),
            ProfileValidationError.InvalidCopyrightYearRange);
        ProfileTestAssertions.Failure(
            CopyrightYearPolicy.Create(CopyrightYearPolicyKind.PublicationYear, 2026, 2020),
            ProfileValidationError.UnexpectedCopyrightStartYear);
    }

    [Fact]
    public void CopyrightNoticeRequiresHolderAndPolicyAndUsesValueEquality()
    {
        CopyrightHolder holder = ProfileTestAssertions.Holder();
        CopyrightYearPolicy policy = ProfileTestAssertions.Year();
        CopyrightNotice notice = ProfileTestAssertions.Success(
            CopyrightNotice.Create(holder, policy));

        Assert.Same(holder, notice.Holder);
        Assert.Same(policy, notice.YearPolicy);
        Assert.Equal(notice, ProfileTestAssertions.Copyright());
        Assert.NotEqual(
            notice,
            ProfileTestAssertions.Success(
                CopyrightNotice.Create(ProfileTestAssertions.Holder("Other"), policy)));
        Assert.NotEqual(
            notice,
            ProfileTestAssertions.Success(
                CopyrightNotice.Create(
                    holder,
                    ProfileTestAssertions.Year(
                        CopyrightYearPolicyKind.PublicationYear,
                        2026))));
        Assert.False(notice.Equals((object)ProfileTestAssertions.Success(
            CopyrightNotice.Create(ProfileTestAssertions.Holder("Other"), policy))));
        Assert.Equal(notice.GetHashCode(), ProfileTestAssertions.Copyright().GetHashCode());
        Assert.False(notice.Equals((CopyrightNotice?)null));
        Assert.False(notice.Equals(new object()));
        ProfileTestAssertions.Failure(
            CopyrightNotice.Create(null, policy),
            ProfileValidationError.NullCopyrightHolder);
        ProfileTestAssertions.Failure(
            CopyrightNotice.Create(holder, null),
            ProfileValidationError.NullCopyrightYearPolicyKind);
    }

    [Fact]
    public void AiDisclosureStatesHaveStableOrderAndExactParsing()
    {
        Assert.Equal(
            ["undeclared", "no-ai-assistance", "ai-assisted"],
            AiDisclosureState.All.Select(value => value.CanonicalIdentifier));
        foreach (AiDisclosureState state in AiDisclosureState.All)
        {
            CanonicalIdentifierParseResult<AiDisclosureState> parsed = AiDisclosureState.TryParse(state.CanonicalIdentifier);
            Assert.True(parsed.IsValid);
            Assert.Same(state, parsed.Value);
            Assert.Equal(state.CanonicalIdentifier, state.ToString());
            Assert.NotEqual(
                state,
                AiDisclosureState.All.First(value => !value.Equals(state)));
            Assert.False(state.Equals(
                (object)AiDisclosureState.All.First(value => !value.Equals(state))));
            Assert.Equal(state.GetHashCode(), parsed.Value!.GetHashCode());
            Assert.False(state.Equals((AiDisclosureState?)null));
            Assert.False(state.Equals(new object()));
        }

        Assert.Equal(CanonicalIdentifierParseError.Empty, AiDisclosureState.TryParse("").Error);
        Assert.Equal(
            CanonicalIdentifierParseError.Unknown,
            AiDisclosureState.TryParse("unknown").Error);
    }

    [Fact]
    public void EveryAiDisclosureStateKeepsStateSeparateFromOptionalText()
    {
        AiDisclosure undeclared = ProfileTestAssertions.Disclosure();
        AiDisclosure noAi = ProfileTestAssertions.Disclosure(
            AiDisclosureState.NoAiAssistance);
        AiDisclosure assisted = ProfileTestAssertions.Disclosure(
            AiDisclosureState.AiAssisted,
            "AI-assisted source preparation was reviewed by a human.");

        Assert.Null(undeclared.Text);
        Assert.Null(noAi.Text);
        Assert.Equal(AiDisclosureState.AiAssisted, assisted.State);
        Assert.NotNull(assisted.Text);
        Assert.Equal(
            assisted,
            ProfileTestAssertions.Disclosure(AiDisclosureState.AiAssisted, assisted.Text));
        Assert.NotEqual(assisted, ProfileTestAssertions.Disclosure(AiDisclosureState.AiAssisted));
        Assert.NotEqual(assisted, noAi);
        Assert.False(assisted.Equals((object)noAi));
        Assert.Equal(
            assisted.GetHashCode(),
            ProfileTestAssertions.Disclosure(AiDisclosureState.AiAssisted, assisted.Text)
                .GetHashCode());
        Assert.False(assisted.Equals((AiDisclosure?)null));
        Assert.False(assisted.Equals(new object()));
    }

    [Theory]
    [InlineData("", ProfileValidationError.EmptyDisclosureText)]
    [InlineData(" ", ProfileValidationError.WhitespaceOnlyDisclosureText)]
    [InlineData(" leading", ProfileValidationError.DisclosureTextEdgeWhitespace)]
    [InlineData("trailing ", ProfileValidationError.DisclosureTextEdgeWhitespace)]
    [InlineData("bad\u0000text", ProfileValidationError.DisclosureTextContainsControlCharacter)]
    public void AiDisclosureRejectsInconsistentOrInvalidText(
        string text,
        ProfileValidationError error) =>
        ProfileTestAssertions.Failure(
            AiDisclosure.Create(AiDisclosureState.AiAssisted, text),
            error);

    [Fact]
    public void AiDisclosureRejectsNullStateUndeclaredTextAndUnusuallyLargeText()
    {
        ProfileTestAssertions.Failure(
            AiDisclosure.Create(null),
            ProfileValidationError.NullAiDisclosureState);
        ProfileTestAssertions.Failure(
            AiDisclosure.Create(AiDisclosureState.Undeclared, "A claim"),
            ProfileValidationError.DisclosureTextNotAllowed);
        ProfileTestAssertions.Failure(
            AiDisclosure.Create(
                AiDisclosureState.AiAssisted,
                new string('A', AiDisclosure.MaximumTextLength + 1)),
            ProfileValidationError.DisclosureTextTooLong);
        Assert.Equal(
            AiDisclosure.MaximumTextLength,
            ProfileTestAssertions.Disclosure(
                AiDisclosureState.AiAssisted,
                new string('A', AiDisclosure.MaximumTextLength)).Text!.Length);
    }
}
