using System.IO.Compression;
using System.Net;
using PackageBuilder.Application.Documentation;
using PackageBuilder.Domain.Manifests;

namespace PackageBuilder.Targets.Portable.Tests;

public sealed class SharedPortableReadmeTests
{
    [Fact]
    public void SharedTemplateProducesTheExistingPackagablePortableDocument()
    {
        ProductManifest manifest = PortableReadmeTestValues.Manifest(PackageBuilder.Domain.Products.ProductCase.Static);
        ReadmeBuildData? build = ReadmeBuildData.Create(PackageBuilder.Domain.Targets.BuildTarget.Portable,
            [new("SilverwingTalonbow.fbx", "FBX", "7.4")],
            new(1.25, 2.5, 0.75, 456, 0, 0, 1, "+Y", "-Z", "Base centre"),
            null, [], ["Import the FBX."]).Value;
        DocumentationResult<PortableReadmeDocument> result = PortableReadmeGenerator.Generate(manifest, PortableReadmeTestValues.Publisher(), build,
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.Error);
        PortableReadmeDocument document = Assert.IsType<PortableReadmeDocument>(result.Value);
        // In-memory archive round trip checks the emitted UTF-8 bytes without disposable packages on disk.
        using var bytes = new MemoryStream();
        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            using Stream entry = archive.CreateEntry("README.md").Open();
            entry.Write(document.Utf8Bytes.ToArray());
        }

        bytes.Position = 0;
        using var reopened = new ZipArchive(bytes, ZipArchiveMode.Read);
        using var reader = new StreamReader(reopened.GetEntry("README.md")!.Open(), new System.Text.UTF8Encoding(false, true));
        string text = reader.ReadToEnd();
        Assert.Equal(document.Text, text);
        Assert.Contains("Triangles: 456", text);
        Assert.Contains("SilverwingTalonbow.fbx", WebUtility.HtmlDecode(text));
    }
}
