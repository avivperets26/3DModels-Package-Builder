namespace PackageBuilder.Infrastructure.Persistence;

/// <summary>A stable, sanitized migration failure safe to show in logs or UI.</summary>
public sealed record SqliteMigrationError(string Code, string Message);
