using PackageBuilder.Domain.Media;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>Conservative decimal-byte defaults from Fab's image requirements, verified 2026-09-11.
/// Callers can supply updated versioned limits to the shared optimizer without changing engine code.</summary>
public static class FabPreviewMediaPolicy
{
    public static PreviewMediaPolicy Current { get; } = new("fab-2026-09-11", 3_000_000, 25_000_000);
}
