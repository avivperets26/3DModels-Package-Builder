using System.Reflection;

namespace PackageBuilder.Application.Tests;

public sealed class ProductionAssemblySmokeTests
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void ReferencedApplicationAssemblyLoadsWithExpectedIdentity()
    {
        var assembly = Assembly.Load(new AssemblyName("PackageBuilder.Application"));

        Assert.Equal("PackageBuilder.Application", assembly.GetName().Name);
    }
}
