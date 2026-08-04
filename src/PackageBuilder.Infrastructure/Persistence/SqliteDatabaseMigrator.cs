using Microsoft.Data.Sqlite;

namespace PackageBuilder.Infrastructure.Persistence;

/// <summary>
/// Validates contained database paths, creates a consistent pre-upgrade backup, and applies the
/// current SQLite schema in one transaction.
/// </summary>
public sealed class SqliteDatabaseMigrator : ISqliteDatabaseMigrator
{
    private const int MaximumBackupAttempts = 10;

    public SqliteMigrationResult Migrate(
        string projectRoot,
        string databasePath,
        string backupDirectory,
        CancellationToken cancellationToken = default)
    {
        PathValidationResult paths = ValidatePaths(projectRoot, databasePath, backupDirectory);
        if (!paths.IsSuccess)
        {
            return SqliteMigrationResult.Failure(paths.ErrorCode!, paths.ErrorMessage!);
        }

        cancellationToken.ThrowIfCancellationRequested();
        bool databaseExisted = File.Exists(paths.DatabasePath!);

        try
        {
            using SqliteConnection connection = Open(paths.DatabasePath!);
            int previousVersion = ReadUserVersion(connection);
            if (previousVersion > SqliteSchema.CurrentVersion)
            {
                return SqliteMigrationResult.Failure(
                    "SQLITE_VERSION_NEWER",
                    "The database schema is newer than this Package Builder version supports.");
            }

            EnsureIntegrity(connection);
            if (previousVersion == SqliteSchema.CurrentVersion)
            {
                return !HasCompleteVersionOneSchema(connection)
                    ? SqliteMigrationResult.Failure(
                        "SQLITE_SCHEMA_INVALID",
                        "The database schema version does not match its required table inventory.")
                    : SqliteMigrationResult.Success(
                    SqliteMigrationOutcome.Current,
                    previousVersion,
                    previousVersion);
            }

            string? backupReference = null;
            if (databaseExisted)
            {
                (string backupPath, string reference) = ReserveBackupPath(
                    paths.ProjectRoot!,
                    paths.DatabasePath!,
                    paths.BackupDirectory!,
                    previousVersion,
                    SqliteSchema.CurrentVersion);
                CreateBackup(connection, backupPath);
                backupReference = reference;
            }

            cancellationToken.ThrowIfCancellationRequested();
            ApplyVersionOne(connection, cancellationToken);
            EnsureIntegrity(connection);

            return SqliteMigrationResult.Success(
                databaseExisted ? SqliteMigrationOutcome.Upgraded : SqliteMigrationOutcome.Created,
                previousVersion,
                SqliteSchema.CurrentVersion,
                backupReference);
        }
        catch (SqliteException)
        {
            return SqliteMigrationResult.Failure(
                "SQLITE_MIGRATION_FAILED",
                "The database migration failed and no partial schema was committed.");
        }
        catch (IOException)
        {
            return SqliteMigrationResult.Failure(
                "SQLITE_STORAGE_FAILED",
                "The database or its backup could not be accessed safely.");
        }
        catch (UnauthorizedAccessException)
        {
            return SqliteMigrationResult.Failure(
                "SQLITE_STORAGE_FAILED",
                "The database or its backup could not be accessed safely.");
        }
    }

    private static SqliteConnection Open(string databasePath)
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 30,
        };
        SqliteConnection connection = new(builder.ConnectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 30000;";
        _ = command.ExecuteNonQuery();
        return connection;
    }

    private static int ReadUserVersion(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void EnsureIntegrity(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        string? result = command.ExecuteScalar() as string;
        if (!StringComparer.Ordinal.Equals(result, "ok"))
        {
            throw new SqliteException("Database integrity validation failed.", 11);
        }
    }

    private static bool HasCompleteVersionOneSchema(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        List<string> parameterNames = [];
        for (int index = 0; index < SqliteSchema.VersionOneTables.Count; index++)
        {
            string parameterName = $"$table{index}";
            parameterNames.Add(parameterName);
            _ = command.Parameters.AddWithValue(parameterName, SqliteSchema.VersionOneTables[index]);
        }

        command.CommandText = $"""
            SELECT COUNT(*)
            FROM sqlite_schema
            WHERE type = 'table' AND name IN ({string.Join(',', parameterNames)});
            """;
        long count = Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        return count == SqliteSchema.VersionOneTables.Count;
    }

    private static void ApplyVersionOne(SqliteConnection connection, CancellationToken cancellationToken)
    {
        using SqliteTransaction transaction = connection.BeginTransaction();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = SqliteSchema.CreateVersionOne;
            _ = command.ExecuteNonQuery();
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // SQLite's online-backup API provides a consistent copy even when the source has multiple pages.
    private static void CreateBackup(SqliteConnection source, string backupPath)
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        };
        using SqliteConnection destination = new(builder.ConnectionString);
        destination.Open();
        source.BackupDatabase(destination);
        EnsureIntegrity(destination);
    }

    private static (string BackupPath, string Reference) ReserveBackupPath(
        string projectRoot,
        string databasePath,
        string backupDirectory,
        int previousVersion,
        int nextVersion)
    {
        string stem = Path.GetFileNameWithoutExtension(databasePath);
        for (int sequence = 1; sequence <= MaximumBackupAttempts; sequence++)
        {
            string fileName = $"{stem}.pre-v{previousVersion}-to-v{nextVersion}.{sequence:D3}.db.bak";
            string candidate = Path.Combine(backupDirectory, fileName);
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return (candidate, Path.GetRelativePath(projectRoot, candidate).Replace('\\', '/'));
            }
        }

        throw new IOException("No safe backup name is available.");
    }

    // Physical containment is checked before SQLite can create or open any file.
    private static PathValidationResult ValidatePaths(
        string projectRoot,
        string databasePath,
        string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)
            || string.IsNullOrWhiteSpace(databasePath)
            || string.IsNullOrWhiteSpace(backupDirectory)
            || projectRoot.Contains('\0', StringComparison.Ordinal)
            || databasePath.Contains('\0', StringComparison.Ordinal)
            || backupDirectory.Contains('\0', StringComparison.Ordinal))
        {
            return PathValidationResult.Failure("SQLITE_PATH_INVALID", "Database paths must be absolute and contained.");
        }

        try
        {
            if (!Path.IsPathFullyQualified(projectRoot)
                || !Path.IsPathFullyQualified(databasePath)
                || !Path.IsPathFullyQualified(backupDirectory))
            {
                return PathValidationResult.Failure("SQLITE_PATH_INVALID", "Database paths must be absolute and contained.");
            }

            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
            string database = Path.GetFullPath(databasePath);
            string backup = Path.TrimEndingDirectorySeparator(Path.GetFullPath(backupDirectory));
            return !Directory.Exists(root)
                || !Directory.Exists(Path.GetDirectoryName(database))
                || !Directory.Exists(backup)
                || !IsStrictDescendant(root, database)
                || !IsStrictDescendant(root, backup)
                ? PathValidationResult.Failure("SQLITE_PATH_INVALID", "Database paths must be absolute and contained.")
                : CrossesReparsePoint(root, database) || CrossesReparsePoint(root, backup)
                ? PathValidationResult.Failure("SQLITE_PATH_REPARSE", "Database paths must not cross a reparse point.")
                : PathValidationResult.Success(root, database, backup);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return PathValidationResult.Failure("SQLITE_PATH_INVALID", "Database paths must be absolute and contained.");
        }
    }

    private static bool IsStrictDescendant(string root, string candidate)
    {
        string prefix = root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CrossesReparsePoint(string root, string candidate)
    {
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
        {
            return true;
        }

        string current = root;
        string relative = Path.GetRelativePath(root, candidate);
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current) && !File.Exists(current))
            {
                break;
            }

            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record PathValidationResult(
        bool IsSuccess,
        string? ProjectRoot,
        string? DatabasePath,
        string? BackupDirectory,
        string? ErrorCode,
        string? ErrorMessage)
    {
        public static PathValidationResult Success(string root, string database, string backup) =>
            new(true, root, database, backup, null, null);

        public static PathValidationResult Failure(string code, string message) =>
            new(false, null, null, null, code, message);
    }
}
