using System.Globalization;
using System.Reflection;
using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Preview;
using PackageBuilder.Domain.Products;

namespace PackageBuilder.Domain.Tests.Preview;

[Trait("Task", "PB-0906")]
public sealed class PreviewPresentationSpecificationTests
{
    [Fact]
    public void ViewRolesHaveStableIdentitiesAndRequiredProjections()
    {
        Assert.Equal(
            [
                "hero",
                "orthographic-front",
                "orthographic-back",
                "orthographic-left",
                "orthographic-right",
                "detail",
                "animation-pose",
                "set-overview",
                "collection-overview",
            ],
            PreviewViewKind.All.Select(kind => kind.CanonicalIdentifier));
        Assert.Equal(
            4,
            PreviewViewKind.All.Count(kind => kind.Projection == PreviewProjection.Orthographic));
        Assert.All(
            PreviewViewKind.All.Where(kind => kind.CanonicalIdentifier.StartsWith(
                "orthographic-",
                StringComparison.Ordinal)),
            kind => Assert.Equal(PreviewProjection.Orthographic, kind.Projection));
        Assert.Equal(PreviewProjection.Perspective, PreviewViewKind.Hero.Projection);
        Assert.Equal("hero", PreviewViewKind.Hero.ToString());
    }

    [Theory]
    [InlineData("static")]
    [InlineData("rigged")]
    public void NonAnimatedProductDefaultsUseFiveApprovedCaptureViews(string productCaseIdentifier)
    {
        ProductCase productCase = ProductCase.All.Single(
            value => value.CanonicalIdentifier == productCaseIdentifier);
        PreviewPresentationSpecification specification = PreviewTestAssertions.AssertSuccess(
            PreviewPresentationDefaults.Create(productCase));

        Assert.Same(productCase, specification.ProductCase);
        Assert.Equal(
            [
                PreviewViewKind.Hero,
                PreviewViewKind.OrthographicFront,
                PreviewViewKind.OrthographicBack,
                PreviewViewKind.OrthographicLeft,
                PreviewViewKind.OrthographicRight,
            ],
            specification.Views.Select(view => view.Kind));
        Assert.All(
            specification.Views,
            view => Assert.Same(PreviewVisibility.EntireProduct, view.Visibility));
        Assert.Same(PreviewPresentationDefaults.Background, specification.Background);
        Assert.Same(PreviewPresentationDefaults.Lighting, specification.Lighting);
    }

    [Fact]
    public void AnimatedDefaultAddsRequiredAnimationPoseWithoutTransportState()
    {
        PreviewPresentationSpecification specification = PreviewTestAssertions.AssertSuccess(
            PreviewPresentationDefaults.Create(ProductCase.RiggedAnimated));

        Assert.Equal(6, specification.Views.Count);
        Assert.Same(PreviewViewKind.AnimationPose, specification.Views[^1].Kind);
        Assert.DoesNotContain(
            typeof(PreviewPresentationSpecification).GetProperties(),
            property => property.Name.Contains("Clip", StringComparison.Ordinal) ||
                property.Name.Contains("Playback", StringComparison.Ordinal));
    }

    [Fact]
    public void StableViewIdsAllowMultipleDetailAndAnimationPoseViews()
    {
        PreviewViewDefinition hero = PreviewTestAssertions.View(PreviewViewKind.Hero);
        PreviewViewDefinition firstPose = PreviewTestAssertions.View(
            PreviewViewKind.AnimationPose,
            id: "AnimationPoseAttack");
        PreviewViewDefinition secondPose = PreviewTestAssertions.View(
            PreviewViewKind.AnimationPose,
            id: "AnimationPoseIdle");
        PreviewViewDefinition detail = PreviewTestAssertions.View(
            PreviewViewKind.Detail,
            id: "DetailMaterial");

        PreviewPresentationSpecification specification = PreviewTestAssertions.AssertSuccess(
            PreviewPresentationSpecification.Create(
                ProductCase.RiggedAnimated,
                [hero, firstPose, secondPose, detail],
                PreviewPresentationDefaults.Background,
                PreviewPresentationDefaults.Lighting));

        Assert.Equal(
            ["Hero", "AnimationPoseAttack", "AnimationPoseIdle", "DetailMaterial"],
            specification.Views.Select(view => view.Id.Value));
    }

    [Fact]
    public void SetAndCollectionDefaultsUseDistinctOverviewAndVisibilitySemantics()
    {
        ItemSetDefinition itemSet = PreviewTestAssertions.Set("Helmet", "Boots");
        ItemCollectionDefinition collection = PreviewTestAssertions.Collection("Sword", "Shield");

        PreviewPresentationSpecification setSpecification = PreviewTestAssertions.AssertSuccess(
            PreviewPresentationDefaults.Create(itemSet));
        PreviewPresentationSpecification collectionSpecification =
            PreviewTestAssertions.AssertSuccess(PreviewPresentationDefaults.Create(collection));

        Assert.Same(ProductCase.ItemSet, setSpecification.ProductCase);
        Assert.Same(PreviewViewKind.SetOverview, setSpecification.Views[^1].Kind);
        Assert.All(
            setSpecification.Views,
            view => Assert.Same(PreviewVisibility.AssembledSet, view.Visibility));
        Assert.Same(ProductCase.ItemCollection, collectionSpecification.ProductCase);
        Assert.Same(PreviewViewKind.CollectionOverview, collectionSpecification.Views[^1].Kind);
        Assert.All(
            collectionSpecification.Views,
            view => Assert.Same(PreviewVisibility.AllCollectionItems, view.Visibility));
    }

    [Fact]
    public void DetailViewsMaySelectOnlyKnownSetOrCollectionItems()
    {
        ItemSetDefinition itemSet = PreviewTestAssertions.Set("Helmet");
        PreviewViewDefinition selectedDetail = PreviewTestAssertions.View(
            PreviewViewKind.Detail,
            PreviewTestAssertions.Selected("Helmet"));
        PreviewPresentationSpecification defaultSet = PreviewTestAssertions.AssertSuccess(
            PreviewPresentationDefaults.Create(itemSet));
        PreviewViewDefinition[] validViews =
        [
            .. defaultSet.Views.Where(view => !view.Kind.Equals(PreviewViewKind.OrthographicRight)),
            selectedDetail,
        ];

        PreviewPresentationSpecification result = PreviewTestAssertions.AssertSuccess(
            PreviewPresentationSpecification.Create(
                itemSet,
                validViews,
                defaultSet.Background,
                defaultSet.Lighting));
        Assert.Equal("Helmet", result.Views[^1].Visibility.SelectedItemId!.Value);

        PreviewViewDefinition unknownDetail = PreviewTestAssertions.View(
            PreviewViewKind.Detail,
            PreviewTestAssertions.Selected("Unknown"),
            "UnknownDetail");
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationSpecification.Create(
                itemSet,
                [defaultSet.Views[0], defaultSet.Views[^1], unknownDetail],
                defaultSet.Background,
                defaultSet.Lighting),
            PreviewPresentationValidationError.UnknownSelectedItem);
    }

    [Fact]
    public void AggregateRejectsCaseIncompatibleViewsAndVisibility()
    {
        PreviewTestAssertions.AssertFailure(
            CreateStatic(
                [
                    PreviewTestAssertions.View(PreviewViewKind.Hero),
                    PreviewTestAssertions.View(PreviewViewKind.AnimationPose),
                ]),
            PreviewPresentationValidationError.ViewNotAllowedForProductCase);
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationSpecification.Create(
                PreviewTestAssertions.Set("Item"),
                [
                    PreviewTestAssertions.View(
                        PreviewViewKind.Hero,
                        PreviewVisibility.AssembledSet),
                    PreviewTestAssertions.View(
                        PreviewViewKind.CollectionOverview,
                        PreviewVisibility.AssembledSet),
                ],
                PreviewPresentationDefaults.Background,
                PreviewPresentationDefaults.Lighting),
            PreviewPresentationValidationError.ViewNotAllowedForProductCase);
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationSpecification.Create(
                ProductCase.Static,
                [
                    PreviewTestAssertions.View(
                        PreviewViewKind.Hero,
                        PreviewVisibility.AllCollectionItems),
                ],
                PreviewPresentationDefaults.Background,
                PreviewPresentationDefaults.Lighting),
            PreviewPresentationValidationError.VisibilityNotAllowedForProductCase);
    }

    [Theory]
    [InlineData("static", PreviewPresentationValidationError.MissingHeroView)]
    [InlineData("rigged-animated", PreviewPresentationValidationError.MissingAnimationPoseView)]
    public void AggregateRequiresCaseSpecificViews(
        string productCaseIdentifier,
        PreviewPresentationValidationError expected)
    {
        ProductCase productCase = ProductCase.All.Single(
            value => value.CanonicalIdentifier == productCaseIdentifier);
        PreviewViewDefinition[] views = expected == PreviewPresentationValidationError.MissingHeroView
            ? [PreviewTestAssertions.View(PreviewViewKind.Detail)]
            : [PreviewTestAssertions.View(PreviewViewKind.Hero)];

        PreviewTestAssertions.AssertFailure(
            PreviewPresentationSpecification.Create(
                productCase,
                views,
                PreviewPresentationDefaults.Background,
                PreviewPresentationDefaults.Lighting),
            expected);
    }

    [Fact]
    public void GroupAggregatesRequireTheirDistinctOverviewRoles()
    {
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationSpecification.Create(
                PreviewTestAssertions.Set("Item"),
                [
                    PreviewTestAssertions.View(
                        PreviewViewKind.Hero,
                        PreviewVisibility.AssembledSet),
                ],
                PreviewPresentationDefaults.Background,
                PreviewPresentationDefaults.Lighting),
            PreviewPresentationValidationError.MissingSetOverviewView);
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationSpecification.Create(
                PreviewTestAssertions.Collection("Item"),
                [
                    PreviewTestAssertions.View(
                        PreviewViewKind.Hero,
                        PreviewVisibility.AllCollectionItems),
                ],
                PreviewPresentationDefaults.Background,
                PreviewPresentationDefaults.Lighting),
            PreviewPresentationValidationError.MissingCollectionOverviewView);
    }

    [Fact]
    public void AggregateRejectsNullEmptyDuplicateAndMissingPresentationParts()
    {
        PreviewViewDefinition hero = PreviewTestAssertions.View(PreviewViewKind.Hero);
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationSpecification.Create(
                (ProductCase?)null,
                [hero],
                PreviewPresentationDefaults.Background,
                PreviewPresentationDefaults.Lighting),
            PreviewPresentationValidationError.NullProductCase);
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationSpecification.Create(
                ProductCase.ItemSet,
                [hero],
                PreviewPresentationDefaults.Background,
                PreviewPresentationDefaults.Lighting),
            PreviewPresentationValidationError.GroupDefinitionRequired);
        PreviewTestAssertions.AssertFailure(
            CreateStatic(null),
            PreviewPresentationValidationError.NullViews);
        PreviewTestAssertions.AssertFailure(
            CreateStatic([]),
            PreviewPresentationValidationError.EmptyViews);
        PreviewTestAssertions.AssertFailure(
            CreateStatic([null]),
            PreviewPresentationValidationError.NullView);
        PreviewTestAssertions.AssertFailure(
            CreateStatic([hero, hero]),
            PreviewPresentationValidationError.DuplicateViewId);
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationSpecification.Create(
                ProductCase.Static,
                [hero],
                null,
                PreviewPresentationDefaults.Lighting),
            PreviewPresentationValidationError.NullBackground);
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationSpecification.Create(
                ProductCase.Static,
                [hero],
                PreviewPresentationDefaults.Background,
                null),
            PreviewPresentationValidationError.NullLighting);
    }

    [Fact]
    public void ApprovedStudioDefaultsMatchExistingUnityPresentationValues()
    {
        PreviewBackground background = PreviewPresentationDefaults.Background;
        PreviewLighting lighting = PreviewPresentationDefaults.Lighting;

        AssertColour(background.OuterColour, 0.012d, 0.014d, 0.018d);
        AssertColour(background.CentreColour, 0.14d, 0.16d, 0.20d);
        Assert.Equal(0.5d, background.CentreX);
        Assert.Equal(0.58d, background.CentreY);
        Assert.Equal(0.7d, background.Radius);
        Assert.Equal(0.82d, background.HorizontalScale);
        AssertLight(lighting.KeyLight, -32d, 42d, 1.15d, 1d, 0.94d, 0.84d);
        AssertLight(lighting.FillLight, 145d, 25d, 0.55d, 0.62d, 0.75d, 1d);
        Assert.Equal(35d, PreviewPresentationDefaults.PerspectiveFieldOfViewDegrees);
        Assert.Equal(1.25d, PreviewPresentationDefaults.FramingPadding);
    }

    [Fact]
    public void PrimitiveFactoriesRejectInvalidValuesWithoutThrowing()
    {
        PreviewTestAssertions.AssertFailure(
            PreviewColour.Create(double.NaN, 0d, 0d),
            PreviewPresentationValidationError.ColourRedOutsideUnitInterval);
        PreviewTestAssertions.AssertFailure(
            PreviewColour.Create(0d, -0.01d, 0d),
            PreviewPresentationValidationError.ColourGreenOutsideUnitInterval);
        PreviewTestAssertions.AssertFailure(
            PreviewColour.Create(0d, 0d, 1.01d),
            PreviewPresentationValidationError.ColourBlueOutsideUnitInterval);
        PreviewTestAssertions.AssertFailure(
            PreviewBackground.Create(null, Colour(), 0.5d, 0.5d, 1d, 1d),
            PreviewPresentationValidationError.NullOuterColour);
        PreviewTestAssertions.AssertFailure(
            PreviewBackground.Create(Colour(), Colour(), -1d, 0.5d, 1d, 1d),
            PreviewPresentationValidationError.BackgroundCentreXOutsideUnitInterval);
        PreviewTestAssertions.AssertFailure(
            PreviewBackground.Create(Colour(), Colour(), 0.5d, 0.5d, 0d, 1d),
            PreviewPresentationValidationError.BackgroundRadiusNotPositiveFinite);
        PreviewTestAssertions.AssertFailure(
            PreviewDirectionalLight.Create(181d, 0d, 1d, Colour()),
            PreviewPresentationValidationError.LightYawOutsideRange);
        PreviewTestAssertions.AssertFailure(
            PreviewDirectionalLight.Create(0d, -91d, 1d, Colour()),
            PreviewPresentationValidationError.LightPitchOutsideRange);
        PreviewTestAssertions.AssertFailure(
            PreviewDirectionalLight.Create(0d, 0d, double.PositiveInfinity, Colour()),
            PreviewPresentationValidationError.LightIntensityNegativeOrNotFinite);
        PreviewTestAssertions.AssertFailure(
            PreviewVisibility.ForSelectedItem(null),
            PreviewPresentationValidationError.NullSelectedItemId);
    }

    [Fact]
    public void PrimitiveFactoriesRejectMissingReferencesPrecisely()
    {
        PreviewTestAssertions.AssertFailure(
            PreviewViewDefinition.Create(
                null,
                PreviewViewKind.Hero,
                PreviewVisibility.EntireProduct),
            PreviewPresentationValidationError.NullViewId);
        PreviewTestAssertions.AssertFailure(
            PreviewViewDefinition.Create(
                PreviewTestAssertions.Id("Hero"),
                null,
                PreviewVisibility.EntireProduct),
            PreviewPresentationValidationError.NullViewKind);
        PreviewTestAssertions.AssertFailure(
            PreviewViewDefinition.Create(
                PreviewTestAssertions.Id("Hero"),
                PreviewViewKind.Hero,
                null),
            PreviewPresentationValidationError.NullVisibility);
        PreviewTestAssertions.AssertFailure(
            PreviewBackground.Create(Colour(), null, 0.5d, 0.5d, 1d, 1d),
            PreviewPresentationValidationError.NullCentreColour);
        PreviewTestAssertions.AssertFailure(
            PreviewDirectionalLight.Create(0d, 0d, 1d, null),
            PreviewPresentationValidationError.NullLightColour);
        PreviewTestAssertions.AssertFailure(
            PreviewLighting.Create(null, PreviewPresentationDefaults.Lighting.FillLight),
            PreviewPresentationValidationError.NullKeyLight);
        PreviewTestAssertions.AssertFailure(
            PreviewLighting.Create(PreviewPresentationDefaults.Lighting.KeyLight, null),
            PreviewPresentationValidationError.NullFillLight);
    }

    [Fact]
    public void DefaultFactoriesRejectMissingOrMismatchedCaseDefinitions()
    {
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationDefaults.Create((ProductCase?)null),
            PreviewPresentationValidationError.NullProductCase);
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationDefaults.Create(ProductCase.ItemSet),
            PreviewPresentationValidationError.GroupDefinitionRequired);
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationDefaults.Create((ItemSetDefinition?)null),
            PreviewPresentationValidationError.GroupDefinitionRequired);
        PreviewTestAssertions.AssertFailure(
            PreviewPresentationDefaults.Create((ItemCollectionDefinition?)null),
            PreviewPresentationValidationError.GroupDefinitionRequired);
    }

    [Fact]
    public void ValuesAreImmutableOrderedAndCultureIndependent()
    {
        PreviewPresentationSpecification first = PreviewTestAssertions.AssertSuccess(
            PreviewPresentationDefaults.Create(ProductCase.Static));
        PreviewPresentationSpecification same = PreviewTestAssertions.AssertSuccess(
            PreviewPresentationDefaults.Create(ProductCase.Static));
        int stableHash = first.GetHashCode();
        CultureInfo previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.True(first.Equals(same));
            Assert.True(first.Equals((object)same));
            Assert.False(first.Equals((PreviewPresentationSpecification?)null));
            Assert.False(first.Equals("preview"));
            Assert.Equal(stableHash, same.GetHashCode());
            IList<PreviewViewDefinition> views = Assert.IsType<IList<PreviewViewDefinition>>(
                first.Views,
                exactMatch: false);
            Assert.True(views.IsReadOnly);
            _ = Assert.Throws<NotSupportedException>(() => views.Add(first.Views[0]));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void PreviewDomainRemainsEngineRendererFilesystemAndUiIndependent()
    {
        Assembly assembly = typeof(PreviewPresentationSpecification).Assembly;
        string[] references =
        [
            .. assembly.GetReferencedAssemblies().Select(value => value.Name ?? string.Empty),
        ];
        string[] forbidden =
        [
            "Unity",
            "Unreal",
            "WPF",
            "PresentationFramework",
            "System.Drawing",
            "System.IO.FileSystem",
            "System.Net",
        ];
        Assert.DoesNotContain(
            references,
            reference => forbidden.Any(
                token => reference.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    private static PreviewPresentationValidationResult<PreviewPresentationSpecification> CreateStatic(
        IEnumerable<PreviewViewDefinition?>? views) =>
        PreviewPresentationSpecification.Create(
            ProductCase.Static,
            views,
            PreviewPresentationDefaults.Background,
            PreviewPresentationDefaults.Lighting);

    private static PreviewColour Colour() =>
        PreviewTestAssertions.AssertSuccess(PreviewColour.Create(0d, 0d, 0d));

    private static void AssertColour(
        PreviewColour colour,
        double red,
        double green,
        double blue)
    {
        Assert.Equal(red, colour.Red);
        Assert.Equal(green, colour.Green);
        Assert.Equal(blue, colour.Blue);
    }

    private static void AssertLight(
        PreviewDirectionalLight light,
        double yaw,
        double pitch,
        double intensity,
        double red,
        double green,
        double blue)
    {
        Assert.Equal(yaw, light.YawDegrees);
        Assert.Equal(pitch, light.PitchDegrees);
        Assert.Equal(intensity, light.Intensity);
        AssertColour(light.Colour, red, green, blue);
    }
}
