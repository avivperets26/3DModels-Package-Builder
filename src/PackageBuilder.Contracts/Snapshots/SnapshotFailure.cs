namespace PackageBuilder.Contracts.Snapshots;

/// <summary>Describes an expected source-snapshot failure without exposing an unsafe physical path.</summary>
public sealed class SnapshotFailure(string code, string location, string diagnostic)
{
    public string Code { get; } = string.IsNullOrWhiteSpace(code)
        ? throw new ArgumentException("A failure code is required.", nameof(code))
        : code;

    public string Location { get; } = string.IsNullOrWhiteSpace(location)
        ? throw new ArgumentException("A safe failure location is required.", nameof(location))
        : location;

    public string Diagnostic { get; } = string.IsNullOrWhiteSpace(diagnostic)
        ? throw new ArgumentException("A safe diagnostic is required.", nameof(diagnostic))
        : diagnostic;
}
