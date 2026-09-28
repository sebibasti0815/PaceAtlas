using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PaceAtlas.WinUIPrototype;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(unused =>
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                    _ = new App();
                }
                catch (Exception ex)
                {
                    ReportStartupError(ex);
                }
            });
        }
        catch (Exception ex)
        {
            ReportStartupError(ex);
        }
    }

    internal static void ReportStartupError(Exception ex)
    {
        var message = ex.ToString();
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PaceAtlas.WinUIPrototype", "startup-error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{message}\n\n");
            message += $"\n\nFehlerprotokoll: {path}";
        }
        catch (Exception) { /* Fehleranzeige bleibt auch ohne Protokolldatei möglich. */ }
        MessageBoxW(IntPtr.Zero, message, "PaceAtlas: WinUI-Start fehlgeschlagen", 0x10);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hwnd, string text, string caption, uint type);
}
