using System.Reflection;

namespace PackageBuilder.App.Wpf.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void ReferencedWpfAssemblyLoadsWithExpectedIdentity()
    {
        var assembly = Assembly.Load(new AssemblyName("PackageBuilder.App.Wpf"));

        Assert.Equal("PackageBuilder.App.Wpf", assembly.GetName().Name);
    }
}
