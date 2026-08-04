using Microsoft.Extensions.DependencyInjection;
using PackageBuilder.App.Wpf.Navigation;
using PackageBuilder.App.Wpf.ViewModels;

namespace PackageBuilder.App.Wpf.Composition;

/// <summary>Registers the presentation shell while leaving application behavior in shared services.</summary>
public static class ShellServiceCollectionExtensions
{
    public static IServiceCollection AddPackageBuilderShell(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddSingleton<IReadOnlyList<ShellModuleViewModel>>(_ => ShellModuleCatalog.Create());
        _ = services.AddSingleton<IShellNavigationService>(provider =>
            new ShellNavigationService(provider.GetRequiredService<IReadOnlyList<ShellModuleViewModel>>()));
        _ = services.AddSingleton<MainWindowViewModel>();
        _ = services.AddTransient<MainWindow>();
        return services;
    }
}
