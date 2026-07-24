using System.Globalization;
using PackageBuilder.Domain.Identifiers;
using PackageBuilder.Domain.Tests.Identifiers;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Domain.Tests.Textures;

[Trait("Task", "PB-0103")]
public sealed class TextureIdentityTests
{
    private static readonly string[] _roleIdentifiers =
    [
        "albedo",
        "normal",
        "metallic",
        "roughness",
        "emission",
        "ambient-occlusion",
        "opacity",
        "height",
    ];

    [Fact]
    public void RolesHaveExactCanonicalNamesOrderAndCompatibility()
    {
        string[] expectedNames =
        [
            "Albedo",
            "Normal",
            "Metallic",
            "Roughness",
            "Emission",
            "Ambient Occlusion",
            "Opacity",
            "Height",
        ];
        ColourSpace[] expectedSpaces =
        [
            ColourSpace.Srgb,
            ColourSpace.Linear,
            ColourSpace.Linear,
            ColourSpace.Linear,
            ColourSpace.Srgb,
            ColourSpace.Linear,
            ColourSpace.Linear,
            ColourSpace.Linear,
        ];

        Assert.Equal(_roleIdentifiers, TextureRole.All.Select(role => role.CanonicalIdentifier));
        Assert.Equal(expectedNames, TextureRole.All.Select(role => role.DisplayName));
        Assert.Equal(expectedSpaces, TextureRole.All.Select(role => role.RequiredColourSpace));
        Assert.Equal(
            [false, true, false, false, false, false, false, false],
            TextureRole.All.Select(role => role.IsNormalMapData));
        Assert.Equal("Albedo", TextureRole.Albedo.DisplayName);
        Assert.DoesNotContain(TextureRole.All, role => role.DisplayName == "Albeado");
    }

    [Theory]
    [InlineData("albedo", 0)]
    [InlineData("normal", 1)]
    [InlineData("metallic", 2)]
    [InlineData("roughness", 3)]
    [InlineData("emission", 4)]
    [InlineData("ambient-occlusion", 5)]
    [InlineData("opacity", 6)]
    [InlineData("height", 7)]
    public void RoleParsingReturnsCanonicalSingleton(string identifier, int index)
    {
        TextureRole role = CanonicalIdentifierTestAssertions.AssertSuccess(
            TextureRole.TryParse(identifier));

        Assert.Same(TextureRole.All[index], role);
        Assert.Equal(identifier, role.ToString());
    }

    [Theory]
    [InlineData(null, CanonicalIdentifierParseError.Null)]
    [InlineData("", CanonicalIdentifierParseError.Empty)]
    [InlineData(" ", CanonicalIdentifierParseError.WhitespaceOnly)]
    [InlineData("Albedo", CanonicalIdentifierParseError.Malformed)]
    [InlineData("Albeado", CanonicalIdentifierParseError.Malformed)]
    [InlineData("albeado", CanonicalIdentifierParseError.Unknown)]
    [InlineData("base-color", CanonicalIdentifierParseError.Unknown)]
    [InlineData("diffuse", CanonicalIdentifierParseError.Unknown)]
    [InlineData("metallic-smoothness", CanonicalIdentifierParseError.Unknown)]
    [InlineData("orm", CanonicalIdentifierParseError.Unknown)]
    public void RoleParsingRejectsInvalidAliasesAndTargetPackingRoles(
        string? identifier,
        CanonicalIdentifierParseError expectedError) =>
        CanonicalIdentifierTestAssertions.AssertFailure(
            TextureRole.TryParse(identifier),
            expectedError);

    [Fact]
    public void ColourSpacesHaveStableIdentitiesAndParsing()
    {
        Assert.Equal(["srgb", "linear"], ColourSpace.All.Select(space => space.CanonicalIdentifier));
        Assert.Equal(["sRGB", "Linear"], ColourSpace.All.Select(space => space.DisplayName));
        Assert.Same(
            ColourSpace.Srgb,
            CanonicalIdentifierTestAssertions.AssertSuccess(ColourSpace.TryParse("srgb")));
        Assert.Same(
            ColourSpace.Linear,
            CanonicalIdentifierTestAssertions.AssertSuccess(ColourSpace.TryParse("linear")));
        CanonicalIdentifierTestAssertions.AssertFailure(
            ColourSpace.TryParse("sRGB"),
            CanonicalIdentifierParseError.Malformed);
        CanonicalIdentifierTestAssertions.AssertFailure(
            ColourSpace.TryParse("gamma"),
            CanonicalIdentifierParseError.Unknown);
    }

    [Fact]
    public void NormalConventionsHaveStableIdentitiesAndParsing()
    {
        Assert.Equal(
            ["auto", "open-gl", "direct-x"],
            NormalConvention.All.Select(convention => convention.CanonicalIdentifier));
        Assert.Equal(
            ["Auto", "OpenGL", "DirectX"],
            NormalConvention.All.Select(convention => convention.DisplayName));

        for (int index = 0; index < NormalConvention.All.Count; index++)
        {
            NormalConvention parsed = CanonicalIdentifierTestAssertions.AssertSuccess(
                NormalConvention.TryParse(
                    NormalConvention.All[index].CanonicalIdentifier));
            Assert.Same(NormalConvention.All[index], parsed);
        }

        CanonicalIdentifierTestAssertions.AssertFailure(
            NormalConvention.TryParse("OpenGL"),
            CanonicalIdentifierParseError.Malformed);
        CanonicalIdentifierTestAssertions.AssertFailure(
            NormalConvention.TryParse("unknown"),
            CanonicalIdentifierParseError.Unknown);
    }

    [Fact]
    public void EqualityHashingAndParsingAreOrdinalAndCultureInvariant()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        int roleHash = TextureRole.Metallic.GetHashCode();
        int colourHash = ColourSpace.Linear.GetHashCode();
        int conventionHash = NormalConvention.DirectX.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            TextureRole role = CanonicalIdentifierTestAssertions.AssertSuccess(
                TextureRole.TryParse("metallic"));
            ColourSpace colour = CanonicalIdentifierTestAssertions.AssertSuccess(
                ColourSpace.TryParse("linear"));
            NormalConvention convention = CanonicalIdentifierTestAssertions.AssertSuccess(
                NormalConvention.TryParse("direct-x"));

            Assert.True(TextureRole.Metallic.Equals(role));
            Assert.True(TextureRole.Metallic.Equals((object)role));
            Assert.False(TextureRole.Metallic.Equals(TextureRole.Roughness));
            Assert.False(TextureRole.Metallic.Equals((TextureRole?)null));
            Assert.False(TextureRole.Metallic.Equals("metallic"));
            Assert.Equal(roleHash, role.GetHashCode());

            Assert.True(ColourSpace.Linear.Equals(colour));
            Assert.True(ColourSpace.Linear.Equals((object)colour));
            Assert.False(ColourSpace.Linear.Equals(ColourSpace.Srgb));
            Assert.False(ColourSpace.Linear.Equals((ColourSpace?)null));
            Assert.False(ColourSpace.Linear.Equals("linear"));
            Assert.Equal(colourHash, colour.GetHashCode());
            Assert.Equal("linear", colour.ToString());

            Assert.True(NormalConvention.DirectX.Equals(convention));
            Assert.True(NormalConvention.DirectX.Equals((object)convention));
            Assert.False(NormalConvention.DirectX.Equals(NormalConvention.OpenGl));
            Assert.False(NormalConvention.DirectX.Equals((NormalConvention?)null));
            Assert.False(NormalConvention.DirectX.Equals("direct-x"));
            Assert.Equal(conventionHash, convention.GetHashCode());
            Assert.Equal("direct-x", convention.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void IdentityRegistriesCannotBeMutated()
    {
        AssertReadOnly(TextureRole.All, TextureRole.Albedo);
        AssertReadOnly(ColourSpace.All, ColourSpace.Srgb);
        AssertReadOnly(NormalConvention.All, NormalConvention.Auto);
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> values, T value)
    {
        IList<T> list = Assert.IsType<IList<T>>(values, exactMatch: false);
        Assert.True(list.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => list.Add(value));
    }
}
