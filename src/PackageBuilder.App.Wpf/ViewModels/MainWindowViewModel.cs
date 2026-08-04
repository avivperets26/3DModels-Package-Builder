using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PackageBuilder.App.Wpf.Navigation;

namespace PackageBuilder.App.Wpf.ViewModels;

/// <summary>Exposes shell navigation and honest product status without containing build logic.</summary>
public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IShellNavigationService _navigation;
    private bool _disposed;

    public MainWindowViewModel(IShellNavigationService navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _navigation.PropertyChanged += NavigationPropertyChanged;
    }

    public string ApplicationName { get; } = "Package Builder";

    public string ReleaseLabel { get; } = "Foundation preview";

    public string PrivacyLabel { get; } = "Local-first · No uploads";

    public IReadOnlyList<ShellModuleViewModel> Modules => _navigation.Modules;

    public ShellModuleViewModel CurrentModule => _navigation.CurrentModule;

    public ShellModuleViewModel SelectedModule
    {
        get => _navigation.CurrentModule;
        set
        {
            if (value is not null)
            {
                _ = _navigation.Navigate(value.Route);
            }
        }
    }

    public bool Navigate(string route) => _navigation.Navigate(route);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _navigation.PropertyChanged -= NavigationPropertyChanged;
        _disposed = true;
    }

    private void NavigationPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (string.Equals(eventArgs.PropertyName, nameof(IShellNavigationService.CurrentModule), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(CurrentModule));
            OnPropertyChanged(nameof(SelectedModule));
        }
    }
}
