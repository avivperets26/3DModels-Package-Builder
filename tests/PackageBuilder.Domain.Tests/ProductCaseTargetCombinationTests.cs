using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Domain.Tests;

[Trait("Task", "PB-0102")]
public sealed class ProductCaseTargetCombinationTests
{
    [Fact]
    public void EveryProductCaseAndTargetCombinationIsRepresentable()
    {
        var combinations = new HashSet<(ProductCase ProductCase, BuildTarget Target)>();

        foreach (ProductCase productCase in ProductCase.All)
        {
            foreach (BuildTarget target in BuildTarget.All)
            {
                Assert.True(combinations.Add((productCase, target)));
            }
        }

        Assert.Equal(15, combinations.Count);
        Assert.Contains((ProductCase.ItemSet, BuildTarget.Portable), combinations);
        Assert.Contains((ProductCase.ItemCollection, BuildTarget.Unity), combinations);
        Assert.Contains((ProductCase.RiggedAnimated, BuildTarget.Unreal), combinations);
    }
}
