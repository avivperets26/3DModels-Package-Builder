using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Products;

namespace PackageBuilder.Domain.Preview;

/// <summary>
/// Provides the approved dark-studio defaults shared by desktop, Unity, Unreal, and media adapters.
/// </summary>
public static class PreviewPresentationDefaults
{
    /// <summary>Gets the existing approved perspective field of view used by engine adapters.</summary>
    public const double PerspectiveFieldOfViewDegrees = 35d;

    /// <summary>Gets the existing approved bounds-framing padding multiplier.</summary>
    public const double FramingPadding = 1.25d;

    /// <summary>Gets the approved near-black radial background.</summary>
    public static PreviewBackground Background { get; } = CreateBackground();

    /// <summary>Gets the approved neutral key/fill studio lighting.</summary>
    public static PreviewLighting Lighting { get; } = CreateLighting();

    /// <summary>Creates approved default views for a non-group product case.</summary>
    public static PreviewPresentationValidationResult<PreviewPresentationSpecification> Create(
        ProductCase? productCase)
    {
        if (productCase is null)
        {
            return PreviewPresentationSpecification.Create(productCase, [], Background, Lighting);
        }

        if (productCase.Equals(ProductCase.ItemSet) || productCase.Equals(ProductCase.ItemCollection))
        {
            return PreviewPresentationSpecification.Create(productCase, [], Background, Lighting);
        }

        var views = StandardViews(PreviewVisibility.EntireProduct).ToList();
        if (productCase.Equals(ProductCase.RiggedAnimated))
        {
            views.Add(View("AnimationPose", PreviewViewKind.AnimationPose, PreviewVisibility.EntireProduct));
        }

        return PreviewPresentationSpecification.Create(productCase, views, Background, Lighting);
    }

    /// <summary>Creates the approved assembled-set default presentation.</summary>
    public static PreviewPresentationValidationResult<PreviewPresentationSpecification> Create(
        ItemSetDefinition? itemSet)
    {
        if (itemSet is null)
        {
            return PreviewPresentationSpecification.Create(itemSet, [], Background, Lighting);
        }

        var views = StandardViews(PreviewVisibility.AssembledSet).ToList();
        views.Add(View("SetOverview", PreviewViewKind.SetOverview, PreviewVisibility.AssembledSet));
        return PreviewPresentationSpecification.Create(itemSet, views, Background, Lighting);
    }

    /// <summary>Creates the approved all-items collection default presentation.</summary>
    public static PreviewPresentationValidationResult<PreviewPresentationSpecification> Create(
        ItemCollectionDefinition? collection)
    {
        if (collection is null)
        {
            return PreviewPresentationSpecification.Create(collection, [], Background, Lighting);
        }

        var views = StandardViews(PreviewVisibility.AllCollectionItems).ToList();
        views.Add(View(
            "CollectionOverview",
            PreviewViewKind.CollectionOverview,
            PreviewVisibility.AllCollectionItems));
        return PreviewPresentationSpecification.Create(collection, views, Background, Lighting);
    }

    private static IEnumerable<PreviewViewDefinition> StandardViews(PreviewVisibility visibility)
    {
        yield return View("Hero", PreviewViewKind.Hero, visibility);
        yield return View("Front", PreviewViewKind.OrthographicFront, visibility);
        yield return View("Back", PreviewViewKind.OrthographicBack, visibility);
        yield return View("Left", PreviewViewKind.OrthographicLeft, visibility);
        yield return View("Right", PreviewViewKind.OrthographicRight, visibility);
    }

    private static PreviewBackground CreateBackground() =>
        PreviewBackground.Create(
            Colour(0.012d, 0.014d, 0.018d),
            Colour(0.14d, 0.16d, 0.20d),
            0.5d,
            0.58d,
            0.7d,
            0.82d).Value!;

    private static PreviewLighting CreateLighting() =>
        PreviewLighting.Create(
            PreviewDirectionalLight.Create(
                -32d,
                42d,
                1.15d,
                Colour(1d, 0.94d, 0.84d)).Value!,
            PreviewDirectionalLight.Create(
                145d,
                25d,
                0.55d,
                Colour(0.62d, 0.75d, 1d)).Value!).Value!;

    private static PreviewColour Colour(double red, double green, double blue) =>
        PreviewColour.Create(red, green, blue).Value!;

    private static PreviewViewDefinition View(
        string id,
        PreviewViewKind kind,
        PreviewVisibility visibility) =>
        PreviewViewDefinition.Create(
            InternalAssetId.Create(id).Value!,
            kind,
            visibility).Value!;
}
