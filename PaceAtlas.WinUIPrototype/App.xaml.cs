using Microsoft.UI.Xaml;

namespace PaceAtlas.WinUIPrototype;

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
        try
        {
            window = new MainWindow();
            window.Activate();
        }
        catch (Exception ex)
        {
            Program.ReportStartupError(ex);
        }
    }
}
