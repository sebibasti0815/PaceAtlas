using System.Media;
using System.Text.Json;

namespace PaceAtlas;

internal sealed class PacingTimerWidget : IDisposable
{
    private sealed class BreakOverlay : Form
    {
        private readonly Font headingFont;
        private readonly Font countdownFont;
        private string heading = "";
        private string countdown = "";

        public BreakOverlay(Font font)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            headingFont = new Font(font.FontFamily, 38f);
            countdownFont = new Font(font.FontFamily, 34f);
        }

        private Rectangle HeadingBounds => new(0, (ClientSize.Height - 166) / 2,
            ClientSize.Width, 90);
        private Rectangle CountdownBounds => new((ClientSize.Width - 240) / 2,
            (ClientSize.Height - 166) / 2 + 90, 240, 76);

        public void SetText(string title, string time)
        {
            if (heading != title) { heading = title; Invalidate(HeadingBounds); }
            if (countdown != time) { countdown = time; Invalidate(CountdownBounds); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var background = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(background, e.ClipRectangle);
            TextRenderer.DrawText(e.Graphics, heading, headingFont, HeadingBounds, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics, countdown, countdownFont, CountdownBounds, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { headingFont.Dispose(); countdownFont.Dispose(); }
            base.Dispose(disposing);
        }
    }

    private sealed class TimerSettings
    {
        public TimerSettings() { }
        public int ActiveMinutes { get; set; } = 60;
        public int PauseMinutes { get; set; } = 30;
    }

    private readonly Form owner;
    private readonly Func<string> currentLanguage;
    private readonly System.Windows.Forms.Timer clock = new() { Interval = 1000 };
    private readonly ToolTip tooltip = new();
    private readonly Font runningFont;
    private readonly List<BreakOverlay> overlays = new();
    private readonly SoundPlayer pauseSound;
    private readonly SoundPlayer resumeSound;
    private readonly Stream pauseAudio;
    private readonly Stream resumeAudio;
    private readonly TimerSettings settings;
    private readonly Label status = new() { AutoSize = false, Width = 112, Height = 34, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Button startButton = new() { Width = 43, Height = 34, FlatStyle = FlatStyle.Flat };
    private readonly Button pauseButton = new() { Width = 43, Height = 34, FlatStyle = FlatStyle.Flat };
    private readonly Label activeCaption = new() { AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 8, 4, 0) };
    private readonly Label pauseCaption = new() { AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(8, 8, 4, 0) };
    private readonly NumericUpDown activeMinutes;
    private readonly NumericUpDown pauseMinutes;
    private DateTimeOffset deadline;
    private bool running;
    private bool resting;

    private bool German => currentLanguage() == "de";
    public bool IsRunning => running;
    public bool IsResting => running && resting;
    public void StartFromTray() { if (!running) Start(); }
    public void ToggleBreakFromTray()
    {
        if (!running) return;
        if (resting) EndBreak(); else BeginBreak(false);
    }
    public void StopFromTray() { if (running) Stop(); }
    private static string SettingsPath => Path.Combine(Store.Folder, "pacing-timer.json");
    public FlowLayoutPanel Control { get; }

    public PacingTimerWidget(Form owner, Func<string> currentLanguage)
    {
        this.owner = owner;
        this.currentLanguage = currentLanguage;
        runningFont = new Font(owner.Font, FontStyle.Bold);
        settings = ReadSettings();
        pauseAudio = Audio("PacingPause.wav");
        resumeAudio = Audio("PacingResume.wav");
        pauseSound = new SoundPlayer(pauseAudio);
        resumeSound = new SoundPlayer(resumeAudio);
        Control = new FlowLayoutPanel { Size = new Size(510, 44), WrapContents = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.FromArgb(240, 240, 240) };
        activeMinutes = new NumericUpDown { Minimum = 2, Maximum = 300, Value = settings.ActiveMinutes,
            Width = 65, Height = 34, Margin = new Padding(0, 7, 0, 0) };
        pauseMinutes = new NumericUpDown { Minimum = 1, Maximum = 120, Value = settings.PauseMinutes,
            Width = 65, Height = 34, Margin = new Padding(0, 7, 5, 0) };
        activeMinutes.ValueChanged += (_, _) => ChangeSettings();
        pauseMinutes.ValueChanged += (_, _) => ChangeSettings();
        startButton.Margin = new Padding(4, 7, 3, 0);
        pauseButton.Margin = new Padding(2, 7, 0, 0);
        status.Margin = new Padding(3, 7, 0, 0);
        startButton.Click += (_, _) => { if (running) Stop(); else Start(); };
        pauseButton.Click += (_, _) => { if (running && resting) EndBreak(); else BeginBreak(false); };
        Control.Controls.AddRange([activeCaption, activeMinutes, pauseCaption, pauseMinutes, startButton, pauseButton, status]);
        clock.Tick += (_, _) => Tick();
        UpdateLanguage();
    }

    private static Stream Audio(string file)
    {
        using var resource = typeof(PacingTimerWidget).Assembly.GetManifestResourceStream("PaceAtlas.Resources." + file)
            ?? throw new InvalidOperationException("Missing pacing audio: " + file);
        var stream = new MemoryStream();
        resource.CopyTo(stream);
        stream.Position = 0;
        return stream;
    }

    private static TimerSettings ReadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var saved = JsonSerializer.Deserialize<TimerSettings>(File.ReadAllText(SettingsPath));
                if (saved is not null) return new TimerSettings
                {
                    ActiveMinutes = Math.Clamp(saved.ActiveMinutes, 2, 300),
                    PauseMinutes = Math.Clamp(saved.PauseMinutes, 1, 120)
                };
            }
        }
        catch (Exception) { /* Invalid settings should not prevent the application from starting. */ }
        return new TimerSettings();
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Store.Folder);
            var temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings));
            File.Move(temp, SettingsPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(owner, German ? "Timer-Einstellungen konnten nicht gespeichert werden: " + ex.Message :
                "Could not save timer settings: " + ex.Message);
        }
    }

    public void UpdateLanguage()
    {
        activeCaption.Text = German ? "Aktiv:" : "Active:";
        pauseCaption.Text = German ? "Pause:" : "Break:";
        Control.Width = Control.Controls.Cast<Control>().Sum(control => control.Width + control.Margin.Horizontal) + 4;
        Control.Left = owner.ClientSize.Width - 165 - Control.Width - 4;
        UpdateDisplay();
    }

    private void ChangeSettings()
    {
        settings.ActiveMinutes = (int)activeMinutes.Value;
        settings.PauseMinutes = (int)pauseMinutes.Value;
        SaveSettings();
        // Current countdown stays unchanged; the next phase uses the new duration.
        UpdateDisplay();
    }

    private void Start()
    {
        CloseOverlays();
        running = true;
        resting = false;
        deadline = DateTimeOffset.UtcNow.AddMinutes(settings.ActiveMinutes);
        clock.Start();
        UpdateDisplay();
    }

    private void Stop()
    {
        clock.Stop();
        CloseOverlays();
        running = false;
        resting = false;
        UpdateDisplay();
    }

    private void BeginBreak(bool showOverlay)
    {
        if (!running) running = true;
        resting = true;
        deadline = DateTimeOffset.UtcNow.AddMinutes(settings.PauseMinutes);
        clock.Start();
        Play(pauseSound);
        if (showOverlay) ShowOverlays();
        UpdateDisplay();
    }

    private void EndBreak()
    {
        CloseOverlays();
        resting = false;
        deadline = DateTimeOffset.UtcNow.AddMinutes(settings.ActiveMinutes);
        clock.Start();
        Play(resumeSound);
        UpdateDisplay();
    }

    private static void Play(SoundPlayer player)
    {
        try { player.Play(); }
        catch (Exception) { SystemSounds.Exclamation.Play(); }
    }

    private void Tick()
    {
        if (!running) return;
        if (DateTimeOffset.UtcNow >= deadline)
        {
            if (resting)
            {
                EndBreak();
                var message = German ? "Pause beendet. Wenn du mehr Ruhe brauchst, nimm sie dir." :
                    "Break finished. Take more rest if you need it.";
                MessageBox.Show(owner, message, "Pacing Timer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else BeginBreak(true);
        }
        UpdateDisplay();
    }

    private void ShowOverlays()
    {
        CloseOverlays();
        foreach (var screen in Screen.AllScreens)
        {
            var overlay = new BreakOverlay(owner.Font) { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
                Bounds = screen.Bounds, TopMost = true, ShowInTaskbar = false, BackColor = Color.FromArgb(48, 64, 86),
                ForeColor = Color.White, Font = owner.Font, KeyPreview = true };
            overlay.SetText(German ? "Zeit für eine Pause" : "Time for a break", "");
            var end = new Button { Text = German ? "Pause vorzeitig beenden" : "End break early",
                Width = 240, Height = 48, Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(Math.Max(10, overlay.ClientSize.Width - 260), Math.Max(10, overlay.ClientSize.Height - 70)) };
            end.Click += (_, _) => EndBreak();
            overlay.CancelButton = end;
            overlay.Controls.Add(end);
            overlay.Show();
            overlays.Add(overlay);
        }
    }

    private void CloseOverlays()
    {
        foreach (var overlay in overlays)
        {
            overlay.Close();
            overlay.Dispose();
        }
        overlays.Clear();
    }

    private void UpdateDisplay()
    {
        var seconds = running ? Math.Max(0, (int)Math.Ceiling((deadline - DateTimeOffset.UtcNow).TotalSeconds)) : 0;
        var countdown = $"{seconds / 60:00}:{seconds % 60:00}";
        var description = !running ? (German ? "Pacing Timer aus" : "Pacing timer off") : resting
            ? (German ? "Pause: " : "Break: ") + countdown
            : (German ? "Bis zur Pause: " : "Until break: ") + countdown;
        status.Text = running ? (resting ? (German ? "Pause " : "Break ") : (German ? "Aktiv " : "Active ")) + countdown :
            (German ? "Timer aus" : "Timer off");
        status.BackColor = Color.Transparent;
        status.Font = running ? runningFont : owner.Font;
        tooltip.SetToolTip(status, description);
        startButton.Text = running ? "■" : "zZz";
        pauseButton.Text = resting ? "▶" : "Ⅱ";
        tooltip.SetToolTip(startButton, running ? (German ? "Pacing beenden" : "Stop pacing") : (German ? "Pacing starten" : "Start pacing"));
        tooltip.SetToolTip(pauseButton, resting ? (German ? "Pause beenden" : "End break") : (German ? "Pause machen" : "Take break"));
        pauseButton.Enabled = running;
        foreach (var overlay in overlays)
        {
            overlay.SetText(German ? "Zeit für eine Pause" : "Time for a break", countdown);
            foreach (var end in overlay.Controls.OfType<Button>())
            {
                var next = German ? "Pause vorzeitig beenden" : "End break early";
                if (end.Text != next) end.Text = next;
            }
        }
    }

    public void Dispose()
    {
        Stop();
        clock.Dispose();
        tooltip.Dispose();
        runningFont.Dispose();
        pauseSound.Dispose();
        resumeSound.Dispose();
        pauseAudio.Dispose();
        resumeAudio.Dispose();
    }
}
