using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Infrastructure.Persistence;
using PackageBuilder.Marketplaces.Fab;

namespace PackageBuilder.App.Wpf.Tests;

public sealed class FabRequirementsUpdateTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("null")]
    [InlineData("mismatched")]
    public async Task DamagedApprovalReceiptsInvalidateCurrentAndPinnedReads(string damage)
    {
        using Workspace workspace = new();
        var updater = new FabRequirementsProfileUpdater(workspace.Repository());
        _ = await updater.CacheAsync(Revision(1), Token);
        TestedFabRequirementsCandidate tested = (await updater.TestAsync("2026-09-12.1", new Suite(), Token)).Value!;
        Assert.True((await updater.ApproveAsync(tested, true, "fixture", _now, Token)).IsSuccess);
        workspace.Execute(damage switch
        {
            "missing" => "DELETE FROM Settings WHERE SettingKey LIKE '%:approval:%';",
            "malformed" => "UPDATE Settings SET ValueJson='{' WHERE SettingKey LIKE '%:approval:%';",
            "null" => "UPDATE Settings SET ValueJson='null' WHERE SettingKey LIKE '%:approval:%';",
            _ => "UPDATE Settings SET ValueJson=replace(ValueJson,'fixture','') WHERE SettingKey LIKE '%:approval:%';",
        });
        Assert.False((await updater.LoadCurrentAsync(Token)).IsSuccess);
        Assert.False((await updater.LoadPinnedAsync(tested.Profile.BuildLockIdentity, Token)).IsSuccess);
        Assert.False((await workspace.Repository().GetApprovalAsync("fab", "2026-09-12.1", Token)).IsSuccess);
    }

    [Fact]
    public async Task OfflineCacheTestReviewPromotionRestartAndOldBuildPinRoundTrip()
    {
        using Workspace workspace = new();
        var updater = new FabRequirementsProfileUpdater(workspace.Repository());
        Assert.Null((await updater.LoadCurrentAsync(Token)).Value);
        Assert.True((await updater.CacheAsync(FabRequirementsBaseline.Profile.CanonicalJson, Token)).IsSuccess);
        Assert.Null((await updater.LoadCurrentAsync(Token)).Value);
        Assert.False((await updater.LoadPinnedAsync(FabRequirementsBaseline.Profile.BuildLockIdentity, Token)).IsSuccess);
        TestedFabRequirementsCandidate tested = (await updater.TestAsync("2026-09-12.1", new Suite(), Token)).Value!;
        Assert.True(tested.Passed);
        Assert.False((await updater.ApproveAsync(tested, false, "fixture-reviewer", _now, Token)).IsSuccess);
        Assert.True((await updater.ApproveAsync(tested, true, "fixture-reviewer", _now, Token)).IsSuccess);
        BuildLockMarketplaceProfile pin = (await updater.LoadCurrentAsync(Token)).Value!.BuildLockIdentity;
        Assert.Equal(FabRequirementsBaseline.Profile.BuildLockIdentity, pin);

        var restarted = new FabRequirementsProfileUpdater(workspace.Repository());
        FabRequirementsProfile second = (await restarted.CacheAsync(Revision(2), Token)).Value!;
        Assert.NotEmpty(FabRequirementsProfileUpdater.Compare(FabRequirementsBaseline.Profile, second));
        TestedFabRequirementsCandidate update = (await restarted.TestAsync(second.Document.Version, new Suite(), Token)).Value!;
        Assert.True((await restarted.ApproveAsync(update, true, "fixture-reviewer", _now, Token)).IsSuccess);
        Assert.Equal(second.Sha256, (await restarted.LoadCurrentAsync(Token)).Value!.Sha256);
        Assert.Equal(FabRequirementsBaseline.Profile.CanonicalJson, (await restarted.LoadPinnedAsync(pin, Token)).Value!.CanonicalJson);
        RequirementsProfileApproval receipt = (await workspace.Repository().GetApprovalAsync("fab", second.Document.Version, Token)).Value!;
        Assert.Equal(update.Evidence, receipt.Evidence);
        Assert.Equal(FabRequirementsBaseline.Profile.Sha256, receipt.ExpectedCurrentSha256);
        Assert.Equal("fixture-reviewer", receipt.Reviewer);
        Assert.True((await restarted.CacheAsync(FabRequirementsBaseline.Profile.CanonicalJson, Token)).IsSuccess);
    }

    [Fact]
    public async Task FailedAndIncompleteTestsCannotApproveAndCancelledTestsLeaveCurrentAlone()
    {
        using Workspace workspace = new();
        var updater = new FabRequirementsProfileUpdater(workspace.Repository());
        _ = await updater.CacheAsync(Revision(1), Token);
        TestedFabRequirementsCandidate failed = (await updater.TestAsync("2026-09-12.1", new Suite(failed: true), Token)).Value!;
        Assert.False(failed.Passed);
        Assert.False((await updater.ApproveAsync(failed, true, "fixture", _now, Token)).IsSuccess);
        Assert.False((await updater.TestAsync("2026-09-12.1", new Suite(incomplete: true), Token)).IsSuccess);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => updater.TestAsync("2026-09-12.1", new Suite(), cancelled.Token));
        Assert.Null((await updater.LoadCurrentAsync(Token)).Value);
    }

    [Fact]
    public async Task ConcurrentReviewedCandidatesCannotOverwriteNewCurrentAndVersionContentsAreImmutable()
    {
        using Workspace workspace = new();
        var updater = new FabRequirementsProfileUpdater(workspace.Repository());
        _ = await updater.CacheAsync(Revision(1), Token);
        _ = await updater.CacheAsync(Revision(2), Token);
        TestedFabRequirementsCandidate one = (await updater.TestAsync("2026-09-12.1", new Suite(), Token)).Value!;
        TestedFabRequirementsCandidate two = (await updater.TestAsync("2026-09-12.2", new Suite(), Token)).Value!;
        Assert.True((await updater.ApproveAsync(one, true, "fixture", _now, Token)).IsSuccess);
        Assert.False((await updater.ApproveAsync(two, true, "fixture", _now, Token)).IsSuccess);
        Assert.False((await updater.ApproveAsync(one, true, "fixture", _now, Token)).IsSuccess);
        JsonObject changed = JsonNode.Parse(Revision(1))!.AsObject();
        changed["rules"]![0]!["summary"] = "Different content under reused revision.";
        Assert.False((await updater.CacheAsync(changed.ToJsonString(), Token)).IsSuccess);
        Assert.Equal(one.Profile.Sha256, (await updater.LoadCurrentAsync(Token)).Value!.Sha256);
        Assert.Equal(one.Profile.CanonicalJson, (await updater.LoadPinnedAsync(one.Profile.BuildLockIdentity, Token)).Value!.CanonicalJson);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-hash")]
    [InlineData("wrong-marketplace")]
    [InlineData("wrong-profile")]
    [InlineData("no-hash")]
    public async Task InvalidBuildPinsNeverFallbackToCurrent(string mutation)
    {
        using Workspace workspace = new();
        var updater = new FabRequirementsProfileUpdater(workspace.Repository());
        BuildLockMarketplaceProfile pin = FabRequirementsBaseline.Profile.BuildLockIdentity;
        _ = await updater.CacheAsync(Revision(1), Token);
        TestedFabRequirementsCandidate tested = (await updater.TestAsync("2026-09-12.1", new Suite(), Token)).Value!;
        Assert.True((await updater.ApproveAsync(tested, true, "fixture", _now, Token)).IsSuccess);
        pin = mutation switch
        {
            "missing" => pin with { Version = "2026-09-12.99+sha256." + FabRequirementsBaseline.Profile.Sha256 },
            "wrong-hash" => pin with { Version = "2026-09-12.1+sha256." + new string('0', 64) },
            "wrong-marketplace" => pin with { Marketplace = "other" },
            "wrong-profile" => pin with { Profile = "other" },
            _ => pin with { Version = "2026-09-12.1" },
        };
        Assert.False((await updater.LoadPinnedAsync(pin, Token)).IsSuccess);
    }

    [Theory]
    [InlineData("future-date")]
    [InlineData("empty-reviewer")]
    [InlineData("early-approval")]
    [InlineData("non-utc")]
    public async Task InvalidApprovalMetadataDoesNotChangeDatabase(string mutation)
    {
        using Workspace workspace = new();
        var updater = new FabRequirementsProfileUpdater(workspace.Repository());
        string json = Revision(1);
        if (mutation == "future-date")
        {
            JsonNode node = JsonNode.Parse(json)!;
            node["effectiveOn"] = "2026-09-13";
            json = node.ToJsonString();
        }
        _ = await updater.CacheAsync(json, Token);
        TestedFabRequirementsCandidate tested = (await updater.TestAsync("2026-09-12.1", new Suite(), Token)).Value!;
        DateTimeOffset approval = mutation switch
        {
            "early-approval" => _now.AddHours(-1),
            "non-utc" => _now.ToOffset(TimeSpan.FromHours(1)),
            _ => _now,
        };
        Assert.False((await updater.ApproveAsync(tested, true, mutation == "empty-reviewer" ? "" : "fixture", approval, Token)).IsSuccess);
        Assert.Null((await updater.LoadCurrentAsync(Token)).Value);
        Assert.Null((await workspace.Repository().GetApprovalAsync("fab", "2026-09-12.1", Token)).Value);
    }

    [Fact]
    public async Task StorageFailureAfterApprovalUpdateRollsBackEntirePromotion()
    {
        using Workspace workspace = new();
        var updater = new FabRequirementsProfileUpdater(workspace.Repository());
        _ = await updater.CacheAsync(Revision(1), Token);
        TestedFabRequirementsCandidate tested = (await updater.TestAsync("2026-09-12.1", new Suite(), Token)).Value!;
        workspace.Execute("CREATE TRIGGER fail_receipt BEFORE INSERT ON Settings BEGIN SELECT RAISE(ABORT, 'fixture failure'); END;");
        Assert.False((await updater.ApproveAsync(tested, true, "fixture", _now, Token)).IsSuccess);
        Assert.False((await workspace.Repository().GetAsync("fab", "2026-09-12.1", Token)).Value!.IsApproved);
        Assert.Null((await updater.LoadCurrentAsync(Token)).Value);
    }

    [Fact]
    public async Task CorruptCacheAndCurrentPointerFailClosedAndInvalidPathsAreRejected()
    {
        using Workspace workspace = new();
        SqliteRequirementsProfileRepository repository = workspace.Repository();
        Assert.False(SqliteRequirementsProfileRepository.Create(workspace.Root, "relative.db").IsSuccess);
        Assert.False(SqliteRequirementsProfileRepository.Create(Path.Combine(workspace.Root, "backups"), workspace.DatabasePath).IsSuccess);
        var updater = new FabRequirementsProfileUpdater(repository);
        _ = await updater.CacheAsync(Revision(1), Token);
        workspace.Execute("UPDATE RequirementsProfiles SET ProfileJson='{}';");
        Assert.False((await updater.LoadCachedAsync("2026-09-12.1", Token)).IsSuccess);
        workspace.Execute("INSERT INTO Settings VALUES ('requirements:fab:current','\"absent\"','2026-09-12T12:00:00Z');");
        Assert.False((await updater.LoadCurrentAsync(Token)).IsSuccess);
    }

    [Fact]
    public async Task RepositoryRejectsForgedApprovalCacheInvalidHashesAndCancelledWrites()
    {
        using Workspace workspace = new();
        SqliteRequirementsProfileRepository repository = workspace.Repository();
        FabRequirementsProfile baseline = FabRequirementsBaseline.Profile;
        CachedRequirementsProfile record = new("fab", baseline.Document.Version, baseline.Document.EffectiveOn,
            baseline.Document.Sources[0].Url, baseline.CanonicalJson, baseline.Sha256, false);
        Assert.False((await repository.CacheAsync(record with { IsApproved = true }, Token)).IsSuccess);
        Assert.False((await repository.CacheAsync(record with { Sha256 = new string('0', 64) }, Token)).IsSuccess);
        Assert.False((await repository.CacheAsync(record with { Marketplace = "../escape" }, Token)).IsSuccess);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.CacheAsync(record, cancelled.Token));
        Assert.Null((await repository.GetAsync("fab", record.Version, Token)).Value);
    }

    [Theory]
    [InlineData("overflow")]
    [InlineData("absolute-reference")]
    [InlineData("traversal")]
    [InlineData("empty-hash")]
    [InlineData("inconsistent")]
    public void SharedEvidenceValidationRejectsMalformedOrOverflowingCounts(string mutation)
    {
        PersistedCompatibilitySuiteResult evidence = Evidence();
        evidence = mutation switch
        {
            "overflow" => evidence with { TotalTests = int.MaxValue, PassedTests = int.MaxValue, FailedTests = int.MaxValue },
            "absolute-reference" => evidence with { EvidenceReference = "C:/fixture/report.json" },
            "traversal" => evidence with { EvidenceReference = "../report.json" },
            "empty-hash" => evidence with { EvidenceSha256 = "" },
            _ => evidence with { TotalTests = 2 },
        };
        Assert.False(CompatibilityEvidenceValidation.IsValid(evidence, CompatibilitySuiteOutcome.Passed));
    }

    private static string Revision(int revision)
    {
        JsonNode root = JsonNode.Parse(FabRequirementsBaseline.Profile.CanonicalJson)!;
        root["version"] = $"2026-09-12.{revision}";
        return root.ToJsonString();
    }

    private static PersistedCompatibilitySuiteResult Evidence() => new("fixture-suite", CompatibilitySuiteOutcome.Passed,
        1, 1, 0, "fixtures/profile-results.json", new string('a', 64), _now.AddMinutes(-1));

    private sealed class Suite(bool failed = false, bool incomplete = false) : IFabRequirementsCompatibilitySuite
    {
        public Task<PersistedCompatibilitySuiteResult> RunAsync(FabRequirementsProfile candidate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A fixture runner's evidence digest includes the exact candidate; updater tests control its outcome.
            string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(candidate.CanonicalJson)));
            return Task.FromResult(Evidence() with
            {
                EvidenceSha256 = digest,
                Outcome = failed ? CompatibilitySuiteOutcome.Failed : CompatibilitySuiteOutcome.Passed,
                PassedTests = failed ? 0 : 1,
                FailedTests = failed ? 1 : 0,
                TotalTests = incomplete ? 0 : 1,
            });
        }
    }

    private sealed class Workspace : IDisposable
    {
        public Workspace()
        {
            Root = Path.Combine(AppContext.BaseDirectory, "PB-1001", Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(Root);
            DatabasePath = Path.Combine(Root, "profiles.db");
            _ = Directory.CreateDirectory(Path.Combine(Root, "backups"));
            Assert.True(new SqliteDatabaseMigrator().Migrate(Root, DatabasePath, Path.Combine(Root, "backups"), Token).IsSuccess);
        }

        public string Root { get; }
        public string DatabasePath { get; }
        public SqliteRequirementsProfileRepository Repository() => SqliteRequirementsProfileRepository.Create(Root, DatabasePath).Value!;

        public void Execute(string sql)
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = DatabasePath, Pooling = false }.ConnectionString);
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            _ = command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            string parent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "PB-1001")) + Path.DirectorySeparatorChar;
            Assert.StartsWith(parent, Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories),
                path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0);
            Directory.Delete(Root, recursive: true);
            Assert.False(Directory.Exists(Root));
        }
    }
}
