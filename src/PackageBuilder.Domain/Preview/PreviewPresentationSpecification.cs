using System.Collections.ObjectModel;
using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Products;

namespace PackageBuilder.Domain.Preview;

/// <summary>
/// Defines immutable, renderer-independent preview views, visibility, background, and lighting.
/// Engine adapters may translate this intent but must not mutate packaged source product data.
/// </summary>
public sealed class PreviewPresentationSpecification : IEquatable<PreviewPresentationSpecification>
{
    private PreviewPresentationSpecification(
        ProductCase productCase,
        ReadOnlyCollection<PreviewViewDefinition> views,
        PreviewBackground background,
        PreviewLighting lighting)
    {
        ProductCase = productCase;
        Views = views;
        Background = background;
        Lighting = lighting;
    }

    /// <summary>Gets the product case whose presentation rules were validated.</summary>
    public ProductCase ProductCase { get; }

    /// <summary>Gets views in exact author-controlled capture order.</summary>
    public IReadOnlyList<PreviewViewDefinition> Views { get; }

    /// <summary>Gets the validated horizon-free background intent.</summary>
    public PreviewBackground Background { get; }

    /// <summary>Gets the validated key/fill studio lighting intent.</summary>
    public PreviewLighting Lighting { get; }

    /// <summary>Creates a presentation for one non-group product case.</summary>
    public static PreviewPresentationValidationResult<PreviewPresentationSpecification> Create(
        ProductCase? productCase,
        IEnumerable<PreviewViewDefinition?>? views,
        PreviewBackground? background,
        PreviewLighting? lighting)
    {
        return productCase is null
            ? Failure(PreviewPresentationValidationError.NullProductCase)
            : productCase.Equals(ProductCase.ItemSet) ||
                productCase.Equals(ProductCase.ItemCollection)
            ? Failure(PreviewPresentationValidationError.GroupDefinitionRequired)
            : CreateCore(productCase, [], views, background, lighting);
    }

    /// <summary>Creates a set presentation and validates selected-item visibility against membership.</summary>
    public static PreviewPresentationValidationResult<PreviewPresentationSpecification> Create(
        ItemSetDefinition? itemSet,
        IEnumerable<PreviewViewDefinition?>? views,
        PreviewBackground? background,
        PreviewLighting? lighting) =>
        itemSet is null
            ? Failure(PreviewPresentationValidationError.GroupDefinitionRequired)
            : CreateCore(
                ProductCase.ItemSet,
                itemSet.Items.Select(item => item.Id),
                views,
                background,
                lighting);

    /// <summary>
    /// Creates a collection presentation and validates selected-item visibility against membership.
    /// </summary>
    public static PreviewPresentationValidationResult<PreviewPresentationSpecification> Create(
        ItemCollectionDefinition? collection,
        IEnumerable<PreviewViewDefinition?>? views,
        PreviewBackground? background,
        PreviewLighting? lighting) =>
        collection is null
            ? Failure(PreviewPresentationValidationError.GroupDefinitionRequired)
            : CreateCore(
                ProductCase.ItemCollection,
                collection.Items.Select(item => item.Id),
                views,
                background,
                lighting);

    /// <inheritdoc />
    public bool Equals(PreviewPresentationSpecification? other) =>
        other is not null && ProductCase.Equals(other.ProductCase) &&
        Views.SequenceEqual(other.Views) && Background.Equals(other.Background) &&
        Lighting.Equals(other.Lighting);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is PreviewPresentationSpecification other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        StablePreviewHash hash = StablePreviewHash.Create()
            .Add(ProductCase.CanonicalIdentifier)
            .Add(Background.GetHashCode())
            .Add(Lighting.GetHashCode());
        foreach (PreviewViewDefinition view in Views)
        {
            hash = hash.Add(view.GetHashCode());
        }

        return hash.ToHashCode();
    }

    private static PreviewPresentationValidationResult<PreviewPresentationSpecification> CreateCore(
        ProductCase productCase,
        IEnumerable<InternalAssetId> itemIds,
        IEnumerable<PreviewViewDefinition?>? views,
        PreviewBackground? background,
        PreviewLighting? lighting)
    {
        if (views is null)
        {
            return Failure(PreviewPresentationValidationError.NullViews);
        }

        var validatedViews = new List<PreviewViewDefinition>();
        var viewIds = new HashSet<InternalAssetId>();
        var kinds = new HashSet<PreviewViewKind>();
        var knownItemIds = new HashSet<InternalAssetId>(itemIds);
        foreach (PreviewViewDefinition? view in views)
        {
            if (view is null)
            {
                return Failure(PreviewPresentationValidationError.NullView);
            }

            if (!viewIds.Add(view.Id))
            {
                return Failure(PreviewPresentationValidationError.DuplicateViewId);
            }

            _ = kinds.Add(view.Kind);

            if (!IsViewAllowed(productCase, view.Kind))
            {
                return Failure(PreviewPresentationValidationError.ViewNotAllowedForProductCase);
            }

            if (!IsVisibilityAllowed(productCase, view.Visibility.Mode))
            {
                return Failure(PreviewPresentationValidationError.VisibilityNotAllowedForProductCase);
            }

            if (view.Visibility.Mode == PreviewVisibilityMode.SelectedItem &&
                !knownItemIds.Contains(view.Visibility.SelectedItemId!))
            {
                return Failure(PreviewPresentationValidationError.UnknownSelectedItem);
            }

            validatedViews.Add(view);
        }

        if (validatedViews.Count == 0)
        {
            return Failure(PreviewPresentationValidationError.EmptyViews);
        }

        PreviewPresentationValidationError requiredViewError =
            RequiredViewError(productCase, kinds);
        return requiredViewError != PreviewPresentationValidationError.None
            ? Failure(requiredViewError)
            : background is null
            ? Failure(PreviewPresentationValidationError.NullBackground)
            : lighting is null
            ? Failure(PreviewPresentationValidationError.NullLighting)
            : PreviewPresentationValidationResult<PreviewPresentationSpecification>.Success(
                new PreviewPresentationSpecification(
                    productCase,
                    Array.AsReadOnly(validatedViews.ToArray()),
                    background,
                    lighting));
    }

    private static PreviewPresentationValidationError RequiredViewError(
        ProductCase productCase,
        HashSet<PreviewViewKind> kinds)
    {
        return !kinds.Contains(PreviewViewKind.Hero)
            ? PreviewPresentationValidationError.MissingHeroView
            : productCase.Equals(ProductCase.RiggedAnimated) &&
            !kinds.Contains(PreviewViewKind.AnimationPose)
            ? PreviewPresentationValidationError.MissingAnimationPoseView
            : productCase.Equals(ProductCase.ItemSet) &&
            !kinds.Contains(PreviewViewKind.SetOverview)
            ? PreviewPresentationValidationError.MissingSetOverviewView
            : productCase.Equals(ProductCase.ItemCollection) &&
            !kinds.Contains(PreviewViewKind.CollectionOverview)
            ? PreviewPresentationValidationError.MissingCollectionOverviewView
            : PreviewPresentationValidationError.None;
    }

    private static bool IsViewAllowed(ProductCase productCase, PreviewViewKind kind)
    {
        return kind.Equals(PreviewViewKind.AnimationPose)
            ? productCase.Equals(ProductCase.RiggedAnimated)
            : kind.Equals(PreviewViewKind.SetOverview)
            ? productCase.Equals(ProductCase.ItemSet)
            : !kind.Equals(PreviewViewKind.CollectionOverview) ||
            productCase.Equals(ProductCase.ItemCollection);
    }

    private static bool IsVisibilityAllowed(ProductCase productCase, PreviewVisibilityMode mode) =>
        productCase.Equals(ProductCase.ItemSet)
            ? mode is PreviewVisibilityMode.AssembledSet or PreviewVisibilityMode.SelectedItem
            : productCase.Equals(ProductCase.ItemCollection)
            ? mode is PreviewVisibilityMode.AllCollectionItems or PreviewVisibilityMode.SelectedItem
            : mode == PreviewVisibilityMode.EntireProduct;

    private static PreviewPresentationValidationResult<PreviewPresentationSpecification> Failure(
        PreviewPresentationValidationError error) =>
        PreviewPresentationValidationResult<PreviewPresentationSpecification>.Failure(error);
}
