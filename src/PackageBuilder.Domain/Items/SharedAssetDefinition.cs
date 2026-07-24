using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Items;

/// <summary>Declares one source asset that may be referenced by multiple item definitions.</summary>
public sealed class SharedAssetDefinition : IEquatable<SharedAssetDefinition>
{
    private SharedAssetDefinition(InternalAssetId id, SourceAsset source)
    {
        Id = id;
        Source = source;
    }

    public InternalAssetId Id { get; }

    public SourceAsset Source { get; }

    public static ItemValidationResult<SharedAssetDefinition> Create(
        InternalAssetId? id,
        SourceAsset? source) =>
        id is null
            ? ItemValidationResult<SharedAssetDefinition>.Failure(
                ItemValidationError.NullSharedAssetId)
            : source is null
            ? ItemValidationResult<SharedAssetDefinition>.Failure(
                ItemValidationError.NullSharedAssetSource)
            : ItemValidationResult<SharedAssetDefinition>.Success(
                new SharedAssetDefinition(id, source));

    public bool Equals(SharedAssetDefinition? other) =>
        other is not null && Id.Equals(other.Id) && Source.Equals(other.Source);

    public override bool Equals(object? obj) =>
        obj is SharedAssetDefinition other && Equals(other);

    public override int GetHashCode() =>
        StableItemHash.Create().Add(Id.Value).Add(Source.GetHashCode()).ToHashCode();
}
