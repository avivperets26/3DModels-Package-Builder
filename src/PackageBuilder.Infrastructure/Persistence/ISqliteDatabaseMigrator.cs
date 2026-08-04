namespace PackageBuilder.Infrastructure.Persistence;

/// <summary>Initializes or upgrades a contained Package Builder metadata database.</summary>
public interface ISqliteDatabaseMigrator
{
    SqliteMigrationResult Migrate(
        string projectRoot,
        string databasePath,
        string backupDirectory,
        CancellationToken cancellationToken = default);
}
