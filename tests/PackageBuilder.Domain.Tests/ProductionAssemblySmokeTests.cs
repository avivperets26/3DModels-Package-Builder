using System.Reflection;

namespace PackageBuilder.Domain.Tests;

public sealed class ProductionAssemblySmokeTests
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void ReferencedDomainAssemblyLoadsWithExpectedIdentity()
    {
        var assembly = Assembly.Load(new AssemblyName("PackageBuilder.Domain"));

        Assert.Equal("PackageBuilder.Domain", assembly.GetName().Name);
    }
}
