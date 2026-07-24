using PackageBuilder.Domain.Materials;
using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Manifests;

/// <summary>Adds stable manifest identity to an existing renderer-independent material.</summary>
public sealed class ManifestMaterial : IEquatable<ManifestMaterial>
{
    private ManifestMaterial(InternalAssetId id, MaterialDefinition definition)
    {
        Id = id;
        Definition = definition;
    }

    public InternalAssetId Id { get; }

    public MaterialDefinition Definition { get; }

    public static ManifestMaterialResult Create(
        InternalAssetId? id,
        MaterialDefinition? definition) =>
        id is null
            ? ManifestMaterialResult.Failure(ManifestMaterialError.NullId)
            : definition is null
            ? ManifestMaterialResult.Failure(ManifestMaterialError.NullDefinition)
            : ManifestMaterialResult.Success(new ManifestMaterial(id, definition));

    public bool Equals(ManifestMaterial? other) =>
        other is not null && Id.Equals(other.Id) && Definition.Equals(other.Definition);

    public override bool Equals(object? obj) => obj is ManifestMaterial other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)Id.GetHashCode()) * 16777619u;
            hash = (hash ^ (uint)Definition.GetHashCode()) * 16777619u;
            return (int)hash;
        }
    }
}

public enum ManifestMaterialError
{
    None = 0,
    NullId,
    NullDefinition,
}

public sealed class ManifestMaterialResult
{
    private ManifestMaterialResult(
        bool isValid,
        ManifestMaterial? value,
        ManifestMaterialError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public ManifestMaterial? Value { get; }

    public ManifestMaterialError Error { get; }

    internal static ManifestMaterialResult Success(ManifestMaterial value) =>
        new(true, value, ManifestMaterialError.None);

    internal static ManifestMaterialResult Failure(ManifestMaterialError error) =>
        new(false, null, error);
}
