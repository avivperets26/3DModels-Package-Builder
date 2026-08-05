using Microsoft.Data.Sqlite;
using PackageBuilder.Infrastructure.Persistence;

namespace PackageBuilder.Infrastructure.Tests.Persistence;

[Collection(SqliteMigrationFixture.Name)]
public sealed class SqliteDatabaseMigratorTests
{
    private readonly SqliteDatabaseMigrator _migrator = new();

    [Fact]
    public void NewDatabaseCreatesEveryApprovedTableTransactionally()
    {
        using SqliteMigrationTestWorkspace workspace = new();

        SqliteMigrationResult result = Migrate(workspace);

        Assert.True(result.IsSuccess);
        Assert.Equal(SqliteMigrationOutcome.Created, result.Outcome);
        Assert.Equal(0, result.PreviousVersion);
        Assert.Equal(SqliteSchema.CurrentVersion, result.CurrentVersion);
        Assert.Null(result.BackupReference);
        Assert.Equal(SqliteSchema.CurrentVersion, workspace.UserVersion());
        Assert.Equal(SqliteSchema.CurrentTables.Order(), workspace.Tables().Order());
    }

    [Fact]
    public void ExistingVersionZeroDatabaseIsBackedUpBeforeUpgrade()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        workspace.Execute("CREATE TABLE LegacySentinel (Value TEXT NOT NULL); INSERT INTO LegacySentinel VALUES ('preserved');");

        SqliteMigrationResult result = Migrate(workspace);

        Assert.True(result.IsSuccess);
        Assert.Equal(SqliteMigrationOutcome.Upgraded, result.Outcome);
        Assert.NotNull(result.BackupReference);
        Assert.False(result.BackupReference!.Contains('\\', StringComparison.Ordinal));
        string backupPath = Path.Combine(workspace.Root, result.BackupReference!.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(backupPath));
        Assert.Equal(0, workspace.UserVersion(backupPath));
        Assert.Equal(["LegacySentinel"], workspace.Tables(backupPath));

        using SqliteConnection backup = Open(backupPath);
        using SqliteCommand command = backup.CreateCommand();
        command.CommandText = "SELECT Value FROM LegacySentinel;";
        Assert.Equal("preserved", command.ExecuteScalar());
    }

    [Fact]
    public void VersionOneDatabaseUpgradesWithApprovalHistoryTablesAndBackup()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        Assert.True(Migrate(workspace).IsSuccess);
        DowngradeToVersionOne(workspace);
        workspace.Execute("""
            INSERT INTO EngineVersions
                (EngineVersionId, EngineKind, Version, Status, LastValidatedAtUtc)
            VALUES ('unity-6000.3.2f1', 'unity', '6000.3.2f1', 'discovered',
                    '2026-08-05T12:00:00.0000000+00:00');
            """);

        SqliteMigrationResult result = Migrate(workspace);

        Assert.True(result.IsSuccess);
        Assert.Equal(SqliteMigrationOutcome.Upgraded, result.Outcome);
        Assert.Equal(1, result.PreviousVersion);
        Assert.Equal(2, result.CurrentVersion);
        Assert.Equal(SqliteSchema.CurrentTables.Order(), workspace.Tables().Order());
        Assert.NotNull(result.BackupReference);
        string backupPath = Path.Combine(
            workspace.Root,
            result.BackupReference!.Replace('/', Path.DirectorySeparatorChar));
        Assert.Equal(1, workspace.UserVersion(backupPath));
        Assert.Equal(SqliteSchema.VersionOneTables.Order(), workspace.Tables(backupPath).Order());

        using SqliteConnection connection = workspace.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Status, Revision FROM EngineVersions WHERE EngineVersionId = 'unity-6000.3.2f1';";
        using SqliteDataReader reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("discovered", reader.GetString(0));
        Assert.Equal(0L, reader.GetInt64(1));
        reader.Close();
        command.CommandText = "SELECT ToStatus FROM EngineVersionTransitions WHERE EngineVersionId = 'unity-6000.3.2f1';";
        Assert.Equal("discovered", command.ExecuteScalar());
    }

    [Fact]
    public void MigrationFailureRollsBackEverySchemaChangeAndPreservesBackup()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        workspace.Execute("CREATE TABLE ValidationFindings (Conflict TEXT NOT NULL); INSERT INTO ValidationFindings VALUES ('original');");

        SqliteMigrationResult result = Migrate(workspace);

        Assert.False(result.IsSuccess);
        Assert.Equal("SQLITE_MIGRATION_FAILED", result.Error!.Code);
        Assert.Equal(0, workspace.UserVersion());
        Assert.Equal(["ValidationFindings"], workspace.Tables());
        string backupPath = Assert.Single(Directory.GetFiles(workspace.BackupDirectory, "*.bak"));
        Assert.Equal(0, workspace.UserVersion(backupPath));
        Assert.Equal(["ValidationFindings"], workspace.Tables(backupPath));
    }

    [Fact]
    public void CurrentDatabaseIsIdempotentAndCreatesNoBackup()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        Assert.True(Migrate(workspace).IsSuccess);

        SqliteMigrationResult result = Migrate(workspace);

        Assert.True(result.IsSuccess);
        Assert.Equal(SqliteMigrationOutcome.Current, result.Outcome);
        Assert.Equal(SqliteSchema.CurrentVersion, result.PreviousVersion);
        Assert.Empty(Directory.GetFiles(workspace.BackupDirectory));
    }

    [Fact]
    public void NewerDatabaseIsRejectedWithoutModificationOrBackup()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        workspace.Execute("PRAGMA user_version = 3; CREATE TABLE FutureData (Value TEXT NOT NULL);");

        SqliteMigrationResult result = Migrate(workspace);

        Assert.False(result.IsSuccess);
        Assert.Equal("SQLITE_VERSION_NEWER", result.Error!.Code);
        Assert.Equal(3, workspace.UserVersion());
        Assert.Equal(["FutureData"], workspace.Tables());
        Assert.Empty(Directory.GetFiles(workspace.BackupDirectory));
    }

    [Fact]
    public void CurrentVersionWithIncompleteTableInventoryFailsClosed()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        workspace.Execute("PRAGMA user_version = 2; CREATE TABLE Products (ProductId TEXT PRIMARY KEY);");

        SqliteMigrationResult result = Migrate(workspace);

        Assert.False(result.IsSuccess);
        Assert.Equal("SQLITE_SCHEMA_INVALID", result.Error!.Code);
        Assert.Equal(2, workspace.UserVersion());
        Assert.Equal(["Products"], workspace.Tables());
        Assert.Empty(Directory.GetFiles(workspace.BackupDirectory));
    }

    [Fact]
    public void SchemaEnforcesForeignKeysAndCanonicalStateValues()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        Assert.True(Migrate(workspace).IsSuccess);
        using SqliteConnection connection = workspace.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; INSERT INTO BuildSteps (BuildStepId, BuildJobId, StepOrder, Stage, Status) VALUES ('step', 'missing', 0, 'preflight', 'invalid');";

        SqliteException exception = Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());

        Assert.Equal(19, exception.SqliteErrorCode);
    }

    [Fact]
    public void SchemaContainsExpectedIndexes()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        Assert.True(Migrate(workspace).IsSuccess);
        using SqliteConnection connection = workspace.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'index' AND name LIKE 'IX_%';";

        Assert.Equal(10L, command.ExecuteScalar());
    }

    [Fact]
    public void SchemaRejectsNonCanonicalSha256Metadata()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        Assert.True(Migrate(workspace).IsSuccess);
        using SqliteConnection connection = workspace.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Products
                (ProductId, DisplayName, AssetId, FolderName, ProductCase, CreatedAtUtc)
            VALUES ('product', 'Product', 'asset', 'folder', 'static', '2026-08-04T00:00:00Z');
            INSERT INTO ProductVersions
                (ProductVersionId, ProductId, Version, ManifestReference, ManifestSha256, CreatedAtUtc)
            VALUES ('version', 'product', '1.0.0', 'manifest.json',
                'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
                '2026-08-04T00:00:00Z');
            """;

        SqliteException exception = Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());

        Assert.Equal(19, exception.SqliteErrorCode);
    }

    [Theory]
    [InlineData("database-outside")]
    [InlineData("backup-outside")]
    [InlineData("relative-root")]
    public void UncontainedOrRelativePathsAreRejected(string scenario)
    {
        using SqliteMigrationTestWorkspace workspace = new();
        string root = workspace.Root;
        string database = workspace.DatabasePath;
        string backup = workspace.BackupDirectory;
        if (scenario == "database-outside")
        {
            database = Path.Combine(Path.GetDirectoryName(root)!, "outside.db");
        }
        else if (scenario == "backup-outside")
        {
            backup = Path.GetDirectoryName(root)!;
        }
        else
        {
            root = ".";
        }

        SqliteMigrationResult result = _migrator.Migrate(
            root,
            database,
            backup,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("SQLITE_PATH_INVALID", result.Error!.Code);
    }

    [Fact]
    public void MissingContainedDirectoriesAreRejectedWithoutCreatingThem()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        string missing = Path.Combine(workspace.Root, "missing", "packagebuilder.db");

        SqliteMigrationResult result = _migrator.Migrate(
            workspace.Root,
            missing,
            workspace.BackupDirectory,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("SQLITE_PATH_INVALID", result.Error!.Code);
        Assert.False(Directory.Exists(Path.GetDirectoryName(missing)));
    }

    [Fact]
    public void ReparsePointInsideDatabasePathIsRejected()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        string physical = Path.Combine(workspace.Root, "physical");
        string link = Path.Combine(workspace.Root, "linked-runtime");
        _ = Directory.CreateDirectory(physical);
        _ = Directory.CreateSymbolicLink(link, physical);
        string backups = Path.Combine(workspace.Root, "safe-backups");
        _ = Directory.CreateDirectory(backups);

        SqliteMigrationResult result = _migrator.Migrate(
            workspace.Root,
            Path.Combine(link, "packagebuilder.db"),
            backups,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("SQLITE_PATH_REPARSE", result.Error!.Code);
    }

    [Fact]
    public void ReparsePointProjectRootIsRejected()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        string physical = Path.Combine(workspace.Root, "physical-root");
        string link = Path.Combine(workspace.Root, "linked-root");
        string runtime = Path.Combine(physical, "runtime-data");
        string backups = Path.Combine(runtime, "backups");
        _ = Directory.CreateDirectory(backups);
        _ = Directory.CreateSymbolicLink(link, physical);

        SqliteMigrationResult result = _migrator.Migrate(
            link,
            Path.Combine(link, "runtime-data", "packagebuilder.db"),
            Path.Combine(link, "runtime-data", "backups"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("SQLITE_PATH_REPARSE", result.Error!.Code);
    }

    [Fact]
    public void CancellationBeforeOpeningDoesNotCreateDatabase()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        using CancellationTokenSource source = new();
        source.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() =>
            _migrator.Migrate(workspace.Root, workspace.DatabasePath, workspace.BackupDirectory, source.Token));
        Assert.False(File.Exists(workspace.DatabasePath));
    }

    [Fact]
    public void BackupNamesNeverOverwriteExistingEvidence()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        workspace.Execute("CREATE TABLE LegacySentinel (Value TEXT NOT NULL);");
        string occupied = Path.Combine(workspace.BackupDirectory, "packagebuilder.pre-v0-to-v2.001.db.bak");
        File.WriteAllText(occupied, "existing evidence");

        SqliteMigrationResult result = Migrate(workspace);

        Assert.True(result.IsSuccess);
        Assert.EndsWith(".002.db.bak", result.BackupReference, StringComparison.Ordinal);
        Assert.Equal("existing evidence", File.ReadAllText(occupied));
    }

    [Fact]
    public void ExhaustedBackupNamesReturnSanitizedStorageFailure()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        workspace.Execute("CREATE TABLE LegacySentinel (Value TEXT NOT NULL);");
        for (int sequence = 1; sequence <= 10; sequence++)
        {
            File.WriteAllText(
                Path.Combine(workspace.BackupDirectory, $"packagebuilder.pre-v0-to-v2.{sequence:D3}.db.bak"),
                "occupied");
        }

        SqliteMigrationResult result = Migrate(workspace);

        Assert.False(result.IsSuccess);
        Assert.Equal("SQLITE_STORAGE_FAILED", result.Error!.Code);
        Assert.Equal(0, workspace.UserVersion());
    }

    [Fact]
    public void InvalidSqliteFileReturnsSanitizedMigrationFailure()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        File.WriteAllText(workspace.DatabasePath, "not a sqlite database");

        SqliteMigrationResult result = Migrate(workspace);

        Assert.False(result.IsSuccess);
        Assert.Equal("SQLITE_MIGRATION_FAILED", result.Error!.Code);
        Assert.DoesNotContain(workspace.Root, result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, "database", "backup")]
    [InlineData("", "database", "backup")]
    [InlineData("root", null, "backup")]
    [InlineData("root", "", "backup")]
    [InlineData("root", "database", null)]
    [InlineData("root", "database", "")]
    [InlineData("root\0", "database", "backup")]
    [InlineData("root", "database\0", "backup")]
    [InlineData("root", "database", "backup\0")]
    public void InvalidTextPathsAreRejected(string? root, string? database, string? backup)
    {
        SqliteMigrationResult result = _migrator.Migrate(
            root!,
            database!,
            backup!,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("SQLITE_PATH_INVALID", result.Error!.Code);
    }

    [Fact]
    public void FailuresDoNotExposePhysicalPathsOrSql()
    {
        using SqliteMigrationTestWorkspace workspace = new();
        workspace.Execute("CREATE TABLE Products (Conflict TEXT NOT NULL);");

        SqliteMigrationResult result = Migrate(workspace);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain(workspace.Root, result.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private SqliteMigrationResult Migrate(SqliteMigrationTestWorkspace workspace) =>
        _migrator.Migrate(
            workspace.Root,
            workspace.DatabasePath,
            workspace.BackupDirectory,
            TestContext.Current.CancellationToken);

    private static void DowngradeToVersionOne(SqliteMigrationTestWorkspace workspace) =>
        workspace.Execute("""
            DROP TABLE EngineVersionTransitions;
            DROP TABLE EngineVersionModules;
            DROP INDEX UX_EngineVersions_ApprovedLatest;
            DROP INDEX IX_EngineVersions_ToolInstallationId;
            ALTER TABLE EngineVersions RENAME TO EngineVersionsV2;
            CREATE TABLE EngineVersions (
                EngineVersionId TEXT PRIMARY KEY NOT NULL,
                EngineKind TEXT NOT NULL CHECK (EngineKind IN ('blender','unity','unreal')),
                Version TEXT NOT NULL,
                Status TEXT NOT NULL,
                ToolInstallationId TEXT REFERENCES ToolInstallations(ToolInstallationId) ON DELETE SET NULL,
                LastValidatedAtUtc TEXT,
                UNIQUE (EngineKind, Version)
            ) STRICT;
            DROP TABLE EngineVersionsV2;
            CREATE INDEX IX_EngineVersions_ToolInstallationId ON EngineVersions(ToolInstallationId);
            PRAGMA user_version = 1;
            """);

    private static SqliteConnection Open(string path)
    {
        SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ConnectionString);
        connection.Open();
        return connection;
    }
}
