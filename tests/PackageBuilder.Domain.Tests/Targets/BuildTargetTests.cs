using System.Globalization;
using PackageBuilder.Domain.Identifiers;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Tests.Identifiers;

namespace PackageBuilder.Domain.Tests.Targets;

[Trait("Task", "PB-0102")]
public sealed class BuildTargetTests
{
    private static readonly string[] _expectedCanonicalOrder =
    [
        "portable",
        "unity",
        "unreal",
    ];

    [Fact]
    public void AllContainsExactlyTheThreeTargetsInStableCanonicalOrder()
    {
        Assert.Equal(
            _expectedCanonicalOrder,
            BuildTarget.All.Select(target => target.CanonicalIdentifier));

        Assert.Same(BuildTarget.Portable, BuildTarget.All[0]);
        Assert.Same(BuildTarget.Unity, BuildTarget.All[1]);
        Assert.Same(BuildTarget.Unreal, BuildTarget.All[2]);
    }

    [Theory]
    [InlineData("portable", 0)]
    [InlineData("unity", 1)]
    [InlineData("unreal", 2)]
    public void TryParseReturnsTheCanonicalSingleton(string identifier, int expectedIndex)
    {
        BuildTarget parsed = CanonicalIdentifierTestAssertions.AssertSuccess(
            BuildTarget.TryParse(identifier));

        Assert.Same(BuildTarget.All[expectedIndex], parsed);
        Assert.Equal(identifier, parsed.CanonicalIdentifier);
        Assert.Equal(identifier, parsed.ToString());
    }

    [Theory]
    [InlineData(null, CanonicalIdentifierParseError.Null)]
    [InlineData("", CanonicalIdentifierParseError.Empty)]
    [InlineData("\r\n", CanonicalIdentifierParseError.WhitespaceOnly)]
    [InlineData(" portable", CanonicalIdentifierParseError.Malformed)]
    [InlineData("portable ", CanonicalIdentifierParseError.Malformed)]
    [InlineData("Portable", CanonicalIdentifierParseError.Malformed)]
    [InlineData("UNITY", CanonicalIdentifierParseError.Malformed)]
    [InlineData("-unity", CanonicalIdentifierParseError.Malformed)]
    [InlineData("unreal-", CanonicalIdentifierParseError.Malformed)]
    [InlineData("unreal--engine", CanonicalIdentifierParseError.Malformed)]
    [InlineData("unity_target", CanonicalIdentifierParseError.Malformed)]
    [InlineData("target/unity", CanonicalIdentifierParseError.Malformed)]
    [InlineData("únity", CanonicalIdentifierParseError.Malformed)]
    [InlineData("blender", CanonicalIdentifierParseError.Unknown)]
    [InlineData("fab", CanonicalIdentifierParseError.Unknown)]
    [InlineData("portabel", CanonicalIdentifierParseError.Unknown)]
    public void TryParseRejectsInvalidOrUnsupportedIdentifiers(
        string? identifier,
        CanonicalIdentifierParseError expectedError) =>
        CanonicalIdentifierTestAssertions.AssertFailure(
            BuildTarget.TryParse(identifier),
            expectedError);

    [Fact]
    public void EqualityAndHashingUseStableOrdinalIdentifiers()
    {
        BuildTarget parsedPortable = CanonicalIdentifierTestAssertions.AssertSuccess(
            BuildTarget.TryParse("portable"));

        Assert.True(BuildTarget.Portable.Equals(parsedPortable));
        Assert.True(BuildTarget.Portable.Equals((object)parsedPortable));
        Assert.False(BuildTarget.Portable.Equals(BuildTarget.Unity));
        Assert.False(BuildTarget.Portable.Equals((BuildTarget?)null));
        Assert.False(BuildTarget.Portable.Equals("portable"));
        Assert.Equal(BuildTarget.Portable.GetHashCode(), parsedPortable.GetHashCode());
    }

    [Fact]
    public void ParsingOrderingEqualityAndHashingAreCultureInvariant()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        string[] originalOrder =
        [
            .. BuildTarget.All.Select(target => target.CanonicalIdentifier),
        ];
        int originalHash = BuildTarget.Unity.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            BuildTarget parsed = CanonicalIdentifierTestAssertions.AssertSuccess(
                BuildTarget.TryParse("unity"));
            Assert.Same(BuildTarget.Unity, parsed);
            Assert.Equal(originalHash, parsed.GetHashCode());
            Assert.Equal(
                originalOrder,
                BuildTarget.All.Select(target => target.CanonicalIdentifier));
            CanonicalIdentifierTestAssertions.AssertFailure(
                BuildTarget.TryParse("UNITY"),
                CanonicalIdentifierParseError.Malformed);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void PortableUnityAndUnrealRemainDistinct()
    {
        Assert.NotEqual(BuildTarget.Portable, BuildTarget.Unity);
        Assert.NotEqual(BuildTarget.Unity, BuildTarget.Unreal);
        Assert.NotEqual(BuildTarget.Portable, BuildTarget.Unreal);
    }

    [Fact]
    public void SupportedTargetRegistryCannotBeMutated()
    {
        IList<BuildTarget> list = Assert.IsType<IList<BuildTarget>>(
            BuildTarget.All,
            exactMatch: false);

        Assert.True(list.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => list.Add(BuildTarget.Portable));
    }
}
