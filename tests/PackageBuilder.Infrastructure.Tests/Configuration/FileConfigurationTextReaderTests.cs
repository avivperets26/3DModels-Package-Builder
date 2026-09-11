using System.Text;
using PackageBuilder.Application.Documentation;
using PackageBuilder.Contracts.Configuration;
using PackageBuilder.Domain.Profiles;
using PackageBuilder.Infrastructure.Configuration;

namespace PackageBuilder.Infrastructure.Tests.Configuration;

public sealed class FileConfigurationTextReaderTests : IDisposable
{
    [Fact]
    public void Utf8BomIsAcceptedButUtf16CannotOverrideTheUtf8Contract()
    {
        string path = CreateTestFile("encoding.json", "{}");
        File.WriteAllText(path, "{}", new UTF8Encoding(true));
        Assert.Equal("{}", new FileConfigurationTextReader().Read(path, 1024).Content);
        File.WriteAllText(path, "{}", Encoding.Unicode);
        Assert.Equal("CONFIG_ENCODING", new FileConfigurationTextReader().Read(path, 1024).Failure!.Code);
    }

    [Fact]
    public void ResolverLoadsConfigurationThroughRealContainedIo()
    {
        string path = CreateTestFile("publisher.json", """
            {"schemaVersion":1,"root":"TestStudio","displayName":"Test Studio","supportContact":{"kind":"email","value":"support@example.com"},"copyright":{"holder":"Test Studio","yearPolicy":{"kind":"publication-year","year":2026}},"aiDisclosure":{"state":"undeclared"}}
            """);
        var resolver = new PackageBuilder.Application.Documentation.PublisherProfileResolver(
            new FileConfigurationTextReader(), new WindowsReparsePointInspector());
        DocumentationResult<PublisherProfile> result = resolver.Resolve(null, "TestStudio",
            [new("TestStudio", Path.GetRelativePath(PackageBuilder.Application.Configuration.PackageBuilderPathConfigurationLoader.ApprovedProjectRoot, path).Replace('\\', '/'))]);
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("TestStudio", result.Value!.Root.Value);
        Assert.Equal("undeclared", result.Value.AiDisclosure.State.CanonicalIdentifier);
    }

    private readonly string _ownedRoot = Path.Combine(
        @"C:\Dev\PackageBuilder",
        "runtime-data",
        "tests",
        "PB-0201",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ReadsValidUtf8WithoutModifyingTheFile()
    {
        string path = CreateTestFile("valid.json", """{"schemaVersion":1}""");
        DateTime before = File.GetLastWriteTimeUtc(path);
        var reader = new FileConfigurationTextReader();

        ConfigurationReadResult result = reader.Read(path, 1024);

        Assert.True(result.IsSuccess);
        Assert.Equal("""{"schemaVersion":1}""", result.Content);
        Assert.Null(result.Failure);
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void MissingFileReturnsSanitizedFailure()
    {
        var reader = new FileConfigurationTextReader();

        string[] paths =
        [
            Path.Combine(_ownedRoot, Guid.NewGuid().ToString("N"), "missing.json"),
            Path.Combine(CreateTestDirectory(), "missing.json"),
        ];
        foreach (string? path in paths)
        {
            ConfigurationReadResult result = reader.Read(path, 1024);

            Assert.False(result.IsSuccess);
            Assert.Equal("CONFIG_FILE_MISSING", result.Failure!.Code);
            Assert.DoesNotContain(_ownedRoot, result.Failure.Diagnostic, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OversizedFileIsRejectedBeforeParsing()
    {
        string path = CreateTestFile("large.json", new string('x', 20));

        ConfigurationReadResult result = new FileConfigurationTextReader().Read(path, 10);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONFIG_TOO_LARGE", result.Failure!.Code);
    }

    [Fact]
    public void InvalidUtf8IsRejected()
    {
        string directory = CreateTestDirectory();
        string path = Path.Combine(directory, "invalid.json");
        File.WriteAllBytes(path, [0xC3, 0x28]);

        ConfigurationReadResult result = new FileConfigurationTextReader().Read(path, 1024);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONFIG_ENCODING", result.Failure!.Code);
    }

    [Fact]
    public void InvalidArgumentsAreRejected()
    {
        var reader = new FileConfigurationTextReader();
        _ = Assert.Throws<ArgumentException>(() => reader.Read("", 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read("file", 0));
    }

    [Fact]
    public void LockedFileReturnsSanitizedReadFailure()
    {
        string path = CreateTestFile("locked.json", "{}");
        using var lockStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        ConfigurationReadResult result = new FileConfigurationTextReader().Read(path, 1024);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONFIG_READ_FAILED", result.Failure!.Code);
    }

    [Fact]
    public void DirectoryPathReturnsSanitizedReadFailure()
    {
        string directory = CreateTestDirectory();

        ConfigurationReadResult result = new FileConfigurationTextReader().Read(directory, 1024);

        Assert.False(result.IsSuccess);
        Assert.Equal("CONFIG_READ_FAILED", result.Failure!.Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_ownedRoot))
        {
            Directory.Delete(_ownedRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private string CreateTestFile(string name, string content)
    {
        string directory = CreateTestDirectory();
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    private string CreateTestDirectory()
    {
        string directory = Path.Combine(_ownedRoot, Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        return directory;
    }
}
