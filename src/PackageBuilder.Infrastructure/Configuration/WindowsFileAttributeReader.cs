namespace PackageBuilder.Infrastructure.Configuration;

public sealed class WindowsFileAttributeReader : IFileAttributeReader
{
    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);
}
