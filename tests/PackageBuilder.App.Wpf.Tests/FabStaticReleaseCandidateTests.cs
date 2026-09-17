using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Domain.Products;
using PackageBuilder.Infrastructure.Persistence;
using PackageBuilder.Marketplaces.Fab;
using static PackageBuilder.App.Wpf.Tests.FabValidatorFixtures;

namespace PackageBuilder.App.Wpf.Tests;

public sealed class FabStaticReleaseCandidateTests
{
    [Fact]
    public Task ReviewedCandidateRequiresCompatibilityAndApprovalBeforeRelease() =>
        ValidateCandidateAsync(FabRequirementsBaseline.StaticReleaseProfile);

    [Fact]
    public Task UnrealCandidateRequiresCompatibilityAndApprovalBeforeRelease() =>
        ValidateCandidateAsync(FabRequirementsBaseline.UnrealStaticReleaseProfile);

    /// <summary>Every new revision must pass the same cache, compatibility, approval and exact-pin gates.</summary>
    private static async Task ValidateCandidateAsync(FabRequirementsProfile profile)
    {
        using var fixture = new FabReleaseFixtures(profile);
        string database = Path.Combine(fixture.Root, "profiles.db");
        _ = Directory.CreateDirectory(Path.Combine(fixture.Root, "backups"));
        Assert.True(new SqliteDatabaseMigrator().Migrate(fixture.Root, database, Path.Combine(fixture.Root, "backups"), Token).IsSuccess);
        SqliteRequirementsProfileRepository repository = SqliteRequirementsProfileRepository.Create(fixture.Root, database).Value!;
        var updater = new FabRequirementsProfileUpdater(repository);
        Assert.True((await updater.CacheAsync(fixture.SelectedProfile.CanonicalJson, Token)).IsSuccess);
        Assert.False((await updater.LoadPinnedAsync(fixture.SelectedProfile.BuildLockIdentity, Token)).IsSuccess);
        TestedFabRequirementsCandidate tested = (await updater.TestAsync(fixture.SelectedProfile.Document.Version,
            new Compatibility(fixture), Token)).Value!;
        Assert.True(tested.Passed);
        Assert.False((await updater.ApproveAsync(tested, false, "local-fixture-review", DateTimeOffset.UtcNow, Token)).IsSuccess);
        Assert.True((await updater.ApproveAsync(tested, true, "local-fixture-review", DateTimeOffset.UtcNow, Token)).IsSuccess);
        Assert.Equal(fixture.SelectedProfile.Sha256, (await updater.LoadPinnedAsync(fixture.SelectedProfile.BuildLockIdentity, Token)).Value!.Sha256);
        // Normal CI proves the reviewed candidate contract. Only the explicitly supplied live run
        // executes engine-backed acceptance and emits its separate real-release receipt.
        string? pointer = Environment.GetEnvironmentVariable("PB_FAB_REAL_RELEASE_POINTER");
        if (pointer is not null)
        { await FabRealReleaseValidation.RunAsync(fixture, updater, pointer, Token); }
    }

    [Theory]
    [InlineData("glb", "static")]
    [InlineData("unreal", "static")]
    [InlineData("unity", "rigged")]
    public void ScopedCandidateDoesNotCertifyUnreviewedFormatsOrProductCases(string format, string productCase)
    {
        var listing = new FabListingConfiguration(productCase == "static" ? ProductCase.Static : ProductCase.Rigged,
            "3d-model", [format]);
        Assert.False(FabRequiredTargetResolver.Resolve(FabRequirementsBaseline.StaticReleaseProfile, listing).IsSuccess);
    }

    [Fact]
    public void NewCandidatePreservesHistoricalAmbiguityAndRejectsUnreviewedForumSources()
    {
        Assert.Equal("unresolved", FabRequirementsBaseline.ArtifactValidationProfile.Rule("other-format-bytes").Status);
        FabRequirementRule rule = FabRequirementsBaseline.StaticReleaseProfile.Rule("other-format-bytes");
        Assert.Equal(6_000_000_000, rule.Limit);
        Assert.Equal("package-builder-policy", rule.Origin);
        JsonNode root = JsonNode.Parse(FabRequirementsBaseline.StaticReleaseProfile.CanonicalJson)!;
        foreach (JsonNode? source in root["sources"]!.AsArray())
        {
            if (source!["id"]!.GetValue<string>() == "fab-upload-reply")
            { source["url"] = "https://forums.unrealengine.com/t/unreviewed/123"; }
        }
        Assert.False(FabRequirementsProfileJson.Load(root.ToJsonString()).IsSuccess);
    }

    /// <summary>Runs candidate compatibility against good and failing actual composer requests before approval.</summary>
    private sealed class Compatibility(FabReleaseFixtures fixture) : IFabRequirementsCompatibilitySuite
    {
        public async Task<PersistedCompatibilitySuiteResult> RunAsync(FabRequirementsProfile candidate, CancellationToken cancellationToken)
        {
            Assert.Equal(fixture.SelectedProfile.Sha256, candidate.Sha256);
            using var success = new MemoryStream();
            Assert.True((await fixture.Composer().ComposeAsync(fixture.Request, success, cancellationToken)).IsSuccess);
            using var missing = new MemoryStream();
            Assert.False((await fixture.Composer().ComposeAsync(fixture.Request with { Sources = [] }, missing, cancellationToken)).IsSuccess);
            using var unapproved = new MemoryStream();
            Assert.False((await fixture.Composer(false).ComposeAsync(fixture.Request, unapproved, cancellationToken)).IsSuccess);
            int checks = 3;
            if (candidate.Document.Rules.Any(rule => rule.Id == "static-unreal-review-scope"))
            {
                FabValidationContext context = fixture.Request.Context with { Listing = fixture.Request.Context.Listing with { Formats = ["fbx", "unity", "unreal"] } };
                byte[] bytes = "synthetic native observation"u8.ToArray();
                FabReleaseRequest request = fixture.Request with
                {
                    Context = context,
                    Unreal = FabUnrealProjectValidatorTests.Project(context, bytes),
                    Sources = [.. fixture.Request.Sources, FabReleaseFixtures.Source(context, "unreal", "unreal", "Product_Unreal.zip", bytes)]
                };
                using var unreal = new MemoryStream();
                Assert.True((await fixture.Composer().ComposeAsync(request, unreal, cancellationToken)).IsSuccess);
                using var stale = new MemoryStream();
                Assert.False((await fixture.Composer().ComposeAsync(request with { Unreal = null }, stale, cancellationToken)).IsSuccess);
                checks += 2;
            }
            string evidence = candidate.Sha256 + "\nvalid-release:pass\nmissing-source:rejected\nunapproved:rejected\nchecks:" + checks;
            return new("fab-static-release-compatibility", CompatibilitySuiteOutcome.Passed, checks, checks, 0,
                "reports/fab-static-compatibility.txt", Identity(Encoding.UTF8.GetBytes(evidence)).Sha256.Value, DateTimeOffset.UtcNow);
        }
    }
}
