using Microsoft.Data.Sqlite;

namespace PackageBuilder.Infrastructure.Tests.Persistence;

internal sealed class SqliteMigrationTestWorkspace : IDisposable
{
    public SqliteMigrationTestWorkspace()
    {
        Root = Path.Combine(AppContext.BaseDirectory, "PB-0210", Guid.NewGuid().ToString("N"));
        DatabaseDirectory = Path.Combine(Root, "runtime-data");
        BackupDirectory = Path.Combine(DatabaseDirectory, "backups");
        DatabasePath = Path.Combine(DatabaseDirectory, "packagebuilder.db");
        _ = Directory.CreateDirectory(BackupDirectory);
    }

    public string Root { get; }

    public string DatabaseDirectory { get; }

    public string BackupDirectory { get; }

    public string DatabasePath { get; }

    public SqliteConnection Open(bool create = false)
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = DatabasePath,
            Mode = create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            Pooling = false,
        };
        SqliteConnection connection = new(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    public void Execute(string sql)
    {
        using SqliteConnection connection = Open(create: true);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    public int UserVersion(string? databasePath = null)
    {
        using SqliteConnection connection = OpenPath(databasePath ?? DatabasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public IReadOnlyList<string> Tables(string? databasePath = null)
    {
        using SqliteConnection connection = OpenPath(databasePath ?? DatabasePath);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> tables = [];
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static SqliteConnection OpenPath(string path)
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        };
        SqliteConnection connection = new(builder.ConnectionString);
        connection.Open();
        return connection;
    }
}
