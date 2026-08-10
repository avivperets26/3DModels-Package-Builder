using System.Collections.ObjectModel;

namespace PackageBuilder.Domain.Preview;

/// <summary>Identifies the renderer-independent camera projection required by a preview view.</summary>
public enum PreviewProjection
{
    Perspective = 0,
    Orthographic,
}

/// <summary>
/// Identifies one closed, engine-neutral preview view role and its required projection.
/// </summary>
public sealed class PreviewViewKind : IEquatable<PreviewViewKind>
{
    private PreviewViewKind(string canonicalIdentifier, PreviewProjection projection)
    {
        CanonicalIdentifier = canonicalIdentifier;
        Projection = projection;
    }

    /// <summary>Gets the primary three-quarter product view.</summary>
    public static PreviewViewKind Hero { get; } = new("hero", PreviewProjection.Perspective);

    /// <summary>Gets the front orthographic view.</summary>
    public static PreviewViewKind OrthographicFront { get; } =
        new("orthographic-front", PreviewProjection.Orthographic);

    /// <summary>Gets the back orthographic view.</summary>
    public static PreviewViewKind OrthographicBack { get; } =
        new("orthographic-back", PreviewProjection.Orthographic);

    /// <summary>Gets the left orthographic view.</summary>
    public static PreviewViewKind OrthographicLeft { get; } =
        new("orthographic-left", PreviewProjection.Orthographic);

    /// <summary>Gets the right orthographic view.</summary>
    public static PreviewViewKind OrthographicRight { get; } =
        new("orthographic-right", PreviewProjection.Orthographic);

    /// <summary>Gets a repeatable close-up detail view.</summary>
    public static PreviewViewKind Detail { get; } = new("detail", PreviewProjection.Perspective);

    /// <summary>Gets a repeatable representative animation-pose view.</summary>
    public static PreviewViewKind AnimationPose { get; } =
        new("animation-pose", PreviewProjection.Perspective);

    /// <summary>Gets an assembled set overview.</summary>
    public static PreviewViewKind SetOverview { get; } =
        new("set-overview", PreviewProjection.Perspective);

    /// <summary>Gets an all-items collection overview.</summary>
    public static PreviewViewKind CollectionOverview { get; } =
        new("collection-overview", PreviewProjection.Perspective);

    private static readonly ReadOnlyCollection<PreviewViewKind> _all =
        Array.AsReadOnly<PreviewViewKind>(
            [
                Hero,
                OrthographicFront,
                OrthographicBack,
                OrthographicLeft,
                OrthographicRight,
                Detail,
                AnimationPose,
                SetOverview,
                CollectionOverview,
            ]);

    /// <summary>Gets every supported view role in stable canonical order.</summary>
    public static IReadOnlyList<PreviewViewKind> All => _all;

    /// <summary>Gets the stable lowercase, hyphen-separated identity.</summary>
    public string CanonicalIdentifier { get; }

    /// <summary>Gets the projection that adapters must use for this role.</summary>
    public PreviewProjection Projection { get; }

    /// <inheritdoc />
    public bool Equals(PreviewViewKind? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PreviewViewKind other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StablePreviewHash.Create().Add(CanonicalIdentifier).ToHashCode();

    /// <inheritdoc />
    public override string ToString() => CanonicalIdentifier;
}
