using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Preview;
using PackageBuilder.Domain.Tests.Items;

namespace PackageBuilder.Domain.Tests.Preview;

internal static class PreviewTestAssertions
{
    public static T AssertSuccess<T>(PreviewPresentationValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid);
        Assert.Equal(PreviewPresentationValidationError.None, result.Error);
        return Assert.IsType<T>(result.Value);
    }

    public static void AssertFailure<T>(
        PreviewPresentationValidationResult<T> result,
        PreviewPresentationValidationError error)
        where T : class
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static PreviewViewDefinition View(
        PreviewViewKind kind,
        PreviewVisibility? visibility = null,
        string? id = null) =>
        AssertSuccess(
            PreviewViewDefinition.Create(
                Id(id ?? DefaultViewId(kind)),
                kind,
                visibility ?? PreviewVisibility.EntireProduct));

    public static PreviewVisibility Selected(string itemId) =>
        AssertSuccess(PreviewVisibility.ForSelectedItem(Id(itemId)));

    public static ItemSetDefinition Set(params string[] itemIds) =>
        ItemTestAssertions.AssertSuccess(
            ItemSetDefinition.Create(
                itemIds.Select(itemId => ItemTestAssertions.Item(itemId)),
                [],
                [],
                null));

    public static ItemCollectionDefinition Collection(params string[] itemIds) =>
        ItemTestAssertions.AssertSuccess(
            ItemCollectionDefinition.Create(
                itemIds.Select(itemId => ItemTestAssertions.Item(itemId)),
                [],
                []));

    public static InternalAssetId Id(string value) => ItemTestAssertions.Id(value);

    private static string DefaultViewId(PreviewViewKind kind)
    {
        string[] parts = kind.CanonicalIdentifier.Split('-');
        return string.Concat(
            parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
