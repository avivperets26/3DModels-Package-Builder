namespace PackageBuilder.Infrastructure.Tests.Persistence;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqliteMigrationFixture
{
    public const string Name = "PB-0210 SQLite migrations";
}
