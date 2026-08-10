namespace PackageBuilder.Domain.Preview;

/// <summary>Tracks capture-overlay visibility and the only control exposed while hidden.</summary>
public sealed record PreviewOverlayState(bool IsVisible, string? FocusedControlId)
{
    /// <summary>Creates the policy-defined initial overlay state.</summary>
    public static PreviewOverlayState Create(PreviewOverlayPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return new(policy.InitiallyVisible, policy.InitiallyVisible ? null : policy.RestoreControlId);
    }

    /// <summary>Toggles visibility and moves focus to the restore control whenever controls hide.</summary>
    public PreviewOverlayState Toggle(PreviewOverlayPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        bool visible = !IsVisible;
        return new(visible, visible ? null : policy.RestoreControlId);
    }
}
