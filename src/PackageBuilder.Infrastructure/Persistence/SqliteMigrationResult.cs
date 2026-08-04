namespace PackageBuilder.Infrastructure.Persistence;

/// <summary>Describes whether schema initialization created, upgraded, or reused the database.</summary>
public enum SqliteMigrationOutcome
{
    Created,
    Upgraded,
    Current,
}

/// <summary>Returns migration evidence without exposing physical paths, SQL, or database content.</summary>
public sealed class SqliteMigrationResult
{
    private SqliteMigrationResult(
        SqliteMigrationOutcome? outcome,
        int previousVersion,
        int currentVersion,
        string? backupReference,
        SqliteMigrationError? error)
    {
        Outcome = outcome;
        PreviousVersion = previousVersion;
        CurrentVersion = currentVersion;
        BackupReference = backupReference;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public SqliteMigrationOutcome? Outcome { get; }

    public int PreviousVersion { get; }

    public int CurrentVersion { get; }

    public string? BackupReference { get; }

    public SqliteMigrationError? Error { get; }

    public static SqliteMigrationResult Success(
        SqliteMigrationOutcome outcome,
        int previousVersion,
        int currentVersion,
        string? backupReference = null) =>
        new(outcome, previousVersion, currentVersion, backupReference, null);

    public static SqliteMigrationResult Failure(string code, string message) =>
        new(null, 0, 0, null, new SqliteMigrationError(code, message));
}
