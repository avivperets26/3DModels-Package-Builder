using System.IO;
using System.Security.Cryptography;
using PackageBuilder.App.Wpf.Media;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Materials;
using PackageBuilder.Domain.Media;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Textures;
using PackageBuilder.Targets.Unreal;

namespace PackageBuilder.App.Wpf.Tests;

[Trait("Task", "PB-1107")]
[Trait("Task", "PB-1108")]
[Trait("Task", "PB-1109")]
public sealed class UnrealSurfaceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryOrmPixelSurvivesPngRoundtripWithoutGammaOrChannelChanges(bool includeAo)
    {
        PreviewRaster ao = Raster(i => (byte)(i % 256));
        PreviewRaster roughness = Raster(i => (byte)(255 - i % 256));
        PreviewRaster metallic = Raster(i => (byte)(i * 17 % 256));
        byte[] source = roughness.Pixels.ToArray();
        PreviewRaster packed = UnrealOrmPacker.Pack(includeAo ? ao : null, roughness, metallic);
        var codec = new WindowsPreviewImageCodec();
        PreviewRaster decoded = WindowsPreviewImageCodec.DecodeTexture(WindowsPreviewImageCodec.EncodeTexture(packed));
        for (int i = 0; i < 256; i++)
        {
            Assert.Equal(includeAo ? (byte)i : (byte)255, decoded.Pixels[i * 4]);
            Assert.Equal((byte)(255 - i), decoded.Pixels[i * 4 + 1]);
            Assert.Equal((byte)(i * 17 % 256), decoded.Pixels[i * 4 + 2]);
            Assert.Equal(255, decoded.Pixels[i * 4 + 3]);
        }
        Assert.Equal(source, roughness.Pixels.ToArray());
    }

    [Fact]
    public void OrmRejectsMismatchedDimensionsAndMalformedRaster()
    {
        var pixel = new PreviewRaster(1, 1, new byte[4]);
        _ = Assert.Throws<ArgumentException>(() => UnrealOrmPacker.Pack(null, pixel, Raster(_ => 0)));
        _ = Assert.Throws<ArgumentException>(() => UnrealOrmPacker.Pack(Raster(_ => 0), pixel, pixel));
        _ = Assert.Throws<ArgumentException>(() => new PreviewRaster(int.MaxValue, 2, []));
        _ = Assert.Throws<ArgumentException>(() => new PreviewRaster(2, 2, new byte[4]));
        _ = Assert.Throws<InvalidDataException>(() => WindowsPreviewImageCodec.DecodeTexture(new byte[10]));
    }

    [Fact]
    public void CanonicalMaterialsPreserveModesAndReferences()
    {
        foreach (SurfaceMode mode in new[] { SurfaceMode.Opaque, SurfaceMode.Cutout, SurfaceMode.Transparent })
        {
            UnrealMaterialEntry entry = Material(mode);
            Assert.Equal(mode.CanonicalIdentifier, entry.Surface);
            Assert.True(entry.TwoSided);
            Assert.Equal("T_ORM", entry.Textures["orm"]);
            Assert.Equal(new double[] { 1, 1, 0, 0 }, entry.Uv);
        }
    }

    [Fact]
    public void SurfacePlanRejectsBadBindingsAndCollisionPolicies()
    {
        var content = UnrealImportPlan.Create(ProductFolderName.Create("PBSurfaceFixture").Value!, ProductCase.Static, TextureSources());
        UnrealMaterialEntry material = Material(SurfaceMode.Opaque);
        UnrealStaticMeshEntry mesh = Mesh("Sample", "opaque", "none", new string('a', 64), 10);
        _ = Assert.Throws<ArgumentException>(() => UnrealSurfacePlan.Create(content, [material], [mesh with { Collision = "automatic" }]));
        _ = Assert.Throws<ArgumentException>(() => UnrealSurfacePlan.Create(content, [material], [mesh with { Materials = ["missing"] }]));
        _ = Assert.Throws<ArgumentException>(() => UnrealSurfacePlan.Create(content, [material, material], []));
        _ = Assert.Throws<ArgumentException>(() => UnrealSurfacePlan.Create(content, [material with { Roughness = double.NaN }], []));
        _ = Assert.Throws<ArgumentException>(() => UnrealSurfacePlan.Create(content, [material with { Surface = "automatic" }], []));
        _ = Assert.Throws<ArgumentException>(() => UnrealSurfacePlan.Create(content, [material with { Opacity = 0.5 }], []));
        _ = Assert.Throws<ArgumentException>(() => UnrealSurfacePlan.Create(content, [material with { Uv = [1, 1, 0] }], []));
        _ = Assert.Throws<ArgumentException>(() => UnrealSurfacePlan.Create(content, [material with { Textures = new Dictionary<string, string> { ["height"] = "T_ORM" } }], []));
        _ = Assert.Throws<ArgumentException>(() => UnrealSurfacePlan.Create(content, [material], [mesh with { SourceReference = "../other.fbx" }]));
        string first = UnrealSurfacePlan.Create(content, [material], [mesh]).ToJson();
        Assert.Equal(first, UnrealSurfacePlan.Create(content, [material], [mesh]).ToJson());
    }

    [Fact]
    public void ExportLiveSurfaceFixtureWhenRequested()
    {
        // The live harness sets a new contained input directory. Ordinary CI still exercises plan creation.
        string? output = Environment.GetEnvironmentVariable("PB_UNREAL_SURFACE_FIXTURE");
        var codec = new WindowsPreviewImageCodec();
        var bytes = TexturePixels().ToDictionary(p => p.Key, p => WindowsPreviewImageCodec.EncodeTexture(p.Value), StringComparer.Ordinal);
        var content = UnrealImportPlan.Create(ProductFolderName.Create("PBSurfaceFixture").Value!, ProductCase.Static, TextureSources(bytes));
        UnrealMaterialEntry[] materials = [Material(SurfaceMode.Cutout), Material(SurfaceMode.Opaque), Material(SurfaceMode.Transparent)];
        string hash = new('a', 64);
        long size = 10;
        if (output is not null)
        {
            string root = FindRepository();
            string path = Path.GetFullPath(output);
            Assert.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(path));
            Assert.False(File.Exists(Path.Combine(path, "unreal-surface-plan.json")));
            byte[] fbx = File.ReadAllBytes(Path.Combine(path, "Asymmetric.fbx"));
            hash = Convert.ToHexStringLower(SHA256.HashData(fbx));
            size = fbx.Length;
            foreach (KeyValuePair<string, byte[]> texture in bytes)
            { File.WriteAllBytes(Path.Combine(path, texture.Key + ".png"), texture.Value); }
            File.WriteAllText(Path.Combine(path, "unreal-import-plan.json"), content.ToJson());
        }
        var plan = UnrealSurfacePlan.Create(content, materials,
            [Mesh("Box", "opaque", "box", hash, size), Mesh("Complex", "cutout", "complex-as-simple", hash, size), Mesh("None", "transparent", "none", hash, size)]);
        Assert.Equal(3, plan.Materials.Count);
        Assert.Equal(3, plan.Meshes.Count);
        if (output is not null)
        { File.WriteAllText(Path.Combine(output, "unreal-surface-plan.json"), plan.ToJson()); }
    }

    internal static UnrealStaticMeshEntry Mesh(string id, string material, string collision, string hash, long size) =>
        new(id, "Asymmetric.fbx", hash, size, [material], collision, [100, 200, 300], ["FixtureSurface"]);

    internal static UnrealMaterialEntry Material(SurfaceMode mode)
    {
        TextureRole[] roles = [TextureRole.Albedo, TextureRole.Normal, TextureRole.Emission, TextureRole.Opacity];
        TextureAssignment[] assignments = [.. roles.Select(role => TextureAssignment.Create(SourceAsset.Create(SourceAssetKind.Image, role.CanonicalIdentifier + ".png").Value!, role, role.RequiredColourSpace,
            role == TextureRole.Normal ? NormalConvention.DirectX : null).Value!)];
        MaterialDefinition definition = MaterialDefinition.Create(0.8, 0.7, 0.6, EmissionProperties.Create(0.2, 0.1, 0.05, 0.4).Value!, 0.7, 0,
            mode == SurfaceMode.Opaque ? 1 : 0.65, mode, mode == SurfaceMode.Cutout ? 0.4 : null,
            UvTransform.Create(1, 1, 0, 0).Value!, true, assignments).Value!;
        return UnrealMaterialEntry.Create(InternalAssetId.Create(mode.CanonicalIdentifier).Value!, definition,
            roles.ToDictionary(r => r.CanonicalIdentifier + ".png", r => "T_" + r.CanonicalIdentifier, StringComparer.Ordinal), "T_ORM");
    }

    internal static Dictionary<string, PreviewRaster> TexturePixels() => new(StringComparer.Ordinal)
    {
        ["albedo"] = Raster(i => (byte)(64 + i % 128), i => (byte)(i % 16 < 8 ? 255 : 0)),
        ["normal"] = new(16, 16, Enumerable.Range(0, 256).SelectMany(i => new byte[] { (byte)(96 + i % 64), 160, 245, 255 }).ToArray()),
        ["emission"] = Raster(_ => 128),
        ["opacity"] = Raster(i => (byte)(i % 16 < 8 ? 255 : 0)),
        ["ORM"] = UnrealOrmPacker.Pack(Raster(i => (byte)(128 + i % 128)), Raster(i => (byte)(i % 256)), Raster(i => (byte)(255 - i % 256)))
    };

    internal static IEnumerable<UnrealTextureSource> TextureSources(Dictionary<string, byte[]>? bytes = null)
    {
        bytes ??= TexturePixels().ToDictionary(p => p.Key, p => WindowsPreviewImageCodec.EncodeTexture(p.Value), StringComparer.Ordinal);
        foreach (KeyValuePair<string, byte[]> entry in bytes)
        {
            TextureRole role = entry.Key == "ORM" ? TextureRole.Roughness : TextureRole.TryParse(entry.Key).Value!;
            TextureAssignment assignment = TextureAssignment.Create(SourceAsset.Create(SourceAssetKind.Image, entry.Key + ".png").Value!, role, role.RequiredColourSpace,
                role == TextureRole.Normal ? NormalConvention.DirectX : null).Value!;
            yield return new(InternalAssetId.Create(entry.Key).Value!, assignment, Convert.ToHexStringLower(SHA256.HashData(entry.Value)), entry.Value.Length);
        }
    }

    private static PreviewRaster Raster(Func<int, byte> red, Func<int, byte>? alpha = null) =>
        new(16, 16, Enumerable.Range(0, 256).SelectMany(i => new byte[] { red(i), 37, 91, alpha?.Invoke(i) ?? 255 }).ToArray());

    internal static string FindRepository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PackageBuilder.sln")))
        { directory = directory.Parent; }
        return directory!.FullName;
    }
}
