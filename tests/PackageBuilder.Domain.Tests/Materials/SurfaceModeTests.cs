using System.Globalization;
using PackageBuilder.Domain.Identifiers;
using PackageBuilder.Domain.Materials;
using PackageBuilder.Domain.Tests.Identifiers;

namespace PackageBuilder.Domain.Tests.Materials;

[Trait("Task", "PB-0104")]
public sealed class SurfaceModeTests
{
    [Fact]
    public void ModesExposeStableCanonicalIdentityAndOrder()
    {
        Assert.Equal(
            ["opaque", "cutout", "transparent"],
            SurfaceMode.All.Select(mode => mode.CanonicalIdentifier));
        Assert.Equal(
            ["Opaque", "Cutout", "Transparent"],
            SurfaceMode.All.Select(mode => mode.DisplayName));

        for (int index = 0; index < SurfaceMode.All.Count; index++)
        {
            SurfaceMode parsed = CanonicalIdentifierTestAssertions.AssertSuccess(
                SurfaceMode.TryParse(SurfaceMode.All[index].CanonicalIdentifier));
            Assert.Same(SurfaceMode.All[index], parsed);
            Assert.Equal(parsed.CanonicalIdentifier, parsed.ToString());
        }
    }

    [Theory]
    [InlineData(null, CanonicalIdentifierParseError.Null)]
    [InlineData("", CanonicalIdentifierParseError.Empty)]
    [InlineData(" ", CanonicalIdentifierParseError.WhitespaceOnly)]
    [InlineData("Opaque", CanonicalIdentifierParseError.Malformed)]
    [InlineData("alpha-blend", CanonicalIdentifierParseError.Unknown)]
    public void ParsingRejectsNonCanonicalInput(
        string? input,
        CanonicalIdentifierParseError error) =>
        CanonicalIdentifierTestAssertions.AssertFailure(SurfaceMode.TryParse(input), error);

    [Fact]
    public void EqualityHashingAndParsingAreOrdinalAndCultureIndependent()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        int hash = SurfaceMode.Transparent.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            SurfaceMode parsed = CanonicalIdentifierTestAssertions.AssertSuccess(
                SurfaceMode.TryParse("transparent"));

            Assert.True(SurfaceMode.Transparent.Equals(parsed));
            Assert.True(SurfaceMode.Transparent.Equals((object)parsed));
            Assert.False(SurfaceMode.Transparent.Equals(SurfaceMode.Opaque));
            Assert.False(SurfaceMode.Transparent.Equals((SurfaceMode?)null));
            Assert.False(SurfaceMode.Transparent.Equals("transparent"));
            Assert.Equal(hash, parsed.GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void RegistryIsImmutable()
    {
        IList<SurfaceMode> list = Assert.IsType<IList<SurfaceMode>>(
            SurfaceMode.All,
            exactMatch: false);
        Assert.True(list.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => list.Add(SurfaceMode.Opaque));
    }
}
