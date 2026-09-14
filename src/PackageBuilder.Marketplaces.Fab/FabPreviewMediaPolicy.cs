using PackageBuilder.Domain.Media;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>Conservative decimal-byte defaults from Fab's image requirements, verified 2026-09-11.
/// Callers can supply updated versioned limits to the shared optimizer without changing engine code.</summary>
public static class FabPreviewMediaPolicy
{
    public static PreviewMediaPolicy Current { get; } = FromProfile(FabRequirementsBaseline.Profile);

    /// <summary>Uses the exact selected rules snapshot; conservative decimal units are a recorded local policy.</summary>
    public static PreviewMediaPolicy FromProfile(FabRequirementsProfile profile) => new(
        $"fab-{profile.Document.Version}", profile.Rule("image-bytes").Limit!.Value,
        profile.Rule("gallery-bytes").Limit!.Value);
}
