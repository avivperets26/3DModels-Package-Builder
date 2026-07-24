using System.Globalization;
using System.Reflection;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Tests.Assets;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Domain.Tests.Textures;

[Trait("Task", "PB-0103")]
public sealed class TextureAssignmentTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void CreateAcceptsEveryValidRoleColourSpacePair(
        int roleIndex)
    {
        TextureRole role = TextureRole.All[roleIndex];
        ColourSpace colourSpace = role.RequiredColourSpace;
        NormalConvention? convention = role.IsNormalMapData
            ? NormalConvention.Auto
            : null;
        SourceAsset image = CreateSource(SourceAssetKind.Image, $"Textures/{role}.png");

        TextureAssignment assignment = TextureAssignmentTestAssertions.AssertSuccess(
            TextureAssignment.Create(image, role, colourSpace, convention));

        Assert.Same(image, assignment.SourceAsset);
        Assert.Same(role, assignment.Role);
        Assert.Same(colourSpace, assignment.ColourSpace);
        Assert.Same(convention, assignment.NormalConvention);
        Assert.Equal($"{role.CanonicalIdentifier}:{image.LogicalReference}", assignment.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void CreateRejectsEveryInvalidRoleColourSpacePair(
        int roleIndex)
    {
        TextureRole role = TextureRole.All[roleIndex];
        ColourSpace colourSpace = role.RequiredColourSpace.Equals(ColourSpace.Srgb)
            ? ColourSpace.Linear
            : ColourSpace.Srgb;

        TextureAssignmentTestAssertions.AssertFailure(
            TextureAssignment.Create(CreateImage(), role, colourSpace, NormalConvention.Auto),
            TextureAssignmentValidationError.IncompatibleColourSpace);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NormalAssignmentAcceptsEveryNormalConvention(int index)
    {
        TextureAssignment assignment = TextureAssignmentTestAssertions.AssertSuccess(
            TextureAssignment.Create(
                CreateImage(),
                TextureRole.Normal,
                ColourSpace.Linear,
                NormalConvention.All[index]));

        Assert.Same(NormalConvention.All[index], assignment.NormalConvention);
        Assert.True(assignment.Role.IsNormalMapData);
    }

    [Fact]
    public void CreateRejectsNullAndContradictoryDependenciesInStableOrder()
    {
        SourceAsset image = CreateImage();
        SourceAsset fbx = CreateSource(SourceAssetKind.Fbx, "model.fbx");

        TextureAssignmentTestAssertions.AssertFailure(
            TextureAssignment.Create(null, null, null),
            TextureAssignmentValidationError.NullSourceAsset);
        TextureAssignmentTestAssertions.AssertFailure(
            TextureAssignment.Create(fbx, null, null),
            TextureAssignmentValidationError.SourceAssetIsNotImage);
        TextureAssignmentTestAssertions.AssertFailure(
            TextureAssignment.Create(image, null, null),
            TextureAssignmentValidationError.NullRole);
        TextureAssignmentTestAssertions.AssertFailure(
            TextureAssignment.Create(image, TextureRole.Albedo, null),
            TextureAssignmentValidationError.NullColourSpace);
        TextureAssignmentTestAssertions.AssertFailure(
            TextureAssignment.Create(
                image,
                TextureRole.Normal,
                ColourSpace.Linear),
            TextureAssignmentValidationError.MissingNormalConvention);
        TextureAssignmentTestAssertions.AssertFailure(
            TextureAssignment.Create(
                image,
                TextureRole.Albedo,
                ColourSpace.Srgb,
                NormalConvention.DirectX),
            TextureAssignmentValidationError.NormalConventionNotApplicable);
    }

    [Fact]
    public void EqualityAndHashingIncludeEveryAssignmentField()
    {
        TextureAssignment first = CreateAssignment(
            CreateImage("Textures/Normal.png"),
            TextureRole.Normal,
            ColourSpace.Linear,
            NormalConvention.OpenGl);
        TextureAssignment same = CreateAssignment(
            CreateImage("Textures/Normal.png"),
            TextureRole.Normal,
            ColourSpace.Linear,
            NormalConvention.OpenGl);
        TextureAssignment differentSource = CreateAssignment(
            CreateImage("Textures/normal.png"),
            TextureRole.Normal,
            ColourSpace.Linear,
            NormalConvention.OpenGl);
        TextureAssignment differentRole = CreateAssignment(
            CreateImage("Textures/Normal.png"),
            TextureRole.Height,
            ColourSpace.Linear,
            null);
        TextureAssignment differentColour = CreateAssignment(
            CreateImage("Textures/Normal.png"),
            TextureRole.Albedo,
            ColourSpace.Srgb,
            null);
        TextureAssignment differentConvention = CreateAssignment(
            CreateImage("Textures/Normal.png"),
            TextureRole.Normal,
            ColourSpace.Linear,
            NormalConvention.DirectX);

        Assert.True(first.Equals(same));
        Assert.True(first.Equals((object)same));
        Assert.False(first.Equals(differentSource));
        Assert.False(first.Equals(differentRole));
        Assert.False(first.Equals(differentColour));
        Assert.False(first.Equals(differentConvention));
        Assert.False(first.Equals((TextureAssignment?)null));
        Assert.False(first.Equals("normal"));
        Assert.Equal(first.GetHashCode(), same.GetHashCode());

        SourceAsset sourceWithOriginal = SourceAssetTestAssertions.AssertSuccess(
            SourceAsset.Create(
                SourceAssetKind.Image,
                "Textures/WithOriginal.png",
                "WithOriginal.png"));
        _ = CreateAssignment(
            sourceWithOriginal,
            TextureRole.Albedo,
            ColourSpace.Srgb,
            null).GetHashCode();
    }

    [Fact]
    public void EqualityAndHashingAreCultureInvariant()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        TextureAssignment assignment = CreateAssignment(
            CreateImage("Textures/FILE.PNG"),
            TextureRole.Albedo,
            ColourSpace.Srgb,
            null);
        int originalHash = assignment.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            TextureAssignment same = CreateAssignment(
                CreateImage("Textures/FILE.PNG"),
                TextureRole.Albedo,
                ColourSpace.Srgb,
                null);
            Assert.True(assignment.Equals(same));
            Assert.Equal(originalHash, same.GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void DomainAssemblyHasNoEngineAdapterFilesystemOrPackageDependency()
    {
        Assembly assembly = typeof(TextureAssignment).Assembly;
        string[] referencedAssemblies = [.. assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)];
        string[] forbiddenTokens =
        [
            "Blender",
            "Unity",
            "Unreal",
            "WPF",
            "Presentation",
            "PackageBuilder.Targets",
            "PackageBuilder.Marketplaces",
            "PackageBuilder.Infrastructure",
            "System.IO.FileSystem",
            "System.Net",
        ];

        Assert.DoesNotContain(
            referencedAssemblies,
            reference => forbiddenTokens.Any(
                token => reference.Contains(token, StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(
            typeof(TextureAssignment).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>(),
            attribute => attribute.Key == "PackageReference");
    }

    private static SourceAsset CreateImage(string reference = "Textures/Image.png") =>
        CreateSource(SourceAssetKind.Image, reference);

    private static SourceAsset CreateSource(SourceAssetKind kind, string reference) =>
        SourceAssetTestAssertions.AssertSuccess(SourceAsset.Create(kind, reference));

    private static TextureAssignment CreateAssignment(
        SourceAsset image,
        TextureRole role,
        ColourSpace colourSpace,
        NormalConvention? convention) =>
        TextureAssignmentTestAssertions.AssertSuccess(
            TextureAssignment.Create(image, role, colourSpace, convention));
}
