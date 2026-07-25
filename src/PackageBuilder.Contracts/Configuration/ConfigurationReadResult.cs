namespace PackageBuilder.Contracts.Configuration;

public sealed class ConfigurationReadResult
{
    private ConfigurationReadResult(string? content, ConfigurationFailure? failure)
    {
        Content = content;
        Failure = failure;
    }

    public string? Content { get; }

    public ConfigurationFailure? Failure { get; }

    public bool IsSuccess => Content is not null;

    public static ConfigurationReadResult Success(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new ConfigurationReadResult(content, null);
    }

    public static ConfigurationReadResult Failed(ConfigurationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new ConfigurationReadResult(null, failure);
    }
}
