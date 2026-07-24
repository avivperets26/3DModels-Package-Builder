using System.Globalization;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Identifiers;
using PackageBuilder.Domain.Tests.Identifiers;

namespace PackageBuilder.Domain.Tests.Assets;

[Trait("Task", "PB-0103")]
public sealed class SourceAssetKindTests
{
    private static readonly string[] _expectedOrder = ["fbx", "glb", "archive", "image"];

    [Fact]
    public void AllContainsExactlyTheFourKindsInStableOrder()
    {
        Assert.Equal(
            _expectedOrder,
            SourceAssetKind.All.Select(kind => kind.CanonicalIdentifier));
        Assert.Same(SourceAssetKind.Fbx, SourceAssetKind.All[0]);
        Assert.Same(SourceAssetKind.Glb, SourceAssetKind.All[1]);
        Assert.Same(SourceAssetKind.Archive, SourceAssetKind.All[2]);
        Assert.Same(SourceAssetKind.Image, SourceAssetKind.All[3]);
    }

    [Theory]
    [InlineData("fbx", 0)]
    [InlineData("glb", 1)]
    [InlineData("archive", 2)]
    [InlineData("image", 3)]
    public void TryParseReturnsCanonicalSingleton(string identifier, int expectedIndex)
    {
        SourceAssetKind kind = CanonicalIdentifierTestAssertions.AssertSuccess(
            SourceAssetKind.TryParse(identifier));

        Assert.Same(SourceAssetKind.All[expectedIndex], kind);
        Assert.Equal(identifier, kind.CanonicalIdentifier);
        Assert.Equal(identifier, kind.ToString());
    }

    [Theory]
    [InlineData(null, CanonicalIdentifierParseError.Null)]
    [InlineData("", CanonicalIdentifierParseError.Empty)]
    [InlineData(" \t", CanonicalIdentifierParseError.WhitespaceOnly)]
    [InlineData("Fbx", CanonicalIdentifierParseError.Malformed)]
    [InlineData("source/image", CanonicalIdentifierParseError.Malformed)]
    [InlineData("texture", CanonicalIdentifierParseError.Unknown)]
    public void TryParseRejectsInvalidOrUnknownIdentifiers(
        string? identifier,
        CanonicalIdentifierParseError expectedError) =>
        CanonicalIdentifierTestAssertions.AssertFailure(
            SourceAssetKind.TryParse(identifier),
            expectedError);

    [Fact]
    public void EqualityHashingAndOrderAreOrdinalAndCultureInvariant()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        int originalHash = SourceAssetKind.Image.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            SourceAssetKind same = CanonicalIdentifierTestAssertions.AssertSuccess(
                SourceAssetKind.TryParse("image"));
            Assert.True(SourceAssetKind.Image.Equals(same));
            Assert.True(SourceAssetKind.Image.Equals((object)same));
            Assert.False(SourceAssetKind.Image.Equals(SourceAssetKind.Fbx));
            Assert.False(SourceAssetKind.Image.Equals((SourceAssetKind?)null));
            Assert.False(SourceAssetKind.Image.Equals("image"));
            Assert.Equal(originalHash, same.GetHashCode());
            Assert.Equal(
                _expectedOrder,
                SourceAssetKind.All.Select(kind => kind.CanonicalIdentifier));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void RegistryCannotBeMutated()
    {
        IList<SourceAssetKind> list = Assert.IsType<IList<SourceAssetKind>>(
            SourceAssetKind.All,
            exactMatch: false);

        Assert.True(list.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => list.Add(SourceAssetKind.Image));
    }
}
