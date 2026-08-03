using System.Buffers.Binary;
using System.IO.Compression;

namespace PackageBuilder.Infrastructure.Tests.Archives;

internal sealed class ArchiveTestWorkspace : IDisposable
{
    public ArchiveTestWorkspace()
    {
        ProjectRoot = FindProjectRoot();
        Root = Path.Combine(
            ProjectRoot,
            "artifacts",
            "PB-0202",
            "tests",
            Guid.NewGuid().ToString("N"));
        ContainmentRoot = Path.Combine(Root, "staging");
        SourceArchivePath = Path.Combine(Root, "source.zip");
        DestinationRoot = Path.Combine(ContainmentRoot, "extracted");
        _ = Directory.CreateDirectory(ContainmentRoot);
    }

    public string ProjectRoot { get; }

    public string Root { get; }

    public string ContainmentRoot { get; }

    public string SourceArchivePath { get; }

    public string DestinationRoot { get; }

    public void CreateArchive(Action<ZipArchive> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        using FileStream stream = File.Create(SourceArchivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        configure(archive);
    }

    public static ZipArchiveEntry AddFile(
        ZipArchive archive,
        string name,
        string content,
        CompressionLevel compression = CompressionLevel.Optimal)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, compression);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
        return entry;
    }

    public void SetFirstCentralDirectoryUncompressedSize(uint uncompressedSize) => SetFirstCentralDirectorySize(24, uncompressedSize);

    public void SetFirstCentralDirectoryCompressedSize(uint compressedSize) => SetFirstCentralDirectorySize(20, compressedSize);

    private void SetFirstCentralDirectorySize(int fieldOffset, uint size)
    {
        byte[] bytes = File.ReadAllBytes(SourceArchivePath);
        ReadOnlySpan<byte> signature = [0x50, 0x4B, 0x01, 0x02];
        int offset = bytes.AsSpan().IndexOf(signature);
        if (offset < 0)
        {
            throw new InvalidOperationException("ZIP central-directory entry was not found.");
        }

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + fieldOffset, sizeof(uint)), size);
        File.WriteAllBytes(SourceArchivePath, bytes);
    }

    public void Dispose()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        // Test-owned links are removed by their tests before this exact contained tree is deleted.
        Directory.Delete(Root, recursive: true);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PackageBuilder.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Package Builder project root could not be located.");
    }
}
