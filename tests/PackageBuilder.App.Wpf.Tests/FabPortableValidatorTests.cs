using System.IO;
using System.IO.Compression;
using PackageBuilder.Contracts.Archives;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Infrastructure.Archives;
using PackageBuilder.Infrastructure.Artifacts;
using PackageBuilder.Marketplaces.Fab;
using static PackageBuilder.App.Wpf.Tests.FabValidatorFixtures;

namespace PackageBuilder.App.Wpf.Tests;

public sealed class FabPortableValidatorTests
{
    [Fact]
    public async Task RealZipAndStreamedHashAreValidatedWithoutExtracting()
    {
        using var workspace = new Workspace();
        FabArtifactValidation result = await Validator().ValidateAsync(Profile(), Context("fbx"), [workspace.Download], Token);
        Assert.True(result.Passed, string.Join("; ", result.Findings.Select(finding => finding.Code.Value + ": " + finding.Explanation.Value)));
        Assert.Equal(Profile().Sha256, result.ProfileSha256);
        Assert.False(Directory.Exists(workspace.Download.Archive.DestinationRoot));
        Assert.True(File.Exists(workspace.Download.Archive.SourceArchivePath));
    }

    [Theory]
    [InlineData("../escape.fbx")]
    [InlineData("Product/CON.fbx")]
    [InlineData("Product/Model:stream.fbx")]
    [InlineData("/outside.fbx")]
    public async Task ActualHostileZipPathsBlockWithoutExtraction(string entry)
    {
        using var workspace = new Workspace(entry);
        Has(await Validator().ValidateAsync(Profile(), Context("fbx"), [workspace.Download], Token), "FAB_ARCHIVE_INVALID");
        Assert.False(Directory.Exists(workspace.Download.Archive.DestinationRoot));
    }

    [Theory]
    [InlineData("receipt", "FAB_CONTENT_CHANGED")]
    [InlineData("product", "FAB_FILE_IRRELEVANT")]
    [InlineData("expected", "FAB_ARCHIVE_CONTENT_INVALID")]
    [InlineData("job", "FAB_EVIDENCE_INVALID")]
    [InlineData("incomplete", "FAB_EVIDENCE_INVALID")]
    [InlineData("target", "UNITY_TEST_ERROR")]
    [InlineData("format", "FAB_FORMAT_INVALID")]
    public async Task StaleAndIncompleteEvidenceNeverPasses(string mutation, string code)
    {
        using var workspace = new Workspace();
        FabPortableDownload item = workspace.Download;
        item = mutation switch
        {
            "receipt" => item with { Content = Identity("changed"u8) },
            "product" => item with { ProductKey = "Other" },
            "expected" => item with { ExpectedFiles = ["Product/Missing.fbx"] },
            "job" => item with { TargetEvidence = item.TargetEvidence with { JobId = PackageBuilder.Domain.BuildJobs.BuildJobId.Create("Other-Job").Value! } },
            "incomplete" => item with { TargetEvidence = item.TargetEvidence with { Completed = false } },
            "target" => item with { TargetEvidence = item.TargetEvidence with { Findings = Failure() } },
            _ => item with { Format = "obj" },
        };
        Has(await Validator().ValidateAsync(Profile(), Context("fbx"), [item], Token), code);
    }

    [Fact]
    public async Task SelectedFormatsAndAdditionalCountAreListingWide()
    {
        using var workspace = new Workspace();
        FabValidationContext context = Context("fbx");
        Has(await Validator().ValidateAsync(Profile(), context, [], Token), "FAB_FORMAT_MISSING");
        FabPortableDownload additional = workspace.Download with { IsAdditional = true };
        Has(await Validator().ValidateAsync(Profile(), context, [workspace.Download, additional, additional, additional, additional], Token), "FAB_LIMIT_EXCEEDED");
        Has(await Validator().ValidateAsync(Profile(), context, [workspace.Download, workspace.Download], Token), "FAB_DOWNLOAD_DUPLICATE");
    }

    [Fact]
    public async Task ExactInclusiveAdditionalByteLimitPassesAndOneByteOverFails()
    {
        using var workspace = new Workspace();
        FabPortableDownload additional = workspace.Download with { IsAdditional = true };
        FabValidationContext context = Context("unity");
        Assert.True((await Validator().ValidateAsync(Profile(root => Limit(root, "additional-bytes", additional.Content.Bytes)), context, [additional], Token)).Passed);
        Has(await Validator().ValidateAsync(Profile(root => Limit(root, "additional-bytes", additional.Content.Bytes - 1)), context, [additional], Token), "FAB_LIMIT_EXCEEDED");
    }

    [Fact]
    public async Task ShippedUnknownRulesRemainBlockingAndHistoricalProfileNeedsNewCapabilities()
    {
        using var workspace = new Workspace();
        Has(await Validator().ValidateAsync(FabRequirementsBaseline.ArtifactValidationProfile, Context("fbx"), [workspace.Download], Token), "FAB_RULE_REVIEW_REQUIRED");
        Has(await Validator().ValidateAsync(FabRequirementsBaseline.Profile, Context("fbx"), [workspace.Download], Token), "FAB_RULE_REVIEW_REQUIRED");
    }

    [Fact]
    public async Task CancellationDoesNotExtractOrReturnPass()
    {
        using var workspace = new Workspace();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Validator().ValidateAsync(Profile(), Context("fbx"), [workspace.Download], cancelled.Token));
        Assert.False(Directory.Exists(workspace.Download.Archive.DestinationRoot));
    }

    private static FabPortableValidator Validator() => new(new SafeZipArchiveService(), new ArtifactHashService());

    [Fact]
    public async Task MutationAfterArchiveInspectionInvalidatesTheReceipt()
    {
        using var workspace = new Workspace();
        var validator = new FabPortableValidator(new MutatingInspector(), new ArtifactHashService());
        Has(await validator.ValidateAsync(Profile(), Context("fbx"), [workspace.Download], Token), "FAB_CONTENT_CHANGED");
    }

    private sealed class MutatingInspector : ISafeArchiveService
    {
        public async Task<ArchiveOperationResult<ArchiveInspection>> InspectAsync(ArchiveOperationRequest request, CancellationToken cancellationToken = default)
        {
            ArchiveOperationResult<ArchiveInspection> result = await new SafeZipArchiveService().InspectAsync(request, cancellationToken);
            Assert.True(result.IsSuccess);
            await File.AppendAllTextAsync(request.SourceArchivePath, "changed", cancellationToken);
            return result;
        }
        public Task<ArchiveOperationResult<ArchiveExtractionReceipt>> ExtractAsync(ArchiveOperationRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The Fab validator must not extract.");
    }

    private sealed class Workspace : IDisposable
    {
        private readonly string _root = Path.Combine(AppContext.BaseDirectory, "PB-1004", Guid.NewGuid().ToString("N"));
        internal Workspace(string entry = "Product/Model.fbx")
        {
            _ = Directory.CreateDirectory(_root);
            try
            {
                string path = Path.Combine(_root, "Product.zip");
                using (FileStream output = File.Create(path))
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
                using (Stream content = archive.CreateEntry(entry).Open())
                { content.Write("synthetic model bytes"u8); }
                ArtifactContentIdentity identity = Identity(File.ReadAllBytes(path));
                string containment = Path.Combine(_root, "Preflight");
                _ = Directory.CreateDirectory(containment);
                ArchiveSafetyPolicy policy = ArchiveSafetyPolicy.Create(1_000_000, 100, 8, 1_000_000, 1_000_000, 100, [".fbx"]).Value!;
                Download = new(Id(), identity, "Product", "fbx", false,
                    new(_root, path, containment, Path.Combine(containment, "Extraction"), policy), [entry],
                    Evidence(Context("fbx"), Id(), identity, "portable-target"));
            }
            catch { Dispose(); throw; }
        }
        internal FabPortableDownload Download { get; }
        public void Dispose()
        {
            string boundary = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "PB-1004")) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(_root).StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
            { throw new InvalidOperationException("Cleanup boundary mismatch."); }
            if (Directory.Exists(_root))
            { Directory.Delete(_root, true); }
        }
    }
}
