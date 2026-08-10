using System.Collections.ObjectModel;
using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Preview;

/// <summary>Identifies empty, all-items, or exactly-one-item visibility.</summary>
public enum PreviewItemSelectionMode
{
    Empty = 0,
    All,
    SelectedItem,
}

/// <summary>Tracks deterministic item order and preview-only visibility selection.</summary>
public sealed class PreviewItemSelectionState
{
    private PreviewItemSelectionState(
        IReadOnlyList<InternalAssetId> items,
        PreviewItemSelectionMode mode,
        int? selectedIndex)
    {
        Items = items;
        Mode = mode;
        SelectedIndex = selectedIndex;
    }

    public IReadOnlyList<InternalAssetId> Items { get; }
    public PreviewItemSelectionMode Mode { get; }
    public int? SelectedIndex { get; }
    public InternalAssetId? SelectedItem => SelectedIndex is int index ? Items[index] : null;

    /// <summary>Validates stable unique item identities and creates a safe initial state.</summary>
    public static PreviewExperienceValidationResult<PreviewItemSelectionState> Create(
        IEnumerable<InternalAssetId?>? items,
        PreviewItemSelectionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (items is null)
        {
            return PreviewExperienceValidationResult<PreviewItemSelectionState>.Failure(
                PreviewExperienceValidationError.NullItems);
        }

        InternalAssetId?[] values = [.. items];
        if (values.Any(value => value is null))
        {
            return PreviewExperienceValidationResult<PreviewItemSelectionState>.Failure(
                PreviewExperienceValidationError.NullItem);
        }

        InternalAssetId[] present = [.. values.Select(value => value!)];
        if (present.Select(value => value.Value).Distinct(StringComparer.Ordinal).Count() != present.Length)
        {
            return PreviewExperienceValidationResult<PreviewItemSelectionState>.Failure(
                PreviewExperienceValidationError.DuplicateItemId);
        }

        PreviewItemSelectionMode mode = present.Length == 0
            ? PreviewItemSelectionMode.Empty
            : policy.InitiallyShowAll
            ? PreviewItemSelectionMode.All
            : PreviewItemSelectionMode.SelectedItem;
        return PreviewExperienceValidationResult<PreviewItemSelectionState>.Success(
            new PreviewItemSelectionState(
                new ReadOnlyCollection<InternalAssetId>(present),
                mode,
                mode == PreviewItemSelectionMode.SelectedItem ? 0 : null));
    }

    /// <summary>Selects the previous item, applying the contract's explicit wrap policy.</summary>
    public PreviewItemSelectionState Previous(PreviewItemSelectionPolicy policy) => Move(-1, policy);

    /// <summary>Selects the next item, applying the contract's explicit wrap policy.</summary>
    public PreviewItemSelectionState Next(PreviewItemSelectionPolicy policy) => Move(1, policy);

    /// <summary>Selects one known item directly without changing packaged item assets.</summary>
    public PreviewExperienceValidationResult<PreviewItemSelectionState> Select(
        InternalAssetId? item)
    {
        if (item is null)
        {
            return PreviewExperienceValidationResult<PreviewItemSelectionState>.Failure(
                PreviewExperienceValidationError.UnknownItem);
        }

        int index = Items.ToList().FindIndex(value => value.Equals(item));
        return index < 0
            ? PreviewExperienceValidationResult<PreviewItemSelectionState>.Failure(
                PreviewExperienceValidationError.UnknownItem)
            : PreviewExperienceValidationResult<PreviewItemSelectionState>.Success(
                new PreviewItemSelectionState(Items, PreviewItemSelectionMode.SelectedItem, index));
    }

    /// <summary>Shows all known items, or preserves the safe empty state when none exist.</summary>
    public PreviewItemSelectionState ShowAll() => Items.Count == 0
        ? this
        : new PreviewItemSelectionState(Items, PreviewItemSelectionMode.All, null);

    private PreviewItemSelectionState Move(int delta, PreviewItemSelectionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (Items.Count == 0)
        {
            return this;
        }

        int origin = SelectedIndex ?? (delta > 0 ? -1 : Items.Count);
        int target = origin + delta;
        target = policy.WrapPreviousNext ? (target % Items.Count + Items.Count) % Items.Count : Math.Clamp(target, 0, Items.Count - 1);

        return new PreviewItemSelectionState(Items, PreviewItemSelectionMode.SelectedItem, target);
    }
}
