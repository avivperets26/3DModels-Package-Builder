using PackageBuilder.Contracts.Configuration;
using PackageBuilder.Infrastructure.Configuration;

namespace PackageBuilder.Infrastructure.Tests.Configuration;

public sealed class WindowsReparsePointInspectorTests
{
    private const string ProjectRoot = @"C:\Dev\PackageBuilder";
    private const string DataRoot = @"C:\Dev\PackageBuilder\runtime-data";

    [Fact]
    public void ExistingPhysicalAndNonexistentDescendantPathsAreAccepted()
    {
        var inspector = new WindowsReparsePointInspector();
        Assert.Null(inspector.Inspect(ProjectRoot, DataRoot, "Data"));
        Assert.Null(inspector.Inspect(ProjectRoot, ProjectRoot, "Repository"));
        Assert.Null(
            inspector.Inspect(
                ProjectRoot,
                @"C:\Dev\PackageBuilder\runtime-data\does-not-exist\child",
                "Jobs"));
    }

    [Fact]
    public void ExistingReparsePointAndItsNonexistentDescendantAreRejected()
    {
        string testDirectory = Path.Combine(
            ProjectRoot,
            "runtime-data",
            "tests",
            "PB-0201",
            Guid.NewGuid().ToString("N"));
        string link = Path.Combine(testDirectory, "escape-link");
        _ = Directory.CreateDirectory(testDirectory);

        try
        {
            _ = Directory.CreateSymbolicLink(link, Path.GetPathRoot(testDirectory)!);
            var inspector = new WindowsReparsePointInspector();
            ConfigurationFailure? direct = inspector.Inspect(
                ProjectRoot,
                link,
                "Temp");
            ConfigurationFailure? descendant = inspector.Inspect(
                ProjectRoot,
                Path.Combine(link, "nonexistent"),
                "Temp");

            Assert.Equal("PATH_REPARSE_POINT", direct!.Code);
            Assert.Equal("PATH_REPARSE_POINT", descendant!.Code);
            Assert.DoesNotContain(link, direct.Diagnostic, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory);
            }
        }
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        var inspector = new WindowsReparsePointInspector();
        _ = Assert.Throws<ArgumentNullException>(() => new WindowsReparsePointInspector(null!));
        _ = Assert.Throws<ArgumentNullException>(() => inspector.Inspect(null!, DataRoot, "Tools"));
        _ = Assert.Throws<ArgumentNullException>(() => inspector.Inspect(ProjectRoot, null!, "Tools"));
        _ = Assert.Throws<ArgumentNullException>(() => inspector.Inspect(ProjectRoot, DataRoot, null!));
    }

    [Fact]
    public void AttributeInspectionFailureReturnsSanitizedFailure()
    {
        foreach (Exception exception in new Exception[] { new IOException(), new UnauthorizedAccessException() })
        {
            var inspector = new WindowsReparsePointInspector(new ThrowingAttributeReader(exception));
            ConfigurationFailure? result = inspector.Inspect(
                ProjectRoot,
                DataRoot,
                "Data");

            Assert.Equal("PATH_INSPECTION_FAILED", result!.Code);
            Assert.DoesNotContain(
                DataRoot,
                result.Diagnostic,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ExistingFileInConfiguredRootIsRejected()
    {
        string directory = Path.Combine(
            ProjectRoot,
            "runtime-data",
            "tests",
            "PB-0201",
            Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "not-a-directory");
        try
        {
            File.WriteAllText(file, "test");

            ConfigurationFailure? result = new WindowsReparsePointInspector().Inspect(
                ProjectRoot,
                file,
                "Temp");

            Assert.Equal("PATH_NOT_DIRECTORY", result!.Code);
            Assert.DoesNotContain(file, result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory);
            }
        }
    }

    private sealed class ThrowingAttributeReader(Exception exception) : IFileAttributeReader
    {
        public FileAttributes GetAttributes(string path) => throw exception;
    }
}
