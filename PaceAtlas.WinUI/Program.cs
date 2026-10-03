using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PaceAtlas.WinUI;

internal static class Program
{
    private const string InstanceName = @"Local\PaceAtlas.WinUI.Instance";
    private static int pendingActivation;
    private static readonly Stopwatch startupClock = new();
    private static LoadingSplash? loadingSplash;
    internal static long StartupElapsedMilliseconds => startupClock.ElapsedMilliseconds;

    internal static void HideLoadingSplash() => Interlocked.Exchange(ref loadingSplash, null)?.Dispose();

    [STAThread]
    private static void Main()
    {
        try
        {
            using var instance = new Mutex(true, InstanceName, out var firstInstance);
            using var activation = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Activate");
            if (!firstInstance)
            {
                activation.Set();
                return;
            }

            startupClock.Start();
            loadingSplash = LoadingSplash.Start();
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(unused =>
            {
                try
                {
                    var dispatcher = DispatcherQueue.GetForCurrentThread();
                    SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(dispatcher));
                    var activationListener = new Thread(() =>
                    {
                        while (true)
                        {
                            activation.WaitOne();
                            Interlocked.Exchange(ref pendingActivation, 1);
                            dispatcher.TryEnqueue(() =>
                            {
                                if (Application.Current is App app && app.ShowFromSecondLaunch())
                                    Interlocked.Exchange(ref pendingActivation, 0);
                            });
                        }
                    }) { IsBackground = true, Name = "PaceAtlas activation listener" };
                    activationListener.Start();
                    _ = new App();
                }
                catch (Exception ex)
                {
                    HideLoadingSplash();
                    ReportStartupError(ex);
                }
            });
        }
        catch (Exception ex)
        {
            HideLoadingSplash();
            ReportStartupError(ex);
        }
        finally { HideLoadingSplash(); }
    }

    internal static bool ConsumePendingActivation() => Interlocked.Exchange(ref pendingActivation, 0) != 0;

    internal static void ReportStartupError(Exception ex)
    {
        var message = ex.ToString();
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PaceAtlas.WinUI", "startup-error.log");
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
