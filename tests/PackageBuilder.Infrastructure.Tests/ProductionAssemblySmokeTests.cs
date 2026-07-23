using System.Reflection;

namespace PackageBuilder.Infrastructure.Tests;

public sealed class ProductionAssemblySmokeTests
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void ReferencedInfrastructureAssemblyLoadsWithExpectedIdentity()
    {
        var assembly = Assembly.Load(new AssemblyName("PackageBuilder.Infrastructure"));

        Assert.Equal("PackageBuilder.Infrastructure", assembly.GetName().Name);
    }
}
