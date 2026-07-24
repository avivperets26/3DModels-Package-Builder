using System.Globalization;
using System.Reflection;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Materials;
using PackageBuilder.Domain.Tests.Assets;
using PackageBuilder.Domain.Tests.Textures;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Domain.Tests.Materials;

[Trait("Task", "PB-0104")]
public sealed class MaterialDefinitionTests
{
    [Theory]
    [InlineData(0d, 0d)]
    [InlineData(1d, 1d)]
    [InlineData(0.25d, 0.75d)]
    public void CreatePreservesMetallicAndRoughnessBoundaryFactors(
        double metallic,
        double roughness)
    {
        MaterialDefinition material = CreateValid(
            metallicFactor: metallic,
            roughnessFactor: roughness);

        Assert.Equal(metallic, material.MetallicFactor);
        Assert.Equal(roughness, material.RoughnessFactor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CreatePreservesNormalAoHeightOpacityAndDoubleSidedProperties(int variation)
    {
        MaterialDefinition material = variation switch
        {
            0 => CreateValid(normalScale: 0d, ambientOcclusionStrength: 0d, heightScale: 0d),
            1 => CreateValid(normalScale: double.MaxValue, ambientOcclusionStrength: 1d),
            2 => CreateValid(heightScale: -double.MaxValue, isDoubleSided: true),
            _ => CreateValid(
                opacity: 0d,
                surfaceMode: SurfaceMode.Transparent,
                isDoubleSided: true),
        };

        Assert.Equal(variation is 0 ? 0d : variation is 1 ? double.MaxValue : 1d, material.NormalScale);
        Assert.Equal(variation is 0 ? 0d : 1d, material.AmbientOcclusionStrength);
        Assert.Equal(variation is 2 ? -double.MaxValue : 0d, material.HeightScale);
        Assert.Equal(variation is 3 ? 0d : 1d, material.Opacity);
        Assert.Equal(variation >= 2, material.IsDoubleSided);
    }

    [Fact]
    public void CreatePreservesEmissionAndUvValueObjects()
    {
        EmissionProperties emission = CreateEmission(2d, 3d, 4d, 5d);
        UvTransform uv = CreateUv(-2d, 0d, 3d, -4d);
        MaterialDefinition material = CreateValid(emission: emission, uvTransform: uv);

        Assert.Same(emission, material.Emission);
        Assert.Same(uv, material.UvTransform);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EverySurfaceModeHasAValidDefinition(int index)
    {
        SurfaceMode mode = SurfaceMode.All[index];
        double opacity = mode.Equals(SurfaceMode.Opaque) ? 1d : 0.5d;
        double? cutoff = mode.Equals(SurfaceMode.Cutout) ? 0.5d : null;
        MaterialDefinition material = CreateValid(
            opacity: opacity,
            surfaceMode: mode,
            alphaCutoff: cutoff);

        Assert.Same(mode, material.SurfaceMode);
        Assert.Equal(opacity, material.Opacity);
        Assert.Equal(cutoff, material.AlphaCutoff);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(0.25d)]
    public void CutoutAcceptsEveryAlphaCutoffBoundary(double cutoff) =>
        Assert.Equal(
            cutoff,
            CreateValid(surfaceMode: SurfaceMode.Cutout, alphaCutoff: cutoff).AlphaCutoff);

    [Fact]
    public void SurfaceRelationshipsRejectContradictions()
    {
        AssertInvalid(
            MaterialDefinitionValidationError.OpaqueRequiresFullOpacity,
            opacity: 0.5d);
        AssertInvalid(
            MaterialDefinitionValidationError.AlphaCutoffNotApplicable,
            alphaCutoff: 0.5d);
        AssertInvalid(
            MaterialDefinitionValidationError.CutoutRequiresAlphaCutoff,
            surfaceMode: SurfaceMode.Cutout);
        AssertInvalid(
            MaterialDefinitionValidationError.AlphaCutoffNotApplicable,
            surfaceMode: SurfaceMode.Transparent,
            opacity: 0.5d,
            alphaCutoff: 0.5d);
    }

    [Theory]
    [InlineData(-double.Epsilon)]
    [InlineData(1.0000000000000002d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void CutoutRejectsInvalidAlphaCutoff(double invalid) =>
        AssertInvalid(
            MaterialDefinitionValidationError.InvalidAlphaCutoff,
            surfaceMode: SurfaceMode.Cutout,
            alphaCutoff: invalid);

    [Theory]
    [InlineData(0, -double.Epsilon)]
    [InlineData(0, 1.0000000000000002d)]
    [InlineData(0, double.NaN)]
    [InlineData(0, double.PositiveInfinity)]
    [InlineData(0, double.NegativeInfinity)]
    [InlineData(1, -double.Epsilon)]
    [InlineData(1, 1.0000000000000002d)]
    [InlineData(1, double.NaN)]
    [InlineData(1, double.PositiveInfinity)]
    [InlineData(1, double.NegativeInfinity)]
    [InlineData(3, -double.Epsilon)]
    [InlineData(3, 1.0000000000000002d)]
    [InlineData(3, double.NaN)]
    [InlineData(3, double.PositiveInfinity)]
    [InlineData(3, double.NegativeInfinity)]
    [InlineData(5, -double.Epsilon)]
    [InlineData(5, 1.0000000000000002d)]
    [InlineData(5, double.NaN)]
    [InlineData(5, double.PositiveInfinity)]
    [InlineData(5, double.NegativeInfinity)]
    public void UnitIntervalPropertiesRejectOutOfRangeAndNonFiniteValues(
        int property,
        double invalid)
    {
        MaterialDefinitionValidationError[] errors =
        [
            MaterialDefinitionValidationError.InvalidMetallicFactor,
            MaterialDefinitionValidationError.InvalidRoughnessFactor,
            MaterialDefinitionValidationError.InvalidNormalScale,
            MaterialDefinitionValidationError.InvalidAmbientOcclusionStrength,
            MaterialDefinitionValidationError.InvalidHeightScale,
            MaterialDefinitionValidationError.InvalidOpacity,
        ];

        MaterialDefinitionValidationResult result = property switch
        {
            0 => CreateResult(metallicFactor: invalid),
            1 => CreateResult(roughnessFactor: invalid),
            3 => CreateResult(ambientOcclusionStrength: invalid),
            _ => CreateResult(
                opacity: invalid,
                surfaceMode: SurfaceMode.Transparent),
        };

        MaterialTestAssertions.AssertFailure(result, errors[property]);
    }

    [Theory]
    [InlineData(-double.Epsilon)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NormalScaleRejectsNegativeAndNonFiniteValues(double invalid) =>
        AssertInvalid(MaterialDefinitionValidationError.InvalidNormalScale, normalScale: invalid);

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void HeightScaleRejectsNonFiniteValues(double invalid) =>
        AssertInvalid(MaterialDefinitionValidationError.InvalidHeightScale, heightScale: invalid);

    [Fact]
    public void CreateRejectsNullDependenciesAndCollectionsInValidationOrder()
    {
        MaterialTestAssertions.AssertFailure(
            MaterialDefinition.Create(
                0.5d,
                0.5d,
                1d,
                null,
                1d,
                0d,
                1d,
                SurfaceMode.Opaque,
                null,
                CreateUv(),
                false,
                []),
            MaterialDefinitionValidationError.NullEmission);
        MaterialTestAssertions.AssertFailure(
            MaterialDefinition.Create(
                0.5d,
                0.5d,
                1d,
                CreateEmission(),
                1d,
                0d,
                1d,
                null,
                null,
                CreateUv(),
                false,
                []),
            MaterialDefinitionValidationError.NullSurfaceMode);
        MaterialTestAssertions.AssertFailure(
            MaterialDefinition.Create(
                0.5d,
                0.5d,
                1d,
                CreateEmission(),
                1d,
                0d,
                1d,
                SurfaceMode.Opaque,
                null,
                null,
                false,
                []),
            MaterialDefinitionValidationError.NullUvTransform);
        MaterialTestAssertions.AssertFailure(
            CreateWithTextureAssignments(null),
            MaterialDefinitionValidationError.NullTextureAssignments);
        MaterialTestAssertions.AssertFailure(
            CreateWithTextureAssignments([null]),
            MaterialDefinitionValidationError.NullTextureAssignment);
    }

    [Fact]
    public void TextureAssignmentsSupportEveryCanonicalRoleAndAreCanonicallyOrdered()
    {
        TextureAssignment[] reversed =
        [
            .. TextureRole.All
                .Reverse()
                .Select(
                    role => CreateAssignment(
                        role,
                        includeOriginalFileName: role.Equals(TextureRole.Albedo))),
        ];

        MaterialDefinition material = MaterialTestAssertions.AssertSuccess(
            CreateWithTextureAssignments(reversed));

        Assert.Equal(
            TextureRole.All,
            material.TextureAssignments.Select(assignment => assignment.Role));
        Assert.Equal(TextureRole.All.Count, material.TextureAssignments.Count);
        _ = material.GetHashCode();
    }

    [Fact]
    public void DuplicateTextureRolesAreRejectedEvenWhenSourcesDiffer()
    {
        TextureAssignment first = CreateAssignment(TextureRole.Albedo, "Textures/A.png");
        TextureAssignment second = CreateAssignment(TextureRole.Albedo, "Textures/B.png");

        MaterialTestAssertions.AssertFailure(
            CreateWithTextureAssignments([first, second]),
            MaterialDefinitionValidationError.DuplicateTextureRole);
    }

    [Fact]
    public void TextureAssignmentInputAndOutputAreImmutableSnapshots()
    {
        var input = new List<TextureAssignment?> { CreateAssignment(TextureRole.Roughness) };
        MaterialDefinition material = MaterialTestAssertions.AssertSuccess(
            CreateWithTextureAssignments(input));
        input.Clear();

        _ = Assert.Single(material.TextureAssignments);
        IList<TextureAssignment> output = Assert.IsType<IList<TextureAssignment>>(
            material.TextureAssignments,
            exactMatch: false);
        Assert.True(output.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(
            () => output.Add(CreateAssignment(TextureRole.Metallic)));
    }

    [Fact]
    public void EqualityAndHashingIncludeEveryPropertyAndCanonicalAssignmentOrder()
    {
        MaterialDefinition first = CreateEqualityCandidate();
        MaterialDefinition same = CreateEqualityCandidate(reverseAssignments: true);

        Assert.True(first.Equals(same));
        Assert.True(first.Equals((object)same));
        Assert.False(first.Equals(CreateEqualityCandidate(metallicFactor: 0.6d)));
        Assert.False(first.Equals(CreateEqualityCandidate(roughnessFactor: 0.6d)));
        Assert.False(first.Equals(CreateEqualityCandidate(normalScale: 2d)));
        Assert.False(
            first.Equals(
                CreateEqualityCandidate(emission: CreateEmission(1d, 1d, 1d, 2d))));
        Assert.False(
            first.Equals(
                CreateEqualityCandidate(ambientOcclusionStrength: 0.5d)));
        Assert.False(first.Equals(CreateEqualityCandidate(heightScale: 2d)));
        Assert.False(first.Equals(CreateEqualityCandidate(opacity: 0.5d)));
        Assert.False(first.Equals(CreateEqualityCandidate(alphaCutoff: 0.75d)));
        Assert.False(
            first.Equals(
                CreateEqualityCandidate(uvTransform: CreateUv(2d, 1d, 0d, 0d))));
        Assert.False(first.Equals(CreateEqualityCandidate(isDoubleSided: false)));
        Assert.False(first.Equals(CreateEqualityCandidate(textureAssignments: [])));
        Assert.False(first.Equals((MaterialDefinition?)null));
        Assert.False(first.Equals("material"));
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
    }

    [Fact]
    public void EqualityHashingCasingAndOrderingAreCultureIndependent()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        MaterialDefinition material = CreateValid(
            textureAssignments: [CreateAssignment(TextureRole.Emission, "Textures/FILE.PNG")]);
        int hash = material.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            MaterialDefinition same = CreateValid(
                textureAssignments: [CreateAssignment(TextureRole.Emission, "Textures/FILE.PNG")]);
            MaterialDefinition differentCase = CreateValid(
                textureAssignments: [CreateAssignment(TextureRole.Emission, "Textures/file.png")]);
            Assert.True(material.Equals(same));
            Assert.False(material.Equals(differentCase));
            Assert.Equal(hash, same.GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void DomainAssemblyRetainsRendererAndInfrastructureIndependence()
    {
        Assembly assembly = typeof(MaterialDefinition).Assembly;
        string[] references =
        [
            .. assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty),
        ];
        string[] forbidden =
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
            references,
            reference => forbidden.Any(
                token => reference.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    private static void AssertInvalid(
        MaterialDefinitionValidationError error,
        double metallicFactor = 0.5d,
        double roughnessFactor = 0.5d,
        double normalScale = 1d,
        double ambientOcclusionStrength = 1d,
        double heightScale = 0d,
        double opacity = 1d,
        SurfaceMode? surfaceMode = null,
        double? alphaCutoff = null) =>
        MaterialTestAssertions.AssertFailure(
            CreateResult(
                metallicFactor,
                roughnessFactor,
                normalScale,
                ambientOcclusionStrength: ambientOcclusionStrength,
                heightScale: heightScale,
                opacity: opacity,
                surfaceMode: surfaceMode ?? SurfaceMode.Opaque,
                alphaCutoff: alphaCutoff),
            error);

    private static MaterialDefinition CreateValid(
        double metallicFactor = 0.5d,
        double roughnessFactor = 0.5d,
        double normalScale = 1d,
        EmissionProperties? emission = null,
        double ambientOcclusionStrength = 1d,
        double heightScale = 0d,
        double opacity = 1d,
        SurfaceMode? surfaceMode = null,
        double? alphaCutoff = null,
        UvTransform? uvTransform = null,
        bool isDoubleSided = false,
        IEnumerable<TextureAssignment?>? textureAssignments = null) =>
        MaterialTestAssertions.AssertSuccess(
            CreateResult(
                metallicFactor,
                roughnessFactor,
                normalScale,
                emission ?? CreateEmission(),
                ambientOcclusionStrength,
                heightScale,
                opacity,
                surfaceMode ?? SurfaceMode.Opaque,
                alphaCutoff,
                uvTransform ?? CreateUv(),
                isDoubleSided,
                textureAssignments ?? []));

    private static MaterialDefinition CreateEqualityCandidate(
        double metallicFactor = 0.5d,
        double roughnessFactor = 0.5d,
        double normalScale = 1d,
        EmissionProperties? emission = null,
        double ambientOcclusionStrength = 1d,
        double heightScale = 0d,
        double opacity = 1d,
        double alphaCutoff = 0.25d,
        UvTransform? uvTransform = null,
        bool isDoubleSided = true,
        IEnumerable<TextureAssignment?>? textureAssignments = null,
        bool reverseAssignments = false)
    {
        IEnumerable<TextureAssignment?> assignments = textureAssignments ??
            (reverseAssignments
                ?
                [
                    CreateAssignment(TextureRole.Roughness),
                    CreateAssignment(TextureRole.Albedo),
                ]
                :
                [
                    CreateAssignment(TextureRole.Albedo),
                    CreateAssignment(TextureRole.Roughness),
                ]);

        return CreateValid(
            metallicFactor,
            roughnessFactor,
            normalScale,
            emission ?? CreateEmission(),
            ambientOcclusionStrength,
            heightScale,
            opacity,
            SurfaceMode.Cutout,
            alphaCutoff,
            uvTransform ?? CreateUv(),
            isDoubleSided,
            assignments);
    }

    private static MaterialDefinitionValidationResult CreateResult(
        double metallicFactor = 0.5d,
        double roughnessFactor = 0.5d,
        double normalScale = 1d,
        EmissionProperties? emission = null,
        double ambientOcclusionStrength = 1d,
        double heightScale = 0d,
        double opacity = 1d,
        SurfaceMode? surfaceMode = null,
        double? alphaCutoff = null,
        UvTransform? uvTransform = null,
        bool isDoubleSided = false,
        IEnumerable<TextureAssignment?>? textureAssignments = null) =>
        MaterialDefinition.Create(
            metallicFactor,
            roughnessFactor,
            normalScale,
            emission ?? CreateEmission(),
            ambientOcclusionStrength,
            heightScale,
            opacity,
            surfaceMode ?? SurfaceMode.Opaque,
            alphaCutoff,
            uvTransform ?? CreateUv(),
            isDoubleSided,
            textureAssignments ?? []);

    private static MaterialDefinitionValidationResult CreateWithTextureAssignments(
        IEnumerable<TextureAssignment?>? assignments) =>
        MaterialDefinition.Create(
            0.5d,
            0.5d,
            1d,
            CreateEmission(),
            1d,
            0d,
            1d,
            SurfaceMode.Opaque,
            null,
            CreateUv(),
            false,
            assignments);

    private static EmissionProperties CreateEmission(
        double red = 0d,
        double green = 0d,
        double blue = 0d,
        double intensity = 0d) =>
        MaterialTestAssertions.AssertSuccess(
            EmissionProperties.Create(red, green, blue, intensity));

    private static UvTransform CreateUv(
        double scaleU = 1d,
        double scaleV = 1d,
        double offsetU = 0d,
        double offsetV = 0d) =>
        MaterialTestAssertions.AssertSuccess(
            UvTransform.Create(scaleU, scaleV, offsetU, offsetV));

    private static TextureAssignment CreateAssignment(
        TextureRole role,
        string? reference = null,
        bool includeOriginalFileName = false)
    {
        string logicalReference = reference ?? $"Textures/{role.CanonicalIdentifier}.png";
        SourceAsset image = SourceAssetTestAssertions.AssertSuccess(
            SourceAsset.Create(
                SourceAssetKind.Image,
                logicalReference,
                includeOriginalFileName ? logicalReference[(logicalReference.LastIndexOf('/') + 1)..] : null));
        return TextureAssignmentTestAssertions.AssertSuccess(
            TextureAssignment.Create(
                image,
                role,
                role.RequiredColourSpace,
                role.IsNormalMapData ? NormalConvention.Auto : null));
    }
}
