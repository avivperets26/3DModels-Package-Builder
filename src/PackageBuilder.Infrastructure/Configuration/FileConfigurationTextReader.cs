using System.Text;
using PackageBuilder.Contracts.Configuration;

namespace PackageBuilder.Infrastructure.Configuration;

public sealed class FileConfigurationTextReader : IConfigurationTextReader
{
    public ConfigurationReadResult Read(string configurationFilePath, int maximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationFilePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

        try
        {
            using var stream = new FileStream(
                configurationFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);

            if (stream.Length > maximumBytes)
            {
                return Failed("CONFIG_TOO_LARGE", "Configuration exceeds the approved size limit.");
            }

            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: false);
            string content = reader.ReadToEnd();
            return ConfigurationReadResult.Success(content);
        }
        catch (FileNotFoundException)
        {
            return Failed("CONFIG_FILE_MISSING", "The repository configuration file is missing.");
        }
        catch (DirectoryNotFoundException)
        {
            return Failed("CONFIG_FILE_MISSING", "The repository configuration file is missing.");
        }
        catch (DecoderFallbackException)
        {
            return Failed("CONFIG_ENCODING", "Configuration must use valid UTF-8 encoding.");
        }
        catch (IOException)
        {
            return Failed("CONFIG_READ_FAILED", "Configuration could not be read safely.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failed("CONFIG_READ_FAILED", "Configuration could not be read safely.");
        }
    }

    private static ConfigurationReadResult Failed(string code, string diagnostic) => ConfigurationReadResult.Failed(new ConfigurationFailure(code, "$", diagnostic));
}
