using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using PackageBuilder.App.Wpf.Media;
using PackageBuilder.Application.Media;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.Preview;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Materials;
using PackageBuilder.Domain.Media;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Preview;
using PackageBuilder.Domain.Products;
using PackageBuilder.Targets.Unreal;

namespace PackageBuilder.App.Wpf.Tests;

[Trait("Task", "PB-1110")]
[Trait("Task", "PB-1111")]
[Trait("Task", "PB-1112")]
public sealed class UnrealOverviewTests
{
    [Fact]
    public void SharedPresentationAndNativePlansProduceDeterministicStaticOverview()
    {
        (UnrealImportPlan content, UnrealSurfacePlan surfaces) = Plans(new string('a', 64), 10);
        PreviewPresentationSpecification presentation = PreviewPresentationDefaults.Create(ProductCase.Static).Value!;
        string json = UnrealOverviewPlan.Create(surfaces, presentation, "Sample product");
        Assert.Equal(json, UnrealOverviewPlan.Create(surfaces, presentation, "Sample product"));
        using var document = JsonDocument.Parse(json);
        Assert.Equal(5, document.RootElement.GetProperty("views").GetArrayLength());
        Assert.Equal(1920, document.RootElement.GetProperty("width").GetInt32());
        Assert.Equal(presentation.Background.OuterColour.Red, document.RootElement.GetProperty("background").GetProperty("outer")[0].GetDouble());
        _ = Assert.Throws<ArgumentException>(() => UnrealOverviewPlan.Create(surfaces, presentation, "bad\nlabel"));
        _ = Assert.Throws<ArgumentException>(() => UnrealOverviewPlan.Create(surfaces, PreviewPresentationDefaults.Create(ProductCase.Rigged).Value!));
        _ = Assert.Throws<ArgumentException>(() => UnrealOverviewPlan.Create(UnrealSurfacePlan.Create(content, surfaces.Materials, []), presentation));
    }

    [Fact]
    public void ExportLiveOverviewFixtureWhenRequested()
    {
        string? output = Environment.GetEnvironmentVariable("PB_UNREAL_OVERVIEW_INPUT");
        if (output is null)
        { return; }
        string path = Path.GetFullPath(output);
        Assert.StartsWith(Path.Combine(UnrealSurfaceTests.FindRepository(), "artifacts", "ue") + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(path, "unreal-overview-plan.json")));
        byte[] fbx = File.ReadAllBytes(Path.Combine(path, "Asymmetric.fbx"));
        (UnrealImportPlan content, UnrealSurfacePlan surfaces) = Plans(Convert.ToHexStringLower(SHA256.HashData(fbx)), fbx.Length);
        foreach (KeyValuePair<string, PreviewRaster> pair in UnrealSurfaceTests.TexturePixels().Where(p => p.Key != "opacity"))
        { File.WriteAllBytes(Path.Combine(path, pair.Key + ".png"), WindowsPreviewImageCodec.EncodeTexture(pair.Value)); }
        File.WriteAllText(Path.Combine(path, "unreal-import-plan.json"), content.ToJson());
        File.WriteAllText(Path.Combine(path, "unreal-surface-plan.json"), surfaces.ToJson());
        File.WriteAllText(Path.Combine(path, "unreal-overview-plan.json"), UnrealOverviewPlan.Create(surfaces, PreviewPresentationDefaults.Create(ProductCase.Static).Value!, "Static product"));
        File.WriteAllText(Path.Combine(path, "preview-experience.json"), PreviewExperienceJson.Serialize(PreviewExperienceDefaults.Contract).Json!);
        if (Environment.GetEnvironmentVariable("PB_UNREAL_INTERACTIVE") == "1")
        { File.WriteAllText(Path.Combine(path, "unreal-preview-plan.json"), UnrealPreviewPlan.Create(content.ProjectName, PreviewExperienceDefaults.Contract)); }
    }

    [Fact]
    public void ValidateLiveOverviewGalleryWhenRequested()
    {
        string? output = Environment.GetEnvironmentVariable("PB_UNREAL_OVERVIEW_MEDIA");
        if (output is null)
        { return; }
        string path = Path.GetFullPath(output);
        Assert.StartsWith(Path.Combine(UnrealSurfaceTests.FindRepository(), "artifacts", "ue") + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
        using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "capture-receipt.json")));
        var captures = new List<PreviewMediaInput>();
        var codec = new WindowsPreviewImageCodec();
        var decoder = new PreviewCaptureDecoder(codec);
        foreach (JsonElement capture in receipt.RootElement.GetProperty("captures").EnumerateArray())
        {
            string id = capture.GetProperty("id").GetString()!;
            double[] bounds = [.. capture.GetProperty("bounds").EnumerateArray().Select(v => v.GetDouble())];
            PreviewMediaInput input = decoder.Decode(BuildArtifactId.Create(id).Value!,
                File.ReadAllBytes(Path.Combine(path, id + ".png")), Sha256Digest.Create(capture.GetProperty("imageSha256").GetString()).Value!,
                File.ReadAllBytes(Path.Combine(path, id + "-coverage.png")), Sha256Digest.Create(capture.GetProperty("coverageSha256").GetString()).Value!,
                bounds[0], bounds[1], bounds[2], bounds[3], capture.GetProperty("depthClipped").GetBoolean(),
                capture.GetProperty("missingMaterials").GetInt32(), capture.GetProperty("visibleHelpers").GetInt32(), TestContext.Current.CancellationToken);
            captures.Add(input);
        }
        Assert.Equal(5, captures.Count);
        PreviewMediaResult result = new PreviewMediaOptimizer(codec).Optimize(captures, new PreviewImagePolicy(), new PreviewMediaPolicy("unreal-live-v1", 3_000_000, 25_000_000), TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(path, "media-validation.json"), JsonSerializer.Serialize(new
        {
            passed = result.Findings.Count == 0,
            findings = result.Findings.Select(f => f.Code.Value),
            images = result.Images.Select(i => new { id = i.ArtifactId.Value, i.Metrics, bytes = i.Bytes.Length })
        }));
        Assert.Empty(result.Findings);
        Assert.Equal(5, result.Images.Count);
    }

    private static (UnrealImportPlan, UnrealSurfacePlan) Plans(string hash, long size)
    {
        var content = UnrealImportPlan.Create(ProductFolderName.Create("PBOverviewFixture").Value!, ProductCase.Static,
            UnrealSurfaceTests.TextureSources().Where(t => t.Id.Value != "opacity"));
        UnrealMaterialEntry material = UnrealSurfaceTests.Material(SurfaceMode.Opaque);
        material = material with { Textures = material.Textures.Where(p => p.Key != "opacity").ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) };
        UnrealStaticMeshEntry mesh = UnrealSurfaceTests.Mesh("Box", "opaque", "box", hash, size) with { ExpectedSizeCm = [300, 300, 300] };
        return (content, UnrealSurfacePlan.Create(content, [material], [mesh]));
    }
}
