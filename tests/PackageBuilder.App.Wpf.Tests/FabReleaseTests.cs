using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PackageBuilder.Contracts.Archives;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Infrastructure.Artifacts;
using PackageBuilder.Marketplaces.Fab;
using static PackageBuilder.App.Wpf.Tests.FabValidatorFixtures;

namespace PackageBuilder.App.Wpf.Tests;

public sealed class FabReleaseTests
{
    [Fact]
    public async Task SyntheticReleaseUsesExistingArtifactStoreAndAtomicPromotion()
    {
        using var fixture = new FabReleaseFixtures();
        string staging = Path.Combine(fixture.Root, "release-staging.zip");
        FabComposedRelease composed;
        using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        { composed = await fixture.Composer().ComposeAsync(fixture.Request, output, Token); }
        Assert.True(composed.IsSuccess);
        string artifacts = Path.Combine(fixture.Root, "Artifacts");
        string builds = Path.Combine(artifacts, "Builds");
        DateTimeOffset created = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
        BuildArtifact artifact = BuildArtifact.Create(Id("fab-release"), fixture.Request.Context.JobId,
            BuildStepId.Create("fab-compose").Value, BuildArtifactRole.Create("fab-release").Value, null,
            BuildArtifactLifecycleState.Staged, "Product/1.0.0/" + composed.FileName, created, created).Value!;
        var store = new ArtifactStore();
        ArtifactStoreOperationResult<ArtifactStoreRecord> staged = await store.StageAsync(new(fixture.Root, artifacts, staging, artifact), Token);
        Assert.True(staged.IsSuccess);
        Assert.Equal(composed.Content, staged.Value!.ContentIdentity);
        Assert.True((await store.TransitionAsync(new(fixture.Root, artifacts, artifact.JobId, artifact.Id,
            BuildArtifactLifecycleState.Staged, BuildArtifactLifecycleState.Validated, created.AddSeconds(1)), Token)).IsSuccess);
        ArtifactPromotionOperationResult promoted = await new AtomicArtifactPromotionService(store, new ArtifactHashService()).PromoteAsync(
            new(fixture.Root, artifacts, builds, artifact.JobId, artifact.Id, created.AddSeconds(2)), Token);
        Assert.True(promoted.IsSuccess, string.Join(",", promoted.Failures.Select(failure => failure.Code)));
        Assert.Equal(composed.Content, Identity(await File.ReadAllBytesAsync(promoted.Value!.ReleasePath, Token)));
        Assert.Equal(BuildArtifactLifecycleState.Promoted, promoted.Value.ArtifactRecord.Artifact.LifecycleState);
    }

    [Fact]
    public async Task SyntheticReleaseConnectsPinnedValidatorsChecklistAndExactArchiveBytes()
    {
        using var fixture = new FabReleaseFixtures();
        using var first = new MemoryStream();
        FabComposedRelease result = await fixture.Composer().ComposeAsync(fixture.Request, first, Token);
        Assert.True(result.IsSuccess, string.Join(",", result.Validation.Findings.Select(f => f.Code.Value)));
        Assert.Equal(Identity(first.ToArray()), result.Content);
        using var second = new MemoryStream();
        Assert.True((await fixture.Composer().ComposeAsync(fixture.Request with { Sources = [.. fixture.Request.Sources.Reverse()] }, second, Token)).IsSuccess);
        Assert.Equal(first.ToArray(), second.ToArray());
        using var zip = new ZipArchive(first, ZipArchiveMode.Read, true);
        Assert.Equal(9, zip.Entries.Count);
        Assert.All(zip.Entries, entry => Assert.StartsWith("Product/1.0.0/", entry.FullName));
        Assert.DoesNotContain(zip.Entries, entry => entry.FullName.Contains("Unreal", StringComparison.Ordinal));
        using var manifestReader = new StreamReader(zip.GetEntry("Product/1.0.0/release-manifest.json")!.Open());
        using var manifest = JsonDocument.Parse(await manifestReader.ReadToEndAsync(Token));
        foreach (JsonElement entry in manifest.RootElement.GetProperty("entries").EnumerateArray())
        {
            using Stream source = zip.GetEntry(entry.GetProperty("path").GetString()!)!.Open();
            using var bytes = new MemoryStream();
            await source.CopyToAsync(bytes, Token);
            Assert.Equal(entry.GetProperty("sha256").GetString(), Identity(bytes.ToArray()).Sha256.Value);
            Assert.Equal(entry.GetProperty("bytes").GetInt64(), bytes.Length);
        }
        using var profileReader = new StreamReader(zip.GetEntry("Product/1.0.0/requirements-profile.json")!.Open());
        Assert.Equal(fixture.SelectedProfile.CanonicalJson, await profileReader.ReadToEndAsync(Token));
        using var checklistReader = new StreamReader(zip.GetEntry("Product/1.0.0/upload-checklist.txt")!.Open());
        string checklist = await checklistReader.ReadToEndAsync(Token);
        Assert.Contains("[ ] Submit", checklist);
        Assert.DoesNotContain("[x]", checklist);
    }

    [Theory]
    [InlineData("docs")]
    [InlineData("unity")]
    [InlineData("portable")]
    public async Task MissingSelectedDeliveryProducesNoOutput(string id)
    {
        using var fixture = new FabReleaseFixtures();
        await Fails(fixture, fixture.Request with { Sources = [.. fixture.Request.Sources.Where(source => source.ArtifactId.Value != id)] }, "FAB_RELEASE_DELIVERY_MISSING");
    }

    [Fact]
    public async Task UnapprovedOrMismatchedProfileNeverFallsBackToCurrent()
    {
        using var fixture = new FabReleaseFixtures();
        using var output = new MemoryStream();
        FabComposedRelease result = await fixture.Composer(false).ComposeAsync(fixture.Request, output, Token);
        Has(result.Validation, "FAB_RELEASE_PROFILE_UNAPPROVED");
        Assert.Equal(0, output.Length);
        await Fails(fixture, fixture.Request with { Versions = fixture.Request.Versions with { MarketplaceProfiles = [FabRequirementsBaseline.Profile.BuildLockIdentity] } }, "FAB_RELEASE_PROFILE_UNAPPROVED");
    }

    [Fact]
    public async Task ShippedProfileCannotPretendUnresolvedOfficialRulesPassed()
    {
        using var fixture = new FabReleaseFixtures(FabRequirementsBaseline.ArtifactValidationProfile);
        await Fails(fixture, fixture.Request, "FAB_RULE_REVIEW_REQUIRED");
    }

    [Fact]
    public async Task UnrealSelectionIsExplicitlyUnsupported()
    {
        using var fixture = new FabReleaseFixtures();
        FabReleaseRequest request = fixture.Request with { Context = fixture.Request.Context with { Listing = fixture.Request.Context.Listing with { Formats = ["fbx", "unity", "unreal"] } } };
        await Fails(fixture, request, "FAB_UNREAL_UNSUPPORTED");
    }

    [Fact]
    public async Task FailedOrStaleUpstreamResultsBlockComposition()
    {
        using var fixture = new FabReleaseFixtures();
        await Fails(fixture, fixture.Request with { Unity = fixture.Request.Unity! with { CleanImport = fixture.Request.Unity!.CleanImport with { Findings = Failure() } } }, "UNITY_TEST_ERROR");
        await Fails(fixture, fixture.Request with { MediaQualityEvidence = [] }, "FAB_EVIDENCE_INVALID");
        await Fails(fixture, fixture.Request with { MediaQualityEvidence = [fixture.Request.MediaQualityEvidence[0] with { Content = Identity("stale"u8) }] }, "FAB_EVIDENCE_INVALID");
        FabReleaseSource docs = fixture.Request.Sources[2];
        await Fails(fixture, fixture.Request with { Sources = fixture.Request.Sources.SetItem(2, docs with { Evidence = docs.Evidence with { Completed = false } }) }, "FAB_EVIDENCE_INVALID");
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("CON")]
    [InlineData("1/2")]
    public async Task UnsafeVersionsCannotCreatePaths(string version)
    {
        using var fixture = new FabReleaseFixtures();
        await Fails(fixture, fixture.Request with { Version = version }, "FAB_RELEASE_INPUT_INVALID");
    }

    [Fact]
    public async Task SourceMutationOrUnexpectedFilesCannotBecomeARelease()
    {
        using var fixture = new FabReleaseFixtures();
        FabReleaseSource source = fixture.Request.Sources[0];
        await Fails(fixture, fixture.Request with { Sources = fixture.Request.Sources.SetItem(0, source with { OpenReadAsync = _ => Task.FromResult<Stream>(new MemoryStream("changed"u8.ToArray())) }) }, "FAB_RELEASE_WRITE_FAILED");
        await Fails(fixture, fixture.Request with { Sources = [.. fixture.Request.Sources, FabReleaseFixtures.Source(fixture.Request.Context, "secret", "source", "private.fbx", "private"u8.ToArray())] }, "FAB_RELEASE_UNREQUESTED");
    }

    [Fact]
    public async Task CancellationClearsPartiallyWrittenStaging()
    {
        using var fixture = new FabReleaseFixtures();
        using var cancellation = new CancellationTokenSource();
        FabReleaseSource source = fixture.Request.Sources[0] with { OpenReadAsync = _ => { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); } };
        using var output = new MemoryStream();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Composer().ComposeAsync(fixture.Request with { Sources = fixture.Request.Sources.SetItem(0, source) }, output, cancellation.Token));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void ListingTextIsDataAndAiChoiceIsExplicit()
    {
        using var fixture = new FabReleaseFixtures();
        FabListingDraft draft = fixture.Request.Listing with { Title = "<script>alert(1)</script>" };
        FabListingChecklist result = FabListingChecklistGenerator.Generate(fixture.SelectedProfile, fixture.Request.Context, draft, fixture.Request.Versions, Token);
        Assert.True(result.Validation.Passed);
        Assert.DoesNotContain("<script>", result.ListingJson);
        using var parsed = JsonDocument.Parse(result.ListingJson);
        Assert.Equal(draft.Title, parsed.RootElement.GetProperty("title").GetString());
        Has(FabListingChecklistGenerator.Generate(fixture.SelectedProfile, fixture.Request.Context, draft with { CreatedWithAi = null }, fixture.Request.Versions, Token).Validation, "FAB_LISTING_METADATA_INVALID");
        Has(FabListingChecklistGenerator.Generate(fixture.SelectedProfile, fixture.Request.Context, draft with { CreatedWithAi = true }, fixture.Request.Versions, Token).Validation, "FAB_LISTING_METADATA_INVALID");
    }

    [Theory]
    [InlineData("http://example.com/dependency")]
    [InlineData("userinfo")]
    [InlineData("https://example.com/dependency?token=private")]
    [InlineData("https://localhost/dependency")]
    public void PrivateDependencyLinksAreRejected(string url)
    {
        // Construct deliberately fake user information rather than storing a credential-shaped URL in source.
        if (url == "userinfo")
        { url = new UriBuilder("https://example.com/dependency") { UserName = "fixture", Password = "unused" }.Uri.AbsoluteUri; }
        using var fixture = new FabReleaseFixtures();
        Has(FabListingChecklistGenerator.Generate(fixture.SelectedProfile, fixture.Request.Context,
            fixture.Request.Listing with { Dependencies = [new("Package", "1.0.0", "Install it.", url)] }, fixture.Request.Versions, Token).Validation, "FAB_LISTING_METADATA_INVALID");
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("CON.txt")]
    [InlineData("/absolute.txt")]
    public async Task ArchiveWriterRejectsHostilePathsBeforeWriting(string path)
    {
        using var output = new MemoryStream();
        var entry = new ReleaseArchiveEntry(path, Identity("x"u8), _ => Task.FromResult<Stream>(new MemoryStream("x"u8.ToArray())));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => new VerifiedReleaseArchiveWriter().WriteAsync([entry], output, Token));
        Assert.Equal(0, output.Length);
    }

    private static async Task Fails(FabReleaseFixtures fixture, FabReleaseRequest request, string code)
    {
        using var output = new MemoryStream();
        FabComposedRelease result = await fixture.Composer().ComposeAsync(request, output, Token);
        Has(result.Validation, code);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Content);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task UnityDependenciesMustAppearInListingWithExactVersions()
    {
        using var fixture = new FabReleaseFixtures();
        FabUnityPackageInspection unity = fixture.Request.Unity! with { ExternalDependencies = [new("Packages/com.example.shader", "1.2.0", true, true)] };
        await Fails(fixture, fixture.Request with { Unity = unity }, "FAB_RELEASE_DEPENDENCY_UNDISCLOSED");
        FabListingDraft listing = fixture.Request.Listing with { Dependencies = [new("Packages/com.example.shader", "1.2.0", "Install this package.", "https://example.com/shader")] };
        using var output = new MemoryStream();
        Assert.True((await fixture.Composer().ComposeAsync(fixture.Request with { Unity = unity, Listing = listing }, output, Token)).IsSuccess);
    }

    [Fact]
    public async Task UnselectedUnityAndCrossCategoryArtifactIdsAreRejected()
    {
        using var fixture = new FabReleaseFixtures();
        FabReleaseRequest portableOnly = fixture.Request with { Context = fixture.Request.Context with { Listing = fixture.Request.Context.Listing with { Formats = ["fbx"] } } };
        await Fails(fixture, portableOnly, "FAB_RELEASE_UNREQUESTED");
        await Fails(fixture, fixture.Request with { Gallery = [fixture.Request.Gallery[0] with { ArtifactId = fixture.Request.Sources[0].ArtifactId }] }, "FAB_RELEASE_DUPLICATE");
    }

    [Fact]
    public async Task OnlySelectedPortableDeliveriesArePackaged()
    {
        using var fixture = new FabReleaseFixtures();
        FabReleaseRequest request = fixture.Request with
        {
            Context = fixture.Request.Context with { Listing = fixture.Request.Context.Listing with { Formats = ["fbx"] } },
            Unity = null,
            Sources = [.. fixture.Request.Sources.Where(source => source.Kind != "unity")],
        };
        using var output = new MemoryStream();
        Assert.True((await fixture.Composer().ComposeAsync(request, output, Token)).IsSuccess);
        using var zip = new ZipArchive(output, ZipArchiveMode.Read);
        Assert.DoesNotContain(zip.Entries, entry => entry.FullName.Contains("/Unity/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("glb")]
    [InlineData("additional")]
    public async Task SelectedCompanionDownloadUsesItsVerifiedInventory(string kind)
    {
        using var fixture = new FabReleaseFixtures();
        string path = Path.Combine(fixture.Root, "Companion.zip");
        using (FileStream output = File.Create(path))
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
        using (Stream entry = archive.CreateEntry("Product/Companion.fbx").Open())
        { entry.Write("synthetic companion"u8); }
        byte[] bytes = await File.ReadAllBytesAsync(path, Token);
        // Additional files may contain a relevant FBX; GLB tests use their own accepted inner extension.
        FabPortableDownload original = fixture.Request.Downloads[0];
        FabValidationContext context = fixture.Request.Context;
        if (kind == "glb")
        {
            using (FileStream output = new(path, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
            using (Stream entry = archive.CreateEntry("Product/Companion.glb").Open())
            { entry.Write("synthetic glb"u8); }
            bytes = await File.ReadAllBytesAsync(path, Token);
            context = context with { Listing = context.Listing with { Formats = ["fbx", "glb", "unity"] } };
        }
        ArchiveSafetyPolicy policy = PackageBuilder.Contracts.Archives.ArchiveSafetyPolicy.Create(1_000_000, 100, 8, 1_000_000, 1_000_000, 100, [".fbx", ".glb"]).Value!;
        var companion = new FabPortableDownload(Id("companion"), Identity(bytes), "Product", kind == "glb" ? "glb" : "fbx", kind == "additional",
            new(fixture.Root, path, original.Archive.DestinationContainmentRoot, original.Archive.DestinationRoot, policy),
            [kind == "glb" ? "Product/Companion.glb" : "Product/Companion.fbx"], Evidence(context, Id("companion"), Identity(bytes), "portable-target"));
        using var release = new MemoryStream();
        FabComposedRelease result = await fixture.Composer().ComposeAsync(fixture.Request with
        {
            Context = context,
            Downloads = [original, companion],
            Sources = [.. fixture.Request.Sources, FabReleaseFixtures.Source(context, "companion", kind, "Companion.zip", bytes)],
        }, release, Token);
        Assert.True(result.IsSuccess, string.Join(",", result.Validation.Findings.Select(f => f.Code.Value)));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("prefix")]
    [InlineData("case")]
    public async Task ArchiveNameCollisionsAreRejected(string kind)
    {
        using var output = new MemoryStream();
        var entry = new ReleaseArchiveEntry("Product/File", Identity("x"u8), _ => Task.FromResult<Stream>(new MemoryStream("x"u8.ToArray())));
        ReleaseArchiveEntry other = entry with { Path = kind switch { "duplicate" => entry.Path, "prefix" => "Product/File/Child", _ => "product/file" } };
        _ = await Assert.ThrowsAsync<ArgumentException>(() => new VerifiedReleaseArchiveWriter().WriteAsync([entry, other], output, Token));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task OversizedSourceStopsAtReceiptBudgetAndClearsOutput()
    {
        using var output = new MemoryStream();
        var entry = new ReleaseArchiveEntry("Product/File.txt", Identity("x"u8), _ => Task.FromResult<Stream>(new MemoryStream("larger"u8.ToArray())));
        _ = await Assert.ThrowsAsync<InvalidDataException>(() => new VerifiedReleaseArchiveWriter().WriteAsync([entry], output, Token));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task ExistingDestinationIsPreserved()
    {
        using var output = new MemoryStream();
        output.Write("existing"u8);
        var entry = new ReleaseArchiveEntry("Product/File.txt", Identity("x"u8), _ => Task.FromResult<Stream>(new MemoryStream("x"u8.ToArray())));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => new VerifiedReleaseArchiveWriter().WriteAsync([entry], output, Token));
        Assert.Equal("existing", Encoding.UTF8.GetString(output.ToArray()));
    }
}
