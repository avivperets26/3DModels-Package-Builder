using System.Reflection;

namespace PackageBuilder.Targets.Portable.Tests;

public sealed class PortableAssemblySmokeTests
{
    [Fact]
    [Trait("Category", "Smoke")]
    public void ReferencedPortableTargetAssemblyLoadsWithExpectedIdentity()
    {
        Assembly assembly = typeof(PortableNamingProfile).Assembly;

        Assert.Equal(new AssemblyName("PackageBuilder.Targets.Portable").Name, assembly.GetName().Name);
    }
}
