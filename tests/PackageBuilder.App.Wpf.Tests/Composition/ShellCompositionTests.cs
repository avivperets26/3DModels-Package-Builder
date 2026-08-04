using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PackageBuilder.App.Wpf.Composition;
using PackageBuilder.App.Wpf.Navigation;
using PackageBuilder.App.Wpf.ViewModels;

namespace PackageBuilder.App.Wpf.Tests.Composition;

public sealed class ShellCompositionTests
{
    [Fact]
    public void ShellServicesResolveWithExpectedLifetimes()
    {
        var services = new ServiceCollection();

        IServiceCollection returned = services.AddPackageBuilderShell();
        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.Same(services, returned);
        Assert.Same(
            provider.GetRequiredService<IShellNavigationService>(),
            provider.GetRequiredService<IShellNavigationService>());
        Assert.Same(
            provider.GetRequiredService<MainWindowViewModel>(),
            provider.GetRequiredService<MainWindowViewModel>());
        Assert.Equal(5, provider.GetRequiredService<IReadOnlyList<ShellModuleViewModel>>().Count);
        _ = Assert.Throws<ArgumentNullException>(() =>
            ShellServiceCollectionExtensions.AddPackageBuilderShell(null!));
    }

    [Fact]
    public async Task HostFactoryBuildsAndStartsWithoutExternalServices()
    {
        using IHost host = ShellHostFactory.Create();

        host.Start();

        Assert.NotNull(host.Services.GetRequiredService<MainWindowViewModel>());
        await host.StopAsync(TestContext.Current.CancellationToken);
    }
}
