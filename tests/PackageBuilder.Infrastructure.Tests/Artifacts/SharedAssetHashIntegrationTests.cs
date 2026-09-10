using PackageBuilder.Application.Items;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.Manifests;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Materials;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Textures;
using PackageBuilder.Infrastructure.Artifacts;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

/// <summary>Connects real contained PB-0204 streamed hash receipts to the PB-0802 application planner.</summary>
[Trait("Task", "PB-0802")]
public sealed class SharedAssetHashIntegrationTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task PhysicalSnapshotHashesDetermineReuseWithoutChangingFiles(bool changeByte, int expectedAssets)
    {
        using var workspace = new ArtifactTestWorkspace();
        byte[] firstBytes = new byte[131_073];
        byte[] secondBytes = [.. firstBytes];
        if (changeByte)
        {
            secondBytes[^1] = 1;
        }

        SourceAsset first = SourceAsset.Create(SourceAssetKind.Image, "textures/first.png").Value!;
        SourceAsset second = SourceAsset.Create(SourceAssetKind.Image, "textures/second.png").Value!;
        string firstFile = Path.GetFullPath(workspace.AddFile(first.LogicalReference, firstBytes));
        string secondFile = Path.GetFullPath(workspace.AddFile(second.LogicalReference, secondBytes));
        var service = new ArtifactHashService();
        ArtifactHashOperationResult<ArtifactHashReceipt> firstResult = await service.HashAsync(new(workspace.ProjectRoot, firstFile,
            BuildArtifactId.Create("FirstImage").Value!), TestContext.Current.CancellationToken);
        ArtifactHashOperationResult<ArtifactHashReceipt> secondResult = await service.HashAsync(new(workspace.ProjectRoot, secondFile,
            BuildArtifactId.Create("SecondImage").Value!), TestContext.Current.CancellationToken);
        Assert.True(firstResult.IsSuccess, string.Join(", ", firstResult.Failures.Select(failure => failure.Code)));
        Assert.True(secondResult.IsSuccess, string.Join(", ", secondResult.Failures.Select(failure => failure.Code)));
        ArtifactHashReceipt firstHash = firstResult.Value!;
        ArtifactHashReceipt secondHash = secondResult.Value!;
        string json = await File.ReadAllTextAsync(Path.Combine(workspace.ProjectRoot,
            "tests", "fixtures", "manifests", "valid", "item-collection.json"), TestContext.Current.CancellationToken);
        ProductManifest draft = ProductManifestJson.Deserialize(json).Value!;
        ProductManifest manifest = ProductManifest.Create(draft.SchemaVersion, draft.PublisherProfileReference,
            draft.DisplayName, draft.AssetId, draft.FolderName, draft.ProductCase, draft.Version, draft.Targets,
            [.. draft.SourceAssets, first, second], [Material("FirstMaterial", first), Material("SecondMaterial", second)],
            draft.Rig, draft.Animations, draft.ItemSet, draft.ItemCollection,
            itemSourceAssignments: [new(draft.ItemCollection!.Items[0].Id, draft.SourceAssets[0].LogicalReference)]).Value!;

        SharedAssetReuseResult result = SharedAssetDeduplicator.Plan(manifest,
            [new(first, firstHash.ContentIdentity), new(second, secondHash.ContentIdentity)], TestContext.Current.CancellationToken);

        Assert.Empty(result.Findings);
        SharedAssetReusePlan plan = Assert.IsType<SharedAssetReusePlan>(result.Plan);
        Assert.Equal(expectedAssets, plan.Textures.Select(alias => alias.Canonical).Distinct().Count());
        Assert.Equal(expectedAssets, plan.Materials.Select(alias => alias.Canonical).Distinct().Count());
        Assert.Equal(firstHash.ContentIdentity, plan.SourceIdentities[0].Content);
        Assert.Equal(secondHash.ContentIdentity, plan.SourceIdentities[1].Content);
        Assert.Equal(firstBytes, await File.ReadAllBytesAsync(firstFile, TestContext.Current.CancellationToken));
        Assert.Equal(secondBytes, await File.ReadAllBytesAsync(secondFile, TestContext.Current.CancellationToken));
    }

    private static ManifestMaterial Material(string id, SourceAsset source) => ManifestMaterial.Create(
        InternalAssetId.Create(id).Value!, MaterialDefinition.Create(0, 1, 1,
            EmissionProperties.Create(0, 0, 0, 0).Value!, 1, 0, 1, SurfaceMode.Opaque, null,
            UvTransform.Create(1, 1, 0, 0).Value!, false,
            [TextureAssignment.Create(source, TextureRole.Albedo, ColourSpace.Srgb).Value!]).Value!).Value!;
}
