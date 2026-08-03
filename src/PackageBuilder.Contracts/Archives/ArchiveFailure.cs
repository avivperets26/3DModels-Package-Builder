namespace PackageBuilder.Contracts.Archives;

/// <summary>Describes an expected archive-policy or extraction failure without exposing an unsafe path.</summary>
public sealed class ArchiveFailure
{
    public ArchiveFailure(string code, string location, string diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);

        Code = code;
        Location = location;
        Diagnostic = diagnostic;
    }

    public string Code { get; }

    public string Location { get; }

    public string Diagnostic { get; }
}
