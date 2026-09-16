using System.Text.Json;
using PackageBuilder.Domain.Materials;

namespace PackageBuilder.Targets.Unreal;

/// <summary>Bounded normalized-FBX import intent; collision is explicitly none, box, or complex-as-simple.</summary>
public sealed record UnrealStaticMeshEntry(string Id, string SourceReference, string Sha256, long ByteCount,
    IReadOnlyList<string> Materials, string Collision, IReadOnlyList<double> ExpectedSizeCm, IReadOnlyList<string> SourceSlots);

/// <summary>Companion to the texture plan; pins graph/mesh policy without changing existing v1 texture requests.</summary>
public sealed partial class UnrealSurfacePlan
{
    private UnrealSurfacePlan(UnrealImportPlan content, UnrealMaterialEntry[] materials, UnrealStaticMeshEntry[] meshes)
    { ProjectName = content.ProjectName; Materials = Array.AsReadOnly(materials); Meshes = Array.AsReadOnly(meshes); }
    /// <summary>Gets the closed companion plan schema.</summary>
    public int SchemaVersion { get; } = 1;
    /// <summary>Gets the pinned graph, geometry and collision policy identity.</summary>
    public string Profile { get; } = "unreal-surfaces-v1";
    /// <summary>Gets the project identity shared with the texture plan and Pack root.</summary>
    public string ProjectName { get; }
    /// <summary>Gets immutable material entries ordered by identity.</summary>
    public IReadOnlyList<UnrealMaterialEntry> Materials { get; }
    /// <summary>Gets immutable normalized-FBX operations ordered by identity.</summary>
    public IReadOnlyList<UnrealStaticMeshEntry> Meshes { get; }

    /// <summary>Rejects unresolved texture/material references and snapshots lists before crossing the engine boundary.</summary>
    public static UnrealSurfacePlan Create(UnrealImportPlan content, IEnumerable<UnrealMaterialEntry> materials,
        IEnumerable<UnrealStaticMeshEntry> meshes)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(meshes);
        UnrealMaterialEntry[] materialArray = [.. materials.Take(129).OrderBy(m => m.Id, StringComparer.Ordinal)];
        UnrealStaticMeshEntry[] meshArray = [.. meshes.Take(129).OrderBy(m => m.Id, StringComparer.Ordinal)];
        if (materialArray.Length > 128 || meshArray.Length > 128)
        { throw new ArgumentException("Surface count exceeds limits."); }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (UnrealMaterialEntry material in materialArray)
        {
            UnrealImportPlan.ValidateName(material.Id);
            ValidateMaterial(material);
            if (!names.Add(material.Id))
            { throw new ArgumentException("Material identity collision."); }
            foreach (KeyValuePair<string, string> texture in material.Textures)
            {
                UnrealTextureEntry entry = content.Textures.Single(t => t.AssetName == texture.Value);
                string compression = texture.Key == "normal" ? "TC_NORMALMAP" : texture.Key is "albedo" or "emission" ? "TC_DEFAULT" : "TC_MASKS";
                if (entry.Compression != compression || entry.Srgb != (texture.Key is "albedo" or "emission"))
                { throw new ArgumentException("Texture policy does not match material use."); }
                if (texture.Key == "albedo" && entry.NoAlpha)
                { throw new ArgumentException("Albedo alpha must be preserved."); }
            }
        }
        var meshNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (UnrealStaticMeshEntry mesh in meshArray)
        {
            UnrealImportPlan.ValidateName(mesh.Id);
            if (!meshNames.Add(mesh.Id) || mesh.Collision is not ("none" or "box" or "complex-as-simple") ||
                mesh.Materials.Count is < 1 or > 64 || mesh.Materials.Any(m => !materialArray.Any(a => a.Id == m)) ||
                mesh.SourceSlots.Count != mesh.Materials.Count || mesh.SourceSlots.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 128) ||
                mesh.SourceSlots.Distinct(StringComparer.Ordinal).Count() != mesh.SourceSlots.Count ||
                mesh.ExpectedSizeCm.Count != 3 || mesh.ExpectedSizeCm.Any(d => !double.IsFinite(d) || d is < 0.000001 or > 1_000_000) ||
                mesh.ByteCount is <= 0 or > 268_435_456 || !Sha256Pattern().IsMatch(mesh.Sha256) ||
                !mesh.SourceReference.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                mesh.SourceReference.Contains('\\') || mesh.SourceReference.Contains(':') ||
                mesh.SourceReference.Split('/').Any(s => s is "" or "." or ".."))
            { throw new ArgumentException("Invalid static mesh plan."); }
        }
        return new(content, [.. materialArray.Select(m => m with
        {
            Emission = Array.AsReadOnly(m.Emission.ToArray()),
            Uv = Array.AsReadOnly(m.Uv.ToArray()),
            Textures = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(m.Textures.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal))
        })],
            [.. meshArray.Select(m => m with { Materials = Array.AsReadOnly(m.Materials.ToArray()), ExpectedSizeCm = Array.AsReadOnly(m.ExpectedSizeCm.ToArray()), SourceSlots = Array.AsReadOnly(m.SourceSlots.ToArray()) })]);
    }
    /// <summary>Serializes explicit adapter settings in deterministic canonical order.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, _jsonOptions);
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Public records are convenient transport values, not a way to bypass canonical domain validation.
    private static void ValidateMaterial(UnrealMaterialEntry material)
    {
        if (material.Emission.Count != 4 || material.Uv.Count != 4 ||
            material.Emission.Any(v => !double.IsFinite(v) || v is < 0 or > 1_000_000) ||
            material.Uv.Any(v => !double.IsFinite(v) || Math.Abs(v) > 1_000_000) ||
            material.NormalScale > 1000 || !double.IsFinite(material.AlphaCutoff) || material.AlphaCutoff is < 0 or > 1 ||
            material.Textures.Keys.Any(k => k is not ("albedo" or "normal" or "orm" or "emission" or "opacity")))
        { throw new ArgumentException("Material exceeds the engine profile bounds."); }
        EmissionPropertiesValidationResult emission = EmissionProperties.Create(material.Emission[0], material.Emission[1], material.Emission[2], material.Emission[3]);
        UvTransformValidationResult uv = UvTransform.Create(material.Uv[0], material.Uv[1], material.Uv[2], material.Uv[3]);
        SurfaceMode? mode = SurfaceMode.TryParse(material.Surface).Value;
        MaterialDefinitionValidationResult validated = MaterialDefinition.Create(material.Metallic, material.Roughness, material.NormalScale,
            emission.Value, material.OcclusionStrength, 0, material.Opacity, mode,
            mode == SurfaceMode.Cutout ? material.AlphaCutoff : null, uv.Value, material.TwoSided, []);
        if (validated.Value is null)
        { throw new ArgumentException("Material intent is invalid."); }
    }

    [System.Text.RegularExpressions.GeneratedRegex("\\A[a-f0-9]{64}\\z")]
    private static partial System.Text.RegularExpressions.Regex Sha256Pattern();
}
