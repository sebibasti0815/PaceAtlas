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
    private sealed class ReminderSettings
    {
        public int IntervalMinutes { get; set; }
        public bool CustomInterval { get; set; }
    }

    private static string ReminderSettingsPath => System.IO.Path.Combine(Store.Folder, "state-reminder.json");
    private readonly DispatcherTimer reminderClock = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly DispatcherTimer reminderDismiss = new() { Interval = TimeSpan.FromSeconds(20) };
    private Window? reminderPopup;
    private DateTimeOffset nextReminder;
    private int reminderMinutes;
    private bool reminderCustom;

    private void InitializeReminder()
    {
        try
        {
            if (File.Exists(ReminderSettingsPath))
            {
                var saved = JsonSerializer.Deserialize<ReminderSettings>(File.ReadAllText(ReminderSettingsPath));
                reminderMinutes = Math.Clamp(saved?.IntervalMinutes ?? 0, 0, 1440);
                reminderCustom = reminderMinutes > 0 && (saved?.CustomInterval == true ||
                    reminderMinutes is not (15 or 30 or 60 or 120));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            reminderMinutes = 0;
        }
        reminderClock.Tick += (_, _) => TickReminder();
        reminderDismiss.Tick += (_, _) => CloseReminderPopup();
        ScheduleReminder();
    }

    private void SetReminderInterval(int minutes, bool custom)
    {
        reminderMinutes = Math.Clamp(minutes, 0, 1440);
        reminderCustom = reminderMinutes > 0 && custom;
        ScheduleReminder();
        try
        {
            Directory.CreateDirectory(Store.Folder);
            var temporary = ReminderSettingsPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new ReminderSettings
                { IntervalMinutes = reminderMinutes, CustomInterval = reminderCustom }));
            File.Move(temporary, ReminderSettingsPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBoxW(WinRT.Interop.WindowNative.GetWindowHandle(this),
                selectedLanguage == "en" ? "Could not save reminder interval: " + ex.Message :
                "Erinnerungsintervall konnte nicht gespeichert werden: " + ex.Message,
                selectedLanguage == "en" ? "Condition reminder" : "Zustandserinnerung", 0x40040);
        }
    }

    private void ScheduleReminder()
    {
        reminderClock.Stop();
        CloseReminderPopup();
        if (reminderMinutes == 0) return;
        nextReminder = DateTimeOffset.UtcNow.AddMinutes(reminderMinutes);
        reminderClock.Start();
    }

    private void TickReminder()
    {
        if (DateTimeOffset.UtcNow < nextReminder || reminderMinutes == 0) return;
        nextReminder = DateTimeOffset.UtcNow.AddMinutes(reminderMinutes);
        if (reminderPopup is not null) return;
        ShowReminderPopup();
    }

    private void ShowReminderPopup()
    {
        bool english = selectedLanguage == "en";
        var title = new TextBlock { Text = english ? "How are you feeling?" : "Wie geht es dir gerade?",
            FontSize = 19, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) };
        var description = new TextBlock { Text = english
                ? "A quick entry helps you notice changes over time."
                : "Ein kurzer Eintrag hilft dir, Veränderungen im Verlauf zu erkennen.",
            TextWrapping = TextWrapping.Wrap, FontSize = 16,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) };
        var record = new Button { Content = english ? "Record condition" : "Zustand erfassen",
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 119, 137)),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), MinWidth = 175,
            MinHeight = 40, HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center };
        record.Click += (_, _) =>
        {
            CloseReminderPopup();
            RestoreFromPacingTray();
            Reset_Click(this, new RoutedEventArgs());
            MainTabs.SelectedItem = StateTab;
            Activate();
        };
        var later = new Button { Content = english ? "Later" : "Später", MinWidth = 100,
            MinHeight = 40, HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center };
        later.Click += (_, _) => CloseReminderPopup();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12,
            Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(record);
        buttons.Children.Add(later);
        var body = new StackPanel { Spacing = 14, Margin = new Thickness(22, 20, 18, 28) };
        body.Children.Add(title);
        body.Children.Add(description);
        body.Children.Add(buttons);
        var panel = new Grid { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 31, 43, 62)) };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.Children.Add(new Border { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 55, 181, 188)) });
        Grid.SetColumn(body, 1);
        panel.Children.Add(body);
        var popup = new Window { Content = panel, Title = english ? "Condition reminder" : "Zustandserinnerung" };
        popup.Closed += (_, _) =>
        {
            reminderDismiss.Stop();
            if (ReferenceEquals(reminderPopup, popup)) reminderPopup = null;
        };
        if (popup.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
        }
        popup.AppWindow.IsShownInSwitchers = false;
        var work = PrimaryWorkArea();
        var popupHandle = WinRT.Interop.WindowNative.GetWindowHandle(popup);
        // AppWindow uses physical pixels; XAML content is measured in effective pixels.
        popup.AppWindow.Move(new PointInt32(work.X + 18, work.Y + 18));
        double scale = Math.Max(1, GetDpiForWindow(popupHandle)) / 96.0;
        int width = Math.Min((int)Math.Ceiling(430 * scale), Math.Max(1, work.Width - 36));
        panel.Measure(new Windows.Foundation.Size(width / scale, double.PositiveInfinity));
        int height = Math.Min(Math.Max(1, work.Height - 36),
            Math.Max(230, (int)Math.Ceiling(panel.DesiredSize.Height * scale)));
        popup.AppWindow.MoveAndResize(new RectInt32(work.X + work.Width - width - 18,
            work.Y + 18, width, height));
        reminderPopup = popup;
        // Do not take focus on appearance; a subsequent click may activate the window
        // normally so WinUI can dispatch Button.Click to either action.
        popup.AppWindow.Show(false);
        reminderDismiss.Start();
    }

    private static RectInt32 PrimaryWorkArea()
    {
        RectInt32 work = new(0, 0, 900, 600);
        MonitorEnum callback = (monitor, hdc, rect, data) =>
        {
            var info = new MonitorInfo { cbSize = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfoW(monitor, ref info) || (info.flags & 1) == 0) return true;
            work = new RectInt32(info.work.Left, info.work.Top,
                info.work.Right - info.work.Left, info.work.Bottom - info.work.Top);
            return false;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return work;
    }

    private void CloseReminderPopup()
    {
        reminderDismiss.Stop();
        var popup = reminderPopup;
        reminderPopup = null;
        popup?.Close();
    }

    private async void ConfigureReminderInterval()
    {
        RestoreFromPacingTray();
        bool english = selectedLanguage == "en";
        var minutes = new NumberBox { Minimum = 5, Maximum = 1440, Width = 135,
            Value = Math.Clamp(reminderMinutes == 0 ? 60 : reminderMinutes, 5, 1440) };
        var fields = new StackPanel { Spacing = 12, Width = 390 };
        fields.Children.Add(new TextBlock { Text = english ? "Remind me every (minutes):" : "Erinnere mich alle (Minuten):" });
        fields.Children.Add(minutes);
        fields.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text = english
            ? "You can turn off reminders from the tray menu at any time."
            : "Im Tray-Menü kannst du die Erinnerung jederzeit ausschalten." });
        var dialog = new ContentDialog { Title = english ? "Condition reminder" : "Zustandserinnerung",
            Content = fields, PrimaryButtonText = english ? "Save" : "Speichern",
            CloseButtonText = english ? "Cancel" : "Abbrechen",
            XamlRoot = ((FrameworkElement)Content).XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !double.IsNaN(minutes.Value))
            SetReminderInterval((int)minutes.Value, true);
    }

    private void DisposeReminder()
    {
        reminderClock.Stop();
        CloseReminderPopup();
    }

}
