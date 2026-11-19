using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private NativeReminderWindow? reminderPopup;
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
        if (reminderPopup?.IsVisible == true) return;
        ShowReminderPopup();
    }

    private void ShowReminderPopup()
    {
        reminderPopup ??= new NativeReminderWindow(
            () => DispatcherQueue.TryEnqueue(() =>
            {
                RestoreFromPacingTray();
                Reset_Click(this, new RoutedEventArgs());
                MainTabs.SelectedItem = StateTab;
                Activate();
            }),
            TraceReminderInput,
            ex => DispatcherQueue.TryEnqueue(() =>
                PacingStatus.Text = N("Erinnerung nicht verfügbar: ", "Reminder unavailable: ") + ex.Message));
        reminderPopup.Show(selectedLanguage == "en");
    }

    private static void TraceReminderInput(string action)
    {
        try
        {
            if (!File.Exists(Path.Combine(WinUiSettingsFolder, "reminder-input-diagnostics.enabled"))) return;
            File.AppendAllText(Path.Combine(WinUiSettingsFolder, "reminder-input.log"),
                DateTimeOffset.Now.ToString("O") + " " + action + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void CloseReminderPopup() => reminderPopup?.Hide();

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
        var popup = reminderPopup;
        reminderPopup = null;
        popup?.Dispose();
    }

}
