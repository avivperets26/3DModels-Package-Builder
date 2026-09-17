using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PackageBuilder.Contracts.Archives;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Infrastructure.Archives;
using PackageBuilder.Infrastructure.Artifacts;
using PackageBuilder.Targets.Unreal;

namespace PackageBuilder.App.Wpf.Tests;

[Trait("Task", "PB-1113")]
[Trait("Task", "PB-1114")]
public sealed class UnrealDeliveryTests
{
    [Fact]
    public async Task DeliveryIsDeterministicAndContainsOnlyOneCleanProject()
    {
        using var first = new MemoryStream();
        using var second = new MemoryStream();
        ImmutableArray<ReleaseArchiveEntry> files = Files();
        _ = await UnrealProjectArchive.CreateAsync("Product", files, new VerifiedReleaseArchiveWriter(), first, TestContext.Current.CancellationToken);
        _ = await UnrealProjectArchive.CreateAsync("Product", [.. files.Reverse()], new VerifiedReleaseArchiveWriter(), second, TestContext.Current.CancellationToken);
        Assert.Equal(first.ToArray(), second.ToArray());
        using var zip = new ZipArchive(first, ZipArchiveMode.Read, true);
        Assert.Equal(files.Length, zip.Entries.Count);
        Assert.All(zip.Entries, e => Assert.StartsWith("Product/", e.FullName, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Saved/log.txt")]
    [InlineData("Intermediate/file.uasset")]
    [InlineData("DerivedDataCache/file.uasset")]
    [InlineData("Binaries/Win64/module.dll")]
    [InlineData("Plugins/Other/Other.uplugin")]
    [InlineData("Content/Other/Mesh.uasset")]
    [InlineData("Content/Product/Meshes/../bad.uasset")]
    [InlineData("C:/source.uasset")]
    [InlineData("Content/Product/Meshes/SM_bad.uasset:stream")]
    public async Task ForeignOrGeneratedContentNeverEntersArchive(string path)
    {
        using var destination = new MemoryStream();
        _ = await Assert.ThrowsAsync<ArgumentException>(() => UnrealProjectArchive.CreateAsync("Product",
            [.. Files(), Entry(path, "foreign")], new VerifiedReleaseArchiveWriter(), destination, TestContext.Current.CancellationToken));
        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task MutatedNativeAssetsClearThePartialArchive()
    {
        using var destination = new MemoryStream();
        ReleaseArchiveEntry original = Entry("Content/Product/Meshes/SM_Product.uasset", "validated");
        ReleaseArchiveEntry changed = original with { OpenReadAsync = _ => Task.FromResult<Stream>(new MemoryStream("changed!!"u8.ToArray())) };
        _ = await Assert.ThrowsAsync<InvalidDataException>(() => UnrealProjectArchive.CreateAsync("Product",
            [.. Files(), changed], new VerifiedReleaseArchiveWriter(), destination, TestContext.Current.CancellationToken));
        Assert.Equal(0, destination.Length);
    }

    /// <summary>Live harness hook: use production ZIP and safe extraction code, then verify every extracted byte.</summary>
    [Fact]
    public async Task CreateAndExtractLiveUnrealDeliveryWhenRequested()
    {
        string? requested = Environment.GetEnvironmentVariable("PB_UNREAL_DELIVERY_JOB");
        if (requested is null)
        { return; }
        string repository = UnrealSurfaceTests.FindRepository();
        string job = Path.GetFullPath(requested);
        Assert.StartsWith(Path.Combine(repository, "artifacts", "ue") + Path.DirectorySeparatorChar, job, StringComparison.OrdinalIgnoreCase);
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(job, "output/delivery/inventory.json")));
        JsonElement root = inventory.RootElement;
        Assert.Equal("unreal-clean-project-v1", root.GetProperty("profile").GetString());
        Assert.Equal("5.8.2", root.GetProperty("engineVersion").GetString());
        string name = root.GetProperty("projectName").GetString()!;
        ImmutableArray<ReleaseArchiveEntry>.Builder entries = ImmutableArray.CreateBuilder<ReleaseArchiveEntry>();
        foreach (JsonElement item in root.GetProperty("files").EnumerateArray())
        {
            string logical = item.GetProperty("logicalReference").GetString()!;
            Assert.True(DeliveryPath.IsValid(logical));
            string path = Path.GetFullPath(Path.Combine(job, logical));
            Assert.StartsWith(job + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
            for (string? ancestor = path; ancestor is not null && ancestor.Length >= job.Length; ancestor = Path.GetDirectoryName(ancestor))
            { Assert.Equal((FileAttributes)0, File.GetAttributes(ancestor) & FileAttributes.ReparsePoint); }
            byte[] bytes = File.ReadAllBytes(path);
            // Source-root leakage is checked in both common Unreal string encodings.
            foreach (Encoding encoding in new[] { Encoding.UTF8, Encoding.Unicode })
            {
                Assert.Equal(-1, bytes.AsSpan().IndexOf(encoding.GetBytes(repository)));
                Assert.Equal(-1, bytes.AsSpan().IndexOf(encoding.GetBytes(repository.Replace('\\', '/'))));
            }
            ArtifactContentIdentity identity = ArtifactContentIdentity.Create(item.GetProperty("byteCount").GetInt64(),
                Sha256Digest.Create(item.GetProperty("sha256").GetString()).Value).Value!;
            entries.Add(new(item.GetProperty("path").GetString()!, identity,
                _ => Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))));
        }
        string archive = Path.Combine(job, "delivery.zip");
        using (var destination = new FileStream(archive, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        { _ = await UnrealProjectArchive.CreateAsync(name, entries.ToImmutable(), new VerifiedReleaseArchiveWriter(), destination, TestContext.Current.CancellationToken); }
        string fresh = Path.Combine(job, "fresh");
        _ = Directory.CreateDirectory(fresh);
        ArchiveSafetyPolicy policy = ArchiveSafetyPolicy.Create(2_000_000_000, 1024, 8, 1_000_000_000, 2_000_000_000, 100,
            [".uproject", ".ini", ".uasset", ".umap", ".md"]).Value!;
        ArchiveOperationResult<ArchiveExtractionReceipt> result = await new SafeZipArchiveService().ExtractAsync(
            new(repository, archive, fresh, Path.Combine(fresh, "extracted"), policy), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, string.Join(", ", result.Failures.Select(f => f.Code)));
        foreach (ReleaseArchiveEntry entry in entries)
        {
            byte[] actual = File.ReadAllBytes(Path.Combine(fresh, "extracted", name, entry.Path));
            Assert.Equal(entry.Content.Bytes, actual.LongLength);
            Assert.Equal(entry.Content.Sha256.Value, Convert.ToHexStringLower(SHA256.HashData(actual)));
        }
        File.WriteAllText(Path.Combine(job, "archive-receipt.json"), JsonSerializer.Serialize(new
        { passed = true, entries = entries.Count, archiveSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(archive))) }));
    }

    private static ImmutableArray<ReleaseArchiveEntry> Files() =>
    [Entry("Product.uproject", "{}"), Entry("Config/DefaultEngine.ini", "config"),
     Entry("Content/Product/Maps/L_Overview.umap", "map"), Entry("Content/Product/Documentation/README.md", "readme")];

    private static ReleaseArchiveEntry Entry(string path, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        return new(path, ArtifactContentIdentity.Create(bytes.LongLength, Sha256Digest.Create(Convert.ToHexStringLower(SHA256.HashData(bytes))).Value).Value!,
            _ => Task.FromResult<Stream>(new MemoryStream(bytes)));
    }
}
