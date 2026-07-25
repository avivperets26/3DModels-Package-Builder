namespace PackageBuilder.Contracts.Configuration;

public sealed class ConfigurationFailure(string code, string property, string diagnostic)
{
    public string Code { get; } = code;

    public string Property { get; } = property;

    public string Diagnostic { get; } = diagnostic;
}
