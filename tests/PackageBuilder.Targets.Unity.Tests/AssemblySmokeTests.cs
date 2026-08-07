using System.Reflection;

namespace PackageBuilder.Targets.Unity.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void TargetAssemblyIsDiscoverable()
    {
        Assembly assembly = typeof(PackageBuilder.Targets.Unity.Projects.UnityProjectCloneService).Assembly;

        Assert.Equal(new AssemblyName("PackageBuilder.Targets.Unity").Name, assembly.GetName().Name);
    }
}
