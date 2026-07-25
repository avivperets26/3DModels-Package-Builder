using System.Text.Json;

namespace PackageBuilder.Contracts.Migrations;

public interface IJsonMigrationStep
{
    MigrationDocumentFamily Family { get; }

    SchemaVersion FromVersion { get; }

    SchemaVersion ToVersion { get; }

    MigrationStepResult Migrate(JsonElement source);
}

public sealed class MigrationStepResult
{
    private MigrationStepResult(
        bool isSuccessful,
        string? outputJson,
        IReadOnlyList<MigrationChange> changes,
        string? diagnosticCode)
    {
        IsSuccessful = isSuccessful;
        OutputJson = outputJson;
        Changes = Array.AsReadOnly(changes.ToArray());
        DiagnosticCode = diagnosticCode;
    }

    public bool IsSuccessful { get; }

    public string? OutputJson { get; }

    public IReadOnlyList<MigrationChange> Changes { get; }

    public string? DiagnosticCode { get; }

    public static MigrationStepResult Success(
        string outputJson,
        IEnumerable<MigrationChange> changes) =>
        new(true, outputJson, changes.ToArray(), null);

    public static MigrationStepResult Failure(string diagnosticCode) =>
        new(false, null, [], diagnosticCode);
}

/// <summary>A small explicit step implementation useful for compiled registrations and tests.</summary>
public sealed class DelegateJsonMigrationStep(
    MigrationDocumentFamily family,
    SchemaVersion fromVersion,
    SchemaVersion toVersion,
    Func<JsonElement, MigrationStepResult> migration) : IJsonMigrationStep
{
    private readonly Func<JsonElement, MigrationStepResult> _migration = migration;

    public MigrationDocumentFamily Family { get; } = family;

    public SchemaVersion FromVersion { get; } = fromVersion;

    public SchemaVersion ToVersion { get; } = toVersion;

    public MigrationStepResult Migrate(JsonElement source) => _migration(source);
}
