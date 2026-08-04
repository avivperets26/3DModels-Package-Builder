using System.ComponentModel;

namespace PackageBuilder.App.Wpf.Navigation;

/// <summary>Owns shell selection while keeping navigation independent from WPF controls.</summary>
public interface IShellNavigationService : INotifyPropertyChanged
{
    IReadOnlyList<ShellModuleViewModel> Modules { get; }

    ShellModuleViewModel CurrentModule { get; }

    bool Navigate(string route);
}
