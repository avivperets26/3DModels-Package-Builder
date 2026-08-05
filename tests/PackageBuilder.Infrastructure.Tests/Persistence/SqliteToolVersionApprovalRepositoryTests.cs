using Microsoft.Data.Sqlite;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Domain.Tools;
using PackageBuilder.Infrastructure.Persistence;

namespace PackageBuilder.Infrastructure.Tests.Persistence;

[Collection(SqliteMigrationFixture.Name)]
public sealed class SqliteToolVersionApprovalRepositoryTests
{
    private static readonly DateTimeOffset _created =
        new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CompletePassingLifecyclePersistsModulesAndCompatibilityEvidence()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteToolVersionApprovalRepository repository = CreateRepository(workspace);
        PersistedToolVersionApproval approval = Approval(
            "unity-6000.3.2f1",
            "6000.3.2f1",
            modules: ["windows", "android"]);
        Assert.True((await repository.CreateAsync(
            approval,
            "transition-discovered",
            TestContext.Current.CancellationToken)).IsSuccess);

        PersistedToolVersionApproval installed = await Transition(
            repository,
            approval.Id,
            0,
            ToolVersionApprovalState.Discovered,
            ToolVersionApprovalState.Installed,
            "transition-installed",
            1);
        PersistedToolVersionApproval candidate = await Transition(
            repository,
            approval.Id,
            installed.Revision,
            ToolVersionApprovalState.Installed,
            ToolVersionApprovalState.Candidate,
            "transition-candidate",
            2);
        PersistedCompatibilitySuiteResult evidence = PassingEvidence("suite-pass", 3);
        PersistedToolVersionApproval approved = await Transition(
            repository,
            approval.Id,
            candidate.Revision,
            ToolVersionApprovalState.Candidate,
            ToolVersionApprovalState.ApprovedLatest,
            "transition-approved",
            4,
            evidence);

        Assert.Equal(ToolVersionApprovalState.ApprovedLatest, approved.State);
        Assert.Equal(3, approved.Revision);
        Assert.Equal(["android", "windows"], approved.AvailableModules);
        RepositoryOperationResult<IReadOnlyList<PersistedToolVersionApprovalTransition>> history =
            await repository.GetHistoryAsync(approval.Id, TestContext.Current.CancellationToken);
        Assert.True(history.IsSuccess);
        Assert.Equal(4, history.Value!.Count);
        Assert.Equal(
            [ToolVersionApprovalState.Discovered, ToolVersionApprovalState.Installed,
             ToolVersionApprovalState.Candidate, ToolVersionApprovalState.ApprovedLatest],
            history.Value.Select(value => value.ToState));
        Assert.Equal(evidence, history.Value[^1].CompatibilityResult);
    }

    [Fact]
    public async Task FailedCandidateCanBeRetriedWithBothResultsRetained()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteToolVersionApprovalRepository repository = CreateRepository(workspace);
        PersistedToolVersionApproval candidate = await CreateCandidate(repository, "unreal-5.7.0", ToolKind.Unreal, "5.7.0");

        PersistedToolVersionApproval rejected = await Transition(
            repository,
            candidate.Id,
            candidate.Revision,
            ToolVersionApprovalState.Candidate,
            ToolVersionApprovalState.Rejected,
            "transition-rejected",
            3,
            FailingEvidence("suite-fail", 2));
        PersistedToolVersionApproval retried = await Transition(
            repository,
            rejected.Id,
            rejected.Revision,
            ToolVersionApprovalState.Rejected,
            ToolVersionApprovalState.Candidate,
            "transition-retry",
            4);
        PersistedCompatibilitySuiteResult passing = PassingEvidence("suite-pass", 5);
        _ = await Transition(
            repository,
            retried.Id,
            retried.Revision,
            ToolVersionApprovalState.Candidate,
            ToolVersionApprovalState.ApprovedLatest,
            "transition-approved",
            6,
            passing);

        IReadOnlyList<PersistedToolVersionApprovalTransition> history =
            (await repository.GetHistoryAsync(candidate.Id, TestContext.Current.CancellationToken)).Value!;
        Assert.Equal(6, history.Count);
        Assert.Equal(CompatibilitySuiteOutcome.Failed, history[3].CompatibilityResult!.Outcome);
        Assert.Equal(CompatibilitySuiteOutcome.Passed, history[5].CompatibilityResult!.Outcome);
    }

    [Fact]
    public async Task PromotingNewerVersionAtomicallyDemotesPreviousApproval()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteToolVersionApprovalRepository repository = CreateRepository(workspace);
        PersistedToolVersionApproval older = await CreateCandidate(repository, "unity-old", ToolKind.Unity, "6000.3.1f1");
        older = await Transition(
            repository,
            older.Id,
            older.Revision,
            ToolVersionApprovalState.Candidate,
            ToolVersionApprovalState.ApprovedLatest,
            "approve-old",
            3,
            PassingEvidence("suite-old", 3));
        PersistedToolVersionApproval newer = await CreateCandidate(repository, "unity-new", ToolKind.Unity, "6000.3.2f1", minuteOffset: 4);

        newer = await Transition(
            repository,
            newer.Id,
            newer.Revision,
            ToolVersionApprovalState.Candidate,
            ToolVersionApprovalState.ApprovedLatest,
            "approve-new",
            7,
            PassingEvidence("suite-new", 7));

        PersistedToolVersionApproval persistedOlder =
            (await repository.GetAsync(older.Id, TestContext.Current.CancellationToken)).Value!;
        Assert.Equal(ToolVersionApprovalState.LastKnownGood, persistedOlder.State);
        Assert.Equal(older.Revision + 1, persistedOlder.Revision);
        Assert.Equal(ToolVersionApprovalState.ApprovedLatest, newer.State);
        IReadOnlyList<PersistedToolVersionApprovalTransition> olderHistory =
            (await repository.GetHistoryAsync(older.Id, TestContext.Current.CancellationToken)).Value!;
        Assert.Equal(ToolVersionApprovalState.LastKnownGood, olderHistory[^1].ToState);
        Assert.Null(olderHistory[^1].CompatibilityResult);
    }

    [Theory]
    [InlineData("missing-pass")]
    [InlineData("failed-for-approval")]
    [InlineData("passed-for-rejection")]
    [InlineData("evidence-on-install")]
    [InlineData("skip-state")]
    public async Task InvalidTransitionsAndEvidenceFailBeforeMutation(string scenario)
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteToolVersionApprovalRepository repository = CreateRepository(workspace);
        PersistedToolVersionApproval candidate = await CreateCandidate(repository, "unity-invalid", ToolKind.Unity, "6000.3.2f1");
        ToolVersionApprovalState expected = ToolVersionApprovalState.Candidate;
        ToolVersionApprovalState target = scenario == "passed-for-rejection"
            ? ToolVersionApprovalState.Rejected
            : scenario == "evidence-on-install"
            ? ToolVersionApprovalState.Installed
            : scenario == "skip-state"
            ? ToolVersionApprovalState.LastKnownGood
            : ToolVersionApprovalState.ApprovedLatest;
        PersistedCompatibilitySuiteResult? evidence = scenario switch
        {
            "missing-pass" => null,
            "failed-for-approval" => FailingEvidence("suite", 3),
            "passed-for-rejection" => PassingEvidence("suite", 3),
            "evidence-on-install" => PassingEvidence("suite", 3),
            _ => null,
        };

        RepositoryOperationResult<PersistedToolVersionApproval> result = await repository.TransitionAsync(
            candidate.Id,
            candidate.Revision,
            expected,
            target,
            "transition-invalid",
            _created.AddMinutes(4),
            evidence,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("REPOSITORY_INPUT_INVALID", result.Error!.Code);
        Assert.Equal(
            ToolVersionApprovalState.Candidate,
            (await repository.GetAsync(candidate.Id, TestContext.Current.CancellationToken)).Value!.State);
    }

    [Fact]
    public async Task ExpectedStateAndRevisionRejectStaleConcurrentTransition()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteToolVersionApprovalRepository repository = CreateRepository(workspace);
        PersistedToolVersionApproval approval = Approval("unity-concurrent", "6000.3.2f1");
        Assert.True((await repository.CreateAsync(approval, "create", TestContext.Current.CancellationToken)).IsSuccess);
        _ = await Transition(
            repository,
            approval.Id,
            0,
            ToolVersionApprovalState.Discovered,
            ToolVersionApprovalState.Installed,
            "install",
            1);

        RepositoryOperationResult<PersistedToolVersionApproval> stale = await repository.TransitionAsync(
            approval.Id,
            0,
            ToolVersionApprovalState.Discovered,
            ToolVersionApprovalState.Installed,
            "stale",
            _created.AddMinutes(2),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("REPOSITORY_STATE_CONFLICT", stale.Error!.Code);
    }

    [Fact]
    public async Task StateAndHistorySurviveRepositoryRestartAndQueryDeterministically()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteToolVersionApprovalRepository repository = CreateRepository(workspace);
        Assert.True((await repository.CreateAsync(
            Approval("unreal", "5.7.0", ToolKind.Unreal),
            "create-unreal",
            TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await repository.CreateAsync(
            Approval("unity", "6000.3.2f1", ToolKind.Unity),
            "create-unity",
            TestContext.Current.CancellationToken)).IsSuccess);

        repository = SqliteToolVersionApprovalRepository.Create(workspace.Root, workspace.DatabasePath).Value!;
        IReadOnlyList<PersistedToolVersionApproval> approvals =
            (await repository.QueryAsync(cancellationToken: TestContext.Current.CancellationToken)).Value!;

        Assert.Equal(["unity", "unreal"], approvals.Select(value => value.Id));
        _ = Assert.Single((await repository.GetHistoryAsync("unity", TestContext.Current.CancellationToken)).Value!);
    }

    [Fact]
    public async Task DuplicateVersionAndModuleIdentityFailClosed()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteToolVersionApprovalRepository repository = CreateRepository(workspace);
        PersistedToolVersionApproval first = Approval("unity-first", "6000.3.2f1");
        Assert.True((await repository.CreateAsync(first, "create-first", TestContext.Current.CancellationToken)).IsSuccess);
        RepositoryOperationResult duplicateVersion = await repository.CreateAsync(
            Approval("unity-second", "6000.3.2f1"),
            "create-second",
            TestContext.Current.CancellationToken);
        RepositoryOperationResult duplicateModules = await repository.CreateAsync(
            Approval("unity-modules", "6000.3.3f1", modules: ["android", "ANDROID"]),
            "create-modules",
            TestContext.Current.CancellationToken);

        Assert.Equal("REPOSITORY_APPROVAL_CONFLICT", duplicateVersion.Error!.Code);
        Assert.Equal("REPOSITORY_INPUT_INVALID", duplicateModules.Error!.Code);
    }

    [Fact]
    public async Task CorruptStoredTimestampReturnsSanitizedDataFailure()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        SqliteToolVersionApprovalRepository repository = CreateRepository(workspace);
        Assert.True((await repository.CreateAsync(
            Approval("unity-corrupt", "6000.3.2f1"),
            "create",
            TestContext.Current.CancellationToken)).IsSuccess);
        workspace.Execute("UPDATE EngineVersions SET UpdatedAtUtc = 'not-a-timestamp' WHERE EngineVersionId = 'unity-corrupt';");

        RepositoryOperationResult<PersistedToolVersionApproval?> result = await repository.GetAsync(
            "unity-corrupt",
            TestContext.Current.CancellationToken);

        Assert.Equal("REPOSITORY_DATA_INVALID", result.Error!.Code);
        Assert.DoesNotContain(workspace.Root, result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SqliteToolVersionApprovalRepository CreateRepository(
        SqliteMigrationTestWorkspace workspace)
    {
        Assert.True(new SqliteDatabaseMigrator().Migrate(
            workspace.Root,
            workspace.DatabasePath,
            workspace.BackupDirectory,
            TestContext.Current.CancellationToken).IsSuccess);
        RepositoryOperationResult<SqliteToolVersionApprovalRepository> result =
            SqliteToolVersionApprovalRepository.Create(workspace.Root, workspace.DatabasePath);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static async Task<PersistedToolVersionApproval> CreateCandidate(
        SqliteToolVersionApprovalRepository repository,
        string id,
        ToolKind tool,
        string version,
        int minuteOffset = 0)
    {
        PersistedToolVersionApproval approval = Approval(id, version, tool, minuteOffset: minuteOffset);
        Assert.True((await repository.CreateAsync(
            approval,
            id + "-discovered",
            TestContext.Current.CancellationToken)).IsSuccess);
        PersistedToolVersionApproval installed = await Transition(
            repository,
            id,
            0,
            ToolVersionApprovalState.Discovered,
            ToolVersionApprovalState.Installed,
            id + "-installed",
            minuteOffset + 1);
        return await Transition(
            repository,
            id,
            installed.Revision,
            ToolVersionApprovalState.Installed,
            ToolVersionApprovalState.Candidate,
            id + "-candidate",
            minuteOffset + 2);
    }

    private static async Task<PersistedToolVersionApproval> Transition(
        SqliteToolVersionApprovalRepository repository,
        string id,
        long revision,
        ToolVersionApprovalState expected,
        ToolVersionApprovalState target,
        string transitionId,
        int minute,
        PersistedCompatibilitySuiteResult? evidence = null)
    {
        RepositoryOperationResult<PersistedToolVersionApproval> result = await repository.TransitionAsync(
            id,
            revision,
            expected,
            target,
            transitionId,
            _created.AddMinutes(minute),
            evidence,
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private static PersistedToolVersionApproval Approval(
        string id,
        string version,
        ToolKind tool = ToolKind.Unity,
        int minuteOffset = 0,
        params string[] modules) => new(
            id,
            ToolVersion.Create(tool, version).Value!,
            ToolVersionApprovalState.Discovered,
            modules,
            null,
            _created.AddMinutes(minuteOffset),
            _created.AddMinutes(minuteOffset),
            0);

    private static PersistedCompatibilitySuiteResult PassingEvidence(string runId, int minute) => new(
        runId,
        CompatibilitySuiteOutcome.Passed,
        8,
        8,
        0,
        $"compatibility/{runId}.json",
        new string('a', 64),
        _created.AddMinutes(minute));

    private static PersistedCompatibilitySuiteResult FailingEvidence(string runId, int minute) => new(
        runId,
        CompatibilitySuiteOutcome.Failed,
        8,
        7,
        1,
        $"compatibility/{runId}.json",
        new string('b', 64),
        _created.AddMinutes(minute));
}
