using PackageBuilder.App.Wpf.Navigation;
using PackageBuilder.App.Wpf.ViewModels;

namespace PackageBuilder.App.Wpf.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void ViewModelExposesShellIdentityAndNavigatesThroughSelection()
    {
        var navigation = new ShellNavigationService(ShellModuleCatalog.Create());
        using var viewModel = new MainWindowViewModel(navigation);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        Assert.Equal("Package Builder", viewModel.ApplicationName);
        Assert.Equal("Foundation preview", viewModel.ReleaseLabel);
        Assert.Contains("No uploads", viewModel.PrivacyLabel, StringComparison.Ordinal);
        Assert.Equal(5, viewModel.Modules.Count);
        Assert.Equal("overview", viewModel.CurrentModule.Route);
        Assert.Same(viewModel.CurrentModule, viewModel.SelectedModule);

        viewModel.SelectedModule = viewModel.Modules[3];

        Assert.Equal("validation", viewModel.CurrentModule.Route);
        Assert.Contains(nameof(MainWindowViewModel.CurrentModule), changed);
        Assert.Contains(nameof(MainWindowViewModel.SelectedModule), changed);
        Assert.True(viewModel.Navigate("settings"));
        Assert.False(viewModel.Navigate("Settings"));
    }

    [Fact]
    public void NullSelectionAndDisposalAreSafeAndIdempotent()
    {
        var navigation = new ShellNavigationService(ShellModuleCatalog.Create());
        var viewModel = new MainWindowViewModel(navigation);
        int changes = 0;
        viewModel.PropertyChanged += (_, _) => changes++;

        viewModel.SelectedModule = null!;
        viewModel.Dispose();
        viewModel.Dispose();
        _ = navigation.Navigate("products");

        Assert.Equal(0, changes);
        Assert.Equal("products", navigation.CurrentModule.Route);
        _ = Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(null!));
    }
}
