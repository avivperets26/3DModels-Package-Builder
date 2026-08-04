using PackageBuilder.App.Wpf.Navigation;

namespace PackageBuilder.App.Wpf.Tests.Navigation;

public sealed class ShellNavigationServiceTests
{
    [Fact]
    public void NavigationStartsAtOverviewAndMovesByExactRoute()
    {
        var navigation = new ShellNavigationService(ShellModuleCatalog.Create());
        var changed = new List<string?>();
        navigation.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        Assert.Equal("overview", navigation.CurrentModule.Route);
        Assert.True(navigation.Navigate("build-queue"));
        Assert.Equal("build-queue", navigation.CurrentModule.Route);
        Assert.Contains(nameof(ShellNavigationService.CurrentModule), changed);
        Assert.True(navigation.Navigate("build-queue"));
        _ = Assert.Single(changed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("BUILD-QUEUE")]
    [InlineData("unknown")]
    public void UnknownOrMalformedRoutesDoNotChangeSelection(string? route)
    {
        var navigation = new ShellNavigationService(ShellModuleCatalog.Create());

        Assert.False(navigation.Navigate(route!));
        Assert.Equal("overview", navigation.CurrentModule.Route);
    }

    [Fact]
    public void ConstructorRejectsMissingNullAndDuplicateModules()
    {
        ShellModuleViewModel module = ShellModuleCatalog.Create()[0];

        _ = Assert.Throws<ArgumentNullException>(() => new ShellNavigationService(null!));
        _ = Assert.Throws<ArgumentException>(() => new ShellNavigationService([]));
        _ = Assert.Throws<ArgumentException>(() => new ShellNavigationService([module, null!]));
        _ = Assert.Throws<ArgumentException>(() => new ShellNavigationService([module, module]));
    }
}
