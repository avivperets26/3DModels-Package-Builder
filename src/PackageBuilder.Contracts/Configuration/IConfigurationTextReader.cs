namespace PackageBuilder.Contracts.Configuration;

public interface IConfigurationTextReader
{
    ConfigurationReadResult Read(string configurationFilePath, int maximumBytes);
}
