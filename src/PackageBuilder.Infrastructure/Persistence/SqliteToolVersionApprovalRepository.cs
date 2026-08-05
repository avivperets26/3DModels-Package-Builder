using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Data.Sqlite;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Infrastructure.Persistence;

/// <summary>Persists engine approval state, installed modules, and append-only compatibility evidence.</summary>
public sealed class SqliteToolVersionApprovalRepository : IToolVersionApprovalRepository
{
    private const int MaximumIdentityLength = 512;
    private const int MaximumTextLength = 4096;
    private readonly string _databasePath;

    private SqliteToolVersionApprovalRepository(string databasePath) => _databasePath = databasePath;

    public static RepositoryOperationResult<SqliteToolVersionApprovalRepository> Create(
        string projectRoot,
        string databasePath)
    {
        if (!TryValidateDatabasePath(projectRoot, databasePath, out string? normalizedPath))
        {
            return Failure<SqliteToolVersionApprovalRepository>(
                "REPOSITORY_PATH_INVALID",
                "The approval database path must be existing, absolute, and contained.");
        }

        try
        {
            using SqliteConnection connection = Open(normalizedPath!);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            int version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            command.CommandText = "PRAGMA quick_check;";
            bool isHealthy = StringComparer.Ordinal.Equals(command.ExecuteScalar() as string, "ok");
            command.CommandText = """
                SELECT COUNT(*) FROM sqlite_schema
                WHERE type = 'table' AND name IN
                    ('EngineVersions','EngineVersionModules','EngineVersionTransitions');
                """;
            bool hasTables = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 3;
            return version == SqliteSchema.CurrentVersion && isHealthy && hasTables
                ? RepositoryOperationResult.Success(new SqliteToolVersionApprovalRepository(normalizedPath!))
                : version != SqliteSchema.CurrentVersion
                ? Failure<SqliteToolVersionApprovalRepository>(
                    "REPOSITORY_SCHEMA_UNSUPPORTED",
                    "The approval database schema is not supported by this version.")
                : Failure<SqliteToolVersionApprovalRepository>(
                    "REPOSITORY_SCHEMA_INVALID",
                    "The approval database schema is incomplete or invalid.");
        }
        catch (SqliteException)
        {
            return StorageFailure<SqliteToolVersionApprovalRepository>();
        }
    }

    public Task<RepositoryOperationResult> CreateAsync(
        PersistedToolVersionApproval approval,
        string initialTransitionId,
        CancellationToken cancellationToken = default) => RunAsync(
        async () =>
        {
            RepositoryOperationResult? validation = ValidateNewApproval(approval, initialTransitionId);
            if (validation is not null)
            {
                return validation;
            }

            await using SqliteConnection connection = await OpenAsync(cancellationToken);
            await using var transaction =
                (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await InsertApprovalAsync(connection, transaction, approval, cancellationToken);
            await InsertModulesAsync(connection, transaction, approval.Id, approval.AvailableModules, cancellationToken);
            await InsertTransitionAsync(
                connection,
                transaction,
                new PersistedToolVersionApprovalTransition(
                    initialTransitionId,
                    approval.Id,
                    null,
                    ToolVersionApprovalState.Discovered,
                    approval.DiscoveredAtUtc,
                    null),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RepositoryOperationResult.Success();
        },
        "REPOSITORY_APPROVAL_CONFLICT");

    public Task<RepositoryOperationResult<PersistedToolVersionApproval?>> GetAsync(
        string approvalId,
        CancellationToken cancellationToken = default) => RunAsync(async () =>
        {
            if (!IsIdentity(approvalId))
            {
                return Invalid<PersistedToolVersionApproval?>("A valid approval identifier is required.");
            }

            await using SqliteConnection connection = await OpenAsync(cancellationToken);
            PersistedToolVersionApproval? approval = await ReadApprovalAsync(
                connection,
                transaction: null,
                approvalId,
                cancellationToken);
            return RepositoryOperationResult.Success<PersistedToolVersionApproval?>(approval);
        });

    public Task<RepositoryOperationResult<IReadOnlyList<PersistedToolVersionApproval>>> QueryAsync(
        ToolKind? tool = null,
        ToolVersionApprovalState? state = null,
        CancellationToken cancellationToken = default) => RunAsync(async () =>
        {
            if (tool is { } toolValue && (!Enum.IsDefined(toolValue) || toolValue == ToolKind.DotNet)
                || state is { } stateValue && !Enum.IsDefined(stateValue))
            {
                return Invalid<IReadOnlyList<PersistedToolVersionApproval>>("The approval query filter is invalid.");
            }

            await using SqliteConnection connection = await OpenAsync(cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT EngineVersionId
                FROM EngineVersions
                WHERE ($tool IS NULL OR EngineKind = $tool)
                  AND ($state IS NULL OR Status = $state);
                """;
            Add(command, "$tool", tool is null ? null : ToolToken(tool.Value));
            Add(command, "$state", state is null ? null : StateToken(state.Value));
            var ids = new List<string>();
            await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    ids.Add(reader.GetString(0));
                }
            }

            var approvals = new List<PersistedToolVersionApproval>(ids.Count);
            foreach (string id in ids)
            {
                approvals.Add((await ReadApprovalAsync(connection, null, id, cancellationToken))!);
            }

            approvals.Sort(static (left, right) =>
            {
                int toolComparison = left.Version.Tool.CompareTo(right.Version.Tool);
                return toolComparison != 0
                    ? toolComparison
                    : right.Version.CompareTo(left.Version);
            });
            return RepositoryOperationResult.Success<IReadOnlyList<PersistedToolVersionApproval>>(
                new ReadOnlyCollection<PersistedToolVersionApproval>(approvals));
        });

    public Task<RepositoryOperationResult<IReadOnlyList<PersistedToolVersionApprovalTransition>>> GetHistoryAsync(
        string approvalId,
        CancellationToken cancellationToken = default) => RunAsync(async () =>
        {
            if (!IsIdentity(approvalId))
            {
                return Invalid<IReadOnlyList<PersistedToolVersionApprovalTransition>>(
                    "A valid approval identifier is required.");
            }

            await using SqliteConnection connection = await OpenAsync(cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT TransitionId, FromStatus, ToStatus, OccurredAtUtc,
                       CompatibilityRunId, CompatibilityOutcome, TotalTests, PassedTests,
                       FailedTests, EvidenceReference, EvidenceSha256, CompletedAtUtc
                FROM EngineVersionTransitions
                WHERE EngineVersionId = $id
                ORDER BY OccurredAtUtc, TransitionId;
                """;
            Add(command, "$id", approvalId);
            var history = new List<PersistedToolVersionApprovalTransition>();
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                history.Add(ReadTransition(reader, approvalId));
            }

            return RepositoryOperationResult.Success<IReadOnlyList<PersistedToolVersionApprovalTransition>>(
                new ReadOnlyCollection<PersistedToolVersionApprovalTransition>(history));
        });

    public Task<RepositoryOperationResult<PersistedToolVersionApproval>> TransitionAsync(
        string approvalId,
        long expectedRevision,
        ToolVersionApprovalState expectedState,
        ToolVersionApprovalState targetState,
        string transitionId,
        DateTimeOffset occurredAtUtc,
        PersistedCompatibilitySuiteResult? compatibilityResult = null,
        CancellationToken cancellationToken = default) => RunAsync(async () =>
        {
            RepositoryOperationResult? validation = ValidateTransition(
                approvalId,
                expectedRevision,
                expectedState,
                targetState,
                transitionId,
                occurredAtUtc,
                compatibilityResult);
            if (validation is not null)
            {
                return Failure<PersistedToolVersionApproval>(validation.Error!.Code, validation.Error.Message);
            }

            await using SqliteConnection connection = await OpenAsync(cancellationToken);
            await using var transaction =
                (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            PersistedToolVersionApproval? current = await ReadApprovalAsync(
                connection,
                transaction,
                approvalId,
                cancellationToken);
            if (current is null)
            {
                return NotFound<PersistedToolVersionApproval>("The requested approval does not exist.");
            }

            if (current.Revision != expectedRevision || current.State != expectedState)
            {
                return Failure<PersistedToolVersionApproval>(
                    "REPOSITORY_STATE_CONFLICT",
                    "The approval state changed before the requested transition could be applied.");
            }

            if (occurredAtUtc < current.UpdatedAtUtc
                || compatibilityResult is { CompletedAtUtc: var completed }
                    && (completed < current.DiscoveredAtUtc || completed > occurredAtUtc))
            {
                return Invalid<PersistedToolVersionApproval>(
                    "Transition and compatibility timestamps must preserve persisted chronology.");
            }

            if (targetState == ToolVersionApprovalState.ApprovedLatest)
            {
                await DemotePreviousApprovedAsync(
                    connection,
                    transaction,
                    current,
                    transitionId,
                    occurredAtUtc,
                    cancellationToken);
            }

            await using (SqliteCommand update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE EngineVersions
                    SET Status = $target,
                        LastValidatedAtUtc = COALESCE($validated, LastValidatedAtUtc),
                        UpdatedAtUtc = $occurred, Revision = Revision + 1
                    WHERE EngineVersionId = $id AND Status = $expected AND Revision = $revision;
                    """;
                Add(update, "$target", StateToken(targetState));
                Add(update, "$validated", compatibilityResult is null
                    ? null
                    : FormatTimestamp(compatibilityResult.CompletedAtUtc));
                Add(update, "$occurred", FormatTimestamp(occurredAtUtc));
                Add(update, "$id", approvalId);
                Add(update, "$expected", StateToken(expectedState));
                Add(update, "$revision", expectedRevision);
                if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Failure<PersistedToolVersionApproval>(
                        "REPOSITORY_STATE_CONFLICT",
                        "The approval state changed before the requested transition could be applied.");
                }
            }

            await InsertTransitionAsync(
                connection,
                transaction,
                new PersistedToolVersionApprovalTransition(
                    transitionId,
                    approvalId,
                    expectedState,
                    targetState,
                    occurredAtUtc,
                    compatibilityResult),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RepositoryOperationResult.Success(
                (await ReadApprovalAsync(connection, null, approvalId, cancellationToken))!);
        });

    private static async Task DemotePreviousApprovedAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PersistedToolVersionApproval promoted,
        string transitionId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = """
            SELECT EngineVersionId
            FROM EngineVersions
            WHERE EngineKind = $tool AND Status = 'approved-latest' AND EngineVersionId <> $id;
            """;
        Add(query, "$tool", ToolToken(promoted.Version.Tool));
        Add(query, "$id", promoted.Id);
        if (await query.ExecuteScalarAsync(cancellationToken) is not string previousId)
        {
            return;
        }

        await using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE EngineVersions
            SET Status = 'last-known-good', UpdatedAtUtc = $occurred, Revision = Revision + 1
            WHERE EngineVersionId = $id AND Status = 'approved-latest';
            """;
        Add(update, "$occurred", FormatTimestamp(occurredAtUtc));
        Add(update, "$id", previousId);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new RepositoryDataException();
        }

        await InsertTransitionAsync(
            connection,
            transaction,
            new PersistedToolVersionApprovalTransition(
                transitionId + ".last-known-good",
                previousId,
                ToolVersionApprovalState.ApprovedLatest,
                ToolVersionApprovalState.LastKnownGood,
                occurredAtUtc,
                null),
            cancellationToken);
    }

    private static async Task InsertApprovalAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PersistedToolVersionApproval approval,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO EngineVersions
                (EngineVersionId, EngineKind, Version, Status, ToolInstallationId,
                 DiscoveredAtUtc, LastValidatedAtUtc, UpdatedAtUtc, Revision)
            VALUES ($id, $tool, $version, $state, $installation,
                    $discovered, NULL, $updated, $revision);
            """;
        Add(command, "$id", approval.Id);
        Add(command, "$tool", ToolToken(approval.Version.Tool));
        Add(command, "$version", approval.Version.Value);
        Add(command, "$state", StateToken(approval.State));
        Add(command, "$installation", approval.ToolInstallationId);
        Add(command, "$discovered", FormatTimestamp(approval.DiscoveredAtUtc));
        Add(command, "$updated", FormatTimestamp(approval.UpdatedAtUtc));
        Add(command, "$revision", approval.Revision);
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertModulesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string approvalId,
        IEnumerable<string> modules,
        CancellationToken cancellationToken)
    {
        foreach (string module in modules.Order(StringComparer.OrdinalIgnoreCase))
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO EngineVersionModules (EngineVersionId, ModuleName) VALUES ($id, $module);";
            Add(command, "$id", approvalId);
            Add(command, "$module", module);
            _ = await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertTransitionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PersistedToolVersionApprovalTransition transition,
        CancellationToken cancellationToken)
    {
        PersistedCompatibilitySuiteResult? result = transition.CompatibilityResult;
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO EngineVersionTransitions
                (TransitionId, EngineVersionId, FromStatus, ToStatus, OccurredAtUtc,
                 CompatibilityRunId, CompatibilityOutcome, TotalTests, PassedTests, FailedTests,
                 EvidenceReference, EvidenceSha256, CompletedAtUtc)
            VALUES ($transition, $approval, $from, $to, $occurred,
                    $run, $outcome, $total, $passed, $failed,
                    $reference, $sha256, $completed);
            """;
        Add(command, "$transition", transition.Id);
        Add(command, "$approval", transition.ApprovalId);
        Add(command, "$from", transition.FromState is null ? null : StateToken(transition.FromState.Value));
        Add(command, "$to", StateToken(transition.ToState));
        Add(command, "$occurred", FormatTimestamp(transition.OccurredAtUtc));
        Add(command, "$run", result?.RunId);
        Add(command, "$outcome", result is null ? null : OutcomeToken(result.Outcome));
        Add(command, "$total", result?.TotalTests);
        Add(command, "$passed", result?.PassedTests);
        Add(command, "$failed", result?.FailedTests);
        Add(command, "$reference", result?.EvidenceReference);
        Add(command, "$sha256", result?.EvidenceSha256);
        Add(command, "$completed", result is null ? null : FormatTimestamp(result.CompletedAtUtc));
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<PersistedToolVersionApproval?> ReadApprovalAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string approvalId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT EngineVersionId, EngineKind, Version, Status, ToolInstallationId,
                   DiscoveredAtUtc, UpdatedAtUtc, Revision
            FROM EngineVersions WHERE EngineVersionId = $id;
            """;
        Add(command, "$id", approvalId);
        string? id;
        ToolVersion? version;
        ToolVersionApprovalState state;
        string? installation;
        DateTimeOffset discovered;
        DateTimeOffset updated;
        long revision;
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            id = reader.GetString(0);
            ToolKind tool = ParseTool(reader.GetString(1));
            version = ToolVersion.Create(tool, reader.GetString(2)).Value;
            state = ParseState(reader.GetString(3));
            installation = reader.IsDBNull(4) ? null : reader.GetString(4);
            discovered = ParseTimestamp(reader.GetString(5));
            updated = ParseTimestamp(reader.GetString(6));
            revision = reader.GetInt64(7);
        }

        if (version is null || revision < 0 || updated < discovered)
        {
            throw new RepositoryDataException();
        }

        await using SqliteCommand modulesCommand = connection.CreateCommand();
        modulesCommand.Transaction = transaction;
        modulesCommand.CommandText = "SELECT ModuleName FROM EngineVersionModules WHERE EngineVersionId = $id ORDER BY ModuleName COLLATE NOCASE, ModuleName;";
        Add(modulesCommand, "$id", approvalId);
        var modules = new List<string>();
        await using SqliteDataReader modulesReader = await modulesCommand.ExecuteReaderAsync(cancellationToken);
        while (await modulesReader.ReadAsync(cancellationToken))
        {
            string module = modulesReader.GetString(0);
            if (!IsModule(module) || modules.Contains(module, StringComparer.OrdinalIgnoreCase))
            {
                throw new RepositoryDataException();
            }

            modules.Add(module);
        }

        return new PersistedToolVersionApproval(
            id,
            version,
            state,
            new ReadOnlyCollection<string>(modules),
            installation,
            discovered,
            updated,
            revision);
    }

    private static PersistedToolVersionApprovalTransition ReadTransition(
        SqliteDataReader reader,
        string approvalId)
    {
        PersistedCompatibilitySuiteResult? result = reader.IsDBNull(4)
            ? null
            : new PersistedCompatibilitySuiteResult(
                reader.GetString(4),
                ParseOutcome(reader.GetString(5)),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetString(9),
                reader.GetString(10),
                ParseTimestamp(reader.GetString(11)));
        return new PersistedToolVersionApprovalTransition(
            reader.GetString(0),
            approvalId,
            reader.IsDBNull(1) ? null : ParseState(reader.GetString(1)),
            ParseState(reader.GetString(2)),
            ParseTimestamp(reader.GetString(3)),
            result);
    }

    private static RepositoryOperationResult? ValidateNewApproval(
        PersistedToolVersionApproval? approval,
        string initialTransitionId) =>
        approval is null || !IsIdentity(approval.Id) || !IsIdentity(initialTransitionId)
            ? Invalid("Complete approval identity is required.")
            : approval.Version is null || approval.Version.Tool == ToolKind.DotNet
            ? Invalid("A canonical Blender, Unity, or Unreal version is required.")
            : approval.State != ToolVersionApprovalState.Discovered || approval.Revision != 0
            ? Invalid("A new approval must begin as Discovered at revision zero.")
            : !IsUtc(approval.DiscoveredAtUtc) || approval.UpdatedAtUtc != approval.DiscoveredAtUtc
            ? Invalid("A new approval must use one UTC discovery timestamp.")
            : approval.ToolInstallationId is not null && !IsIdentity(approval.ToolInstallationId)
            ? Invalid("The tool-installation identifier is invalid.")
            : !ValidateModules(approval.AvailableModules)
            ? Invalid("Available module identifiers must be unique non-empty trimmed values.")
            : null;

    private static RepositoryOperationResult? ValidateTransition(
        string approvalId,
        long expectedRevision,
        ToolVersionApprovalState expectedState,
        ToolVersionApprovalState targetState,
        string transitionId,
        DateTimeOffset occurredAtUtc,
        PersistedCompatibilitySuiteResult? result)
    {
        if (!IsIdentity(approvalId) || !IsIdentity(transitionId) || expectedRevision < 0
            || !IsUtc(occurredAtUtc)
            || !ToolVersionApprovalTransitionPolicy.CanTransition(expectedState, targetState))
        {
            return Invalid("The requested approval transition is invalid.");
        }

        bool requiresPassed = expectedState == ToolVersionApprovalState.Candidate
            && targetState == ToolVersionApprovalState.ApprovedLatest;
        bool requiresFailed = expectedState == ToolVersionApprovalState.Candidate
            && targetState == ToolVersionApprovalState.Rejected;
        return requiresPassed
            ? !ValidateCompatibility(result, CompatibilitySuiteOutcome.Passed)
                ? Invalid("Candidate approval requires complete passing compatibility evidence.")
                : null
            : requiresFailed
            ? !ValidateCompatibility(result, CompatibilitySuiteOutcome.Failed)
                ? Invalid("Candidate rejection requires complete failing compatibility evidence.")
                : null
            : result is not null
            ? Invalid("Compatibility evidence is accepted only when approving or rejecting a candidate.")
            : null;
    }

    private static bool ValidateCompatibility(
        PersistedCompatibilitySuiteResult? result,
        CompatibilitySuiteOutcome requiredOutcome) =>
        result is not null
        && result.Outcome == requiredOutcome
        && IsIdentity(result.RunId)
        && result.TotalTests > 0
        && result.PassedTests >= 0
        && result.FailedTests >= 0
        && result.PassedTests + result.FailedTests == result.TotalTests
        && (requiredOutcome == CompatibilitySuiteOutcome.Passed
            ? result.FailedTests == 0 && result.PassedTests == result.TotalTests
            : result.FailedTests > 0)
        && IsLogicalReference(result.EvidenceReference)
        && IsSha256(result.EvidenceSha256)
        && IsUtc(result.CompletedAtUtc);

    private static bool ValidateModules(IReadOnlyCollection<string>? modules)
    {
        if (modules is null)
        {
            return false;
        }

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return modules.All(module => IsModule(module) && unique.Add(module));
    }

    private static bool IsModule(string? value) => IsText(value, 256);

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = CreateConnection(_databasePath);
        await connection.OpenAsync(cancellationToken);
        await ConfigureAsync(connection, cancellationToken);
        return connection;
    }

    private static SqliteConnection Open(string databasePath)
    {
        SqliteConnection connection = CreateConnection(databasePath);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 30000;";
        _ = command.ExecuteNonQuery();
        return connection;
    }

    private static SqliteConnection CreateConnection(string databasePath) => new(
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 30,
        }.ConnectionString);

    private static async Task ConfigureAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 30000;";
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Add(SqliteCommand command, string name, object? value) =>
        _ = command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static string ToolToken(ToolKind tool) => tool switch
    {
        ToolKind.Blender => "blender",
        ToolKind.Unity => "unity",
        ToolKind.Unreal => "unreal",
        _ => throw new RepositoryDataException(),
    };

    private static ToolKind ParseTool(string value) => value switch
    {
        "blender" => ToolKind.Blender,
        "unity" => ToolKind.Unity,
        "unreal" => ToolKind.Unreal,
        _ => throw new RepositoryDataException(),
    };

    private static string StateToken(ToolVersionApprovalState state) => state switch
    {
        ToolVersionApprovalState.Discovered => "discovered",
        ToolVersionApprovalState.Installed => "installed",
        ToolVersionApprovalState.Candidate => "candidate",
        ToolVersionApprovalState.ApprovedLatest => "approved-latest",
        ToolVersionApprovalState.Rejected => "rejected",
        ToolVersionApprovalState.LastKnownGood => "last-known-good",
        _ => throw new RepositoryDataException(),
    };

    private static ToolVersionApprovalState ParseState(string value) => value switch
    {
        "discovered" => ToolVersionApprovalState.Discovered,
        "installed" => ToolVersionApprovalState.Installed,
        "candidate" => ToolVersionApprovalState.Candidate,
        "approved-latest" => ToolVersionApprovalState.ApprovedLatest,
        "rejected" => ToolVersionApprovalState.Rejected,
        "last-known-good" => ToolVersionApprovalState.LastKnownGood,
        _ => throw new RepositoryDataException(),
    };

    private static string OutcomeToken(CompatibilitySuiteOutcome outcome) => outcome switch
    {
        CompatibilitySuiteOutcome.Passed => "passed",
        CompatibilitySuiteOutcome.Failed => "failed",
        _ => throw new RepositoryDataException(),
    };

    private static CompatibilitySuiteOutcome ParseOutcome(string value) => value switch
    {
        "passed" => CompatibilitySuiteOutcome.Passed,
        "failed" => CompatibilitySuiteOutcome.Failed,
        _ => throw new RepositoryDataException(),
    };

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        !DateTimeOffset.TryParseExact(
            value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTimeOffset timestamp)
        || !IsUtc(timestamp)
            ? throw new RepositoryDataException()
            : timestamp;

    private static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;

    private static bool IsIdentity(string? value) =>
        IsText(value, MaximumIdentityLength) && !value!.Any(char.IsControl);

    private static bool IsText(string? value, int maximumLength = MaximumTextLength) =>
        value is not null && value.Length is > 0 && value.Length <= maximumLength
        && !string.IsNullOrWhiteSpace(value) && !char.IsWhiteSpace(value[0])
        && !char.IsWhiteSpace(value[^1]) && !value.Any(char.IsControl);

    private static bool IsLogicalReference(string? value) =>
        IsText(value) && value![0] != '/' && !value.Contains('\\', StringComparison.Ordinal)
        && !value.Contains(':', StringComparison.Ordinal) && value.Split('/').All(segment =>
            segment.Length > 0 && segment is not "." and not ".."
            && !char.IsWhiteSpace(segment[0]) && !char.IsWhiteSpace(segment[^1]));

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool TryValidateDatabasePath(
        string? projectRoot,
        string? databasePath,
        out string? normalizedPath)
    {
        normalizedPath = null;
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(databasePath))
        {
            return false;
        }

        try
        {
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
            string candidate = Path.GetFullPath(databasePath);
            if (!Path.IsPathFullyQualified(projectRoot) || !Path.IsPathFullyQualified(databasePath)
                || !Directory.Exists(root) || !File.Exists(candidate)
                || !candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string current = root;
            foreach (string segment in Path.GetRelativePath(root, candidate).Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                current = Path.Combine(current, segment);
            }

            if ((File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            normalizedPath = candidate;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task<RepositoryOperationResult> RunAsync(
        Func<Task<RepositoryOperationResult>> operation,
        string conflictCode)
    {
        try
        {
            return await operation();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            return RepositoryOperationResult.Failure(
                conflictCode,
                "The approval repository rejected conflicting metadata.");
        }
        catch (SqliteException)
        {
            return StorageFailure();
        }
        catch (RepositoryDataException)
        {
            return RepositoryOperationResult.Failure(
                "REPOSITORY_DATA_INVALID",
                "Stored approval metadata is invalid or incompatible.");
        }
    }

    private static async Task<RepositoryOperationResult<T>> RunAsync<T>(
        Func<Task<RepositoryOperationResult<T>>> operation)
    {
        try
        {
            return await operation();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            return Failure<T>("REPOSITORY_APPROVAL_CONFLICT", "The approval repository rejected conflicting metadata.");
        }
        catch (SqliteException)
        {
            return StorageFailure<T>();
        }
        catch (RepositoryDataException)
        {
            return Failure<T>("REPOSITORY_DATA_INVALID", "Stored approval metadata is invalid or incompatible.");
        }
    }

    private static RepositoryOperationResult Invalid(string message) =>
        RepositoryOperationResult.Failure("REPOSITORY_INPUT_INVALID", message);

    private static RepositoryOperationResult<T> Invalid<T>(string message) =>
        RepositoryOperationResult.Failure<T>("REPOSITORY_INPUT_INVALID", message);

    private static RepositoryOperationResult<T> NotFound<T>(string message) =>
        RepositoryOperationResult.Failure<T>("REPOSITORY_NOT_FOUND", message);

    private static RepositoryOperationResult StorageFailure() =>
        RepositoryOperationResult.Failure("REPOSITORY_STORAGE_FAILED", "The approval repository could not complete the operation safely.");

    private static RepositoryOperationResult<T> StorageFailure<T>() =>
        RepositoryOperationResult.Failure<T>("REPOSITORY_STORAGE_FAILED", "The approval repository could not complete the operation safely.");

    private static RepositoryOperationResult<T> Failure<T>(string code, string message) =>
        RepositoryOperationResult.Failure<T>(code, message);

    private sealed class RepositoryDataException : Exception;
}
