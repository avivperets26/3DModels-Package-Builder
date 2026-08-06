using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Portable.Tests;

internal sealed class PortablePackageTestScenario
{
    private readonly IReadOnlyDictionary<string, byte[]> _content;

    private PortablePackageTestScenario(
        PortableNamingProfile naming,
        PortableFolderLayout layout,
        PortableTextureCopyReceipt textureReceipt,
        PortableReadmeDocument readme,
        IReadOnlyDictionary<string, byte[]> content)
    {
        Naming = naming;
        Layout = layout;
        TextureReceipt = textureReceipt;
        Readme = readme;
        _content = content;
    }

    public PortableNamingProfile Naming { get; }

    public PortableFolderLayout Layout { get; }

    public PortableTextureCopyReceipt TextureReceipt { get; }

    public PortableReadmeDocument Readme { get; }

    public static async Task<PortablePackageTestScenario> CreateAsync(bool includeGlb = true)
    {
        PortableNamingProfile naming = PortableTestValues.Naming();
        ProductManifest manifest = PortableReadmeTestValues.Manifest(ProductCase.Static, withTexture: true);
        TextureAssignment assignment = manifest.Materials[0].Definition.TextureAssignments[0];
        byte[] textureBytes = PortableTestValues.Png(8, 4);
        ArtifactStoreRecord textureRecord = PortableTestValues.RecordForBytes(
            "texture-albedo",
            textureBytes,
            "normalized-texture");
        using var textureSource = new MemoryStream(textureBytes, writable: false);
        using var textureDestination = new MemoryStream();
        PortableTextureCopyReceipt textureReceipt = (await PortableTextureProcessor.CopyAsync(
            textureRecord,
            assignment,
            naming,
            PortableFileExtension.Png,
            PortableFileExtension.Png,
            textureSource,
            textureDestination,
            TestContext.Current.CancellationToken)).Receipt!;
        PortableReadmeResult<PortableReadmeRequest> requestResult = PortableReadmeRequest.Create(
            manifest,
            PortableReadmeTestValues.Publisher(),
            naming,
            PortableModelDimensions.Create(1.25, 2.5, 0.75).Value,
            includeGlb ? PortableGlbVariant.Standard : null,
            [textureReceipt],
            ["Import the FBX or GLB into your preferred engine."]);
        Assert.True(requestResult.IsValid, requestResult.Error.ToString());
        PortableReadmeDocument readme = PortableReadmeGenerator.Generate(requestResult.Value).Value!;

        byte[] fbxBytes = "FBX deterministic fixture"u8.ToArray();
        byte[] readmeBytes = [.. readme.Utf8Bytes];
        byte[] glbBytes = "glTF deterministic fixture"u8.ToArray();
        var artifacts = new List<PortableCompositionArtifact>
        {
            Artifact("fbx", fbxBytes, PortableArtifactPurpose.Fbx),
            Artifact("archive", [1], PortableArtifactPurpose.FbxArchive),
            Artifact("flat-readme", readmeBytes, PortableArtifactPurpose.FlatReadme),
            Artifact("product-readme", readmeBytes, PortableArtifactPurpose.ProductReadme),
            Artifact(
                "texture-albedo",
                textureBytes,
                PortableArtifactPurpose.Texture,
                TextureRole.Albedo,
                PortableFileExtension.Png),
        };
        if (includeGlb)
        {
            artifacts.Add(Artifact(
                "glb",
                glbBytes,
                PortableArtifactPurpose.Glb,
                glbVariant: PortableGlbVariant.Standard));
        }

        PortableFolderLayout layout = PortableFolderComposer.Compose(naming, artifacts).Value!;
        var content = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["fbx"] = fbxBytes,
            ["flat-readme"] = readmeBytes,
            ["texture-albedo"] = textureBytes,
        };
        return new PortablePackageTestScenario(naming, layout, textureReceipt, readme, content);
    }

    public IReadOnlyList<PortableFbxArchiveSource> Sources(bool reverse = false)
    {
        IEnumerable<PortableFolderEntry> entries = reverse ? Layout.FlatFbxEntries.Reverse() : Layout.FlatFbxEntries;
        return
        [
            .. entries.Select(entry => new PortableFbxArchiveSource(
                entry.Source,
                new MemoryStream(_content[entry.Source.Artifact.Id.Value], writable: false))),
        ];
    }

    public async Task<(MemoryStream Stream, PortableFbxArchiveReceipt Receipt)> BuildAsync()
    {
        var stream = new MemoryStream();
        PortableFbxArchiveSource[] sources = [.. Sources()];
        PortableFbxArchiveResult result = await PortableFbxArchiveBuilder.CreateAsync(
            Layout,
            sources,
            stream,
            TestContext.Current.CancellationToken);
        foreach (PortableFbxArchiveSource source in sources)
        {
            source.Stream.Dispose();
        }

        return (stream, Assert.IsType<PortableFbxArchiveReceipt>(result.Receipt));
    }

    public IReadOnlyList<PortableAssetReferenceEvidence> References() =>
    [
        new(Naming.FbxFileName, [TextureReceipt.FileName]),
        .. Layout.ProductEntries.Where(static entry => entry.RelativePath.EndsWith(".glb", StringComparison.Ordinal))
            .Select(static entry => new PortableAssetReferenceEvidence(entry.RelativePath, [])),
    ];

    public IReadOnlyList<PortableReimportEvidence> Reimports() =>
    [
        new(Naming.FbxFileName, true),
        .. Layout.ProductEntries.Where(static entry => entry.RelativePath.EndsWith(".glb", StringComparison.Ordinal))
            .Select(static entry => new PortableReimportEvidence(entry.RelativePath, true)),
    ];

    private static PortableCompositionArtifact Artifact(
        string id,
        byte[] bytes,
        PortableArtifactPurpose purpose,
        TextureRole? textureRole = null,
        PortableFileExtension? extension = null,
        PortableGlbVariant? glbVariant = null) =>
        PortableCompositionArtifact.Create(
            PortableTestValues.RecordForBytes(id, bytes, PortableTestValues.RoleFor(purpose)),
            purpose,
            textureRole,
            extension: extension,
            glbVariant: glbVariant).Value!;
}
