using System.Text;
using PackageBuilder.Contracts.Configuration;
using PackageBuilder.Infrastructure.Configuration;

namespace PackageBuilder.Infrastructure.Tests.Configuration;

public sealed class FileConfigurationTextReaderTests : IDisposable
{
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
