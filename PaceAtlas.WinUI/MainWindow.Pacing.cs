using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private readonly Microsoft.UI.Xaml.DispatcherTimer pacingClock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<(Window Window, TextBlock Countdown)> pacingOverlays = new();
    private PacingTrayIcon? pacingTray;
    private bool pacingRunning, pacingResting, settingPacingControls;
    private bool hiddenToTray, restoringFromTray, startInTray, wasMaximized;
    private int activeMinutes = 60, pauseMinutes = 30;
    private DateTimeOffset pacingDeadline;
    private static string TimerSettingsPath => Path.Combine(Store.Folder, "pacing-timer.json");
    private static string TraySettingsPath => Path.Combine(WinUiSettingsFolder, "tray-settings.json");

    private void InitializePacing()
    {
        settingPacingControls = true;
        try
        {
            if (File.Exists(TimerSettingsPath))
            {
                using var settings = JsonDocument.Parse(File.ReadAllText(TimerSettingsPath));
                activeMinutes = Math.Clamp(settings.RootElement.GetProperty("ActiveMinutes").GetInt32(), 2, 300);
                pauseMinutes = Math.Clamp(settings.RootElement.GetProperty("PauseMinutes").GetInt32(), 1, 120);
            }
        }
        catch (Exception) { activeMinutes = 60; pauseMinutes = 30; }
        PacingActiveMinutes.Value = activeMinutes;
        PacingPauseMinutes.Value = pauseMinutes;
        settingPacingControls = false;
        pacingClock.Tick += (_, _) => TickPacing();
        UpdatePacingDisplay();

        try
        {
            if (File.Exists(TraySettingsPath))
                startInTray = JsonSerializer.Deserialize<TraySettings>(File.ReadAllText(TraySettingsPath))?.StartInTray == true;
            else
            {
                var previous = Path.Combine(Store.Folder, "window.json");
                if (File.Exists(previous))
                    startInTray = JsonDocument.Parse(File.ReadAllText(previous)).RootElement
                        .TryGetProperty("StartInTray", out var value) && value.GetBoolean();
            }
        }
        catch (Exception) { startInTray = false; }

        try
        {
            var iconFile = Path.Combine(WinUiSettingsFolder, "pacing-tray.ico");
            Directory.CreateDirectory(WinUiSettingsFolder);
            using (var icon = typeof(MainWindow).Assembly.GetManifestResourceStream("PaceAtlas.WinUI.app.ico"))
            {
                if (icon is not null)
                {
                    using var output = File.Create(iconFile);
                    icon.CopyTo(output);
                }
            }
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            pacingTray = new PacingTrayIcon(hwnd, iconFile, () => pacingRunning, () => pacingResting,
                () => startInTray, () => selectedLanguage, RestoreFromPacingTray, StartPacing, TogglePacingBreak, StopPacing,
                () => { startInTray = !startInTray; SaveTraySettings(); },
                () => Close(),
                () =>
                {
                    if (AppWindow.Presenter is OverlappedPresenter presenter &&
                        presenter.State != OverlappedPresenterState.Minimized)
                        wasMaximized = presenter.State == OverlappedPresenterState.Maximized;
                    HideToPacingTray();
                },
                ShowAboutFromTray,
                () => reminderMinutes, () => reminderCustom, SetReminderInterval, ConfigureReminderInterval);
        }
        catch (Exception ex) { PacingStatus.Text = N("Tray nicht verfügbar: ", "Tray unavailable: ") + ex.Message; }
        AppWindow.Changed += (_, _) =>
        {
            if (hiddenToTray || restoringFromTray || AppWindow.Presenter is not OverlappedPresenter presenter) return;
            if (presenter.State == OverlappedPresenterState.Maximized) wasMaximized = true;
            else if (presenter.State == OverlappedPresenterState.Restored) wasMaximized = false;
            if (presenter.State == OverlappedPresenterState.Minimized)
                DispatcherQueue.TryEnqueue(HideMinimizedToPacingTray);
        };
        AppWindow.Closing += PacingWindow_Closing;
        if (startInTray && pacingTray is not null)
            (Content as FrameworkElement)!.Loaded += (_, _) => HideToPacingTray();
        Closed += (_, _) =>
        {
            pacingClock.Stop();
            DisposeReminder();
            ClosePacingOverlays();
            pacingTray?.Dispose();
        };
    }

    private sealed class TraySettings { public bool StartInTray { get; set; } }

    private void SaveTraySettings()
    {
        try { SaveJson(TraySettingsPath, new TraySettings { StartInTray = startInTray }); }
        catch (Exception ex) { PacingStatus.Text = N("Tray-Einstellung: ", "Tray setting: ") + ex.Message; }
    }

    private void PacingMinutes_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (settingPacingControls || PacingActiveMinutes is null || PacingPauseMinutes is null ||
            double.IsNaN(PacingActiveMinutes.Value) || double.IsNaN(PacingPauseMinutes.Value)) return;
        activeMinutes = Math.Clamp((int)PacingActiveMinutes.Value, 2, 300);
        pauseMinutes = Math.Clamp((int)PacingPauseMinutes.Value, 1, 120);
        try { SaveJson(TimerSettingsPath, new { ActiveMinutes = activeMinutes, PauseMinutes = pauseMinutes }); }
        catch (Exception ex) { PacingStatus.Text = N("Timer-Einstellungen: ", "Timer settings: ") + ex.Message; }
        // A changed interval applies to the next phase, exactly as in WinForms.
    }

    private void PacingStart_Click(object sender, RoutedEventArgs e)
    {
        if (pacingRunning) StopPacing(); else StartPacing();
    }
    private void PacingBreak_Click(object sender, RoutedEventArgs e) => TogglePacingBreak();
    private void StartPacing()
    {
        if (pacingRunning) return;
        ClosePacingOverlays();
        pacingRunning = true; pacingResting = false;
        pacingDeadline = DateTimeOffset.UtcNow.AddMinutes(activeMinutes);
        pacingClock.Start(); UpdatePacingDisplay();
    }
    private void StopPacing()
    {
        pacingClock.Stop(); ClosePacingOverlays();
        pacingRunning = pacingResting = false;
        UpdatePacingDisplay();
    }
    private void TogglePacingBreak()
    {
        if (!pacingRunning) return;
        if (pacingResting) EndPacingBreak(); else BeginPacingBreak(false);
    }
    private void BeginPacingBreak(bool automatic)
    {
        pacingResting = true;
        pacingDeadline = DateTimeOffset.UtcNow.AddMinutes(pauseMinutes);
        PlayPacingSound("PacingPause.wav");
        if (automatic) ShowPacingOverlays();
        UpdatePacingDisplay();
    }
    private void EndPacingBreak()
    {
        ClosePacingOverlays();
        pacingResting = false;
        pacingDeadline = DateTimeOffset.UtcNow.AddMinutes(activeMinutes);
        PlayPacingSound("PacingResume.wav");
        UpdatePacingDisplay();
    }
    private void TickPacing()
    {
        if (!pacingRunning) return;
        if (DateTimeOffset.UtcNow >= pacingDeadline)
        {
            if (pacingResting)
            {
                EndPacingBreak();
                MessageBoxW(WinRT.Interop.WindowNative.GetWindowHandle(this),
                    "Pause beendet. Wenn du mehr Ruhe brauchst, nimm sie dir.", "Pacing Timer", 0x40040);
            }
            else BeginPacingBreak(true);
        }
        UpdatePacingDisplay();
    }

    private void PlayPacingSound(string name)
    {
        try
        {
            var path = Path.Combine(WinUiSettingsFolder, name);
            if (!File.Exists(path))
            {
                using var source = typeof(MainWindow).Assembly.GetManifestResourceStream("PaceAtlas.Resources." + name);
                if (source is null) return;
                using var output = File.Create(path);
                source.CopyTo(output);
            }
            PlaySoundW(path, IntPtr.Zero, 0x20001); // SND_FILENAME | SND_ASYNC
        }
        catch (Exception) { MessageBeep(0xFFFFFFFF); }
    }

    private void ShowPacingOverlays()
    {
        ClosePacingOverlays();
        foreach (var bounds in MonitorBounds())
        {
            var heading = new TextBlock { Text = "Zeit für eine Pause", FontSize = 38,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) };
            var countdown = new TextBlock { FontSize = 34,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) };
            var center = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center };
            center.Children.Add(heading); center.Children.Add(countdown);
            var end = new Button { Content = "Pause vorzeitig beenden", Width = 240, Height = 48,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 20, 20) };
            end.Click += (_, _) => EndPacingBreak();
            var root = new Grid { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 48, 64, 86)) };
            root.Children.Add(center); root.Children.Add(end);
            LocalizeTree(root);
            var overlay = new Window { Content = root, Title = "Pacing Timer" };
            if (overlay.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsAlwaysOnTop = true;
            }
            overlay.AppWindow.MoveAndResize(bounds);
            overlay.Activate();
            pacingOverlays.Add((overlay, countdown));
        }
    }

    private void ClosePacingOverlays()
    {
        foreach (var (overlay, _) in pacingOverlays) overlay.Close();
        pacingOverlays.Clear();
    }

    private void UpdatePacingDisplay()
    {
        var seconds = pacingRunning ? Math.Max(0, (int)Math.Ceiling((pacingDeadline - DateTimeOffset.UtcNow).TotalSeconds)) : 0;
        var countdown = $"{seconds / 60:00}:{seconds % 60:00}";
        PacingStatus.Text = !pacingRunning ? (selectedLanguage == "en" ? "Timer off" : "Timer aus") :
            pacingResting ? (selectedLanguage == "en" ? "Break " : "Pause ") + countdown :
            (selectedLanguage == "en" ? "Active " : "Aktiv ") + countdown;
        PacingStatus.FontWeight = pacingRunning ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
        PacingStartButton.Content = pacingRunning ? "■" : "zZz";
        PacingBreakButton.Content = pacingResting ? "▶" : "Ⅱ";
        PacingBreakButton.IsEnabled = pacingRunning;
        ToolTipService.SetToolTip(PacingStatus, !pacingRunning ?
            (selectedLanguage == "en" ? "Pacing timer off" : "Pacing Timer aus") :
            pacingResting ? (selectedLanguage == "en" ? "Break: " : "Pause: ") + countdown :
            (selectedLanguage == "en" ? "Until break: " : "Bis zur Pause: ") + countdown);
        ToolTipService.SetToolTip(PacingStartButton, pacingRunning ? T("Pacing beenden") : T("Pacing starten"));
        ToolTipService.SetToolTip(PacingBreakButton, pacingResting ? T("Pause beenden") : T("Pause machen"));
        foreach (var (_, label) in pacingOverlays) label.Text = countdown;
    }

    private void HideToPacingTray()
    {
        if (hiddenToTray || restoringFromTray || pacingTray is null) return;
        hiddenToTray = true;
        // Hiding the actual HWND removes its taskbar button, including in Release builds.
        HidePacingWindow(WinRT.Interop.WindowNative.GetWindowHandle(this), 0); // SW_HIDE
    }
    private void HideMinimizedToPacingTray()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (restoringFromTray) return;
        if (IsIconic(hwnd) || AppWindow.Presenter is OverlappedPresenter
            { State: OverlappedPresenterState.Minimized })
            HideToPacingTray();
    }
    private void RestoreFromPacingTray()
    {
        if (hiddenToTray)
        {
            restoringFromTray = true;
            try
            {
                hiddenToTray = false;
                AppWindow.Show();
                if (AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Restore();
                    if (wasMaximized) presenter.Maximize();
                }
            }
            finally { restoringFromTray = false; }
        }
        Activate();
    }
    private void PacingWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!pacingRunning) return;
        args.Cancel = MessageBoxW(hiddenToTray ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(this),
            "Der Pacing Timer läuft noch. PaceAtlas wirklich beenden?", "Pacing Timer", 0x40024) != 6;
    }

    private static List<RectInt32> MonitorBounds()
    {
        var result = new List<RectInt32>();
        MonitorEnum callback = (monitor, hdc, rect, data) =>
        {
            var info = new MonitorInfo { cbSize = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfoW(monitor, ref info))
                result.Add(new RectInt32(info.monitor.Left, info.monitor.Top,
                    info.monitor.Right - info.monitor.Left, info.monitor.Bottom - info.monitor.Top));
            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return result;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo { public uint cbSize; public Rect monitor, work; public uint flags; }
    private delegate bool MonitorEnum(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnum callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int MessageBoxW(IntPtr hwnd, string text, string caption, uint type);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidePacingWindow(IntPtr hwnd, int command);
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] private static extern bool PlaySoundW(string name, IntPtr module, uint flags);
    [DllImport("user32.dll")] private static extern bool MessageBeep(uint type);
}
