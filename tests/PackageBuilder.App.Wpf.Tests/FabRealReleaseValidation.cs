using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PackageBuilder.App.Wpf.Media;
using PackageBuilder.Application.Documentation;
using PackageBuilder.Application.Media;
using PackageBuilder.Contracts.Archives;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.Manifests;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Preview;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Infrastructure.Archives;
using PackageBuilder.Infrastructure.Artifacts;
using PackageBuilder.Marketplaces.Fab;
using static PackageBuilder.App.Wpf.Tests.FabValidatorFixtures;

namespace PackageBuilder.App.Wpf.Tests;

/// <summary>Consumes one owned live engine run; receipt emission distinguishes real acceptance from normal CI.
/// Inputs are local trusted harness results. Every file is contained, bounded and checked before use.</summary>
internal static class FabRealReleaseValidation
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    internal static async Task RunAsync(FabReleaseFixtures fixture, FabRequirementsProfileUpdater updater,
        string pointerPath, CancellationToken token)
    {
        string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        pointerPath = Contained(repository, pointerPath);
        using var pointer = JsonDocument.Parse(File.ReadAllText(pointerPath));
        JsonElement p = pointer.RootElement;
        string run = Contained(repository, p.GetProperty("runRoot").GetString()!);
        Assert.StartsWith(Path.Combine(repository, "artifacts", "u") + Path.DirectorySeparatorChar, run, StringComparison.OrdinalIgnoreCase);
        string project = Contained(run, p.GetProperty("cleanProject").GetString()!);
        using var clean = JsonDocument.Parse(File.ReadAllText(Contained(run, p.GetProperty("cleanReimportResult").GetString()!)));
        Assert.True(clean.RootElement.GetProperty("passed").GetBoolean());
        Assert.Empty(clean.RootElement.GetProperty("findings").EnumerateArray());
        using var inspection = JsonDocument.Parse(File.ReadAllText(Contained(run, "fab-inspection.json")));
        JsonElement observed = inspection.RootElement;
        Assert.Equal(fixture.Request.Versions.Unity.Value, observed.GetProperty("unityVersion").GetString());
        FabValidationContext context = fixture.Request.Context with { ProductKey = "StoneArch", ItemKeys = ["StoneArch"] };
        string package = Contained(run, p.GetProperty("package").GetString()!);
        byte[] packageBytes = Read(package);
        var assets = new List<FabUnityAsset>();
        foreach (JsonElement asset in observed.GetProperty("assets").EnumerateArray())
        {
            string path = asset.GetProperty("path").GetString()!;
            ArtifactContentIdentity identity = Identity(Read(Contained(project, path)));
            Assert.Equal(identity.Sha256.Value, asset.GetProperty("sha256").GetString());
            Assert.Equal(identity.Bytes, asset.GetProperty("bytes").GetInt64());
            assets.Add(new(path, identity, [.. asset.GetProperty("dependencies").EnumerateArray().Select(v => v.GetString()!)], asset.GetProperty("isUsed").GetBoolean()));
        }
        using var dependencies = JsonDocument.Parse(File.ReadAllText(Contained(project, "Packages/packages-lock.json")));
        var external = assets.SelectMany(a => a.Dependencies).Where(d => d.StartsWith("Packages/", StringComparison.Ordinal))
            .Select(d => string.Join('/', d.Split('/').Take(2))).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(root => new FabUnityDependency(root, dependencies.RootElement.GetProperty("dependencies")
                .GetProperty(root[9..]).GetProperty("version").GetString()!, true, true)).ToImmutableArray();
        string[] expected = [.. File.ReadAllLines(Contained(run, p.GetProperty("packageManifest").GetString()!)).Where(path => File.Exists(Contained(project, path)))];
        var unity = new FabUnityPackageInspection(Id("unity"), Identity(packageBytes), "StoneArch.unitypackage",
            observed.GetProperty("productRoot").GetString()!, [.. expected], [.. assets], external,
            Evidence(context, Id("unity"), Identity(packageBytes), "unity-package"),
            Evidence(context, Id("unity"), Identity(packageBytes), "unity-clean-import"));

        var codec = new WindowsPreviewImageCodec();
        var decoder = new PreviewCaptureDecoder(codec);
        PreviewMediaInput[] captures = [.. observed.GetProperty("images").EnumerateArray().Select(image => decoder.Decode(
            Id(image.GetProperty("role").GetString()!), Read(Contained(project, image.GetProperty("file").GetString()!)),
            Sha256Digest.Create(image.GetProperty("sha256").GetString()).Value!,
            Read(Contained(project, image.GetProperty("coverageFile").GetString()!)), Sha256Digest.Create(image.GetProperty("coverageSha256").GetString()).Value!,
            image.GetProperty("left").GetDouble(), image.GetProperty("bottom").GetDouble(), image.GetProperty("right").GetDouble(), image.GetProperty("top").GetDouble(),
            image.GetProperty("depthClipped").GetBoolean(), image.GetProperty("missingMaterials").GetInt32(), image.GetProperty("visibleHelpers").GetInt32(), token))];
        PreviewMediaResult media = new PreviewMediaOptimizer(codec).Optimize(captures, new(), FabPreviewMediaPolicy.FromProfile(fixture.SelectedProfile), token);
        Assert.True(media.IsSuccessful, string.Join(",", media.Findings.Select(f => f.Code.Value)));
        Assert.Equal(5, media.Images.Count);
        ImmutableArray<FabGalleryImage> gallery = [.. media.Images.Select(image => new FabGalleryImage(image.ArtifactId,
            Identity(image.Bytes), image.ArtifactId.Value + (image.Format == PreviewImageFormat.Png ? ".png" : ".jpg"), image.Bytes.ToArray(),
            context.ProductKey, image.ArtifactId.Value == "hero" ? PreviewViewKind.Hero :
                PreviewViewKind.All.Single(v => v.CanonicalIdentifier == "orthographic-" + image.ArtifactId.Value), context.ItemKeys, image.ArtifactId.Value == "hero"))];

        var portable = File.ReadAllLines(Path.Combine(repository, "artifacts/PB-0507/manual/latest.txt"))
            .Select(line => line.Split('=', 2)).Where(parts => parts.Length == 2).ToDictionary(parts => parts[0], parts => parts[1]);
        string archive = Contained(repository, portable["release"]);
        using var portableReport = JsonDocument.Parse(File.ReadAllText(Contained(repository, portable["report"])));
        Assert.Equal("passed", portableReport.RootElement.GetProperty("status").GetString());
        Assert.Empty(portableReport.RootElement.GetProperty("findings").EnumerateArray());
        byte[] archiveBytes = Read(archive);
        Assert.Equal(Identity(archiveBytes).Sha256.Value, portableReport.RootElement.GetProperty("releaseArtifact").GetProperty("sha256").GetString());
        using ZipArchive zip = ZipFile.OpenRead(archive);
        using var reimport = JsonDocument.Parse(File.ReadAllText(Contained(run, "fab-portable-reimport.json")));
        Assert.True(reimport.RootElement.GetProperty("passed").GetBoolean());
        Assert.Equal(fixture.Request.Versions.Blender.Value, reimport.RootElement.GetProperty("blenderVersion").GetString());
        using (Stream model = zip.Entries.Single(e => e.FullName.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)).Open())
        using (var modelBytes = new MemoryStream())
        {
            await model.CopyToAsync(modelBytes, token);
            string hash = Identity(modelBytes.ToArray()).Sha256.Value;
            Assert.Equal(hash, reimport.RootElement.GetProperty("sourceSha256").GetString());
            Assert.Equal(hash, assets.Single(a => a.Path.EndsWith("/Source/StoneArch.fbx", StringComparison.Ordinal)).Content.Sha256.Value);
        }
        string[] planned = [.. zip.Entries.Where(e => !e.FullName.EndsWith('/')).Select(e => e.FullName)];
        string extraction = Path.Combine(fixture.Root, "preflight");
        _ = Directory.CreateDirectory(extraction);
        ArchiveSafetyPolicy policy = ArchiveSafetyPolicy.Create(100_000_000, 1000, 8, 100_000_000, 100_000_000, 1000, [".fbx", ".txt", ".md", ".json", ".png"]).Value!;
        var download = new FabPortableDownload(Id("portable"), Identity(archiveBytes), context.ProductKey, "fbx", false,
            new(repository, archive, extraction, Path.Combine(extraction, "expanded"), policy), [.. planned],
            Evidence(context, Id("portable"), Identity(archiveBytes), "portable-target"));

        JsonNode manifestJson = JsonNode.Parse(File.ReadAllText(Path.Combine(repository, "tests/fixtures/portable/static-vertical-slice/product-manifest.json")))!;
        manifestJson["publisherProfileReference"] = fixture.Request.Listing.Publisher.Root.Value;
        manifestJson["targets"] = new JsonArray("unity");
        ProductManifest manifest = ProductManifestJson.Deserialize(manifestJson.ToJsonString()).Value!;
        ReadmeBuildData build = ReadmeBuildData.Create(BuildTarget.Unity, [new("Unity/StoneArch.unitypackage", "unitypackage", observed.GetProperty("unityVersion").GetString()!)],
            new(observed.GetProperty("width").GetDouble(), observed.GetProperty("height").GetDouble(), observed.GetProperty("depth").GetDouble(),
                observed.GetProperty("triangles").GetInt64(), observed.GetProperty("materials").GetInt32(), observed.GetProperty("textures").GetInt32(), 1, "+Y", "+Z", "bottom-center"),
            new("unity", observed.GetProperty("unityVersion").GetString()!, "URP", external.Single(d => d.Root == "Packages/com.unity.render-pipelines.universal").Version),
            external.Select(d => new ReadmeDependency(d.Root, d.Version)),
            ["Create a Unity URP project with the listed dependencies; import StoneArch.unitypackage.", "Open the overview scene and enter Play mode; use the on-screen camera controls.", "Drag the product prefab into your scene. This is a disposable static validation fixture."]).Value!;
        DocumentationResult<ReadmeDocument> document = SharedReadmeGenerator.Generate(manifest, fixture.Request.Listing.Publisher, build, token);
        Assert.True(document.IsSuccess, document.Error);
        FabListingDraft listing = fixture.Request.Listing with { ProductKey = "StoneArch", Title = "StoneArch validation cube", Description = "A disposable static cube fixture for release validation.", Dependencies = [.. external.Select(d => new FabListingDependency(d.Root, d.Version, "Install through Unity Package Manager.", null))] };
        FabReleaseRequest request = fixture.Request with
        {
            Context = context,
            Listing = listing,
            Downloads = [download],
            Unity = unity,
            Gallery = gallery,
            Sources = [FabReleaseFixtures.Source(context, "portable", "fbx", "StoneArch_FBX.zip", archiveBytes),
                FabReleaseFixtures.Source(context, "unity", "unity", "StoneArch.unitypackage", packageBytes),
                FabReleaseFixtures.Source(context, "docs", "documentation", "README.md", Encoding.UTF8.GetBytes(document.Value!.Text))],
            MediaQualityEvidence = [.. gallery.Select(image => Evidence(context, image.ArtifactId, image.Content, "media-quality"))]
        };
        using var output = new MemoryStream();
        var composer = new FabReleaseComposer(updater, new(new SafeZipArchiveService(), new ArtifactHashService()), new(codec), new VerifiedReleaseArchiveWriter());
        FabComposedRelease release = await composer.ComposeAsync(request, output, token);
        string report = JsonSerializer.Serialize(new
        {
            realEngineRun = true,
            passed = release.IsSuccess,
            profile = fixture.SelectedProfile.Sha256,
            bytes = release.Content?.Bytes,
            sha256 = release.Content?.Sha256.Value,
            findings = release.Validation.Findings.Select(f => new { code = f.Code.Value, message = f.Explanation.Value }),
            unityAssets = assets.Count,
            galleryImages = gallery.Length
        }, _json);
        if (!release.IsSuccess)
        { await File.WriteAllTextAsync(Contained(run, "fab-release-result.json"), report, token); }
        Assert.True(release.IsSuccess, report);
        using var composed = new ZipArchive(output, ZipArchiveMode.Read, true);
        Assert.Equal(13, composed.Entries.Count);
        using var reader = new StreamReader(composed.GetEntry("StoneArch/1.0.0/release-manifest.json")!.Open());
        using var inventory = JsonDocument.Parse(await reader.ReadToEndAsync(token));
        foreach (JsonElement entry in inventory.RootElement.GetProperty("entries").EnumerateArray())
        {
            using Stream content = composed.GetEntry(entry.GetProperty("path").GetString()!)!.Open();
            using var bytes = new MemoryStream();
            await content.CopyToAsync(bytes, token);
            Assert.Equal(entry.GetProperty("sha256").GetString(), Identity(bytes.ToArray()).Sha256.Value);
        }
        await File.WriteAllTextAsync(Contained(run, "fab-release-result.json"), report, token);
    }

    private static byte[] Read(string path)
    {
        Assert.InRange(new FileInfo(path).Length, 1, 100_000_000);
        return File.ReadAllBytes(path);
    }

    private static string Contained(string root, string path)
    {
        string full = Path.GetFullPath(path, root);
        Assert.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, full, StringComparison.OrdinalIgnoreCase);
        for (string? current = full; current is not null && current != root; current = Path.GetDirectoryName(current))
        {
            if (File.Exists(current) || Directory.Exists(current))
            { Assert.Equal(0, (int)(File.GetAttributes(current) & FileAttributes.ReparsePoint)); }
        }
        return full;
    }
}
