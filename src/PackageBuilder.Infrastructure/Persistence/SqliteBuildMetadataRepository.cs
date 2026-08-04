using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Data.Sqlite;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Infrastructure.Persistence;

/// <summary>
/// Implements the typed job, artifact, finding, and tool metadata repositories over the current
/// SQLite schema. Every write is parameterized, expected-state transitions are atomic, and
/// diagnostics omit physical paths, SQL, and stored content.
/// </summary>
public sealed class SqliteBuildMetadataRepository :
    IBuildJobRepository,
    IBuildArtifactRepository,
    IValidationFindingRepository,
    IToolInstallationRepository
{
    private const int MaximumIdentityLength = 1024;
    private const int MaximumTextLength = 4096;
    private readonly string _databasePath;

    private SqliteBuildMetadataRepository(string databasePath) => _databasePath = databasePath;

    /// <summary>Creates a repository only for an existing, current, contained SQLite database.</summary>
    public static RepositoryOperationResult<SqliteBuildMetadataRepository> Create(
        string projectRoot,
        string databasePath)
    {
        if (!TryValidateDatabasePath(projectRoot, databasePath, out string? normalizedPath))
        {
            return Failure<SqliteBuildMetadataRepository>(
                "REPOSITORY_PATH_INVALID",
                "The repository database path must be existing, absolute, and contained.");
        }

        try
        {
            using SqliteConnection connection = Open(normalizedPath!);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            int version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (version != SqliteSchema.CurrentVersion)
            {
                return Failure<SqliteBuildMetadataRepository>(
                    "REPOSITORY_SCHEMA_UNSUPPORTED",
                    "The repository database schema is not supported by this version.");
            }

            command.CommandText = "PRAGMA quick_check;";
            return StringComparer.Ordinal.Equals(command.ExecuteScalar() as string, "ok") &&
                HasRequiredTables(connection)
                ? RepositoryOperationResult.Success(
                    new SqliteBuildMetadataRepository(normalizedPath!))
                : Failure<SqliteBuildMetadataRepository>(
                    "REPOSITORY_SCHEMA_INVALID",
                    "The repository database schema is incomplete or invalid.");
        }
        catch (SqliteException)
        {
            return StorageFailure<SqliteBuildMetadataRepository>();
        }
    }

    public Task<RepositoryOperationResult> CreateAsync(
        PersistedBuildJob job,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            async () =>
            {
                RepositoryOperationResult? validation = ValidateNewJob(job);
                if (validation is not null)
                {
                    return validation;
                }

                await using SqliteConnection connection = await OpenAsync(cancellationToken);
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO BuildJobs
                        (BuildJobId, ProductVersionId, State, CreatedAtUtc, UpdatedAtUtc, FailureCode)
                    VALUES ($id, $productVersionId, $state, $createdAt, $updatedAt, NULL);
                    """;
                Add(command, "$id", job.Id.Value);
                Add(command, "$productVersionId", job.ProductVersionId);
                Add(command, "$state", job.State.CanonicalIdentifier);
                Add(command, "$createdAt", FormatTimestamp(job.CreatedAtUtc));
                Add(command, "$updatedAt", FormatTimestamp(job.UpdatedAtUtc));
                _ = await command.ExecuteNonQueryAsync(cancellationToken);
                return RepositoryOperationResult.Success();
            },
            "REPOSITORY_JOB_CONFLICT");

    public Task<RepositoryOperationResult<PersistedBuildJob?>> GetAsync(
        BuildJobId jobId,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            async () =>
            {
                if (jobId is null)
                {
                    return Invalid<PersistedBuildJob?>("A job identifier is required.");
                }

                await using SqliteConnection connection = await OpenAsync(cancellationToken);
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    SELECT BuildJobId, ProductVersionId, State, CreatedAtUtc, UpdatedAtUtc, FailureCode
                    FROM BuildJobs WHERE BuildJobId = $id;
                    """;
                Add(command, "$id", jobId.Value);
                await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
                PersistedBuildJob? result = await reader.ReadAsync(cancellationToken)
                    ? ReadJob(reader)
                    : null;
                return RepositoryOperationResult.Success<PersistedBuildJob?>(result);
            });

    public Task<RepositoryOperationResult<IReadOnlyList<PersistedBuildJob>>> QueryAsync(
        BuildJobState? state = null,
        CancellationToken cancellationToken = default) =>
        QueryJobsAsync(state, resumableOnly: false, cancellationToken);

    public Task<RepositoryOperationResult<IReadOnlyList<PersistedBuildJob>>> GetResumableAsync(
        CancellationToken cancellationToken = default) =>
        QueryJobsAsync(state: null, resumableOnly: true, cancellationToken);

    public Task<RepositoryOperationResult<PersistedBuildJob>> TransitionAsync(
        BuildJobId jobId,
        BuildJobState expectedState,
        BuildJobState targetState,
        DateTimeOffset occurredAtUtc,
        string? failureCode = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            async () =>
            {
                RepositoryOperationResult? validation = ValidateTransition(
                    jobId,
                    expectedState,
                    targetState,
                    occurredAtUtc,
                    failureCode);
                if (validation is not null)
                {
                    return RepositoryOperationResult.Failure<PersistedBuildJob>(
                        validation.Error!.Code,
                        validation.Error.Message);
                }

                await using SqliteConnection connection = await OpenAsync(cancellationToken);
                await using var transaction =
                    (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
                PersistedBuildJob? current = await ReadJobAsync(
                    connection,
                    transaction,
                    jobId,
                    cancellationToken);
                if (current is null)
                {
                    return NotFound<PersistedBuildJob>("The requested job does not exist.");
                }

                if (!current.State.Equals(expectedState))
                {
                    return RepositoryOperationResult.Failure<PersistedBuildJob>(
                        "REPOSITORY_STATE_CONFLICT",
                        "The job state changed before the requested transition could be applied.");
                }

                if (occurredAtUtc < current.UpdatedAtUtc)
                {
                    return Invalid<PersistedBuildJob>(
                        "The transition timestamp cannot precede the persisted job timestamp.");
                }

                await using SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    UPDATE BuildJobs
                    SET State = $target, UpdatedAtUtc = $updatedAt, FailureCode = $failureCode
                    WHERE BuildJobId = $id AND State = $expected AND UpdatedAtUtc = $previousUpdatedAt;
                    """;
                Add(command, "$target", targetState.CanonicalIdentifier);
                Add(command, "$updatedAt", FormatTimestamp(occurredAtUtc));
                Add(command, "$failureCode", failureCode);
                Add(command, "$id", jobId.Value);
                Add(command, "$expected", expectedState.CanonicalIdentifier);
                Add(command, "$previousUpdatedAt", FormatTimestamp(current.UpdatedAtUtc));
                int changed = await command.ExecuteNonQueryAsync(cancellationToken);
                if (changed != 1)
                {
                    return RepositoryOperationResult.Failure<PersistedBuildJob>(
                        "REPOSITORY_STATE_CONFLICT",
                        "The job state changed before the requested transition could be applied.");
                }

                await transaction.CommitAsync(cancellationToken);
                return RepositoryOperationResult.Success(
                    current with
                    {
                        State = targetState,
                        UpdatedAtUtc = occurredAtUtc,
                        FailureCode = failureCode,
                    });
            });

    public Task<RepositoryOperationResult> AddAsync(
        PersistedBuildArtifact artifact,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            async () =>
            {
                RepositoryOperationResult? validation = ValidateArtifact(artifact);
                if (validation is not null)
                {
                    return validation;
                }

                await using SqliteConnection connection = await OpenAsync(cancellationToken);
                if (artifact.StepId is not null &&
                    !await StepBelongsToJobAsync(
                        connection,
                        artifact.StepId,
                        artifact.JobId,
                        cancellationToken))
                {
                    return Invalid("The artifact step must belong to the same build job.");
                }

                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO Artifacts
                        (ArtifactId, BuildJobId, BuildStepId, Role, Target, LogicalReference,
                         Sha256, ByteCount, LifecycleState, CreatedAtUtc)
                    VALUES
                        ($id, $jobId, $stepId, $role, $target, $reference,
                         $sha256, $byteCount, $lifecycle, $createdAt);
                    """;
                Add(command, "$id", artifact.Id.Value);
                Add(command, "$jobId", artifact.JobId.Value);
                Add(command, "$stepId", artifact.StepId?.Value);
                Add(command, "$role", artifact.Role.CanonicalIdentifier);
                Add(command, "$target", artifact.Target?.CanonicalIdentifier);
                Add(command, "$reference", artifact.LogicalReference);
                Add(command, "$sha256", artifact.Sha256);
                Add(command, "$byteCount", artifact.ByteCount);
                Add(command, "$lifecycle", artifact.LifecycleState.CanonicalIdentifier);
                Add(command, "$createdAt", FormatTimestamp(artifact.CreatedAtUtc));
                _ = await command.ExecuteNonQueryAsync(cancellationToken);
                return RepositoryOperationResult.Success();
            },
            "REPOSITORY_ARTIFACT_CONFLICT");

    Task<RepositoryOperationResult<IReadOnlyList<PersistedBuildArtifact>>>
        IBuildArtifactRepository.GetByJobAsync(
            BuildJobId jobId,
            CancellationToken cancellationToken) =>
        RunAsync(
            async () =>
            {
                if (jobId is null)
                {
                    return Invalid<IReadOnlyList<PersistedBuildArtifact>>(
                        "A job identifier is required.");
                }

                await using SqliteConnection connection = await OpenAsync(cancellationToken);
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    SELECT ArtifactId, BuildJobId, BuildStepId, Role, Target, LogicalReference,
                           Sha256, ByteCount, LifecycleState, CreatedAtUtc
                    FROM Artifacts WHERE BuildJobId = $jobId
                    ORDER BY CreatedAtUtc, ArtifactId;
                    """;
                Add(command, "$jobId", jobId.Value);
                await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
                List<PersistedBuildArtifact> values = [];
                while (await reader.ReadAsync(cancellationToken))
                {
                    values.Add(ReadArtifact(reader));
                }

                return RepositoryOperationResult.Success<IReadOnlyList<PersistedBuildArtifact>>(
                    new ReadOnlyCollection<PersistedBuildArtifact>(values));
            });

    public Task<RepositoryOperationResult> AddAsync(
        PersistedValidationFinding finding,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            async () =>
            {
                RepositoryOperationResult? validation = ValidateFinding(finding);
                if (validation is not null)
                {
                    return validation;
                }

                await using SqliteConnection connection = await OpenAsync(cancellationToken);
                if (finding.Finding.RelatedArtifactId is not null &&
                    !await ArtifactBelongsToJobAsync(
                        connection,
                        finding.Finding.RelatedArtifactId,
                        finding.JobId,
                        cancellationToken))
                {
                    return Invalid("The related artifact must belong to the same build job.");
                }

                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO ValidationFindings
                        (FindingId, BuildJobId, ArtifactId, Code, Severity, Explanation, Source,
                         SuggestedAction, BlocksRelease, CreatedAtUtc)
                    VALUES
                        ($id, $jobId, $artifactId, $code, $severity, $explanation, $source,
                         $action, $blocksRelease, $createdAt);
                    """;
                Add(command, "$id", finding.Id);
                Add(command, "$jobId", finding.JobId.Value);
                Add(command, "$artifactId", finding.Finding.RelatedArtifactId?.Value);
                Add(command, "$code", finding.Finding.Code.Value);
                Add(command, "$severity", finding.Finding.Severity.SerializedToken);
                Add(command, "$explanation", finding.Finding.Explanation.Value);
                Add(command, "$source", finding.Finding.Source.Value);
                Add(command, "$action", finding.Finding.SuggestedAction?.Value);
                Add(command, "$blocksRelease", finding.Finding.BlocksRelease ? 1 : 0);
                Add(command, "$createdAt", FormatTimestamp(finding.CreatedAtUtc));
                _ = await command.ExecuteNonQueryAsync(cancellationToken);
                return RepositoryOperationResult.Success();
            },
            "REPOSITORY_FINDING_CONFLICT");

    Task<RepositoryOperationResult<IReadOnlyList<PersistedValidationFinding>>>
        IValidationFindingRepository.GetByJobAsync(
            BuildJobId jobId,
            CancellationToken cancellationToken) =>
        RunAsync(
            async () =>
            {
                if (jobId is null)
                {
                    return Invalid<IReadOnlyList<PersistedValidationFinding>>(
                        "A job identifier is required.");
                }

                await using SqliteConnection connection = await OpenAsync(cancellationToken);
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    SELECT FindingId, BuildJobId, ArtifactId, Code, Severity, Explanation, Source,
                           SuggestedAction, BlocksRelease, CreatedAtUtc
                    FROM ValidationFindings WHERE BuildJobId = $jobId
                    ORDER BY CreatedAtUtc, FindingId;
                    """;
                Add(command, "$jobId", jobId.Value);
                await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
                List<PersistedValidationFinding> values = [];
                while (await reader.ReadAsync(cancellationToken))
                {
                    values.Add(ReadFinding(reader));
                }

                return RepositoryOperationResult.Success<IReadOnlyList<PersistedValidationFinding>>(
                    new ReadOnlyCollection<PersistedValidationFinding>(values));
            });

    public Task<RepositoryOperationResult> UpsertAsync(
        PersistedToolInstallation installation,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            async () =>
            {
                RepositoryOperationResult? validation = ValidateTool(installation);
                if (validation is not null)
                {
                    return validation;
                }

                await using SqliteConnection connection = await OpenAsync(cancellationToken);
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO ToolInstallations
                        (ToolInstallationId, ToolName, Version, ExecutableReference, Sha256,
                         IsApproved, DiscoveredAtUtc)
                    VALUES ($id, $name, $version, $reference, $sha256, $approved, $discoveredAt)
                    ON CONFLICT(ToolInstallationId) DO UPDATE SET
                        ToolName = excluded.ToolName,
                        Version = excluded.Version,
                        ExecutableReference = excluded.ExecutableReference,
                        Sha256 = excluded.Sha256,
                        IsApproved = excluded.IsApproved,
                        DiscoveredAtUtc = excluded.DiscoveredAtUtc;
                    """;
                Add(command, "$id", installation.Id);
                Add(command, "$name", installation.ToolName);
                Add(command, "$version", installation.Version);
                Add(command, "$reference", installation.ExecutableReference);
                Add(command, "$sha256", installation.Sha256);
                Add(command, "$approved", installation.IsApproved ? 1 : 0);
                Add(command, "$discoveredAt", FormatTimestamp(installation.DiscoveredAtUtc));
                _ = await command.ExecuteNonQueryAsync(cancellationToken);
                return RepositoryOperationResult.Success();
            },
            "REPOSITORY_TOOL_CONFLICT");

    public Task<RepositoryOperationResult<IReadOnlyList<PersistedToolInstallation>>> QueryAsync(
        bool? approved = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            async () =>
            {
                await using SqliteConnection connection = await OpenAsync(cancellationToken);
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = approved.HasValue
                    ? """
                        SELECT ToolInstallationId, ToolName, Version, ExecutableReference, Sha256,
                               IsApproved, DiscoveredAtUtc
                        FROM ToolInstallations WHERE IsApproved = $approved
                        ORDER BY ToolName, Version, ExecutableReference;
                        """
                    : """
                        SELECT ToolInstallationId, ToolName, Version, ExecutableReference, Sha256,
                               IsApproved, DiscoveredAtUtc
                        FROM ToolInstallations
                        ORDER BY ToolName, Version, ExecutableReference;
                        """;
                if (approved.HasValue)
                {
                    Add(command, "$approved", approved.Value ? 1 : 0);
                }

                await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
                List<PersistedToolInstallation> values = [];
                while (await reader.ReadAsync(cancellationToken))
                {
                    values.Add(ReadTool(reader));
                }

                return RepositoryOperationResult.Success<IReadOnlyList<PersistedToolInstallation>>(
                    new ReadOnlyCollection<PersistedToolInstallation>(values));
            });

    private Task<RepositoryOperationResult<IReadOnlyList<PersistedBuildJob>>> QueryJobsAsync(
        BuildJobState? state,
        bool resumableOnly,
        CancellationToken cancellationToken) =>
        RunAsync(
            async () =>
            {
                await using SqliteConnection connection = await OpenAsync(cancellationToken);
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = resumableOnly
                    ? """
                        SELECT BuildJobId, ProductVersionId, State, CreatedAtUtc, UpdatedAtUtc, FailureCode
                        FROM BuildJobs
                        WHERE State NOT IN ('completed', 'failed', 'cancelled')
                        ORDER BY CreatedAtUtc, BuildJobId;
                        """
                    : state is null
                    ? """
                        SELECT BuildJobId, ProductVersionId, State, CreatedAtUtc, UpdatedAtUtc, FailureCode
                        FROM BuildJobs ORDER BY CreatedAtUtc, BuildJobId;
                        """
                    : """
                        SELECT BuildJobId, ProductVersionId, State, CreatedAtUtc, UpdatedAtUtc, FailureCode
                        FROM BuildJobs WHERE State = $state ORDER BY CreatedAtUtc, BuildJobId;
                        """;
                if (!resumableOnly && state is not null)
                {
                    Add(command, "$state", state.CanonicalIdentifier);
                }

                await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
                List<PersistedBuildJob> values = [];
                while (await reader.ReadAsync(cancellationToken))
                {
                    values.Add(ReadJob(reader));
                }

                return RepositoryOperationResult.Success<IReadOnlyList<PersistedBuildJob>>(
                    new ReadOnlyCollection<PersistedBuildJob>(values));
            });

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new(ConnectionString(_databasePath));
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 30000;";
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static SqliteConnection Open(string databasePath)
    {
        SqliteConnection connection = new(ConnectionString(databasePath));
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 30000;";
        _ = command.ExecuteNonQuery();
        return connection;
    }

    private static bool HasRequiredTables(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        List<string> parameterNames = [];
        for (int index = 0; index < SqliteSchema.VersionOneTables.Count; index++)
        {
            string parameterName = $"$table{index}";
            parameterNames.Add(parameterName);
            Add(command, parameterName, SqliteSchema.VersionOneTables[index]);
        }

        command.CommandText = $"""
            SELECT COUNT(*) FROM sqlite_schema
            WHERE type = 'table' AND name IN ({string.Join(',', parameterNames)});
            """;
        long count = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        return count == SqliteSchema.VersionOneTables.Count;
    }

    private static string ConnectionString(string databasePath) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 30,
        }.ConnectionString;

    private static async Task<PersistedBuildJob?> ReadJobAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        BuildJobId jobId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT BuildJobId, ProductVersionId, State, CreatedAtUtc, UpdatedAtUtc, FailureCode
            FROM BuildJobs WHERE BuildJobId = $id;
            """;
        Add(command, "$id", jobId.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadJob(reader) : null;
    }

    private static PersistedBuildJob ReadJob(SqliteDataReader reader)
    {
        BuildJobId id = BuildJobId.Create(reader.GetString(0)).Value
            ?? throw new RepositoryDataException();
        BuildJobState state = BuildJobState.TryParse(reader.GetString(2)).Value
            ?? throw new RepositoryDataException();
        return new PersistedBuildJob(
            id,
            reader.IsDBNull(1) ? null : reader.GetString(1),
            state,
            ParseTimestamp(reader.GetString(3)),
            ParseTimestamp(reader.GetString(4)),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }

    private static PersistedBuildArtifact ReadArtifact(SqliteDataReader reader)
    {
        BuildArtifactId id = BuildArtifactId.Create(reader.GetString(0)).Value
            ?? throw new RepositoryDataException();
        BuildJobId jobId = BuildJobId.Create(reader.GetString(1)).Value
            ?? throw new RepositoryDataException();
        BuildStepId? stepId = reader.IsDBNull(2)
            ? null
            : BuildStepId.Create(reader.GetString(2)).Value ?? throw new RepositoryDataException();
        BuildArtifactRole role = BuildArtifactRole.Create(reader.GetString(3)).Value
            ?? throw new RepositoryDataException();
        BuildTarget? target = reader.IsDBNull(4)
            ? null
            : BuildTarget.TryParse(reader.GetString(4)).Value ?? throw new RepositoryDataException();
        BuildArtifactLifecycleState lifecycle =
            BuildArtifactLifecycleState.TryParse(reader.GetString(8)).Value
            ?? throw new RepositoryDataException();
        return new PersistedBuildArtifact(
            id,
            jobId,
            stepId,
            role,
            target,
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetInt64(7),
            lifecycle,
            ParseTimestamp(reader.GetString(9)));
    }

    private static PersistedValidationFinding ReadFinding(SqliteDataReader reader)
    {
        BuildJobId jobId = BuildJobId.Create(reader.GetString(1)).Value
            ?? throw new RepositoryDataException();
        BuildArtifactId? artifactId = reader.IsDBNull(2)
            ? null
            : BuildArtifactId.Create(reader.GetString(2)).Value
                ?? throw new RepositoryDataException();
        FindingCode code = FindingCode.Create(reader.GetString(3)).Value
            ?? throw new RepositoryDataException();
        FindingSeverity severity = FindingSeverity.ParseToken(reader.GetString(4)).Value
            ?? throw new RepositoryDataException();
        FindingExplanation explanation = FindingExplanation.Create(reader.GetString(5)).Value
            ?? throw new RepositoryDataException();
        FindingSourceComponent source = FindingSourceComponent.Create(reader.GetString(6)).Value
            ?? throw new RepositoryDataException();
        CorrectiveAction? action = reader.IsDBNull(7)
            ? null
            : CorrectiveAction.Create(reader.GetString(7)).Value
                ?? throw new RepositoryDataException();
        ValidationFinding finding = ValidationFinding.Create(
            code,
            severity,
            explanation,
            source,
            artifactId,
            action,
            reader.GetInt64(8) == 1).Value ?? throw new RepositoryDataException();
        return new PersistedValidationFinding(
            reader.GetString(0),
            jobId,
            finding,
            ParseTimestamp(reader.GetString(9)));
    }

    private static PersistedToolInstallation ReadTool(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetInt64(5) == 1,
            ParseTimestamp(reader.GetString(6)));

    private static RepositoryOperationResult? ValidateNewJob(PersistedBuildJob? job) =>
        job is null || job.Id is null || job.State is null
            ? Invalid("Complete job metadata is required.")
            : !job.State.Equals(BuildJobState.Queued)
            ? Invalid("A new job must begin in the queued state.")
            : !IsUtc(job.CreatedAtUtc) || !IsUtc(job.UpdatedAtUtc) ||
              job.UpdatedAtUtc < job.CreatedAtUtc
            ? Invalid("Job timestamps must be ordered UTC values.")
            : job.FailureCode is not null
            ? Invalid("A new job cannot contain a failure code.")
            : job.ProductVersionId is not null && !IsIdentity(job.ProductVersionId)
            ? Invalid("The product-version identifier is invalid.")
            : null;

    private static RepositoryOperationResult? ValidateTransition(
        BuildJobId? jobId,
        BuildJobState? expected,
        BuildJobState? target,
        DateTimeOffset occurredAtUtc,
        string? failureCode) =>
        jobId is null || expected is null || target is null
            ? Invalid("Job and state values are required.")
            : !IsUtc(occurredAtUtc)
            ? Invalid("The transition timestamp must be UTC.")
            : !BuildJobTransitionPolicy.IsApproved(expected, target)
            ? Invalid("The requested job-state transition is not approved.")
            : target.Equals(BuildJobState.Failed) &&
              (failureCode is null || !FindingCode.Create(failureCode).IsValid)
            ? Invalid("A failed job requires a valid stable failure code.")
            : !target.Equals(BuildJobState.Failed) && failureCode is not null
            ? Invalid("A failure code is allowed only for a failed job.")
            : null;

    private static RepositoryOperationResult? ValidateArtifact(PersistedBuildArtifact? artifact) =>
        artifact is null || artifact.Id is null || artifact.JobId is null || artifact.Role is null ||
        artifact.LifecycleState is null
            ? Invalid("Complete artifact metadata is required.")
            : !IsLogicalReference(artifact.LogicalReference)
            ? Invalid("The artifact logical reference is invalid.")
            : artifact.Sha256 is not null && !IsSha256(artifact.Sha256)
            ? Invalid("The artifact SHA-256 value is invalid.")
            : artifact.ByteCount < 0
            ? Invalid("The artifact byte count cannot be negative.")
            : !IsUtc(artifact.CreatedAtUtc)
            ? Invalid("The artifact timestamp must be UTC.")
            : null;

    private static RepositoryOperationResult? ValidateFinding(PersistedValidationFinding? finding) =>
        finding is null || finding.JobId is null || finding.Finding is null ||
        !IsIdentity(finding.Id)
            ? Invalid("Complete finding metadata is required.")
            : !IsUtc(finding.CreatedAtUtc)
            ? Invalid("The finding timestamp must be UTC.")
            : null;

    private static RepositoryOperationResult? ValidateTool(PersistedToolInstallation? tool) =>
        tool is null || !IsIdentity(tool.Id) || !IsText(tool.ToolName) || !IsText(tool.Version)
            ? Invalid("Complete tool metadata is required.")
            : !IsLogicalReference(tool.ExecutableReference)
            ? Invalid("The tool executable reference is invalid.")
            : tool.Sha256 is not null && !IsSha256(tool.Sha256)
            ? Invalid("The tool SHA-256 value is invalid.")
            : !IsUtc(tool.DiscoveredAtUtc)
            ? Invalid("The tool discovery timestamp must be UTC.")
            : null;

    private static async Task<bool> StepBelongsToJobAsync(
        SqliteConnection connection,
        BuildStepId stepId,
        BuildJobId jobId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM BuildSteps WHERE BuildStepId = $stepId AND BuildJobId = $jobId;";
        Add(command, "$stepId", stepId.Value);
        Add(command, "$jobId", jobId.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<bool> ArtifactBelongsToJobAsync(
        SqliteConnection connection,
        BuildArtifactId artifactId,
        BuildJobId jobId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM Artifacts WHERE ArtifactId = $artifactId AND BuildJobId = $jobId;";
        Add(command, "$artifactId", artifactId.Value);
        Add(command, "$jobId", jobId.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    private static void Add(SqliteCommand command, string name, object? value) =>
        _ = command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return !DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset result) ||
            !IsUtc(result)
            ? throw new RepositoryDataException()
            : result;
    }

    private static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;

    private static bool IsIdentity(string? value) =>
        IsText(value, MaximumIdentityLength) && !value!.Any(char.IsControl);

    private static bool IsText(string? value, int maximumLength = MaximumTextLength) =>
        value is not null && value.Length is > 0 && value.Length <= maximumLength &&
        !string.IsNullOrWhiteSpace(value) && !char.IsWhiteSpace(value[0]) &&
        !char.IsWhiteSpace(value[^1]) && !value.Any(char.IsControl);

    private static bool IsLogicalReference(string? value)
    {
        if (!IsText(value) || value![0] == '/' || value.Contains('\\', StringComparison.Ordinal) ||
            value.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = value.Split('/');
        return segments.All(
            segment => segment.Length > 0 && segment is not "." and not ".." &&
                !char.IsWhiteSpace(segment[0]) && !char.IsWhiteSpace(segment[^1]));
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool TryValidateDatabasePath(
        string? projectRoot,
        string? databasePath,
        out string? normalizedPath)
    {
        normalizedPath = null;
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(databasePath) ||
            projectRoot.Contains('\0', StringComparison.Ordinal) ||
            databasePath.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            if (!Path.IsPathFullyQualified(projectRoot) || !Path.IsPathFullyQualified(databasePath))
            {
                return false;
            }

            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
            string candidate = Path.GetFullPath(databasePath);
            if (!Directory.Exists(root) || !File.Exists(candidate) ||
                !candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string current = root;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            foreach (string segment in Path.GetRelativePath(root, candidate).Split(
                         Path.DirectorySeparatorChar,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if (File.Exists(current) || Directory.Exists(current))
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        return false;
                    }
                }
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
        string conflictCode = "REPOSITORY_CONSTRAINT_VIOLATION")
    {
        try
        {
            return await operation();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            return RepositoryOperationResult.Failure(
                conflictCode,
                "The repository rejected conflicting or missing related metadata.");
        }
        catch (SqliteException)
        {
            return StorageFailure();
        }
        catch (RepositoryDataException)
        {
            return RepositoryOperationResult.Failure(
                "REPOSITORY_DATA_INVALID",
                "Stored repository metadata is invalid or incompatible.");
        }
    }

    private static async Task<RepositoryOperationResult<T>> RunAsync<T>(
        Func<Task<RepositoryOperationResult<T>>> operation)
    {
        try
        {
            return await operation();
        }
        catch (SqliteException)
        {
            return StorageFailure<T>();
        }
        catch (RepositoryDataException)
        {
            return RepositoryOperationResult.Failure<T>(
                "REPOSITORY_DATA_INVALID",
                "Stored repository metadata is invalid or incompatible.");
        }
    }

    private static RepositoryOperationResult Invalid(string message) =>
        RepositoryOperationResult.Failure("REPOSITORY_INPUT_INVALID", message);

    private static RepositoryOperationResult<T> Invalid<T>(string message) =>
        RepositoryOperationResult.Failure<T>("REPOSITORY_INPUT_INVALID", message);

    private static RepositoryOperationResult<T> NotFound<T>(string message) =>
        RepositoryOperationResult.Failure<T>("REPOSITORY_NOT_FOUND", message);

    private static RepositoryOperationResult StorageFailure() =>
        RepositoryOperationResult.Failure(
            "REPOSITORY_STORAGE_FAILED",
            "The metadata repository could not complete the operation safely.");

    private static RepositoryOperationResult<T> StorageFailure<T>() =>
        RepositoryOperationResult.Failure<T>(
            "REPOSITORY_STORAGE_FAILED",
            "The metadata repository could not complete the operation safely.");

    private static RepositoryOperationResult<T> Failure<T>(string code, string message) =>
        RepositoryOperationResult.Failure<T>(code, message);

    private sealed class RepositoryDataException : Exception;
}
