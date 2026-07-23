using System.Reflection;

namespace PackageBuilder.Contract.Tests;

public sealed class ProductionAssemblySmokeTests
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void ReferencedContractsAssemblyLoadsWithExpectedIdentity()
    {
        var assembly = Assembly.Load(new AssemblyName("PackageBuilder.Contracts"));

        Assert.Equal("PackageBuilder.Contracts", assembly.GetName().Name);
    }
}
