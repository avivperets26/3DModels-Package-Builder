namespace PackageBuilder.Contracts.Configuration;

public interface IReparsePointInspector
{
    ConfigurationFailure? Inspect(string projectRoot, string configuredRoot, string logicalProperty);
}
