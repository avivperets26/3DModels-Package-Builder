using PackageBuilder.Contracts.Configuration;

namespace PackageBuilder.Infrastructure.Configuration;

public sealed class WindowsReparsePointInspector(IFileAttributeReader attributeReader) : IReparsePointInspector
{
    private readonly IFileAttributeReader _attributeReader = attributeReader ?? throw new ArgumentNullException(nameof(attributeReader));

    public WindowsReparsePointInspector()
        : this(new WindowsFileAttributeReader())
    {
    }

    public ConfigurationFailure? Inspect(
        string projectRoot,
        string configuredRoot,
        string logicalProperty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalProperty);

        string current = projectRoot;
        string relative = Path.GetRelativePath(projectRoot, configuredRoot);
        if (!StringComparer.Ordinal.Equals(relative, "."))
        {
            foreach (string segment in relative.Split(
                         Path.DirectorySeparatorChar,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if (!Directory.Exists(current))
                {
                    if (File.Exists(current))
                    {
                        return Failure(
                            logicalProperty,
                            "PATH_NOT_DIRECTORY",
                            "Configured root crosses an existing file; choose a dedicated directory.");
                    }

                    break;
                }

                FileAttributes attributes;
                try
                {
                    attributes = _attributeReader.GetAttributes(current);
                }
                catch (IOException)
                {
                    return Failure(
                        logicalProperty,
                        "PATH_INSPECTION_FAILED",
                        "Configured root could not be inspected safely.");
                }
                catch (UnauthorizedAccessException)
                {
                    return Failure(
                        logicalProperty,
                        "PATH_INSPECTION_FAILED",
                        "Configured root could not be inspected safely.");
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return Failure(
                        logicalProperty,
                        "PATH_REPARSE_POINT",
                        "Configured root crosses an existing reparse point; choose a physical contained directory.");
                }
            }
        }

        return null;
    }

    private static ConfigurationFailure Failure(
        string property,
        string code,
        string diagnostic) => new(code, property, diagnostic);
}
