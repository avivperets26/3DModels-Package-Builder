using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PackageBuilder.App.Wpf.Media;
using PackageBuilder.Contracts.Archives;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Contracts.Profiles;
using PackageBuilder.Domain.Preview;
using PackageBuilder.Domain.Profiles;
using PackageBuilder.Domain.Tools;
using PackageBuilder.Infrastructure.Archives;
using PackageBuilder.Infrastructure.Artifacts;
using PackageBuilder.Marketplaces.Fab;
using static PackageBuilder.App.Wpf.Tests.FabValidatorFixtures;

namespace PackageBuilder.App.Wpf.Tests;

/// <summary>Owned small synthetic integration inputs, never a claimed real Unity export or official profile approval.</summary>
internal sealed class FabReleaseFixtures : IDisposable
{
    internal string Root { get; } = Path.Combine(AppContext.BaseDirectory, "PB-1010", Guid.NewGuid().ToString("N"));
    internal FabReleaseRequest Request { get; }
    internal FabRequirementsProfile SelectedProfile { get; }
    internal FabReleaseFixtures(FabRequirementsProfile? profile = null)
    {
        SelectedProfile = profile ?? Profile();
        _ = Directory.CreateDirectory(Root);
        try
        {
            FabValidationContext context = Context() with { Listing = Context().Listing with { Formats = ["fbx", "unity"] } };
            string path = Path.Combine(Root, "Product.zip");
            using (FileStream output = File.Create(path))
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
            using (Stream entry = zip.CreateEntry("Product/Model.fbx").Open())
            { entry.Write("synthetic model"u8); }
            byte[] zipBytes = File.ReadAllBytes(path);
            ArtifactContentIdentity zipIdentity = Identity(zipBytes);
            string containment = Path.Combine(Root, "Preflight");
            _ = Directory.CreateDirectory(containment);
            ArchiveSafetyPolicy policy = ArchiveSafetyPolicy.Create(1_000_000, 100, 8, 1_000_000, 1_000_000, 100, [".fbx"]).Value!;
            var download = new FabPortableDownload(Id("portable"), zipIdentity, "Product", "fbx", false,
                new(Root, path, containment, Path.Combine(containment, "Extraction"), policy), ["Product/Model.fbx"],
                Evidence(context, Id("portable"), zipIdentity, "portable-target"));
            byte[] unityBytes = "synthetic unity package"u8.ToArray();
            string[] names = ["Assets/Product/Models/Model.fbx", "Assets/Product/Scenes/Overview.unity", "Assets/Product/Documentation/README.md"];
            ImmutableArray<FabUnityAsset> assets = [.. names.Select(name => new FabUnityAsset(name, Identity(Encoding.UTF8.GetBytes(name)), [], true))];
            var unity = new FabUnityPackageInspection(Id("unity"), Identity(unityBytes), "Product.unitypackage", "Assets/Product", [.. names], assets, [],
                Evidence(context, Id("unity"), Identity(unityBytes), "unity-package"), Evidence(context, Id("unity"), Identity(unityBytes), "unity-clean-import"));
            byte[] pixels = new byte[1920 * 1080 * 3];
            Array.Fill(pixels, (byte)100);
            var bitmap = BitmapSource.Create(1920, 1080, 96, 96, PixelFormats.Rgb24, null, pixels, 1920 * 3);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var imageStream = new MemoryStream();
            encoder.Save(imageStream);
            byte[] image = imageStream.ToArray();
            var gallery = new FabGalleryImage(Id("hero"), Identity(image), "Hero.png", image, "Product", PreviewViewKind.Hero, ["Item"], true);
            var versions = new BuildLock(context.JobId, "1.0.0", ToolVersion.Create(ToolKind.DotNet, "10.0.302").Value!,
                ToolVersion.Create(ToolKind.Blender, "5.0.0").Value!, ToolVersion.Create(ToolKind.Unity, "6000.3.10f1").Value!,
                ToolVersion.Create(ToolKind.Unreal, "5.8.0").Value!, 1, [new("unity-worker", "1.0.0")], [SelectedProfile.BuildLockIdentity]);
            PublisherProfile publisher = PublisherProfileJson.Deserialize("""
                {"schemaVersion":1,"root":"FixturePublisher","displayName":"Fixture Publisher","supportContact":{"kind":"secure-url","value":"https://example.com/support"},"copyright":{"holder":"Fixture Publisher","yearPolicy":{"kind":"publication-year","year":2026}},"aiDisclosure":{"state":"no-ai-assistance"}}
                """).Value!;
            var listing = new FabListingDraft("Product", "Stone Arch", "A static architectural model.", "Import and place the prefab.",
                "One model with an overview scene.", ["Architecture"], ["Windows"], [], publisher, false);
            Request = new(context, "1.0.0", versions, listing, [download], unity, [gallery],
                [Source(context, "portable", "fbx", "Product.zip", zipBytes), Source(context, "unity", "unity", "Product.unitypackage", unityBytes),
                 Source(context, "docs", "documentation", "README.md", "Fixture usage documentation"u8.ToArray())],
                [Evidence(context, gallery.ArtifactId, gallery.Content, "media-quality")]);
        }
        catch { Dispose(); throw; }
    }

    internal FabReleaseComposer Composer(bool approved = true, IReleaseArchiveWriter? writer = null) => new(
        new(new FixtureProfileRepository(SelectedProfile, approved)), new(new SafeZipArchiveService(), new ArtifactHashService()),
        new(new WindowsPreviewImageCodec()), writer ?? new VerifiedReleaseArchiveWriter());

    internal static FabReleaseSource Source(FabValidationContext context, string id, string kind, string name, byte[] bytes) =>
        new(Id(id), kind, name, Identity(bytes), _ => Task.FromResult<Stream>(new MemoryStream(bytes, false)),
            Evidence(context, Id(id), Identity(bytes), kind == "documentation" ? "documentation" : kind));

    public void Dispose()
    {
        string boundary = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "PB-1010")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(Root).StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException("Cleanup escaped test boundary."); }
        if (Directory.Exists(Root))
        { Directory.Delete(Root, true); }
    }

    // Test-only repository response; no production database or approval receipt is written.
    private sealed class FixtureProfileRepository(FabRequirementsProfile profile, bool approved) : IRequirementsProfileRepository
    {
        public Task<RepositoryOperationResult<CachedRequirementsProfile?>> GetAsync(string marketplace, string version, CancellationToken cancellationToken = default) =>
            Task.FromResult(RepositoryOperationResult.Success<CachedRequirementsProfile?>(version == profile.Document.Version ?
                new("fab", profile.Document.Version, profile.Document.EffectiveOn, profile.Document.Sources[0].Url, profile.CanonicalJson, profile.Sha256, approved) : null));
        public Task<RepositoryOperationResult<CachedRequirementsProfile?>> GetCurrentAsync(string marketplace, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("A release must never resolve current instead of its pin.");
        public Task<RepositoryOperationResult> CacheAsync(CachedRequirementsProfile value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RepositoryOperationResult> ApproveAsync(RequirementsProfileApproval value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RepositoryOperationResult<RequirementsProfileApproval?>> GetApprovalAsync(string marketplace, string version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
