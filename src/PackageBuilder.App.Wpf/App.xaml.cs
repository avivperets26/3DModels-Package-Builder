using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PackageBuilder.App.Wpf.Composition;
using PackageBuilder.App.Wpf.ViewModels;

namespace PackageBuilder.App.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    /// <summary>Composes the local host and displays a safe fatal-startup surface on failure.</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _host = ShellHostFactory.Create();
            _host.Start();
            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
        catch (Exception exception)
        {
            _host?.Dispose();
            _host = null;
            var failureWindow = new StartupFailureWindow(StartupFailureViewModel.FromException(exception));
            _ = failureWindow.ShowDialog();
            Shutdown(-1);
        }
    }

    /// <summary>Stops and disposes the host when the desktop lifetime ends.</summary>
    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
