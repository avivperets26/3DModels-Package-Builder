using PackageBuilder.App.Wpf.ViewModels;

namespace PackageBuilder.App.Wpf.Tests.ViewModels;

public sealed class StartupFailureViewModelTests
{
    [Fact]
    public void FatalExceptionBecomesStableNonSensitiveGuidance()
    {
        const string PrivateDetail = "C:\\Users\\private\\token-secret.txt";

        var result = StartupFailureViewModel.FromException(
            new InvalidOperationException(PrivateDetail));

        Assert.Equal("APP_STARTUP_FAILED", result.ErrorCode);
        Assert.Equal("Package Builder could not start", result.Title);
        Assert.DoesNotContain(PrivateDetail, result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(PrivateDetail, result.Guidance, StringComparison.Ordinal);
        Assert.Contains("No package was built or published", result.Guidance, StringComparison.Ordinal);
        _ = Assert.Throws<ArgumentNullException>(() => StartupFailureViewModel.FromException(null!));
    }
}
