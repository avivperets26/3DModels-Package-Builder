using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Preview;

/// <summary>Pairs one typed preview role with explicit renderer-independent visibility intent.</summary>
public sealed class PreviewViewDefinition : IEquatable<PreviewViewDefinition>
{
    private PreviewViewDefinition(
        InternalAssetId id,
        PreviewViewKind kind,
        PreviewVisibility visibility)
    {
        Id = id;
        Kind = kind;
        Visibility = visibility;
    }

    /// <summary>Gets the stable identity that distinguishes repeated detail or pose views.</summary>
    public InternalAssetId Id { get; }

    /// <summary>Gets the view role and required projection.</summary>
    public PreviewViewKind Kind { get; }

    /// <summary>Gets the subjects an adapter may render for this view.</summary>
    public PreviewVisibility Visibility { get; }

    /// <summary>
    /// Creates a view. Product-case compatibility and item membership are validated by
    /// <see cref="PreviewPresentationSpecification"/>.
    /// </summary>
    public static PreviewPresentationValidationResult<PreviewViewDefinition> Create(
        InternalAssetId? id,
        PreviewViewKind? kind,
        PreviewVisibility? visibility) =>
        id is null
            ? PreviewPresentationValidationResult<PreviewViewDefinition>.Failure(
                PreviewPresentationValidationError.NullViewId)
            : kind is null
            ? PreviewPresentationValidationResult<PreviewViewDefinition>.Failure(
                PreviewPresentationValidationError.NullViewKind)
            : visibility is null
            ? PreviewPresentationValidationResult<PreviewViewDefinition>.Failure(
                PreviewPresentationValidationError.NullVisibility)
            : PreviewPresentationValidationResult<PreviewViewDefinition>.Success(
                new PreviewViewDefinition(id, kind, visibility));

    /// <inheritdoc />
    public bool Equals(PreviewViewDefinition? other) =>
        other is not null && Id.Equals(other.Id) && Kind.Equals(other.Kind) &&
        Visibility.Equals(other.Visibility);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PreviewViewDefinition other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StablePreviewHash.Create()
            .Add(Id.Value)
            .Add(Kind.CanonicalIdentifier)
            .Add(Visibility.GetHashCode())
            .ToHashCode();
}
