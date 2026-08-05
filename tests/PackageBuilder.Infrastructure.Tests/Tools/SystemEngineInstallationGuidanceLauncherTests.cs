using PackageBuilder.Infrastructure.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

[Trait("Task", "PB-0309")]
public sealed class SystemEngineInstallationGuidanceLauncherTests
{
    [Theory]
    [InlineData("http://www.blender.org/download/")]
    [InlineData("https://example.invalid/download/")]
    [InlineData("https://www.blender.org/download/?unexpected=true")]
    [InlineData("https://docs.unity3d.com/Manual/OtherPage.html")]
    public async Task RejectsEveryNonAllowlistedUriWithoutLaunching(string value)
    {
        var launcher = new SystemEngineInstallationGuidanceLauncher();

        Assert.False(await launcher.OpenAsync(new Uri(value), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancellationPreventsLaunch()
    {
        var launcher = new SystemEngineInstallationGuidanceLauncher();
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.False(await launcher.OpenAsync(new Uri("https://www.blender.org/download/"), source.Token));
    }
}
