using System.Security.Cryptography;
using System.Text.Json;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Textures;
using PackageBuilder.Targets.Unreal;

namespace PackageBuilder.App.Wpf.Tests;

[Trait("Task", "PB-1105")]
[Trait("Task", "PB-1106")]
public sealed class UnrealImportPlanTests
{
    [Theory]
    [InlineData("static", "Documentation,Maps,Materials,Meshes,Textures")]
    [InlineData("rigged", "Documentation,Maps,Materials,Meshes,Skeletons,Textures")]
    [InlineData("rigged-animated", "Animations,Documentation,Maps,Materials,Meshes,Skeletons,Textures")]
    [InlineData("item-set", "Blueprints,Documentation,Maps,Materials,Meshes,Textures")]
    [InlineData("item-collection", "Blueprints,Documentation,Maps,Materials,Meshes,Textures")]
    public void ProductCaseControlsOnlyApplicableFolders(string productCase, string folders)
    {
        var plan = UnrealImportPlan.Create(ProductFolderName.Create("SamplePack").Value!,
            ProductCase.TryParse(productCase).Value!, []);
        Assert.Equal("SamplePack", plan.ProjectName);
        Assert.Equal("/Game/SamplePack", plan.PackRoot);
        Assert.Equal(folders, string.Join(',', plan.Folders));
    }

    [Theory]
    [InlineData("albedo", null, true, "TC_DEFAULT", false, false)]
    [InlineData("emission", null, true, "TC_DEFAULT", true, false)]
    [InlineData("normal", "open-gl", false, "TC_NORMALMAP", true, true)]
    [InlineData("normal", "direct-x", false, "TC_NORMALMAP", true, false)]
    [InlineData("metallic", null, false, "TC_MASKS", true, false)]
    [InlineData("roughness", null, false, "TC_MASKS", true, false)]
    [InlineData("ambient-occlusion", null, false, "TC_MASKS", true, false)]
    [InlineData("opacity", null, false, "TC_MASKS", false, false)]
    [InlineData("height", null, false, "TC_MASKS", true, false)]
    public void CanonicalRolesMapToExplicitEngineSettings(string role, string? normal, bool srgb,
        string compression, bool noAlpha, bool flip)
    {
        UnrealTextureEntry entry = Assert.Single(Plan([Source("Example", role, normal)]).Textures);
        Assert.Equal((srgb, compression, noAlpha, flip), (entry.Srgb, entry.Compression, entry.NoAlpha, entry.FlipGreenChannel));
        Assert.Equal("T_Example", entry.AssetName);
        Assert.Equal("texture.png", entry.SourceReference);
    }

    [Fact]
    public void UnknownNormalOrientationAndCaseCollisionsFailBeforeEngineExecution()
    {
        _ = Assert.Throws<ArgumentException>(() => Plan([Source("Normal", "normal", "auto")]));
        _ = Assert.Throws<ArgumentException>(() => Plan([Source("Same", "albedo"), Source("same", "height")]));
        _ = Assert.Throws<ArgumentException>(() => Plan([Source("Bad", "height") with { Sha256 = "bad" }]));
        _ = Assert.Throws<ArgumentException>(() => Plan([Source("Bad", "height") with { ByteCount = 0 }]));
        _ = Assert.Throws<ArgumentException>(() => Plan(Enumerable.Range(0, 129).Select(i => Source("Texture" + i, "albedo"))));
        _ = Assert.ThrowsAny<ArgumentException>(() => UnrealImportPlan.Create(ProductFolderName.Create("Has Space").Value!, ProductCase.Static, []));
    }

    [Fact]
    public void HostPlanMatchesSharedEngineFixtureAndIsIndependentOfInputOrdering()
    {
        UnrealTextureSource[] sources = [Source("Albedo", "albedo"), Source("Emission", "emission"),
            Source("NormalGL", "normal", "open-gl"), Source("NormalDX", "normal", "direct-x"),
            Source("Metallic", "metallic"), Source("Roughness", "roughness"), Source("AO", "ambient-occlusion"),
            Source("Opacity", "opacity"), Source("Height", "height")];
        string json = Plan(sources).ToJson();
        Assert.Equal(json, Plan(sources.Reverse()).ToJson());
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureRoot, "unreal-import-plan.json")));
        using var actual = JsonDocument.Parse(json);
        Assert.True(JsonElement.DeepEquals(expected.RootElement, actual.RootElement));
    }

    private static UnrealImportPlan Plan(IEnumerable<UnrealTextureSource> sources) =>
        UnrealImportPlan.Create(ProductFolderName.Create("PBTextureFixture").Value!, ProductCase.Static, sources);

    private static UnrealTextureSource Source(string id, string role, string? normal = null)
    {
        TextureRole parsedRole = TextureRole.TryParse(role).Value!;
        SourceAsset asset = SourceAsset.Create(SourceAssetKind.Image, "texture.png").Value!;
        TextureAssignment assignment = TextureAssignment.Create(asset, parsedRole, parsedRole.RequiredColourSpace,
            normal is null ? null : NormalConvention.TryParse(normal).Value).Value!;
        byte[] bytes = File.ReadAllBytes(Path.Combine(FixtureRoot, "source", "texture.png"));
        return new(InternalAssetId.Create(id).Value!, assignment, Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length);
    }

    private static string FixtureRoot
    {
        get
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);
            while (current is not null && !File.Exists(Path.Combine(current.FullName, "PackageBuilder.sln")))
            { current = current.Parent; }
            return Path.Combine(current!.FullName, "tests", "fixtures", "unreal");
        }
    }
}
