using PackageBuilder.App.Wpf.Navigation;

namespace PackageBuilder.App.Wpf.Tests.Navigation;

public sealed class ShellModuleCatalogTests
{
    [Fact]
    public void CatalogProvidesFiveUniqueHonestDestinations()
    {
        IReadOnlyList<ShellModuleViewModel> modules = ShellModuleCatalog.Create();

        Assert.Equal(5, modules.Count);
        Assert.Equal(
            ["overview", "products", "build-queue", "validation", "settings"],
            modules.Select(module => module.Route));
        Assert.Equal(5, modules.Select(module => module.Route).Distinct(StringComparer.Ordinal).Count());
        Assert.All(modules, module =>
        {
            Assert.StartsWith("_", module.AccessKeyLabel);
            Assert.Equal(3, module.Highlights.Count);
            Assert.False(string.IsNullOrWhiteSpace(module.NextStep));
            Assert.False(string.IsNullOrWhiteSpace(module.Readiness));
        });
        Assert.Contains("not active", modules[1].Description, StringComparison.Ordinal);
        Assert.Contains("arrive", modules[2].Description, StringComparison.Ordinal);
        Assert.Contains("read-only placeholders", modules[4].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleCopiesInputsAndRejectsInvalidValues()
    {
        var highlights = new List<ShellHighlight> { new("Label", "Value", "Detail") };
        var module = new ShellModuleViewModel(
            "route",
            "Label",
            "_Label",
            "L",
            "EYEBROW",
            "Title",
            "Description",
            "Ready",
            "Next",
            highlights);
        highlights.Clear();

        _ = Assert.Single(module.Highlights);
        _ = Assert.Throws<ArgumentException>(() => new ShellModuleViewModel(
            "", "Label", "_Label", "L", "EYEBROW", "Title", "Description", "Ready", "Next", []));
        _ = Assert.Throws<ArgumentNullException>(() => new ShellModuleViewModel(
            "route", "Label", "_Label", "L", "EYEBROW", "Title", "Description", "Ready", "Next", null!));
    }
}
