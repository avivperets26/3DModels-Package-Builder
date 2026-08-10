namespace PackageBuilder.Domain.Preview;

/// <summary>Names shared preview intents which engine adapters bind to their native input APIs.</summary>
public enum PreviewAction
{
    OrbitPointer = 0,
    OrbitUp,
    OrbitDown,
    OrbitLeft,
    OrbitRight,
    ZoomPointer,
    ZoomIn,
    ZoomOut,
    ResetView,
    LightDirectionPointer,
    LightUp,
    LightDown,
    LightLeft,
    LightRight,
    ResetLight,
    ToggleOverlay,
    FocusNext,
    FocusPrevious,
    ActivateFocusedControl,
    ItemPrevious,
    ItemNext,
    ItemDirect,
    ItemAll,
    AnimationSelect,
    AnimationPlayPause,
    AnimationReplay,
    AnimationScrub,
    AnimationLoop,
}

/// <summary>Maps one normalized pointer or keyboard gesture to a shared preview intent.</summary>
public sealed record PreviewInputBinding(PreviewAction Action, string Input)
{
    internal static IReadOnlySet<PreviewAction> RequiredActions { get; } =
        new HashSet<PreviewAction>(Enum.GetValues<PreviewAction>());
}
