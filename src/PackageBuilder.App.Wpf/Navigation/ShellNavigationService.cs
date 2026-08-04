using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PackageBuilder.App.Wpf.Navigation;

/// <summary>Provides deterministic, case-sensitive module navigation for the desktop shell.</summary>
public sealed class ShellNavigationService : ObservableObject, IShellNavigationService
{
    private readonly ReadOnlyDictionary<string, ShellModuleViewModel> _modulesByRoute;
    private ShellModuleViewModel _currentModule;

    public ShellNavigationService(IEnumerable<ShellModuleViewModel> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ShellModuleViewModel[] snapshot = [.. modules];
        if (snapshot.Length == 0)
        {
            throw new ArgumentException("At least one shell module is required.", nameof(modules));
        }

        if (snapshot.Any(module => module is null))
        {
            throw new ArgumentException("Shell modules cannot contain null values.", nameof(modules));
        }

        var byRoute = new Dictionary<string, ShellModuleViewModel>(StringComparer.Ordinal);
        foreach (ShellModuleViewModel module in snapshot)
        {
            if (!byRoute.TryAdd(module.Route, module))
            {
                throw new ArgumentException("Shell module routes must be unique.", nameof(modules));
            }
        }

        Modules = new ReadOnlyCollection<ShellModuleViewModel>(snapshot);
        _modulesByRoute = new ReadOnlyDictionary<string, ShellModuleViewModel>(byRoute);
        _currentModule = snapshot[0];
    }

    public IReadOnlyList<ShellModuleViewModel> Modules { get; }

    public ShellModuleViewModel CurrentModule
    {
        get => _currentModule;
        private set => SetProperty(ref _currentModule, value);
    }

    public bool Navigate(string route)
    {
        if (string.IsNullOrWhiteSpace(route) || !_modulesByRoute.TryGetValue(route, out ShellModuleViewModel? module))
        {
            return false;
        }

        CurrentModule = module;
        return true;
    }
}
