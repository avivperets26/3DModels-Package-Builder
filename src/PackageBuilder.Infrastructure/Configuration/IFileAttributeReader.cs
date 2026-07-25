namespace PackageBuilder.Infrastructure.Configuration;

public interface IFileAttributeReader
{
    FileAttributes GetAttributes(string path);
}
