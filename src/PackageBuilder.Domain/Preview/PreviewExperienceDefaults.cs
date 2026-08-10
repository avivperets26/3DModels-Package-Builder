namespace PackageBuilder.Domain.Preview;

/// <summary>Creates the approved PB-0913 contract from PB-0906 presentation tokens.</summary>
public static class PreviewExperienceDefaults
{
    /// <summary>Gets the validated current contract.</summary>
    public static PreviewExperienceContract Contract { get; } = Create();

    private static PreviewExperienceContract Create()
    {
        PreviewNavigationPolicy navigation = PreviewNavigationPolicy.Create(
            -80d, 80d, 0.75d, 50d, 0.22d, 5d, 0.16d, 0.14d).Value!;
        PreviewLightControlPolicy light = PreviewLightControlPolicy.Create(
            -80d, 80d, 1d, 5d).Value!;
        PreviewAnimationTransportPolicy animation =
            PreviewAnimationTransportPolicy.Create(true, false, 0.1d).Value!;
        PreviewAccessibilityPolicy accessibility = PreviewAccessibilityPolicy.Create(
        [
            Control("reset-view", "Reset View", "Reset preview view", "Restore the default camera orbit and zoom.", 0),
            Control("light-direction", "Light Direction", "Adjust key light direction", "Change the key-light yaw and pitch without changing intensity.", 1),
            Control("reset-light", "Reset Light", "Reset key light", "Restore the approved studio key-light direction.", 2),
            Control("item-previous", "Previous", "Previous preview item", "Show the previous item in deterministic order.", 3),
            Control("item-next", "Next", "Next preview item", "Show the next item in deterministic order.", 4),
            Control("item-select", "Item", "Select preview item", "Choose one item directly or show all items.", 5),
            Control("animation-select", "Animation", "Select animation", "Choose an animation clip without changing its source asset.", 6),
            Control("animation-play-pause", "Play", "Play or pause animation", "Play, pause, or resume the selected animation.", 7),
            Control("animation-replay", "Replay", "Replay animation", "Restart the selected animation from zero.", 8),
            Control("animation-timeline", "Timeline", "Animation timeline", "Scrub within the selected animation duration.", 9),
            Control("animation-loop", "Loop", "Loop animation preview", "Toggle preview looping without changing source clip import settings.", 10),
            Control("hide-controls", "Hide Controls", "Hide preview controls", "Hide the overlay for an unobstructed capture.", 11),
            Control("show-controls", "Show Controls", "Show preview controls", "Restore the hidden preview controls.", 12),
        ]).Value!;

        return PreviewExperienceContract.Create(
            PreviewExperienceContract.CurrentContractVersion,
            PreviewPresentationDefaults.Background,
            PreviewPresentationDefaults.Lighting,
            navigation,
            light,
            new PreviewOverlayPolicy(true, "show-controls"),
            new PreviewItemSelectionPolicy(true, true),
            animation,
            Bindings(),
            accessibility).Value!;
    }

    private static PreviewAccessibleControl Control(
        string id,
        string label,
        string name,
        string help,
        int order) => new(id, label, name, help, order);

    private static IEnumerable<PreviewInputBinding> Bindings()
    {
        yield return new(PreviewAction.OrbitPointer, "pointer-primary-drag");
        yield return new(PreviewAction.OrbitUp, "arrow-up");
        yield return new(PreviewAction.OrbitDown, "arrow-down");
        yield return new(PreviewAction.OrbitLeft, "arrow-left");
        yield return new(PreviewAction.OrbitRight, "arrow-right");
        yield return new(PreviewAction.ZoomPointer, "pointer-wheel");
        yield return new(PreviewAction.ZoomIn, "page-up");
        yield return new(PreviewAction.ZoomIn, "equals");
        yield return new(PreviewAction.ZoomIn, "numpad-plus");
        yield return new(PreviewAction.ZoomOut, "page-down");
        yield return new(PreviewAction.ZoomOut, "minus");
        yield return new(PreviewAction.ZoomOut, "numpad-minus");
        yield return new(PreviewAction.ResetView, "r");
        yield return new(PreviewAction.LightDirectionPointer, "focused-slider-pointer");
        yield return new(PreviewAction.LightUp, "focused-light-arrow-up");
        yield return new(PreviewAction.LightDown, "focused-light-arrow-down");
        yield return new(PreviewAction.LightLeft, "focused-light-arrow-left");
        yield return new(PreviewAction.LightRight, "focused-light-arrow-right");
        yield return new(PreviewAction.ResetLight, "l");
        yield return new(PreviewAction.ToggleOverlay, "h");
        yield return new(PreviewAction.FocusNext, "tab");
        yield return new(PreviewAction.FocusPrevious, "shift-tab");
        yield return new(PreviewAction.ActivateFocusedControl, "enter");
        yield return new(PreviewAction.ActivateFocusedControl, "space");
        yield return new(PreviewAction.ItemPrevious, "focused-previous-enter-or-space");
        yield return new(PreviewAction.ItemNext, "focused-next-enter-or-space");
        yield return new(PreviewAction.ItemDirect, "focused-item-selector");
        yield return new(PreviewAction.ItemAll, "focused-all-items-enter-or-space");
        yield return new(PreviewAction.AnimationSelect, "focused-animation-selector");
        yield return new(PreviewAction.AnimationPlayPause, "focused-play-pause-enter-or-space");
        yield return new(PreviewAction.AnimationReplay, "focused-replay-enter-or-space");
        yield return new(PreviewAction.AnimationScrub, "focused-timeline-arrows-or-pointer");
        yield return new(PreviewAction.AnimationLoop, "focused-loop-enter-or-space");
    }
}
