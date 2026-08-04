namespace PackageBuilder.App.Wpf.ViewModels;

/// <summary>Converts fatal startup exceptions into a stable, non-sensitive user-facing state.</summary>
public sealed class StartupFailureViewModel
{
    private StartupFailureViewModel()
    {
    }

    public string ErrorCode { get; } = "APP_STARTUP_FAILED";

    public string Title { get; } = "Package Builder could not start";

    public string Message { get; } =
        "The application could not compose its required services. Close it, review the local logs, and try again.";

    public string Guidance { get; } =
        "No package was built or published. If the problem continues, keep the error code for troubleshooting.";

    public static StartupFailureViewModel FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new StartupFailureViewModel();
    }
}
