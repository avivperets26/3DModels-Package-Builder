using System.Text.Json;
using System.Text.RegularExpressions;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Unreal;

/// <summary>Pairs a validated canonical assignment with its immutable input snapshot identity.</summary>
public sealed record UnrealTextureSource(InternalAssetId Id, TextureAssignment Assignment, string Sha256, long ByteCount);

/// <summary>Explicit Unreal import settings; workers apply these without redefining domain texture roles.</summary>
public sealed record UnrealTextureEntry(string AssetName, string SourceReference, string Sha256, long ByteCount,
    bool Srgb, string Compression, bool NoAlpha, bool FlipGreenChannel);

/// <summary>Versioned, deterministic Unreal content plan. No filesystem or engine operations occur here.</summary>
public sealed partial class UnrealImportPlan
{
    private UnrealImportPlan(string projectName, IReadOnlyList<string> folders, IReadOnlyList<UnrealTextureEntry> textures)
    {
        ProjectName = projectName;
        Folders = folders;
        Textures = textures;
    }

    /// <summary>Gets the closed worker plan schema version.</summary>
    public int SchemaVersion { get; } = 1;
    /// <summary>Gets the versioned target naming and import profile.</summary>
    public string Profile { get; } = "unreal-content-v1";
    /// <summary>Gets the safe project identity, also used as the sole content Pack folder.</summary>
    public string ProjectName { get; }
    /// <summary>Gets the sole product-owned virtual asset root.</summary>
    public string PackRoot => "/Game/" + ProjectName;
    /// <summary>Gets sorted product-relative folder names, including case-specific optional folders.</summary>
    public IReadOnlyList<string> Folders { get; }
    /// <summary>Gets sorted texture operations with immutable source identities and stable references.</summary>
    public IReadOnlyList<UnrealTextureEntry> Textures { get; }

    /// <summary>Projects canonical identities and roles into Unreal settings; ambiguous normals and collisions fail closed.</summary>
    public static UnrealImportPlan Create(ProductFolderName product, ProductCase productCase, IEnumerable<UnrealTextureSource> textures)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(productCase);
        ArgumentNullException.ThrowIfNull(textures);
        if (!Segment().IsMatch(product.Value))
        {
            throw new ArgumentException("Unreal project names require an ASCII letter followed by up to 39 letters, digits or underscores.", nameof(product));
        }

        var folders = new List<string> { "Meshes", "Materials", "Textures", "Maps", "Documentation" };
        if (productCase == ProductCase.Rigged || productCase == ProductCase.RiggedAnimated)
        { folders.Add("Skeletons"); }
        if (productCase == ProductCase.RiggedAnimated)
        { folders.Add("Animations"); }
        if (productCase == ProductCase.ItemSet || productCase == ProductCase.ItemCollection)
        { folders.Add("Blueprints"); }
        var entries = new List<UnrealTextureEntry>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (UnrealTextureSource source in textures)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(source.Id);
            ArgumentNullException.ThrowIfNull(source.Assignment);
            string name = "T_" + source.Id.Value;
            if (entries.Count == 128 || !Segment().IsMatch(source.Id.Value) || !names.Add(name) ||
                source.ByteCount is <= 0 or > 268_435_456 || !Hash().IsMatch(source.Sha256 ?? string.Empty))
            {
                throw new ArgumentException("Texture identity, count or snapshot bounds are invalid.", nameof(textures));
            }

            TextureAssignment assignment = source.Assignment;
            bool normal = assignment.Role.IsNormalMapData;
            if (normal && assignment.NormalConvention == NormalConvention.Auto)
            {
                throw new ArgumentException("Resolve automatic normal orientation before Unreal import.", nameof(textures));
            }

            bool srgb = assignment.ColourSpace == ColourSpace.Srgb;
            bool preserveAlpha = assignment.Role == TextureRole.Albedo || assignment.Role == TextureRole.Opacity;
            entries.Add(new(name, assignment.SourceAsset.LogicalReference, source.Sha256!, source.ByteCount,
                srgb, normal ? "TC_NORMALMAP" : srgb ? "TC_DEFAULT" : "TC_MASKS", !preserveAlpha,
                normal && assignment.NormalConvention == NormalConvention.OpenGl));
        }

        return new(product.Value, Array.AsReadOnly(folders.Order(StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(entries.OrderBy(entry => entry.AssetName, StringComparer.Ordinal).ToArray()));
    }

    /// <summary>Serializes the closed worker boundary as UTF-8-compatible JSON with stable property ordering.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, _jsonOptions);

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    [GeneratedRegex("\\A[A-Za-z][A-Za-z0-9_]{0,39}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Segment();
    [GeneratedRegex("\\A[a-f0-9]{64}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Hash();
}
