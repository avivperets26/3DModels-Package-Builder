using System.Collections.ObjectModel;

namespace PackageBuilder.Domain.Preview;

/// <summary>Defines bounded camera navigation independently of an engine coordinate system.</summary>
public sealed record PreviewNavigationPolicy(
    double MinimumPitchDegrees,
    double MaximumPitchDegrees,
    double MinimumDistanceMultiplier,
    double MaximumDistanceMultiplier,
    double PointerOrbitDegreesPerUnit,
    double KeyboardOrbitStepDegrees,
    double PointerZoomStep,
    double KeyboardZoomStep)
{
    /// <summary>Validates camera limits and positive interaction increments.</summary>
    public static PreviewExperienceValidationResult<PreviewNavigationPolicy> Create(
        double minimumPitchDegrees,
        double maximumPitchDegrees,
        double minimumDistanceMultiplier,
        double maximumDistanceMultiplier,
        double pointerOrbitDegreesPerUnit,
        double keyboardOrbitStepDegrees,
        double pointerZoomStep,
        double keyboardZoomStep)
    {
        double[] values =
        [
            minimumPitchDegrees,
            maximumPitchDegrees,
            minimumDistanceMultiplier,
            maximumDistanceMultiplier,
            pointerOrbitDegreesPerUnit,
            keyboardOrbitStepDegrees,
            pointerZoomStep,
            keyboardZoomStep,
        ];
        bool valid = values.All(double.IsFinite) &&
            minimumPitchDegrees >= -90d && maximumPitchDegrees <= 90d &&
            minimumPitchDegrees < maximumPitchDegrees &&
            minimumDistanceMultiplier > 0d &&
            minimumDistanceMultiplier <= 1d &&
            maximumDistanceMultiplier >= 1d &&
            minimumDistanceMultiplier < maximumDistanceMultiplier &&
            pointerOrbitDegreesPerUnit > 0d && keyboardOrbitStepDegrees > 0d &&
            pointerZoomStep > 0d && pointerZoomStep < 1d &&
            keyboardZoomStep > 0d && keyboardZoomStep < 1d;
        return valid
            ? PreviewExperienceValidationResult<PreviewNavigationPolicy>.Success(
                new(
                    minimumPitchDegrees,
                    maximumPitchDegrees,
                    minimumDistanceMultiplier,
                    maximumDistanceMultiplier,
                    pointerOrbitDegreesPerUnit,
                    keyboardOrbitStepDegrees,
                    pointerZoomStep,
                    keyboardZoomStep))
            : PreviewExperienceValidationResult<PreviewNavigationPolicy>.Failure(
                PreviewExperienceValidationError.InvalidNumericRange);
    }
}

/// <summary>Defines bounded key-light direction adjustment while preserving PB-0906 light tokens.</summary>
public sealed record PreviewLightControlPolicy(
    double MinimumPitchDegrees,
    double MaximumPitchDegrees,
    double PointerStepDegrees,
    double KeyboardStepDegrees)
{
    /// <summary>Validates direction limits and positive adjustment increments.</summary>
    public static PreviewExperienceValidationResult<PreviewLightControlPolicy> Create(
        double minimumPitchDegrees,
        double maximumPitchDegrees,
        double pointerStepDegrees,
        double keyboardStepDegrees)
    {
        bool valid = double.IsFinite(minimumPitchDegrees) &&
            double.IsFinite(maximumPitchDegrees) &&
            minimumPitchDegrees >= -90d && maximumPitchDegrees <= 90d &&
            minimumPitchDegrees < maximumPitchDegrees &&
            double.IsFinite(pointerStepDegrees) && pointerStepDegrees > 0d &&
            double.IsFinite(keyboardStepDegrees) && keyboardStepDegrees > 0d;
        return valid
            ? PreviewExperienceValidationResult<PreviewLightControlPolicy>.Success(
                new(minimumPitchDegrees, maximumPitchDegrees, pointerStepDegrees, keyboardStepDegrees))
            : PreviewExperienceValidationResult<PreviewLightControlPolicy>.Failure(
                PreviewExperienceValidationError.InvalidNumericRange);
    }
}

/// <summary>Defines capture-safe overlay visibility and deterministic focus restoration.</summary>
public sealed record PreviewOverlayPolicy(bool InitiallyVisible, string RestoreControlId);

/// <summary>Defines deterministic ordering and edge behavior for item selection.</summary>
public sealed record PreviewItemSelectionPolicy(bool WrapPreviousNext, bool InitiallyShowAll);

/// <summary>Defines non-destructive animation-preview defaults and timeline increments.</summary>
public sealed record PreviewAnimationTransportPolicy(
    bool SelectFirstAnimation,
    bool InitiallyPlaying,
    double KeyboardScrubSeconds)
{
    /// <summary>Validates a positive finite keyboard timeline increment.</summary>
    public static PreviewExperienceValidationResult<PreviewAnimationTransportPolicy> Create(
        bool selectFirstAnimation,
        bool initiallyPlaying,
        double keyboardScrubSeconds) =>
        double.IsFinite(keyboardScrubSeconds) && keyboardScrubSeconds > 0d
            ? PreviewExperienceValidationResult<PreviewAnimationTransportPolicy>.Success(
                new(selectFirstAnimation, initiallyPlaying, keyboardScrubSeconds))
            : PreviewExperienceValidationResult<PreviewAnimationTransportPolicy>.Failure(
                PreviewExperienceValidationError.InvalidStep);
}

/// <summary>Describes one focusable control's stable accessibility semantics.</summary>
public sealed record PreviewAccessibleControl(
    string Id,
    string Label,
    string AccessibleName,
    string AccessibleHelp,
    int FocusOrder);

/// <summary>Defines predictable focus order, visible focus, and semantic control descriptions.</summary>
public sealed class PreviewAccessibilityPolicy
{
    private PreviewAccessibilityPolicy(IReadOnlyList<PreviewAccessibleControl> controls)
    {
        Controls = controls;
    }

    public static bool VisibleFocusRequired => true;

    public IReadOnlyList<PreviewAccessibleControl> Controls { get; }

    /// <summary>Validates non-empty names plus unique stable IDs and focus positions.</summary>
    public static PreviewExperienceValidationResult<PreviewAccessibilityPolicy> Create(
        IEnumerable<PreviewAccessibleControl?>? controls)
    {
        if (controls is null)
        {
            return PreviewExperienceValidationResult<PreviewAccessibilityPolicy>.Failure(
                PreviewExperienceValidationError.EmptyAccessibilityControls);
        }

        PreviewAccessibleControl?[] values = [.. controls];
        if (values.Length == 0)
        {
            return PreviewExperienceValidationResult<PreviewAccessibilityPolicy>.Failure(
                PreviewExperienceValidationError.EmptyAccessibilityControls);
        }

        if (values.Any(value => value is null))
        {
            return PreviewExperienceValidationResult<PreviewAccessibilityPolicy>.Failure(
                PreviewExperienceValidationError.NullAccessibilityControl);
        }

        PreviewAccessibleControl[] present = [.. values!];
        return present.Any(value => string.IsNullOrWhiteSpace(value.Id) ||
            string.IsNullOrWhiteSpace(value.Label) ||
            string.IsNullOrWhiteSpace(value.AccessibleName) ||
            string.IsNullOrWhiteSpace(value.AccessibleHelp) || value.FocusOrder < 0)
            ? PreviewExperienceValidationResult<PreviewAccessibilityPolicy>.Failure(
                PreviewExperienceValidationError.MissingAccessibleName)
            : present.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != present.Length
            ? PreviewExperienceValidationResult<PreviewAccessibilityPolicy>.Failure(
                PreviewExperienceValidationError.DuplicateControlId)
            : present.Select(value => value.FocusOrder).Distinct().Count() != present.Length
            ? PreviewExperienceValidationResult<PreviewAccessibilityPolicy>.Failure(
                PreviewExperienceValidationError.DuplicateFocusOrder)
            : PreviewExperienceValidationResult<PreviewAccessibilityPolicy>.Success(
            new PreviewAccessibilityPolicy(
                new ReadOnlyCollection<PreviewAccessibleControl>(
                    [.. present.OrderBy(value => value.FocusOrder)])));
    }
}

/// <summary>
/// Aggregates the versioned engine-neutral preview experience; adapters translate it into engine
/// APIs but must not redefine its state or accessibility semantics.
/// </summary>
public sealed class PreviewExperienceContract
{
    private PreviewExperienceContract(
        int contractVersion,
        PreviewBackground background,
        PreviewLighting lighting,
        PreviewNavigationPolicy navigation,
        PreviewLightControlPolicy lightControls,
        PreviewOverlayPolicy overlay,
        PreviewItemSelectionPolicy itemSelection,
        PreviewAnimationTransportPolicy animationTransport,
        IReadOnlyList<PreviewInputBinding> bindings,
        PreviewAccessibilityPolicy accessibility)
    {
        ContractVersion = contractVersion;
        Background = background;
        Lighting = lighting;
        Navigation = navigation;
        LightControls = lightControls;
        Overlay = overlay;
        ItemSelection = itemSelection;
        AnimationTransport = animationTransport;
        Bindings = bindings;
        Accessibility = accessibility;
    }

    public const int CurrentContractVersion = 1;

    public int ContractVersion { get; }
    public PreviewBackground Background { get; }
    public PreviewLighting Lighting { get; }
    public PreviewNavigationPolicy Navigation { get; }
    public PreviewLightControlPolicy LightControls { get; }
    public PreviewOverlayPolicy Overlay { get; }
    public PreviewItemSelectionPolicy ItemSelection { get; }
    public PreviewAnimationTransportPolicy AnimationTransport { get; }
    public IReadOnlyList<PreviewInputBinding> Bindings { get; }
    public PreviewAccessibilityPolicy Accessibility { get; }

    /// <summary>Validates and creates a complete version-one preview experience contract.</summary>
    public static PreviewExperienceValidationResult<PreviewExperienceContract> Create(
        int contractVersion,
        PreviewBackground? background,
        PreviewLighting? lighting,
        PreviewNavigationPolicy? navigation,
        PreviewLightControlPolicy? lightControls,
        PreviewOverlayPolicy? overlay,
        PreviewItemSelectionPolicy? itemSelection,
        PreviewAnimationTransportPolicy? animationTransport,
        IEnumerable<PreviewInputBinding?>? bindings,
        PreviewAccessibilityPolicy? accessibility)
    {
        PreviewExperienceValidationError error = contractVersion != CurrentContractVersion
            ? PreviewExperienceValidationError.UnsupportedContractVersion
            : background is null || lighting is null
            ? PreviewExperienceValidationError.NullPresentation
            : navigation is null
            ? PreviewExperienceValidationError.NullNavigation
            : lightControls is null
            ? PreviewExperienceValidationError.NullLightControls
            : overlay is null
            ? PreviewExperienceValidationError.NullOverlay
            : itemSelection is null
            ? PreviewExperienceValidationError.NullItemSelection
            : animationTransport is null
            ? PreviewExperienceValidationError.NullAnimationTransport
            : bindings is null
            ? PreviewExperienceValidationError.NullBindings
            : accessibility is null
            ? PreviewExperienceValidationError.NullAccessibility
            : PreviewExperienceValidationError.None;
        if (error != PreviewExperienceValidationError.None)
        {
            return PreviewExperienceValidationResult<PreviewExperienceContract>.Failure(error);
        }

        if (!PreviewNavigationPolicy.Create(
            navigation!.MinimumPitchDegrees,
            navigation.MaximumPitchDegrees,
            navigation.MinimumDistanceMultiplier,
            navigation.MaximumDistanceMultiplier,
            navigation.PointerOrbitDegreesPerUnit,
            navigation.KeyboardOrbitStepDegrees,
            navigation.PointerZoomStep,
            navigation.KeyboardZoomStep).IsValid ||
            !PreviewLightControlPolicy.Create(
                lightControls!.MinimumPitchDegrees,
                lightControls.MaximumPitchDegrees,
                lightControls.PointerStepDegrees,
                lightControls.KeyboardStepDegrees).IsValid)
        {
            return PreviewExperienceValidationResult<PreviewExperienceContract>.Failure(
                PreviewExperienceValidationError.InvalidNumericRange);
        }

        if (!PreviewAnimationTransportPolicy.Create(
            animationTransport!.SelectFirstAnimation,
            animationTransport.InitiallyPlaying,
            animationTransport.KeyboardScrubSeconds).IsValid)
        {
            return PreviewExperienceValidationResult<PreviewExperienceContract>.Failure(
                PreviewExperienceValidationError.InvalidStep);
        }

        if (string.IsNullOrWhiteSpace(overlay!.RestoreControlId))
        {
            return PreviewExperienceValidationResult<PreviewExperienceContract>.Failure(
                PreviewExperienceValidationError.InvalidState);
        }

        PreviewInputBinding?[] bindingValues = [.. bindings!];
        if (bindingValues.Length == 0)
        {
            return PreviewExperienceValidationResult<PreviewExperienceContract>.Failure(
                PreviewExperienceValidationError.EmptyBindings);
        }

        if (bindingValues.Any(value => value is null))
        {
            return PreviewExperienceValidationResult<PreviewExperienceContract>.Failure(
                PreviewExperienceValidationError.NullBinding);
        }

        PreviewInputBinding[] presentBindings = [.. bindingValues!];
        return presentBindings.Any(binding =>
            !Enum.IsDefined(binding.Action) || string.IsNullOrWhiteSpace(binding.Input))
            ? PreviewExperienceValidationResult<PreviewExperienceContract>.Failure(
                PreviewExperienceValidationError.InvalidState)
            : presentBindings.Distinct().Count() != presentBindings.Length
            ? PreviewExperienceValidationResult<PreviewExperienceContract>.Failure(
                PreviewExperienceValidationError.DuplicateBinding)
            : !PreviewInputBinding.RequiredActions.All(required =>
            presentBindings.Any(binding => binding.Action == required))
            ? PreviewExperienceValidationResult<PreviewExperienceContract>.Failure(
                PreviewExperienceValidationError.MissingRequiredBinding)
            : PreviewExperienceValidationResult<PreviewExperienceContract>.Success(
            new PreviewExperienceContract(
                contractVersion,
                background!,
                lighting!,
                navigation!,
                lightControls!,
                overlay!,
                itemSelection!,
                animationTransport!,
                new ReadOnlyCollection<PreviewInputBinding>(
                    [.. presentBindings.OrderBy(value => value.Action).ThenBy(value => value.Input)]),
                accessibility!));
    }
}
