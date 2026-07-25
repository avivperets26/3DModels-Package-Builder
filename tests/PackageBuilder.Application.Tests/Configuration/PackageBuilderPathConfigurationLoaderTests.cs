using System.Globalization;
using System.Text.Json;
using PackageBuilder.Application.Configuration;
using PackageBuilder.Contracts.Configuration;

namespace PackageBuilder.Application.Tests.Configuration;

[Collection("PB-0201 current directory isolation")]
public sealed class PackageBuilderPathConfigurationLoaderTests
{
    private const string ValidJson = """
        {
          "schemaVersion": 1,
          "roots": {
            "repository": "C:\\Dev\\PackageBuilder",
            "tools": "tools",
            "downloads": "downloads",
            "data": "runtime-data",
            "sourceAssets": "runtime-data\\source-assets",
            "jobs": "runtime-data\\jobs",
            "cache": "runtime-data\\engine-caches",
            "temp": "runtime-data\\temp",
            "templates": "runtime-data\\engine-templates",
            "builds": "artifacts\\Builds",
            "artifacts": "artifacts",
            "logs": "logs"
          }
        }
        """;

    [Fact]
    public void LoadReadsOnlyTheFixedRepositoryConfigurationFile()
    {
        var reader = new StubReader(ConfigurationReadResult.Success(ValidJson));
        var loader = new PackageBuilderPathConfigurationLoader(reader, new AcceptingInspector());

        PackageBuilderPathConfigurationResult result = loader.Load();

        Assert.True(result.IsSuccess);
        Assert.Equal(
            @"C:\Dev\PackageBuilder\packagebuilder.paths.json",
            reader.RequestedPath);
        Assert.Equal(PackageBuilderPathConfigurationLoader.MaximumConfigurationBytes, reader.MaximumBytes);
    }

    [Fact]
    public void LoadReturnsStructuredReaderFailure()
    {
        var expected = new ConfigurationFailure("CONFIG_FILE_MISSING", "$", "Missing.");
        var loader = new PackageBuilderPathConfigurationLoader(
            new StubReader(ConfigurationReadResult.Failed(expected)),
            new AcceptingInspector());

        PackageBuilderPathConfigurationResult result = loader.Load();

        Assert.False(result.IsSuccess);
        Assert.Same(expected, Assert.Single(result.Failures));
    }

    [Fact]
    public void ConstructorsRejectNullCollaborators()
    {
        _ = Assert.Throws<ArgumentNullException>(
            () => new PackageBuilderPathConfigurationLoader(null!, new AcceptingInspector()));
        _ = Assert.Throws<ArgumentNullException>(
            () => new PackageBuilderPathConfigurationLoader(new StubReader(ConfigurationReadResult.Success("{}")), null!));
        _ = Assert.Throws<ArgumentNullException>(() => ConfigurationReadResult.Success(null!));
        _ = Assert.Throws<ArgumentNullException>(() => ConfigurationReadResult.Failed(null!));
    }

    [Fact]
    public void ValidConfigurationCreatesEveryTypedCanonicalRoot()
    {
        PackageBuilderPathConfigurationResult result = Parse(ValidJson);

        Assert.True(result.IsSuccess);
        PackageBuilderPathConfiguration configuration = Assert.IsType<PackageBuilderPathConfiguration>(result.Configuration);
        Assert.Equal(12, configuration.Roots.Count);
        Assert.Equal(PathRootKind.Repository, configuration.Repository.Kind);
        Assert.Equal(@"C:\Dev\PackageBuilder", configuration.Repository.FullPath);
        Assert.Equal(@"C:\Dev\PackageBuilder\tools", configuration.Tools.FullPath);
        Assert.Equal(@"C:\Dev\PackageBuilder\downloads", configuration.Downloads.FullPath);
        Assert.Equal(@"C:\Dev\PackageBuilder\runtime-data", configuration.Data.FullPath);
        Assert.Equal(@"C:\Dev\PackageBuilder\runtime-data\source-assets", configuration.SourceAssets.FullPath);
        Assert.Equal(@"C:\Dev\PackageBuilder\runtime-data\jobs", configuration.Jobs.FullPath);
        Assert.Equal(@"C:\Dev\PackageBuilder\runtime-data\engine-caches", configuration.Cache.FullPath);
        Assert.Equal(@"C:\Dev\PackageBuilder\runtime-data\temp", configuration.Temp.FullPath);
        Assert.Equal(@"C:\Dev\PackageBuilder\runtime-data\engine-templates", configuration.Templates.FullPath);
        Assert.Equal(@"C:\Dev\PackageBuilder\artifacts\Builds", configuration.Builds.FullPath);
        Assert.Equal(@"C:\Dev\PackageBuilder\artifacts", configuration.Artifacts.FullPath);
        Assert.Equal(@"C:\Dev\PackageBuilder\logs", configuration.Logs.FullPath);
    }

    [Fact]
    public void AbsoluteRelativeDotSegmentsCaseAndTrailingSeparatorsNormalizeDeterministically()
    {
        string json = ReplaceRoot(ValidJson, "repository", @"c:\dev\packagebuilder\\");
        json = ReplaceRoot(json, "tools", @".\runtime-data\..\tools\\");

        PackageBuilderPathConfigurationResult result = Parse(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(@"C:\Dev\PackageBuilder\tools", result.Configuration!.Tools.FullPath);
    }

    [Fact]
    public void RelativeResolutionDoesNotDependOnCurrentWorkingDirectory()
    {
        string original = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = Path.Combine(
                PackageBuilderPathConfigurationLoader.ApprovedProjectRoot,
                "runtime-data");

            PackageBuilderPathConfigurationResult result = Parse(ValidJson);

            Assert.True(result.IsSuccess);
            Assert.Equal(@"C:\Dev\PackageBuilder\tools", result.Configuration!.Tools.FullPath);
        }
        finally
        {
            Environment.CurrentDirectory = original;
        }
    }

    [Fact]
    public void ConfiguredRootEqualityAndHashingAreOrdinalCaseInsensitiveAndKindSensitive()
    {
        ConfiguredPathRoot first = Parse(ValidJson).Configuration!.Tools;
        ConfiguredPathRoot caseVariant = Parse(ReplaceRoot(ValidJson, "tools", @"C:\DEV\PACKAGEBUILDER\TOOLS"))
            .Configuration!.Tools;
        ConfiguredPathRoot otherKind = Parse(ReplaceRoot(ValidJson, "downloads", @"C:\Dev\PackageBuilder\tools2"))
            .Configuration!.Downloads;

        Assert.Equal(first, caseVariant);
        Assert.Equal(first.GetHashCode(), caseVariant.GetHashCode());
        Assert.NotEqual(first, otherKind);
        Assert.False(first.Equals(null));
        Assert.False(first.Equals(new object()));
        Assert.Equal(first.FullPath, first.ToString());
    }

    [Fact]
    public void EqualityHashingAndNormalizationAreCultureIndependent()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            ConfiguredPathRoot lower = Parse(ReplaceRoot(ValidJson, "tools", @"C:\Dev\PackageBuilder\i-tools"))
                .Configuration!.Tools;
            ConfiguredPathRoot upper = Parse(ReplaceRoot(ValidJson, "tools", @"c:\dev\packagebuilder\I-TOOLS"))
                .Configuration!.Tools;

            Assert.Equal(lower, upper);
            Assert.Equal(lower.GetHashCode(), upper.GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyRootsAreRejected(string value) => AssertFailure(ReplaceRoot(ValidJson, "tools", value), "PATH_EMPTY");

    [Theory]
    [InlineData(" tools")]
    [InlineData("tools ")]
    public void SurroundingWhitespaceIsRejected(string value) => AssertFailure(ReplaceRoot(ValidJson, "tools", value), "PATH_SURROUNDING_WHITESPACE");

    [Theory]
    [InlineData("%TEMP%")]
    [InlineData("${TEMP}")]
    public void UnresolvedPlaceholdersAreRejected(string value) => AssertFailure(ReplaceRoot(ValidJson, "tools", value), "PATH_UNRESOLVED_VALUE");

    [Theory]
    [InlineData("bad\u0001path")]
    [InlineData("bad<path")]
    [InlineData("bad>path")]
    [InlineData("bad\"path")]
    [InlineData("bad|path")]
    [InlineData("bad?path")]
    [InlineData("bad*path")]
    public void InvalidAndControlCharactersAreRejected(string value) => AssertFailure(ReplaceRoot(ValidJson, "tools", value), "PATH_INVALID_CHARACTER");

    [Fact]
    public void PathApiFailuresReturnStructuredValidation()
    {
        AssertFailure(
            ReplaceRoot(ValidJson, "tools", new string('a', 40_000)),
            "PATH_INVALID");
    }

    [Theory]
    [InlineData(@"\\?\C:\Dev\PackageBuilder\tools")]
    [InlineData(@"\\.\C:\Dev\PackageBuilder\tools")]
    [InlineData(@"\??\C:\Dev\PackageBuilder\tools")]
    public void DeviceAndExtendedPrefixesAreRejected(string value) => AssertFailure(ReplaceRoot(ValidJson, "tools", value), "PATH_DEVICE_PREFIX");

    [Fact]
    public void UncPathsAreRejected() => AssertFailure(ReplaceRoot(ValidJson, "tools", @"\\server\share"), "PATH_UNC");

    [Fact]
    public void SeparatorOnlyRootIsRejected() => AssertFailure(ReplaceRoot(ValidJson, "tools", @"\"), "PATH_SEPARATOR_ROOT");

    [Fact]
    public void RootedSeparatorPathIsRejected() => AssertFailure(ReplaceRoot(ValidJson, "tools", @"\tools"), "PATH_ROOTED_SEPARATOR");

    [Fact]
    public void DriveRelativePathIsRejected() => AssertFailure(ReplaceRoot(ValidJson, "tools", "C:tools"), "PATH_DRIVE_RELATIVE");

    [Theory]
    [InlineData("tools:stream")]
    [InlineData(@"C:\Dev\PackageBuilder\tools:stream")]
    [InlineData(@"C:\Dev\PackageBuilder\tools::$DATA")]
    public void AlternateDataStreamSyntaxIsRejected(string value) => AssertFailure(ReplaceRoot(ValidJson, "tools", value), "PATH_ALTERNATE_DATA_STREAM");

    [Theory]
    [InlineData(@"..\outside")]
    [InlineData(@"C:\Dev\PackageBuilder-Evil")]
    [InlineData(@"D:\PackageBuilder")]
    public void TraversalSiblingPrefixSystemAndOtherDriveEscapesAreRejected(string value) => AssertFailure(ReplaceRoot(ValidJson, "tools", value), "PATH_OUTSIDE_PROJECT");

    [Fact]
    public void SystemDirectoryPathIsRejectedWithoutLeakingItToDiagnostics()
    {
        string systemPath = string.Concat(@"C:\", "Windows");
        PackageBuilderPathConfigurationResult result = Parse(ReplaceRoot(ValidJson, "tools", systemPath));

        Assert.False(result.IsSuccess);
        ConfigurationFailure failure = Assert.Single(result.Failures);
        Assert.Equal("PATH_OUTSIDE_PROJECT", failure.Code);
        Assert.DoesNotContain(systemPath, failure.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserProfilePathIsRejectedWithoutLeakingItToDiagnostics()
    {
        string userProfilePath = string.Concat(@"C:\", "Users", @"\Example");
        PackageBuilderPathConfigurationResult result = Parse(ReplaceRoot(ValidJson, "tools", userProfilePath));

        Assert.False(result.IsSuccess);
        ConfigurationFailure failure = Assert.Single(result.Failures);
        Assert.Equal("PATH_OUTSIDE_PROJECT", failure.Code);
        Assert.DoesNotContain(userProfilePath, failure.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DedicatedWritableRootCannotBeProjectRoot()
    {
        AssertFailure(
            ReplaceRoot(ValidJson, "tools", @"C:\Dev\PackageBuilder"),
            "PATH_PROJECT_ROOT_FORBIDDEN");
    }

    [Fact]
    public void RepositoryRootMustResolveToApprovedRoot()
    {
        AssertFailure(
            ReplaceRoot(ValidJson, "repository", @"C:\Dev\PackageBuilder\other"),
            "CONFIG_PROJECT_ROOT_MISMATCH");
    }

    [Fact]
    public void DuplicateCanonicalRootsAreRejected() => AssertFailure(ReplaceRoot(ValidJson, "downloads", "tools"), "PATH_ROOT_COLLISION");

    [Fact]
    public void RequiredChildHierarchyIsEnforced()
    {
        AssertFailure(ReplaceRoot(ValidJson, "jobs", "jobs"), "PATH_HIERARCHY");
        AssertFailure(ReplaceRoot(ValidJson, "builds", "builds"), "PATH_HIERARCHY");
    }

    [Fact]
    public void UnapprovedParentAndSiblingNestingIsRejected()
    {
        AssertFailure(ReplaceRoot(ValidJson, "downloads", @"tools\downloads"), "PATH_UNAPPROVED_NESTING");
        AssertFailure(
            ReplaceRoot(ValidJson, "jobs", @"runtime-data\source-assets\jobs"),
            "PATH_UNAPPROVED_NESTING");
    }

    [Theory]
    [InlineData(null, "CONFIG_EMPTY")]
    [InlineData("", "CONFIG_EMPTY")]
    [InlineData("{", "CONFIG_MALFORMED_JSON")]
    [InlineData("[]", "CONFIG_ROOT_TYPE")]
    public void EmptyMalformedAndWrongRootJsonAreRejected(string? json, string code) => AssertFailure(json, code);

    [Fact]
    public void OversizedConfigurationContentIsRejected() => AssertFailure(new string('x', PackageBuilderPathConfigurationLoader.MaximumConfigurationBytes + 1), "CONFIG_TOO_LARGE");

    [Fact]
    public void DuplicateUnknownMissingAndWrongTopLevelPropertiesAreRejected()
    {
        AssertFailure(
            """{"schemaVersion":1,"schemaVersion":1,"roots":{}}""",
            "CONFIG_DUPLICATE_PROPERTY");
        AssertFailure(
            """{"schemaVersion":1,"unexpected":true,"unexpected":false,"roots":{}}""",
            "CONFIG_DUPLICATE_PROPERTY");
        AssertFailure(
            """{"schemaVersion":1,"unexpected":true,"roots":{}}""",
            "CONFIG_UNKNOWN_PROPERTY");
        AssertFailure("""{"roots":{}}""", "CONFIG_MISSING_PROPERTY");
        AssertFailure("""{"schemaVersion":1}""", "CONFIG_MISSING_PROPERTY");
        AssertFailure("""{"schemaVersion":"1","roots":{}}""", "CONFIG_SCHEMA_VERSION");
        AssertFailure("""{"schemaVersion":2,"roots":{}}""", "CONFIG_SCHEMA_VERSION");
        AssertFailure("""{"schemaVersion":1,"roots":[]}""", "CONFIG_ROOTS_TYPE");
    }

    [Fact]
    public void DuplicateUnknownMissingAndWrongRootPropertiesAreRejected()
    {
        AssertFailure(
            ValidJson.Replace(
                @"""tools"": ""tools"",",
                @"""tools"": ""tools"", ""tools"": ""tools2"",",
                StringComparison.Ordinal),
            "CONFIG_DUPLICATE_PROPERTY");
        AssertFailure(
            ValidJson.Replace(
                @"""tools"": ""tools"",",
                @"""unknown"": ""tools"", ""unknown"": ""tools2"",",
                StringComparison.Ordinal),
            "CONFIG_DUPLICATE_PROPERTY");
        AssertFailure(
            ValidJson.Replace(
                @"""tools"": ""tools"",",
                @"""unknown"": ""tools"",",
                StringComparison.Ordinal),
            "CONFIG_UNKNOWN_ROOT");
        AssertFailure(
            ValidJson.Replace(
                @"""tools"": ""tools"",",
                string.Empty,
                StringComparison.Ordinal),
            "CONFIG_MISSING_ROOT");
        AssertFailure(
            ValidJson.Replace(
                @"""tools"": ""tools"",",
                @"""tools"": 42,",
                StringComparison.Ordinal),
            "CONFIG_ROOT_TYPE");
    }

    [Fact]
    public void UntrustedUnknownPropertyNamesAreNotEchoed()
    {
        string sensitiveName = string.Concat(@"C:\", "Users", @"\Example");
        string json = string.Concat(
            "{\"schemaVersion\":1,\"roots\":{},",
            JsonSerializer.Serialize(sensitiveName),
            ":true}");

        PackageBuilderPathConfigurationResult result = Parse(json);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain(result.Failures, failure => failure.Property.Contains(sensitiveName, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Failures, failure => failure.Diagnostic.Contains(sensitiveName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InspectorFailuresAreAggregatedWithoutReturningUnsafeConfiguration()
    {
        var inspector = new RejectingInspector(PathRootKind.Tools, PathRootKind.Logs);
        var loader = new PackageBuilderPathConfigurationLoader(
            new StubReader(ConfigurationReadResult.Success(ValidJson)),
            inspector);

        PackageBuilderPathConfigurationResult result = loader.ParseAndValidate(ValidJson);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Configuration);
        Assert.Equal(2, result.Failures.Count);
        Assert.All(result.Failures, failure => Assert.Equal("PATH_REPARSE_POINT", failure.Code));
    }

    [Fact]
    public void ResultAndRootCollectionsAreImmutableSnapshots()
    {
        var failureSource = new List<ConfigurationFailure>
        {
            new("FAILURE", "$", "Diagnostic."),
        };
        var read = ConfigurationReadResult.Failed(failureSource[0]);
        Assert.False(read.IsSuccess);
        Assert.Null(read.Content);
        Assert.Same(failureSource[0], read.Failure);

        PackageBuilderPathConfigurationResult result = Parse(ValidJson);
        _ = Assert.IsType<IReadOnlyList<ConfiguredPathRoot>>(
            result.Configuration!.Roots,
            exactMatch: false);
        _ = Assert.Throws<NotSupportedException>(
            () => ((IList<ConfiguredPathRoot>)result.Configuration.Roots).Add(result.Configuration.Tools));
    }

    private static PackageBuilderPathConfigurationResult Parse(string? json)
    {
        var loader = new PackageBuilderPathConfigurationLoader(
            new StubReader(ConfigurationReadResult.Success(ValidJson)),
            new AcceptingInspector());
        return loader.ParseAndValidate(json!);
    }

    private static void AssertFailure(string? json, string expectedCode)
    {
        PackageBuilderPathConfigurationResult result = Parse(json);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Configuration);
        Assert.Contains(result.Failures, failure => failure.Code == expectedCode);
        Assert.All(result.Failures, failure =>
        {
            Assert.False(string.IsNullOrWhiteSpace(failure.Property));
            Assert.DoesNotContain(
                string.Concat(@"C:\", "Users", @"\"),
                failure.Diagnostic,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                string.Concat(@"C:\", "Windows"),
                failure.Diagnostic,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string ReplaceRoot(string json, string name, string value)
    {
        string oldValueStart = $"\"{name}\": \"";
        int start = json.IndexOf(oldValueStart, StringComparison.Ordinal);
        Assert.True(start >= 0);
        int valueStart = start + oldValueStart.Length;
        int valueEnd = json.IndexOf('"', valueStart);
        while (valueEnd > valueStart && json[valueEnd - 1] == '\\')
        {
            int slashCount = 0;
            for (int index = valueEnd - 1; index >= valueStart && json[index] == '\\'; index--)
            {
                slashCount++;
            }

            if (slashCount % 2 == 0)
            {
                break;
            }

            valueEnd = json.IndexOf('"', valueEnd + 1);
        }

        return string.Concat(
            json.AsSpan(0, valueStart),
            JsonSerializer.Serialize(value).AsSpan(1, JsonSerializer.Serialize(value).Length - 2),
            json.AsSpan(valueEnd));
    }

    private sealed class StubReader(ConfigurationReadResult result) : IConfigurationTextReader
    {
        public string? RequestedPath { get; private set; }

        public int MaximumBytes { get; private set; }

        public ConfigurationReadResult Read(string configurationFilePath, int maximumBytes)
        {
            RequestedPath = configurationFilePath;
            MaximumBytes = maximumBytes;
            return result;
        }
    }

    private sealed class AcceptingInspector : IReparsePointInspector
    {
        public ConfigurationFailure? Inspect(
            string projectRoot,
            string configuredRoot,
            string logicalProperty)
        {
            Assert.Equal(
                PackageBuilderPathConfigurationLoader.ApprovedProjectRoot,
                projectRoot,
                ignoreCase: true);
            return null;
        }
    }

    private sealed class RejectingInspector(params PathRootKind[] rejected) : IReparsePointInspector
    {
        public ConfigurationFailure? Inspect(
            string projectRoot,
            string configuredRoot,
            string logicalProperty)
        {
            return rejected.Any(kind => StringComparer.Ordinal.Equals(kind.ToString(), logicalProperty))
                ? new ConfigurationFailure(
                    "PATH_REPARSE_POINT",
                    logicalProperty,
                    "Configured root crosses a reparse point.")
                : null;
        }
    }
}
