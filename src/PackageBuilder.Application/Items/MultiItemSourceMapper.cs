using PackageBuilder.Domain.Manifests;

namespace PackageBuilder.Application.Items;

/// <summary>Applies reviewed source ownership through the canonical manifest validator.</summary>
public static class MultiItemSourceMapper
{
    /// <summary>
    /// Produces a new manifest only when every model source and item has unambiguous ownership.
    /// Failure retains the original draft unchanged; callers display findings and request review.
    /// </summary>
    public static ProductManifestValidationResult Map(
        ProductManifest manifest,
        IEnumerable<ItemSourceAssignment?> assignments)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(assignments);
        return manifest.WithItemSourceAssignments(assignments);
    }
}
