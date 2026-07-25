using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PackageBuilder.Contracts.Configuration;

namespace PackageBuilder.Application.Configuration;

public sealed partial class PackageBuilderPathConfigurationLoader(
    IConfigurationTextReader textReader,
    IReparsePointInspector reparsePointInspector)
{
    public const string ApprovedProjectRoot = @"C:\Dev\PackageBuilder";
    public const string ConfigurationFileName = "packagebuilder.paths.json";
    public const int MaximumConfigurationBytes = 64 * 1024;

    private const int CurrentSchemaVersion = 1;

    private static readonly Dictionary<string, PathRootKind> _propertyKinds =
        new(StringComparer.Ordinal)
        {
            ["repository"] = PathRootKind.Repository,
            ["tools"] = PathRootKind.Tools,
            ["downloads"] = PathRootKind.Downloads,
            ["data"] = PathRootKind.Data,
            ["sourceAssets"] = PathRootKind.SourceAssets,
            ["jobs"] = PathRootKind.Jobs,
            ["cache"] = PathRootKind.Cache,
            ["temp"] = PathRootKind.Temp,
            ["templates"] = PathRootKind.Templates,
            ["builds"] = PathRootKind.Builds,
            ["artifacts"] = PathRootKind.Artifacts,
            ["logs"] = PathRootKind.Logs,
        };

    private static readonly PathRootKind[] _dataChildren =
    [
        PathRootKind.SourceAssets,
        PathRootKind.Jobs,
        PathRootKind.Cache,
        PathRootKind.Temp,
        PathRootKind.Templates,
    ];

    private readonly IConfigurationTextReader _textReader = textReader ?? throw new ArgumentNullException(nameof(textReader));
    private readonly IReparsePointInspector _reparsePointInspector = reparsePointInspector
            ?? throw new ArgumentNullException(nameof(reparsePointInspector));

    public PackageBuilderPathConfigurationResult Load()
    {
        string filePath = Path.Combine(ApprovedProjectRoot, ConfigurationFileName);
        ConfigurationReadResult readResult = _textReader.Read(filePath, MaximumConfigurationBytes);
        return !readResult.IsSuccess
            ? PackageBuilderPathConfigurationResult.Failed(
                [readResult.Failure!])
            : ParseAndValidate(readResult.Content!);
    }

    public PackageBuilderPathConfigurationResult ParseAndValidate(string json)
    {
        if (json is null)
        {
            return PackageBuilderPathConfigurationResult.Failed(
                [Failure("CONFIG_EMPTY", "$", "Configuration content is required.")]);
        }

        if (json.Length == 0)
        {
            return PackageBuilderPathConfigurationResult.Failed(
                [Failure("CONFIG_EMPTY", "$", "Configuration content is required.")]);
        }

        if (Encoding.UTF8.GetByteCount(json) > MaximumConfigurationBytes)
        {
            return PackageBuilderPathConfigurationResult.Failed(
                [Failure("CONFIG_TOO_LARGE", "$", "Configuration exceeds the approved size limit.")]);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 8,
                });
        }
        catch (JsonException)
        {
            return PackageBuilderPathConfigurationResult.Failed(
                [Failure("CONFIG_MALFORMED_JSON", "$", "Configuration must be valid strict JSON.")]);
        }

        using (document)
        {
            var structuralFailures = new List<ConfigurationFailure>();
            Dictionary<PathRootKind, string>? rawRoots = ParseStructure(document.RootElement, structuralFailures);
            if (structuralFailures.Count != 0)
            {
                return PackageBuilderPathConfigurationResult.Failed(structuralFailures);
            }

            Dictionary<PathRootKind, ConfiguredPathRoot>? normalized = NormalizeRoots(rawRoots!, structuralFailures);
            if (structuralFailures.Count != 0)
            {
                return PackageBuilderPathConfigurationResult.Failed(structuralFailures);
            }

            ValidateHierarchy(normalized!, structuralFailures);
            if (structuralFailures.Count != 0)
            {
                return PackageBuilderPathConfigurationResult.Failed(structuralFailures);
            }

            var configuration = new PackageBuilderPathConfiguration(normalized!);
            foreach (ConfiguredPathRoot root in configuration.Roots)
            {
                ConfigurationFailure? reparseFailure = _reparsePointInspector.Inspect(
                    configuration.Repository.FullPath,
                    root.FullPath,
                    root.Kind.ToString());
                if (reparseFailure is not null)
                {
                    structuralFailures.Add(reparseFailure);
                }
            }

            return structuralFailures.Count == 0
                ? PackageBuilderPathConfigurationResult.Success(configuration)
                : PackageBuilderPathConfigurationResult.Failed(structuralFailures);
        }
    }

    private static Dictionary<PathRootKind, string>? ParseStructure(
        JsonElement root,
        List<ConfigurationFailure> failures)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            failures.Add(Failure("CONFIG_ROOT_TYPE", "$", "Configuration must be a JSON object."));
            return null;
        }

        JsonElement rootsElement = default;
        bool hasSchemaVersion = false;
        bool hasRoots = false;
        var topLevelNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!topLevelNames.Add(property.Name))
            {
                string logicalProperty = property.Name is "schemaVersion" or "roots"
                    ? property.Name
                    : "$";
                failures.Add(Failure("CONFIG_DUPLICATE_PROPERTY", logicalProperty, "Duplicate configuration property."));
                continue;
            }

            switch (property.Name)
            {
                case "schemaVersion":
                    hasSchemaVersion = true;
                    if (property.Value.ValueKind != JsonValueKind.Number
                        || !property.Value.TryGetInt32(out int version)
                        || version != CurrentSchemaVersion)
                    {
                        failures.Add(Failure(
                            "CONFIG_SCHEMA_VERSION",
                            property.Name,
                            "schemaVersion must be the supported integer value 1."));
                    }

                    break;
                case "roots":
                    hasRoots = true;
                    rootsElement = property.Value;
                    break;
                default:
                    failures.Add(Failure("CONFIG_UNKNOWN_PROPERTY", "$", "Unknown configuration property."));
                    break;
            }
        }

        if (!hasSchemaVersion)
        {
            failures.Add(Failure("CONFIG_MISSING_PROPERTY", "schemaVersion", "Required configuration property is missing."));
        }

        if (!hasRoots)
        {
            failures.Add(Failure("CONFIG_MISSING_PROPERTY", "roots", "Required configuration property is missing."));
            return null;
        }

        if (rootsElement.ValueKind != JsonValueKind.Object)
        {
            failures.Add(Failure("CONFIG_ROOTS_TYPE", "roots", "roots must be a JSON object."));
            return null;
        }

        var values = new Dictionary<PathRootKind, string>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in rootsElement.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                string logicalProperty = _propertyKinds.ContainsKey(property.Name)
                    ? property.Name
                    : "roots";
                failures.Add(Failure("CONFIG_DUPLICATE_PROPERTY", logicalProperty, "Duplicate root property."));
                continue;
            }

            if (!_propertyKinds.TryGetValue(property.Name, out PathRootKind kind))
            {
                failures.Add(Failure("CONFIG_UNKNOWN_ROOT", "roots", "Unknown configured root."));
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                failures.Add(Failure("CONFIG_ROOT_TYPE", property.Name, "Configured root must be a JSON string."));
                continue;
            }

            values[kind] = property.Value.GetString()!;
        }

        foreach (KeyValuePair<string, PathRootKind> expected in _propertyKinds)
        {
            if (!values.ContainsKey(expected.Value))
            {
                failures.Add(Failure("CONFIG_MISSING_ROOT", expected.Key, "Required configured root is missing."));
            }
        }

        return failures.Count == 0 ? values : null;
    }

    private static Dictionary<PathRootKind, ConfiguredPathRoot>? NormalizeRoots(
        IReadOnlyDictionary<PathRootKind, string> rawRoots,
        List<ConfigurationFailure> failures)
    {
        string approvedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ApprovedProjectRoot));
        var normalized = new Dictionary<PathRootKind, ConfiguredPathRoot>();

        foreach (KeyValuePair<PathRootKind, string> pair in rawRoots)
        {
            string property = _propertyKinds.Single(item => item.Value == pair.Key).Key;
            string? path = Normalize(pair.Value, approvedRoot, property, failures);
            if (path is not null)
            {
                normalized[pair.Key] = new ConfiguredPathRoot(pair.Key, path);
            }
        }

        ConfiguredPathRoot repository = normalized[PathRootKind.Repository];
        if (!StringComparer.OrdinalIgnoreCase.Equals(repository.FullPath, approvedRoot))
        {
            failures.Add(Failure(
                "CONFIG_PROJECT_ROOT_MISMATCH",
                "repository",
                "Repository root must resolve to the approved Package Builder project root."));
        }

        return failures.Count == 0 ? normalized : null;
    }

    private static string? Normalize(
        string raw,
        string approvedRoot,
        string property,
        List<ConfigurationFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            failures.Add(Failure("PATH_EMPTY", property, "Configured root must not be empty or whitespace."));
            return null;
        }

        if (!StringComparer.Ordinal.Equals(raw, raw.Trim()))
        {
            failures.Add(Failure("PATH_SURROUNDING_WHITESPACE", property, "Configured root must not contain surrounding whitespace."));
            return null;
        }

        if (raw.Contains('%', StringComparison.Ordinal) || raw.Contains("${", StringComparison.Ordinal))
        {
            failures.Add(Failure("PATH_UNRESOLVED_VALUE", property, "Environment or placeholder expansion is not supported."));
            return null;
        }

        string windowsPath = raw.Replace('/', '\\');
        if (DevicePathPrefixRegex().IsMatch(windowsPath))
        {
            failures.Add(Failure("PATH_DEVICE_PREFIX", property, "Device and extended-length path prefixes are not accepted."));
            return null;
        }

        if (raw.Any(char.IsControl)
            || raw.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0)
        {
            failures.Add(Failure("PATH_INVALID_CHARACTER", property, "Configured root contains an invalid path character."));
            return null;
        }

        if (windowsPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            failures.Add(Failure("PATH_UNC", property, "UNC paths are not accepted."));
            return null;
        }

        if (windowsPath is @"\")
        {
            failures.Add(Failure("PATH_SEPARATOR_ROOT", property, "Separator-only rooted paths are not accepted."));
            return null;
        }

        if (windowsPath.StartsWith('\\'))
        {
            failures.Add(Failure("PATH_ROOTED_SEPARATOR", property, "Rooted paths without an approved drive are not accepted."));
            return null;
        }

        if (DriveRelativeRegex().IsMatch(windowsPath))
        {
            failures.Add(Failure("PATH_DRIVE_RELATIVE", property, "Drive-relative paths are not accepted."));
            return null;
        }

        int colonIndex = windowsPath.IndexOf(':');
        if (colonIndex >= 0 && (colonIndex != 1 || windowsPath.IndexOf(':', colonIndex + 1) >= 0))
        {
            failures.Add(Failure("PATH_ALTERNATE_DATA_STREAM", property, "Alternate data-stream syntax is not accepted."));
            return null;
        }

        string fullPath;
        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(windowsPath, approvedRoot));
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            failures.Add(Failure("PATH_INVALID", property, "Configured root is not a valid canonical Windows path."));
            return null;
        }

        if (!IsSameOrDescendant(approvedRoot, fullPath))
        {
            failures.Add(Failure("PATH_OUTSIDE_PROJECT", property, "Configured root resolves outside the approved project root."));
            return null;
        }

        if (property != "repository" && StringComparer.OrdinalIgnoreCase.Equals(approvedRoot, fullPath))
        {
            failures.Add(Failure("PATH_PROJECT_ROOT_FORBIDDEN", property, "This configured root requires a dedicated child directory."));
            return null;
        }

        return fullPath;
    }

    private static void ValidateHierarchy(
        IReadOnlyDictionary<PathRootKind, ConfiguredPathRoot> roots,
        List<ConfigurationFailure> failures)
    {
        ConfiguredPathRoot[] values = [.. roots.Values];
        for (int index = 0; index < values.Length; index++)
        {
            for (int otherIndex = index + 1; otherIndex < values.Length; otherIndex++)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(values[index].FullPath, values[otherIndex].FullPath))
                {
                    failures.Add(Failure(
                        "PATH_ROOT_COLLISION",
                        values[otherIndex].Kind.ToString(),
                        "Configured roots must not resolve to the same directory."));
                }
            }
        }

        foreach (PathRootKind child in _dataChildren)
        {
            RequireDedicatedDescendant(roots, PathRootKind.Data, child, failures);
        }

        RequireDedicatedDescendant(roots, PathRootKind.Artifacts, PathRootKind.Builds, failures);

        PathRootKind[] siblings =
        [
            PathRootKind.Tools,
            PathRootKind.Downloads,
            PathRootKind.Data,
            PathRootKind.Artifacts,
            PathRootKind.Logs,
        ];
        RejectAncestorRelationships(roots, siblings, failures);
        RejectAncestorRelationships(roots, _dataChildren, failures);
    }

    private static void RequireDedicatedDescendant(
        IReadOnlyDictionary<PathRootKind, ConfiguredPathRoot> roots,
        PathRootKind parent,
        PathRootKind child,
        List<ConfigurationFailure> failures)
    {
        if (!IsStrictDescendant(roots[parent].FullPath, roots[child].FullPath))
        {
            failures.Add(Failure(
                "PATH_HIERARCHY",
                child.ToString(),
                "Configured child root must be a dedicated descendant of its approved parent root."));
        }
    }

    private static void RejectAncestorRelationships(
        IReadOnlyDictionary<PathRootKind, ConfiguredPathRoot> roots,
        PathRootKind[] kinds,
        List<ConfigurationFailure> failures)
    {
        for (int index = 0; index < kinds.Length; index++)
        {
            for (int otherIndex = index + 1; otherIndex < kinds.Length; otherIndex++)
            {
                ConfiguredPathRoot first = roots[kinds[index]];
                ConfiguredPathRoot second = roots[kinds[otherIndex]];
                if (IsStrictDescendant(first.FullPath, second.FullPath)
                    || IsStrictDescendant(second.FullPath, first.FullPath))
                {
                    failures.Add(Failure(
                        "PATH_UNAPPROVED_NESTING",
                        second.Kind.ToString(),
                        "Configured roots use a hierarchy that is not approved."));
                }
            }
        }
    }

    internal static bool IsSameOrDescendant(string parent, string candidate)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(parent, candidate)
            || IsStrictDescendant(parent, candidate);
    }

    internal static bool IsStrictDescendant(string parent, string candidate)
    {
        string boundary = parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(boundary, StringComparison.OrdinalIgnoreCase);
    }

    private static ConfigurationFailure Failure(string code, string property, string diagnostic) => new(code, property, diagnostic);

    [GeneratedRegex(@"^(?:\\\\[?.]\\|\\\?\?\\)", RegexOptions.CultureInvariant)]
    private static partial Regex DevicePathPrefixRegex();

    [GeneratedRegex(@"^[A-Za-z]:[^\\/]", RegexOptions.CultureInvariant)]
    private static partial Regex DriveRelativeRegex();
}
