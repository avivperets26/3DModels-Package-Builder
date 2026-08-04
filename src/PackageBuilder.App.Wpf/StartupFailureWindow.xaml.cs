using System.Windows;
using PackageBuilder.App.Wpf.ViewModels;

namespace PackageBuilder.App.Wpf;

public partial class StartupFailureWindow : Window
{
    public StartupFailureWindow(StartupFailureViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
    }
}
