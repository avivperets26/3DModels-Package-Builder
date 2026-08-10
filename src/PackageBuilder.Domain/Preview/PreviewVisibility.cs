using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Preview;

/// <summary>Identifies which validated product subject is visible in a preview view.</summary>
public enum PreviewVisibilityMode
{
    EntireProduct = 0,
    AssembledSet,
    AllCollectionItems,
    SelectedItem,
}

/// <summary>
/// Represents engine-neutral visibility intent. It identifies subjects without mutating source
/// model transforms, renderers, rigs, or animation data.
/// </summary>
public sealed class PreviewVisibility : IEquatable<PreviewVisibility>
{
    private PreviewVisibility(PreviewVisibilityMode mode, InternalAssetId? selectedItemId)
    {
        Mode = mode;
        SelectedItemId = selectedItemId;
    }

    /// <summary>Gets visibility for the complete non-group product.</summary>
    public static PreviewVisibility EntireProduct { get; } =
        new(PreviewVisibilityMode.EntireProduct, null);

    /// <summary>Gets visibility for a set in its declared assembled arrangement.</summary>
    public static PreviewVisibility AssembledSet { get; } =
        new(PreviewVisibilityMode.AssembledSet, null);

    /// <summary>Gets visibility for every independently usable collection item.</summary>
    public static PreviewVisibility AllCollectionItems { get; } =
        new(PreviewVisibilityMode.AllCollectionItems, null);

    /// <summary>Gets the visibility behavior.</summary>
    public PreviewVisibilityMode Mode { get; }

    /// <summary>Gets the selected item identity when <see cref="Mode"/> is SelectedItem.</summary>
    public InternalAssetId? SelectedItemId { get; }

    /// <summary>Creates visibility for one item; membership is checked by the aggregate.</summary>
    public static PreviewPresentationValidationResult<PreviewVisibility> ForSelectedItem(
        InternalAssetId? itemId) =>
        itemId is null
            ? PreviewPresentationValidationResult<PreviewVisibility>.Failure(
                PreviewPresentationValidationError.NullSelectedItemId)
            : PreviewPresentationValidationResult<PreviewVisibility>.Success(
                new PreviewVisibility(PreviewVisibilityMode.SelectedItem, itemId));

    /// <inheritdoc />
    public bool Equals(PreviewVisibility? other) =>
        other is not null && Mode == other.Mode && Equals(SelectedItemId, other.SelectedItemId);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PreviewVisibility other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StablePreviewHash.Create()
            .Add((int)Mode)
            .Add(SelectedItemId?.Value ?? string.Empty)
            .ToHashCode();
}
