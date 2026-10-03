using System.Diagnostics;
using Microsoft.UI.Xaml;

namespace PaceAtlas.WinUI;

public partial class App : Application
{
    private Window? window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            Program.ReportStartupError(args.Exception);
            args.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var startup = Stopwatch.StartNew();
        try
        {
            var mainWindow = new MainWindow();
            window = mainWindow;
            var constructionMs = startup.ElapsedMilliseconds;
            window.Activate();
            if (Program.ConsumePendingActivation())
                mainWindow.ShowFromSecondLaunch();
            var totalMs = Program.StartupElapsedMilliseconds;
            if (totalMs >= 500)
            {
                var details = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} · {totalMs} ms insgesamt, " +
                    $"{totalMs - startup.ElapsedMilliseconds} ms vor OnLaunched, " +
                    $"{constructionMs - mainWindow.StartupConstructorMs} ms vor Fensterkonstruktor (u. a. Datenbank), " +
                    $"{mainWindow.StartupConstructorMs} ms Fensteraufbau ({mainWindow.StartupTiming}), " +
                    $"{startup.ElapsedMilliseconds - constructionMs} ms Aktivierung{Environment.NewLine}";
                _ = Task.Run(() =>
                {
                    try
                    {
                        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "PaceAtlas.WinUI", "startup-timing.log");
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.AppendAllText(path, details);
                    }
                    catch (Exception) { /* Timing is diagnostic only. */ }
                });
            }
        }
        catch (Exception ex)
        {
            Program.ReportStartupError(ex);
        }
        finally { Program.HideLoadingSplash(); }
    }

    internal bool ShowFromSecondLaunch()
    {
        if (window is not MainWindow mainWindow) return false;
        mainWindow.ShowFromSecondLaunch();
        return true;
    }
}
