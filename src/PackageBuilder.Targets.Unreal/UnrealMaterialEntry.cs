using PackageBuilder.Domain.Materials;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Unreal;

/// <summary>Explicit graph intent projected from the canonical material; engine workers do not infer surface modes.</summary>
public sealed record UnrealMaterialEntry(string Id, string Surface, bool TwoSided, double AlphaCutoff,
    double Metallic, double Roughness, double NormalScale, double OcclusionStrength, double Opacity,
    IReadOnlyList<double> Emission, IReadOnlyList<double> Uv, IReadOnlyDictionary<string, string> Textures)
{
    /// <summary>Resolves canonical texture source references to imported asset names and an optional prepacked ORM map.</summary>
    public static UnrealMaterialEntry Create(InternalAssetId id, MaterialDefinition material,
        IReadOnlyDictionary<string, string> textureAssets, string? ormAsset = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(textureAssets);
        UnrealImportPlan.ValidateName(id.Value);
        if (material.HeightScale != 0 || material.TextureAssignments.Any(a => a.Role.CanonicalIdentifier == "height"))
        { throw new ArgumentException("Displacement requires a separate supported target policy.", nameof(material)); }
        var textures = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (TextureAssignment assignment in material.TextureAssignments)
        {
            string role = assignment.Role.CanonicalIdentifier;
            if (role is "ambient-occlusion" or "roughness" or "metallic")
            {
                if (ormAsset is null)
                { throw new ArgumentException("Separate scalar maps require ORM packing first.", nameof(ormAsset)); }
                continue;
            }
            if (!textureAssets.TryGetValue(assignment.SourceAsset.LogicalReference, out string? asset))
            { throw new ArgumentException("A canonical texture assignment has no imported asset.", nameof(textureAssets)); }
            textures.Add(role, asset);
        }
        if (ormAsset is not null)
        { textures.Add("orm", ormAsset); }
        return new(id.Value, material.SurfaceMode.CanonicalIdentifier, material.IsDoubleSided, material.AlphaCutoff ?? 0.333,
            material.MetallicFactor, material.RoughnessFactor, material.NormalScale, material.AmbientOcclusionStrength,
            material.Opacity, Array.AsReadOnly([material.Emission.Red, material.Emission.Green, material.Emission.Blue, material.Emission.Intensity]),
            Array.AsReadOnly([material.UvTransform.ScaleU, material.UvTransform.ScaleV, material.UvTransform.OffsetU, material.UvTransform.OffsetV]),
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(textures));
    }
}
