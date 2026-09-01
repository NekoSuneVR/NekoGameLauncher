using NekoGameLauncher.Services;
using System.Windows;
using System.Windows.Threading;

namespace NekoGameLauncher;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) CrashLogService.Write("AppDomain.UnhandledException", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLogService.Write("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            var log = CrashLogService.Write("App.OnStartup", ex);
            MessageBox.Show(
                "Neko Game Launcher could not start.\n\n" + ex.Message +
                (string.IsNullOrWhiteSpace(log) ? string.Empty : $"\n\nCrash log:\n{log}"),
                "Neko Game Launcher startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var log = CrashLogService.Write("DispatcherUnhandledException", e.Exception);
        MessageBox.Show(
            "Neko Game Launcher hit an unexpected error instead of closing silently.\n\n" + e.Exception.Message +
            (string.IsNullOrWhiteSpace(log) ? string.Empty : $"\n\nCrash log:\n{log}"),
            "Neko Game Launcher error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
