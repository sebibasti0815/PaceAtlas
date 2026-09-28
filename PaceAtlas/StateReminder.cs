using System.Text.Json;

namespace PaceAtlas;

internal sealed class StateReminder : IDisposable
{
    private sealed class ReminderSettings
    {
        public ReminderSettings() { }
        public int IntervalMinutes { get; set; }
        public bool CustomInterval { get; set; }
    }

    private sealed class ReminderPopup : Form
    {
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;
        private readonly System.Windows.Forms.Timer dismiss = new() { Interval = 20000 };

        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WsExNoActivate | WsExToolWindow;
                return parameters;
            }
        }

        public ReminderPopup(bool german, Action openEntry)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(540, 230);
            Opacity = 0.86;
            BackColor = Color.FromArgb(35, 48, 67);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10);
            var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromPoint(Cursor.Position).WorkingArea;
            if (Width > area.Width - 36) Width = Math.Max(320, area.Width - 36);
            Location = new Point(area.Right - Width - 18, area.Top + 18);

            var accent = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = Color.FromArgb(55, 181, 188) };
            var title = new Label { Text = german ? "Wie geht es dir gerade?" : "How are you feeling?",
                Font = new Font(Font, FontStyle.Bold), Location = new Point(23, 20),
                Size = new Size(Width - 48, 36) };
            var description = new Label { Text = german
                ? "Ein kurzer Eintrag hilft dir, Veränderungen im Verlauf zu erkennen."
                : "A quick entry helps you notice changes over time.",
                Location = new Point(23, 63), Size = new Size(Width - 48, 91) };
            var record = new Button { Text = german ? "Zustand erfassen" : "Record condition",
                Location = new Point(23, 171), Size = new Size(185, 38), FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(22, 119, 137), ForeColor = Color.White };
            record.FlatAppearance.BorderSize = 0;
            record.Click += (_, _) => { Close(); openEntry(); };
            var later = new Button { Text = german ? "Später" : "Later", Location = new Point(220, 171),
                Size = new Size(100, 38), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            later.Click += (_, _) => Close();
            Controls.AddRange([accent, title, description, record, later]);
            dismiss.Tick += (_, _) => Close();
            Shown += (_, _) => dismiss.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) dismiss.Dispose();
            base.Dispose(disposing);
        }
    }

    private readonly Func<string> language;
    private readonly Action openEntry;
    private readonly System.Windows.Forms.Timer clock = new() { Interval = 10000 };
    private ReminderPopup? popup;
    private DateTimeOffset nextReminder;
    private int intervalMinutes;
    private bool customInterval;
    private static string SettingsPath => Path.Combine(Store.Folder, "state-reminder.json");

    public int IntervalMinutes => intervalMinutes;
    public bool CustomInterval => customInterval;

    public StateReminder(Func<string> language, Action openEntry)
    {
        this.language = language;
        this.openEntry = openEntry;
        try
        {
            if (File.Exists(SettingsPath))
            {
                var saved = JsonSerializer.Deserialize<ReminderSettings>(File.ReadAllText(SettingsPath));
                intervalMinutes = Math.Clamp(saved?.IntervalMinutes ?? 0, 0, 1440);
                customInterval = intervalMinutes > 0 && (saved?.CustomInterval == true ||
                    intervalMinutes is not (15 or 30 or 60 or 120));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { intervalMinutes = 0; }
        clock.Tick += (_, _) => Tick();
        Schedule();
    }

    public void SetInterval(int minutes, bool custom = false)
    {
        intervalMinutes = Math.Clamp(minutes, 0, 1440);
        customInterval = intervalMinutes > 0 && custom;
        Schedule();
        try
        {
            Directory.CreateDirectory(Store.Folder);
            var temporary = SettingsPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new ReminderSettings
            { IntervalMinutes = intervalMinutes, CustomInterval = customInterval }));
            File.Move(temporary, SettingsPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(language() == "de" ? "Erinnerungsintervall konnte nicht gespeichert werden: " + ex.Message
                : "Could not save reminder interval: " + ex.Message);
        }
    }

    private void Schedule()
    {
        clock.Stop();
        popup?.Close();
        if (intervalMinutes == 0) return;
        nextReminder = DateTimeOffset.UtcNow.AddMinutes(intervalMinutes);
        clock.Start();
    }

    private void Tick()
    {
        if (DateTimeOffset.UtcNow < nextReminder) return;
        nextReminder = DateTimeOffset.UtcNow.AddMinutes(intervalMinutes);
        if (popup is { IsDisposed: false }) return;
        popup = new ReminderPopup(language() == "de", openEntry);
        popup.FormClosed += (_, _) => { popup?.Dispose(); popup = null; };
        popup.Show();
    }

    public void Dispose()
    {
        clock.Dispose();
        popup?.Close();
        popup?.Dispose();
    }
}
