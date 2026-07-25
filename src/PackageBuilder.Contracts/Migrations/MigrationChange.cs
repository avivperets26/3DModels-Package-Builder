namespace PackageBuilder.Contracts.Migrations;

public enum MigrationChangeKind
{
    Addition = 0,
    Removal,
    Rename,
    Default,
    Conversion,
    Warning,
    ReviewRequired,
}

/// <summary>One explicit, deterministic audit entry produced by a migration step.</summary>
public sealed record MigrationChange(
    MigrationChangeKind Kind,
    string? SourcePointer,
    string? TargetPointer,
    string Description)
{
    internal bool IsStructurallyValid =>
        Description.Length != 0 &&
        IsPointer(SourcePointer) &&
        IsPointer(TargetPointer) &&
        Kind switch
        {
            MigrationChangeKind.Addition or MigrationChangeKind.Default =>
                SourcePointer is null && TargetPointer is not null,
            MigrationChangeKind.Removal =>
                SourcePointer is not null && TargetPointer is null,
            MigrationChangeKind.Rename =>
                SourcePointer is not null && TargetPointer is not null &&
                !string.Equals(SourcePointer, TargetPointer, StringComparison.Ordinal),
            MigrationChangeKind.Conversion =>
                SourcePointer is not null && TargetPointer is not null,
            MigrationChangeKind.Warning or MigrationChangeKind.ReviewRequired =>
                SourcePointer is not null || TargetPointer is not null,
            _ => false,
        };

    private static bool IsPointer(string? value) =>
        value is null ||
        value.Length == 0 ||
        value[0] == '/' && !value.Any(char.IsControl);
}
