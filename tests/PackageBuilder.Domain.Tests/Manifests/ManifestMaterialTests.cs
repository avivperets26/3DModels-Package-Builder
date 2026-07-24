using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Materials;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Tests.Materials;

namespace PackageBuilder.Domain.Tests.Manifests;

public sealed class ManifestMaterialTests
{
    [Fact]
    public void CreateRetainsTypedIdentityAndMaterial()
    {
        InternalAssetId id = InternalAssetId.Create("Material").Value!;
        MaterialDefinition definition = CreateMaterial();

        ManifestMaterialResult result = ManifestMaterial.Create(id, definition);

        Assert.True(result.IsValid);
        Assert.Equal(ManifestMaterialError.None, result.Error);
        Assert.Same(id, result.Value!.Id);
        Assert.Same(definition, result.Value.Definition);
    }

    [Fact]
    public void CreateRejectsEachMissingValue()
    {
        Assert.Equal(
            ManifestMaterialError.NullId,
            ManifestMaterial.Create(null, CreateMaterial()).Error);
        Assert.Equal(
            ManifestMaterialError.NullDefinition,
            ManifestMaterial.Create(
                InternalAssetId.Create("Material").Value,
                null).Error);
    }

    [Fact]
    public void EqualityAndStableHashIncludeIdentityAndDefinition()
    {
        MaterialDefinition definition = CreateMaterial();
        ManifestMaterial left = ManifestMaterial.Create(
            InternalAssetId.Create("Material").Value,
            definition).Value!;
        ManifestMaterial equal = ManifestMaterial.Create(
            InternalAssetId.Create("Material").Value,
            definition).Value!;
        ManifestMaterial other = ManifestMaterial.Create(
            InternalAssetId.Create("Other").Value,
            definition).Value!;

        Assert.Equal(left, equal);
        Assert.Equal(left.GetHashCode(), equal.GetHashCode());
        Assert.True(left.Equals((object)equal));
        Assert.NotEqual(left, other);
        Assert.False(left.Equals(null));
        Assert.False(left.Equals(new object()));
    }

    internal static MaterialDefinition CreateMaterial() =>
        MaterialTestAssertions.AssertSuccess(
            MaterialDefinition.Create(
                0,
                1,
                1,
                MaterialTestAssertions.AssertSuccess(
                    EmissionProperties.Create(0, 0, 0, 0)),
                1,
                0,
                1,
                SurfaceMode.Opaque,
                null,
                MaterialTestAssertions.AssertSuccess(UvTransform.Create(1, 1, 0, 0)),
                false,
                []));
}
