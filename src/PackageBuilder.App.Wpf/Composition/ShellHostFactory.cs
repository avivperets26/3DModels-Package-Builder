using Microsoft.Extensions.Hosting;

namespace PackageBuilder.App.Wpf.Composition;

/// <summary>Creates the minimal local host used as the WPF composition and lifetime boundary.</summary>
public static class ShellHostFactory
{
    public static IHost Create() =>
        new HostBuilder()
            .ConfigureServices(services => services.AddPackageBuilderShell())
            .Build();
}
