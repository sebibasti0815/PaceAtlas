using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace PaceAtlas;
public sealed class MainForm : Form
{
    private sealed class ResizeSnapshot : PictureBox
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Bitmap? Snapshot
        {
            get => Image as Bitmap;
            set => Image = value;
        }

        public ResizeSnapshot()
        {
            Dock = DockStyle.Fill;
            SizeMode = PictureBoxSizeMode.StretchImage;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Snapshot is not null) Refresh();
        }
    }

    private readonly ResizeSnapshot resizeSnapshot = new();
    private readonly List<Control> hiddenForResize = new();

    protected override void OnResizeBegin(EventArgs e)
    {
        base.OnResizeBegin(e);
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

        Bitmap? bitmap = null;
        try
        {
            bitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (var graphics = Graphics.FromImage(bitmap))
                graphics.CopyFromScreen(PointToScreen(Point.Empty), Point.Empty, ClientSize);

            resizeSnapshot.Snapshot = bitmap;
            bitmap = null;
            resizeSnapshot.BackColor = BackColor;
            Controls.Add(resizeSnapshot);
            resizeSnapshot.BringToFront();
            foreach (Control control in Controls)
            {
                if (control == resizeSnapshot || !control.Visible) continue;
                hiddenForResize.Add(control);
                control.Visible = false;
            }
            resizeSnapshot.Refresh();
        }
        catch (Exception)
        {
            bitmap?.Dispose();
            EndResizeSnapshot();
        }
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        EndResizeSnapshot();
        base.OnResizeEnd(e);
    }

    private void EndResizeSnapshot()
    {
        if (resizeSnapshot.Parent == this) Controls.Remove(resizeSnapshot);
        foreach (Control control in hiddenForResize) control.Visible = true;
        hiddenForResize.Clear();
        var bitmap = resizeSnapshot.Snapshot;
        resizeSnapshot.Snapshot = null;
        bitmap?.Dispose();
    }

    private sealed class BufferedTableLayoutPanel : TableLayoutPanel
    {
        public BufferedTableLayoutPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    private sealed class BufferedTabControl : TabControl
    {
        public BufferedTabControl()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    private static readonly Color Canvas = Color.FromArgb(238, 243, 248);
    private static readonly Color Surface = Color.White;
    private static readonly Color Ink = Color.FromArgb(35, 48, 67);
    private static readonly Color Accent = Color.FromArgb(22, 119, 137);
    private static readonly Color Pale = Color.FromArgb(226, 237, 243);
    private static readonly string[] OverallNames = ["gut", "leicht eingeschränkt", "mittel", "schlecht", "sehr schlecht"];
    private static readonly string[] PemNames = ["nein", "vermutet", "erkannt"];
    private static readonly string[] SymptomNames = ["Erschöpfung", "Brain Fog", "Schmerzen", "Geräuschempfindlichkeit", "Ohrgeräusche", "Lichtempfindlichkeit", "Atemprobleme", "Schwindel/Kreislauf", "Herzrasen", "Zittern", "Kältegefühl/Schüttelfrost", "Angst/Unruhe", "Hyperarousal", "Nicht erholsamer Schlaf", "Sehkraft"];
    private static readonly string[] PainNames = ["Finger", "Hände", "Unterarme", "Oberarme", "Kopf", "Nacken", "Rücken", "Beine", "Füße", "Sonstige"];
    private static readonly string[] ActivityNames = ["Körperlich", "Kognitiv", "Sozial", "Akustisch", "Visuell", "Emotional/Stress", "Fahrt/Transport"];
    private static readonly string[] RestNames = ["Hingelegt", "Reizarm", "Augen geschlossen", "Geschlafen"];
    private static readonly string[] MeasureNames = ["Ibuprofen", "Naproxen", "Cannabis", "Benzodiazepin", "Sonstige"];
    private readonly Store store = new();
    private List<string> activityOptions = new();
    private List<string> restOptions = new();
    private List<string> measureOptions = new();
    private readonly DateTimePicker stateTime = Clock();
    private readonly ComboBox overall = Choice(OverallNames);
    private readonly ComboBox pem = Choice(PemNames);
    private readonly CheckBox crash = new() { Text = "Crash", AutoSize = true, Margin = new Padding(3, 9, 12, 0) };
    private readonly NumericUpDown pulse = new() { Minimum = 0, Maximum = 250, Width = 76 };
    private readonly Dictionary<string,ComboBox> symptoms = new();
    private readonly Dictionary<string, CheckBox> painChecks = new();
    private readonly TextBox stateNote = new() { Width = 350 };
    private readonly DateTimePicker intervalStart = Clock();
    private readonly DateTimePicker intervalEnd = Clock();
    private readonly CheckBox openActivity = new() { Text = "Läuft – Ende später eintragen", AutoSize = true, Margin = new Padding(14, 7, 3, 0) };
    private readonly ComboBox intervalKind = Choice(["Aktivität", "Ruhe", "Schlaf"]);
    private readonly ComboBox sleepRecovery = Choice(["nicht bewertet", "keine", "gering", "mittel", "deutlich"]);
    private readonly Panel activityFields = new() { Dock = DockStyle.Fill };
    private readonly TableLayoutPanel intervalLayout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
    private readonly FlowLayoutPanel sleepFields = new() { Dock = DockStyle.Fill, WrapContents = false };
    private readonly CheckedListBox dimensions = new() { CheckOnClick = true, Dock = DockStyle.Fill, Height = 124, IntegralHeight = false };
    private readonly ToolTip optionToolTip = new();
    private Button? dimensionOptionsButton;
    private Button? measureOptionsButton;
    private readonly ComboBox intensity = Choice(["leicht", "mittel", "stark", "sehr stark"]);
    private readonly CheckBox ncHeadphones = new() { Text = "NC-Kopfhörer", Width = 320, Height = 38 };
    private readonly CheckBox loopEarplugs = new() { Text = "Loop-Ohrstöpsel", Width = 185, Height = 38 };
    private readonly CheckBox calmerEarplugs = new() { Text = "Calmer-Ohrstöpsel", Width = 200, Height = 38 };
    private readonly TextBox intervalNote = new() { Width = 300 };
    private readonly DateTimePicker measureTime = Clock();
    private readonly ComboBox measure = Choice(MeasureNames);
    private readonly TextBox dose = new() { Width = 105 };
    private readonly TextBox measureNote = new() { Width = 300 };
    private readonly DateTimePicker planTime = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 100 };
    private readonly TextBox planName = new() { Width = 260 };
    private readonly TextBox planDose = new() { Width = 145 };
    private readonly DateTimePicker planStart = new() { Format = DateTimePickerFormat.Short, Width = 130 };
    private readonly DateTimePicker planEnd = new() { Format = DateTimePickerFormat.Short, Width = 130, Enabled = false };
    private readonly CheckBox planOngoing = new() { Text = "Läuft noch", Checked = true, AutoSize = true };
    private readonly ComboBox planQuantity = new() { Width = 90, DropDownStyle = ComboBoxStyle.DropDown, Text = "1" };
    private readonly ComboBox planForm = new() { Width = 155, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly Label goalSummary = new() { AutoSize = true, Margin = new Padding(8, 10, 0, 0) };
    private List<string> selectedGoals = new();
    private readonly DateTimePicker intakeDay = new() { Format = DateTimePickerFormat.Short, Width = 150 };
    private readonly DataGridView planGrid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
    private readonly DataGridView planGridRight = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
    private readonly TabControl topTabs = new BufferedTabControl { Dock = DockStyle.Fill };
    private readonly TabControl medicationTabs = new BufferedTabControl { Dock = DockStyle.Fill };
    private readonly DataGridView intakeGrid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
    private readonly DataGridView productGrid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
    private readonly TextBox manufacturer = new() { Width = 190 };
    private readonly TextBox supplier = new() { Width = 190 };
    private readonly NumericUpDown packUnits = new() { DecimalPlaces = 2, Maximum = 1000000, Width = 110 };
    private readonly NumericUpDown packPrice = new() { DecimalPlaces = 2, Maximum = 1000000, Width = 110 };
    private readonly NumericUpDown purchasePacks = new() { DecimalPlaces = 0, Minimum = 1, Maximum = 10000, Value = 1, Width = 95 };
    private readonly NumericUpDown stockTarget = new() { DecimalPlaces = 2, Maximum = 1000000, Width = 110 };
    private readonly Label stockSummary = new() { Dock = DockStyle.Fill, Padding = new Padding(5, 6, 5, 4) };
    private readonly TabPage stockPage = new("Packungen und Vorrat");
    private long selectedProductId;
    private long editingPlanId;
    private MedicationPlan? loadedPlanBaseline;
    private string blankPlanTime = "";
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
    private Button? endActivityButton;
    private readonly ComboBox period = Choice(["7 Tage", "30 Tage", "3 Monate", "1 Jahr", "Gesamt"]);
    private readonly ChartPanel chart = new() { Dock = DockStyle.Fill };
    private readonly Label summary = new() { AutoSize = false, Dock = DockStyle.Top, Height = 48 };
    private readonly TextBox assessment = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical, TabStop = false };
    private readonly RichTextBox aiResult = new() { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = RichTextBoxScrollBars.Vertical, TabStop = false, DetectUrls = false };
    private readonly Label aiInfo = new() { Dock = DockStyle.Top, Height = 56, Padding = new Padding(6, 5, 6, 3) };
    private readonly TabControl assessmentTabs = new BufferedTabControl { Dock = DockStyle.Fill };
    private string aiModel = "gpt-6-sol";
    private bool aiBusy;
    private List<Entry> entries = new();
    private long editingId;
    private string editingKind = "";
    private string language = Localization.SystemLanguage;
    private readonly Dictionary<Control, string> originalLabels = new();
    private readonly Dictionary<DataGridViewColumn, string> originalHeaders = new();
    private readonly Button languageButton = new() { Size = new Size(90, 34), FlatStyle = FlatStyle.Flat };
    private PacingTimerWidget? pacingTimer;
    private StateReminder? stateReminder;
    private readonly NotifyIcon trayIcon = new();
    private readonly ContextMenuStrip trayMenu = new();
    private ToolStripMenuItem? reminderMenu;
    private FormWindowState restoreWindowState = FormWindowState.Normal;
    private bool startInTray;
    private bool trayReady;
    private readonly Bitmap germanFlag = MakeFlag(false);
    private readonly Bitmap americanFlag = MakeFlag(true);
    private static Bitmap MakeFlag(bool american)
    {
        var flag = new Bitmap(32, 20);
        using var graphics = Graphics.FromImage(flag);
        if (american)
        {
            graphics.Clear(Color.White);
            using var red = new SolidBrush(Color.FromArgb(178, 34, 52));
            for (var y = 0; y < 13; y += 2) graphics.FillRectangle(red, 0, y * 20 / 13, 32, 20f / 13);
            using var blue = new SolidBrush(Color.FromArgb(60, 59, 110));
            graphics.FillRectangle(blue, 0, 0, 14, 11);
            for (var row = 0; row < 5; ++row)
                for (var col = 0; col < 5; ++col)
                    graphics.FillEllipse(Brushes.White, 1 + col * 2.7f, 1 + row * 2f, 1.1f, 1.1f);
        }
        else
        {
            graphics.FillRectangle(Brushes.Black, 0, 0, 32, 7);
            using var red = new SolidBrush(Color.FromArgb(221, 0, 0));
            using var gold = new SolidBrush(Color.FromArgb(255, 206, 0));
            graphics.FillRectangle(red, 0, 7, 32, 7);
            graphics.FillRectangle(gold, 0, 14, 32, 6);
        }
        graphics.DrawRectangle(Pens.Gray, 0, 0, 31, 19);
        return flag;
    }
    private string T(string value) => Localization.Translate(value, language);
    private void LocalizeDialog(Control parent)
    {
        if (parent is not TextBox && parent is not ComboBox && parent is not ListBox && parent is not DataGridView)
            parent.Text = T(parent.Text);
        foreach (Control child in parent.Controls) LocalizeDialog(child);
    }
    private void RememberLabels(Control parent)
    {
        if (parent is not TextBox && parent is not ComboBox && parent is not NumericUpDown && parent is not DateTimePicker && parent is not ChartPanel
            && !string.IsNullOrEmpty(parent.Text) && !originalLabels.ContainsKey(parent)) originalLabels[parent] = parent.Text;
        foreach (Control child in parent.Controls) RememberLabels(child);
    }
    private static void TranslateItems(ComboBox box, string language)
    {
        var index = box.SelectedIndex;
        var canonical = box.Items.Cast<object>().Select(item => Localization.Canonical(item.ToString() ?? "")).ToArray();
        box.Items.Clear();
        box.Items.AddRange(canonical.Select(value => Localization.Translate(value, language)).Cast<object>().ToArray());
        box.SelectedIndex = Math.Clamp(index, -1, box.Items.Count - 1);
    }
    private void ApplyLanguage()
    {
        SuspendLayout();
        var checkedDimensions = dimensions.CheckedItems.Cast<string>().Select(Localization.Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (control, german) in originalLabels)
            if (!control.IsDisposed) control.Text = T(german);
        foreach (var check in new[] { ncHeadphones, loopEarplugs, calmerEarplugs })
            check.Width = Math.Max(140, TextRenderer.MeasureText(check.Text, check.Font,
                new Size(int.MaxValue, check.Height), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width + 58);
        foreach (var (column, german) in originalHeaders) column.HeaderText = T(german);
        foreach (var box in new[] { overall, pem, intervalKind, sleepRecovery, intensity, measure, period }) TranslateItems(box, language);
        foreach (var box in symptoms.Values) TranslateItems(box, language);
        foreach (var picker in new[] { stateTime, intervalStart, intervalEnd, measureTime })
            picker.CustomFormat = language == "de" ? "dd.MM.yyyy HH:mm" : "MM/dd/yyyy HH:mm";
        intakeDay.Format = DateTimePickerFormat.Custom;
        intakeDay.CustomFormat = language == "de" ? "dd.MM.yyyy" : "MM/dd/yyyy";
        // Plan form is editable; preserve user-entered text while translating its suggestions.
        var currentForm = planForm.Text;
        planForm.Items.Clear();
        planForm.Items.AddRange(new[] { "Kapsel", "Tablette", "mg", "ml", "Spray", "Pflaster" }.Select(T).Cast<object>().ToArray());
        planForm.Text = T(Localization.Canonical(currentForm));
        foreach (var view in new[] { planGrid, planGridRight })
            foreach (DataGridViewRow row in view.Rows)
                if (row.Tag is MedicationPlan plan) row.Cells[4].Value = T(plan.Form);
        ResetDimensions();
        if (editingId != 0 && editingKind is "Aktivität" or "Ruhe")
            foreach (var name in checkedDimensions)
                if (!dimensions.Items.Cast<string>().Any(item => Localization.Canonical(item) == name)) dimensions.Items.Add(T(name));
        for (var index = 0; index < dimensions.Items.Count; ++index)
            dimensions.SetItemChecked(index, checkedDimensions.Contains(Localization.Canonical(dimensions.Items[index]?.ToString() ?? "")));
        if (intakeGrid.Columns.Contains("status") && intakeGrid.Columns["status"] is DataGridViewComboBoxColumn statusColumn)
        {
            var statuses = intakeGrid.Rows.Cast<DataGridViewRow>().Select(row => Localization.Canonical(Convert.ToString(row.Cells["status"].Value) ?? "")).ToArray();
            statusColumn.DataSource = null;
            statusColumn.Items.Clear();
            statusColumn.Items.AddRange(new[] { T("Offen"), T("Genommen"), T("Ausgelassen") });
            for (var i = 0; i < statuses.Length; ++i) intakeGrid.Rows[i].Cells["status"].Value = Localization.StatusLabel(statuses[i], language);
        }
        languageButton.AccessibleName = language == "de" ? "Sprache auswählen" : "Select language";
        if (dimensionOptionsButton != null)
        {
            var description = language == "de" ? "Belastungsarten und Ruheformen verwalten" : "Manage activity and rest types";
            dimensionOptionsButton.AccessibleName = description; optionToolTip.SetToolTip(dimensionOptionsButton, description);
        }
        if (measureOptionsButton != null)
        {
            var description = language == "de" ? "Maßnahmen verwalten" : "Manage measures";
            measureOptionsButton.AccessibleName = description; optionToolTip.SetToolTip(measureOptionsButton, description);
        }
        languageButton.Invalidate();
        pacingTimer?.UpdateLanguage();
        UpdateGoalSummary();
        chart.UiLanguage = language;
        chart.Invalidate();
        foreach (DataGridViewRow row in intakeGrid.Rows)
            row.Cells["form"].Value = T(Localization.Canonical(Convert.ToString(row.Cells["form"].Value) ?? ""));
        RefreshInventory();
        medicationTabs.Invalidate();
        UpdateAnalysis();
        ResumeLayout(true);
    }
    private void SwitchLanguage(string selected)
    {
        if (language == selected) return;
        intakeGrid.EndEdit();
        language = selected;
        ApplyLanguage();
        LoadEntries();
        SaveWindowSettings();
    }
    private static string WindowSettingsPath => Path.Combine(Store.Folder, "window.json");
    public sealed class WindowSettings
    {
        public WindowSettings() { }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Maximized { get; set; }
        public bool StartInTray { get; set; }
        public string? Language { get; set; }
        public Dictionary<string, GridSettings> Tables { get; set; } = new();
    }
    public sealed class GridSettings
    {
        public Dictionary<string, float> ColumnWeights { get; set; } = new();
        public string? SortColumn { get; set; }
        public bool SortDescending { get; set; }
    }
    private readonly Dictionary<string, GridSettings> tableSettings = new();
    private IEnumerable<(string Name, DataGridView View)> PersistentTables()
    {
        yield return ("entries", grid);
        yield return ("planLeft", planGrid);
        yield return ("planRight", planGridRight);
        yield return ("dailyIntakes", intakeGrid);
        yield return ("products", productGrid);
    }
    private void ApplyTableSettings()
    {
        foreach (var (name, view) in PersistentTables())
        {
            if (!tableSettings.TryGetValue(name, out var saved)) continue;
            foreach (DataGridViewColumn column in view.Columns)
                if (saved.ColumnWeights.TryGetValue(column.Name, out var weight) && weight > 0 && float.IsFinite(weight))
                    column.FillWeight = weight;
            RestoreTableSort(view);
        }
    }
    private void RestoreTableSort(DataGridView view)
    {
        var name = PersistentTables().FirstOrDefault(item => ReferenceEquals(item.View, view)).Name;
        if (name is null || !tableSettings.TryGetValue(name, out var saved) || saved.SortColumn is null) return;
        if (!view.Columns.Contains(saved.SortColumn)) return;
        try { view.Sort(view.Columns[saved.SortColumn], saved.SortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending); }
        catch (InvalidOperationException) { }
    }

    public MainForm()
    {
        Text = "PaceAtlas · ME/CFS Verlauf";
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        MinimumSize = new Size(1480, 820); Size = new Size(1540, 960); StartPosition = FormStartPosition.CenterScreen;
        FormClosing += (_, e) =>
        {
            if (pacingTimer?.IsRunning == true && MessageBox.Show(this,
                language == "de" ? "Der Pacing Timer läuft noch. PaceAtlas wirklich beenden?" :
                    "The pacing timer is still running. Close PaceAtlas anyway?",
                "Pacing Timer", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            SaveWindowSettings();
        };
        Font = new Font("Segoe UI", 9.5f); BackColor = Canvas;
        var root = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 650)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(root);
        var top = topTabs; root.Controls.Add(top, 0, 0);
        top.TabPages.Add(MakeStateTab()); top.TabPages.Add(MakeIntervalTab()); top.TabPages.Add(MakeMeasureTab()); top.TabPages.Add(MakeMedicationTab());
        foreach (var (name, view) in PersistentTables())
        {
            foreach (DataGridViewColumn column in view.Columns)
                if (string.IsNullOrWhiteSpace(column.Name)) column.Name = "column" + column.Index;
            view.Sorted += (_, _) =>
            {
                if (view.SortedColumn is null) return;
                if (!tableSettings.TryGetValue(name, out var saved)) tableSettings[name] = saved = new GridSettings();
                saved.SortColumn = view.SortedColumn?.Name;
                saved.SortDescending = view.SortOrder == SortOrder.Descending;
            };
        }
        var windowEmblem = new PictureBox { Image = Icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(30, 30), Location = new Point(ClientSize.Width - 54, 14),
            Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Canvas };
        Controls.Add(windowEmblem);
        windowEmblem.BringToFront();
        windowEmblem.Cursor = Cursors.Hand;
        windowEmblem.Click += (_, _) => ShowAbout();
        languageButton.Location = new Point(ClientSize.Width - 155, 12);
        languageButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        var languages = new ContextMenuStrip();
        languages.Items.Add("Deutsch", germanFlag, (_, _) => SwitchLanguage("de"));
        languages.Items.Add("English (US)", americanFlag, (_, _) => SwitchLanguage("en"));
        languageButton.Paint += (_, e) =>
        {
            e.Graphics.DrawImage(language == "de" ? germanFlag : americanFlag, new Rectangle(7, 7, 30, 19));
            TextRenderer.DrawText(e.Graphics, language == "de" ? "DE ▾" : "EN ▾", languageButton.Font,
                new Rectangle(42, 0, languageButton.Width - 43, languageButton.Height), languageButton.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        };
        languageButton.Text = "";
        languageButton.Click += (_, _) => languages.Show(languageButton, new Point(0, languageButton.Height));
        Controls.Add(languageButton);
        languageButton.BringToFront();
        var lower = new BufferedTabControl { Dock = DockStyle.Fill };
        root.Controls.Add(lower, 0, 1);
        var listTab = new TabPage("Datenliste") { Padding = new Padding(4) };
        var analysisTab = new TabPage("Auswertung") { Padding = new Padding(4) };
        lower.TabPages.Add(listTab);
        lower.TabPages.Add(analysisTab);
        var history = new Panel { Dock = DockStyle.Fill }; listTab.Controls.Add(history);
        grid.Columns.Add("time", "Zeit"); grid.Columns.Add("kind", "Typ"); grid.Columns.Add("detail", "Inhalt"); grid.Columns.Add("note", "Notiz");
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            grid.CurrentCell = grid.Rows[e.RowIndex].Cells[0];
            grid.Rows[e.RowIndex].Selected = true;
            EditSelected();
        };
        grid.Columns[0].FillWeight = 19; grid.Columns[1].FillWeight = 12; grid.Columns[2].FillWeight = 47; grid.Columns[3].FillWeight = 22;
        grid.SelectionChanged += (_, _) => UpdateEndActivityButton();
        grid.CurrentCellChanged += (_, _) => UpdateEndActivityButton();
        history.Controls.Add(grid);
        var bar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 42, ColumnCount = 2, RowCount = 1 };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var entryActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var fileActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight };
        endActivityButton = Button("Laufenden Zeitraum jetzt beenden", EndSelectedActivity);
        endActivityButton.Enabled = false;
        entryActions.Controls.AddRange([Button("Bearbeiten", EditSelected), endActivityButton, Button("Löschen", DeleteSelected)]);
        fileActions.Controls.AddRange([Button("CSV exportieren", Export), Button("Backup erstellen", Backup), Button("Backup einspielen", Restore)]);
        bar.Controls.Add(entryActions, 0, 0);
        bar.Controls.Add(fileActions, 1, 0);
        history.Controls.Add(bar);
        var analysis = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) }; analysisTab.Controls.Add(analysis);
        var analysisLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };
        analysisLeft.Controls.Add(chart);
        analysisLeft.Controls.Add(summary);
        var filterBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 58 };
        filterBar.Controls.Add(new Label { Text = "Auswertung:", AutoSize = true, Margin = new Padding(3, 7, 5, 0) }); filterBar.Controls.Add(period);
        analysisLeft.Controls.Add(filterBar);
        var assessmentCard = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), BackColor = Surface, BorderStyle = BorderStyle.FixedSingle };
        var assessmentTitle = new Label { Text = "Automatische Einordnung", Dock = DockStyle.Top, Height = 36, Font = new Font(Font, FontStyle.Bold) };
        var localPage = new TabPage("Lokal"); localPage.Controls.Add(assessment);
        var aiPage = new TabPage("KI"); aiPage.Controls.Add(aiResult); aiPage.Controls.Add(aiInfo);
        assessmentTabs.TabPages.Add(localPage); assessmentTabs.TabPages.Add(aiPage);
        var aiButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 49, WrapContents = false };
        aiButtons.Controls.Add(Button("Mit KI analysieren", () => _ = AnalyzeWithAiAsync()));
        aiButtons.Controls.Add(Button("Verbindung ...", ConfigureAi));
        assessmentCard.Controls.Add(assessmentTabs);
        assessmentCard.Controls.Add(aiButtons);
        assessmentCard.Controls.Add(assessmentTitle);
        var assessmentArea = new Panel { Dock = DockStyle.Right, Width = 480, Padding = new Padding(12, 0, 0, 0) };
        assessmentArea.Controls.Add(assessmentCard);
        analysis.Controls.Add(analysisLeft);
        analysis.Controls.Add(assessmentArea);
        period.SelectedIndexChanged += (_,_) => { UpdateAnalysis(); RestoreAiResponse(); };
        intervalKind.SelectedIndexChanged += (_,_) => { ResetDimensions(); UpdateIntervalEndMode(); };
        openActivity.CheckedChanged += (_,_) =>
        {
            if (!openActivity.Checked && editingId != 0 && editingKind is "Aktivität" or "Ruhe" or "Schlaf")
                intervalEnd.Value = DateTime.Now;
            UpdateIntervalEndMode();
        };
        overall.DropDownWidth = 245; overall.SelectedIndex = 2; pem.SelectedIndex = 0; intensity.SelectedIndex = 1; intervalKind.SelectedIndex = 0; measure.SelectedIndex = 0; period.SelectedIndex = 1;
        ApplyPemFromTodaysEntries();
        ApplyTheme(this);
        StyleTabs(top, 325);
        StyleTabs(lower, 220);
        StyleTabs(medicationTabs, 310);
        RestoreWindowSettings();
        ApplyTableSettings();
        RememberLabels(this);
        originalLabels[stockPage] = "Packungen und Vorrat";
        foreach (var column in PersistentTables().SelectMany(item => item.View.Columns.Cast<DataGridViewColumn>())) originalHeaders[column] = column.HeaderText;
        pacingTimer = new PacingTimerWidget(this, () => language);
        pacingTimer.Control.Location = new Point(languageButton.Left - pacingTimer.Control.Width - 8, 17);
        Controls.Add(pacingTimer.Control);
        pacingTimer.Control.BringToFront();
        FormClosed += (_, _) => pacingTimer.Dispose();
        stateReminder = new StateReminder(() => language, OpenConditionFromReminder);
        SetupTray();
        if (startInTray)
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
        }
        ReloadChoiceOptions();
        ApplyLanguage();
        LoadMedicationPlans();
        LoadAiModel();
        LoadEntries();
        Shown += (_, _) =>
        {
            if (startInTray) Hide();
            RefreshInventory();
            medicationTabs.Invalidate();
        };
    }

    private void SetupTray()
    {
        trayIcon.Icon = Icon;
        trayIcon.Text = "PaceAtlas";
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        trayMenu.Opening += (_, _) => BuildTrayMenu();
        trayIcon.Visible = true;
        restoreWindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
        trayReady = true;
        Resize += (_, _) =>
        {
            if (!trayReady) return;
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
            }
            else if (Visible)
                restoreWindowState = WindowState;
        };
        FormClosed += (_, _) =>
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayMenu.Dispose();
            stateReminder?.Dispose();
        };
    }

    private void RestoreFromTray()
    {
        if (IsDisposed) return;
        ShowInTaskbar = true;
        WindowState = restoreWindowState;
        Show();
        BringToFront();
        Activate();
    }

    private void OpenConditionFromReminder()
    {
        RestoreFromTray();
        ResetEdit();
        topTabs.SelectedIndex = 0;
        Activate();
    }

    private void BuildTrayMenu()
    {
        bool german = language == "de";
        var previousItems = trayMenu.Items.Cast<ToolStripItem>().ToArray();
        trayMenu.Items.Clear();
        foreach (var item in previousItems) item.Dispose();
        var open = trayMenu.Items.Add(german ? "PaceAtlas öffnen" : "Open PaceAtlas", null, (_, _) => RestoreFromTray());
        open.Font = new Font(trayMenu.Font, FontStyle.Bold);
        trayMenu.Items.Add(new ToolStripSeparator());

        var start = trayMenu.Items.Add(german ? "Pacing starten" : "Start pacing", null, (_, _) => pacingTimer?.StartFromTray());
        start.Enabled = pacingTimer?.IsRunning == false;
        var pause = trayMenu.Items.Add(pacingTimer?.IsResting == true
            ? (german ? "Pause beenden" : "End break") : (german ? "Pause starten" : "Start break"),
            null, (_, _) => pacingTimer?.ToggleBreakFromTray());
        pause.Enabled = pacingTimer?.IsRunning == true;
        var stop = trayMenu.Items.Add(german ? "Pacing stoppen" : "Stop pacing", null, (_, _) => pacingTimer?.StopFromTray());
        stop.Enabled = pacingTimer?.IsRunning == true;
        trayMenu.Items.Add(new ToolStripSeparator());

        var reminder = new ToolStripMenuItem(german ? "Zustandserinnerung" : "Condition reminder");
        reminderMenu = reminder;
        foreach (int minutes in new[] { 0, 15, 30, 60, 120 })
        {
            var caption = minutes == 0 ? (german ? "Aus" : "Off") :
                minutes < 60 ? $"{minutes} " + (german ? "Minuten" : "minutes") :
                $"{minutes / 60} " + (german ? (minutes == 60 ? "Stunde" : "Stunden") : (minutes == 60 ? "hour" : "hours"));
            reminder.DropDownItems.Add(new ToolStripMenuItem(caption, null,
                (_, _) => { stateReminder?.SetInterval(minutes); RefreshReminderMenu(reminder); })
            { Checked = stateReminder?.IntervalMinutes == minutes &&
                (minutes == 0 || stateReminder?.CustomInterval == false), Tag = minutes });
        }
        reminder.DropDownItems.Add(new ToolStripSeparator());
        reminder.DropDownItems.Add(new ToolStripMenuItem(german ? "Eigenes Intervall ..." : "Custom interval ...", null,
            (_, _) => { RestoreFromTray(); ConfigureReminderInterval(); }) { Tag = "custom" });
        reminder.DropDownOpening += (_, _) => RefreshReminderMenu(reminder);
        RefreshReminderMenu(reminder);
        trayMenu.Items.Add(reminder);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(new ToolStripMenuItem(german ? "Beim Start im Tray öffnen" : "Start in the tray", null,
            (_, _) => { startInTray = !startInTray; SaveWindowSettings(); }) { Checked = startInTray });
        trayMenu.Items.Add(german ? "Info ..." : "About ...", null,
            (_, _) => { RestoreFromTray(); ShowAbout(); });
        trayMenu.Items.Add(german ? "PaceAtlas beenden" : "Exit PaceAtlas", null,
            (_, _) => Close());
    }

    private void ShowAbout()
    {
        var version = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "?";
        MessageBox.Show(this, $"Pace Atlas\nVersion {version}\nME/CFS", language == "de" ? "Info zu Pace Atlas" : "About Pace Atlas",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void RefreshReminderMenu(ToolStripMenuItem reminder)
    {
        if (reminder.IsDisposed || stateReminder is null) return;
        bool german = language == "de";
        foreach (ToolStripMenuItem item in reminder.DropDownItems.OfType<ToolStripMenuItem>())
        {
            if (item.Tag is int minutes)
                item.Checked = stateReminder.IntervalMinutes == minutes &&
                    (minutes == 0 || !stateReminder.CustomInterval);
            else if (item.Tag is "custom")
            {
                item.Checked = stateReminder.CustomInterval;
                item.Text = (german ? "Eigenes Intervall ..." : "Custom interval ...") +
                    (stateReminder.CustomInterval ? $" ({stateReminder.IntervalMinutes} " +
                        (german ? "Minuten)" : "minutes)") : "");
            }
        }
        reminder.DropDown.PerformLayout();
    }

    private void ConfigureReminderInterval()
    {
        bool german = language == "de";
        using var dialog = new Form { Text = german ? "Zustandserinnerung" : "Condition reminder",
            Font = Font, Icon = Icon, FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen, MaximizeBox = false, MinimizeBox = false,
            ShowInTaskbar = false, TopMost = true, ClientSize = new Size(448, 152) };
        var explanation = new Label { Text = german ? "Erinnere mich alle (Minuten):" : "Remind me every (minutes):",
            Location = new Point(20, 23), AutoSize = true };
        var minutes = new NumericUpDown { Minimum = 5, Maximum = 1440,
            Value = Math.Clamp(stateReminder?.IntervalMinutes ?? 60, 5, 1440),
            Location = new Point(279, 18), Width = 135 };
        var hint = new Label { Text = german ? "Im Tray-Menü kannst du die Erinnerung jederzeit ausschalten." :
            "You can turn off reminders from the tray menu at any time.",
            Location = new Point(20, 61), Size = new Size(408, 34) };
        var save = new Button { Text = german ? "Speichern" : "Save", DialogResult = DialogResult.OK,
            Location = new Point(222, 105), Size = new Size(95, 34) };
        var cancel = new Button { Text = german ? "Abbrechen" : "Cancel", DialogResult = DialogResult.Cancel,
            Location = new Point(325, 105), Size = new Size(100, 34) };
        dialog.Controls.AddRange([explanation, minutes, hint, save, cancel]);
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            stateReminder?.SetInterval((int)minutes.Value, custom: true);
            if (reminderMenu is not null) RefreshReminderMenu(reminderMenu);
        }
    }
    private static string AiModelPath => Path.Combine(Store.Folder, "ai-settings.json");
    private void LoadAiModel()
    {
        try
        {
            if (File.Exists(AiModelPath))
            {
                var saved = JsonSerializer.Deserialize<string>(File.ReadAllText(AiModelPath));
                if (saved is "gpt-6-sol" or "gpt-6-astra" or "gpt-6-luna") aiModel = saved;
            }
        }
        catch (IOException) { }
        catch (JsonException) { }
    }
    private void ConfigureAi()
    {
        var de = language == "de";
        using var dialog = new Form { Text = de ? "KI-Verbindung einrichten" : "Set up AI connection", StartPosition = FormStartPosition.CenterParent, Size = new Size(760, 460), MinimumSize = new Size(700, 440), Font = Font, Icon = Icon, BackColor = Canvas, AutoScaleMode = AutoScaleMode.Font };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 22, 24, 20), ColumnCount = 1, RowCount = 6 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        var key = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(2, 0, 2, 14), UseSystemPasswordChar = true, Text = AiConnection.ReadKey() ?? "" };
        var model = new ComboBox { Dock = DockStyle.Fill, Margin = new Padding(2, 0, 2, 14), DropDownStyle = ComboBoxStyle.DropDownList };
        model.Items.AddRange(["gpt-6-sol", "gpt-6-astra", "gpt-6-luna"]);
        model.SelectedItem = aiModel;
        layout.Controls.Add(new Label { Text = de ? "OpenAI API-Schlüssel:" : "OpenAI API key:", Dock = DockStyle.Fill, Padding = new Padding(2, 2, 0, 0) }, 0, 0);
        layout.Controls.Add(key, 0, 1);
        layout.Controls.Add(new Label { Text = de ? "Modell:" : "Model:", Dock = DockStyle.Fill, Padding = new Padding(2, 2, 0, 0) }, 0, 2);
        layout.Controls.Add(model, 0, 3);
        layout.Controls.Add(new Label { Text = de
            ? "Der Schlüssel wird in der Windows-Anmeldeinformationsverwaltung gespeichert.\nProtokolldaten werden nur nach deiner Bestätigung übertragen.\nDie API-Nutzung wird separat berechnet. Leer speichern entfernt den Schlüssel."
            : "The key is saved in Windows Credential Manager.\nLog data is sent only after your confirmation.\nAPI usage is billed separately. Saving an empty field removes the key.",
            Dock = DockStyle.Fill, Padding = new Padding(2, 12, 2, 4) }, 0, 4);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var save = Button(de ? "Speichern" : "Save", () => { dialog.DialogResult = DialogResult.OK; dialog.Close(); });
        var cancel = Button(de ? "Abbrechen" : "Cancel", () => { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); });
        foreach (var button in new[] { save, cancel }) { button.AutoSize = false; button.Size = new Size(140, 37); button.Margin = new Padding(7, 4, 0, 0); }
        actions.Controls.Add(save); actions.Controls.Add(cancel); layout.Controls.Add(actions, 0, 5);
        dialog.Controls.Add(layout);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            AiConnection.SaveKey(key.Text);
            aiModel = model.SelectedItem?.ToString() ?? "gpt-6-sol";
            File.WriteAllText(AiModelPath, JsonSerializer.Serialize(aiModel));
        }
        catch (Exception ex) { Error(ex); }
    }
    private async Task AnalyzeWithAiAsync()
    {
        if (aiBusy) return;
        var key = AiConnection.ReadKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            ConfigureAi();
            key = AiConnection.ReadKey();
            if (string.IsNullOrWhiteSpace(key)) return;
        }
        var de = language == "de";
        var selected = AiEntries();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, de ? "Im gewählten Zeitraum liegen keine Einträge vor." : "No records in the selected period.");
            return;
        }
        var recent = selected.TakeLast(300).ToList();
        var data = AiSnapshot(recent);
        using var dialog = new Form { Text = de ? "Daten für die KI-Anfrage prüfen" : "Review data for AI request", StartPosition = FormStartPosition.CenterParent, Size = new Size(850, 660), MinimumSize = new Size(650, 480), Font = Font, Icon = Icon };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 47));
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = de
            ? $"{recent.Count} von {selected.Count} Einträgen (höchstens die letzten 300) inklusive Notizen und Medikamentenangaben. Du kannst den Text vor dem Senden bearbeiten. Die Anfrage kann API-Kosten verursachen."
            : $"{recent.Count} of {selected.Count} records (at most the latest 300), including notes and medication details. You may edit the text before sending. API charges may apply." }, 0, 0);
        var preview = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9), Text = data };
        layout.Controls.Add(preview, 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        actions.Controls.Add(Button(de ? "An OpenAI senden" : "Send to OpenAI", () => { dialog.DialogResult = DialogResult.OK; dialog.Close(); }));
        actions.Controls.Add(Button(de ? "Abbrechen" : "Cancel", () => { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); }));
        layout.Controls.Add(actions, 0, 2); dialog.Controls.Add(layout);
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(preview.Text)) return;
        aiBusy = true;
        period.Enabled = false;
        assessmentTabs.SelectedIndex = 1;
        aiInfo.Text = de ? "Neue Auswertung läuft ..." : "Generating a new analysis ...";
        aiResult.Text = de ? "Analyse läuft ..." : "Analyzing ...";
        try
        {
            var question = de ? "Analysiere die dokumentierte Lage und mögliche Muster für künftiges Pacing. Antworte auf Deutsch. Die folgenden Daten wurden von mir zur Übertragung freigegeben:\n" : "Analyze documented condition and potential patterns for future pacing. Reply in English. I approved sending these records:\n";
            var response = await AiConnection.AnalyzeAsync(key, aiModel, question + preview.Text);
            store.SaveAiAnalysis(period.SelectedIndex, language, aiModel, AiSnapshotHash(preview.Text), response);
            aiBusy = false;
            RestoreAiResponse();
        }
        catch (Exception ex)
        {
            aiResult.Text = de ? "Die KI-Anfrage ist fehlgeschlagen: " + ex.Message : "The AI request failed: " + ex.Message;
            aiInfo.Text = de ? "Keine neue Antwort gespeichert." : "No new response was saved.";
        }
        finally { aiBusy = false; period.Enabled = true; }
    }
    private List<Entry> AiEntries()
    {
        DateTime threshold = period.SelectedIndex switch { 0 => DateTime.Now.AddDays(-7), 1 => DateTime.Now.AddDays(-30),
            2 => DateTime.Now.AddMonths(-3), 3 => DateTime.Now.AddYears(-1), _ => DateTime.MinValue };
        return entries.Where(e => e.Start >= threshold && e.Start <= DateTime.Now).OrderBy(e => e.Start).ThenBy(e => e.Id).ToList();
    }
    private static string AiSnapshot(IEnumerable<Entry> selected) => JsonSerializer.Serialize(selected.Select(e => new
        { Type = e.Kind, e.Start, e.End, e.Data, e.Note }), new JsonSerializerOptions { WriteIndented = true });
    private static string AiSnapshotHash(string snapshot) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
    private void RestoreAiResponse()
    {
        if (aiBusy || period.SelectedIndex < 0) return;
        var saved = store.LatestAiAnalysis(period.SelectedIndex, language);
        if (saved is null)
        {
            aiResult.Clear(); aiInfo.Text = language == "de" ? "Für diesen Zeitraum ist noch keine KI-Antwort gespeichert." : "No AI response is saved for this period.";
            return;
        }
        ShowAiResponse(saved.Response);
        var current = AiSnapshotHash(AiSnapshot(AiEntries().TakeLast(300)));
        var outdated = saved.SnapshotHash != current;
        aiInfo.Text = language == "de"
            ? $"Gespeichert am {saved.Created.ToLocalTime():dd.MM.yyyy HH:mm} · {saved.Model}" +
              (outdated ? "\nSeit dieser Auswertung haben sich die ausgewerteten Daten geändert." : "")
            : $"Saved on {saved.Created.ToLocalTime():MM/dd/yyyy HH:mm} · {saved.Model}" +
              (outdated ? "\nThe analyzed data has changed since this response." : "");
        assessmentTabs.SelectedIndex = 1;
    }
    private void ShowAiResponse(string markdown)
    {
        aiResult.Clear();
        var text = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        // Some models place a bullet directly after a heading without a line break.
        text = Regex.Replace(text, @"(?m)^(#{1,6}\s+[^\n]+?)(\s*-\s+\*\*)", "$1\n$2");
        using var headingFont = new Font(aiResult.Font, FontStyle.Bold);
        using var boldFont = new Font(aiResult.Font, FontStyle.Bold);
        foreach (var line in text.Split('\n'))
        {
            var heading = Regex.Match(line, @"^\s*#{1,6}\s+(.+)$");
            var content = heading.Success ? heading.Groups[1].Value : line;
            if (heading.Success)
            {
                if (aiResult.TextLength > 0) aiResult.AppendText(Environment.NewLine);
                aiResult.SelectionFont = headingFont;
                aiResult.SelectionColor = Ink;
                aiResult.AppendText(content.Trim() + Environment.NewLine);
                aiResult.SelectionFont = aiResult.Font;
                continue;
            }
            content = Regex.Replace(content, @"^\s*[-*]\s+", "  • ");
            var parts = Regex.Split(content, @"(\*\*[^*]+\*\*)");
            foreach (var part in parts)
            {
                var bold = part.StartsWith("**", StringComparison.Ordinal) && part.EndsWith("**", StringComparison.Ordinal) && part.Length > 4;
                aiResult.SelectionFont = bold ? boldFont : aiResult.Font;
                aiResult.SelectionColor = Ink;
                aiResult.AppendText(bold ? part[2..^2] : part.Replace("**", ""));
            }
            aiResult.AppendText(Environment.NewLine);
        }
        aiResult.SelectionStart = 0;
        aiResult.ScrollToCaret();
    }
    private static void ApplyTheme(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            control.ForeColor = Ink;
            switch (control)
            {
                case TabPage page:
                    page.BackColor = Surface;
                    break;
                case DataGridView view:
                    view.AllowUserToResizeRows = false;
                    view.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
                    view.BackgroundColor = Surface;
                    view.GridColor = Pale;
                    view.BorderStyle = BorderStyle.None;
                    view.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                    view.EnableHeadersVisualStyles = false;
                    view.ColumnHeadersDefaultCellStyle.BackColor = Pale;
                    view.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
                    view.ColumnHeadersDefaultCellStyle.SelectionBackColor = Pale;
                    view.ColumnHeadersDefaultCellStyle.SelectionForeColor = Ink;
                    view.DefaultCellStyle.BackColor = Surface;
                    view.DefaultCellStyle.ForeColor = Ink;
                    view.DefaultCellStyle.SelectionBackColor = Color.FromArgb(211, 232, 239);
                    view.DefaultCellStyle.SelectionForeColor = Ink;
                    view.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 253);
                    view.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(211, 232, 239);
                    view.AlternatingRowsDefaultCellStyle.SelectionForeColor = Ink;
                    view.RowTemplate.Height = 33;
                    break;
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 1;
                    button.FlatAppearance.BorderColor = Color.FromArgb(190, 207, 217);
                    button.BackColor = Surface;
                    button.FlatAppearance.MouseOverBackColor = Pale;
                    if (button.Text.Contains("speichern", StringComparison.OrdinalIgnoreCase))
                    {
                        button.BackColor = Accent;
                        button.ForeColor = Surface;
                        button.FlatAppearance.BorderColor = Accent;
                        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(14, 97, 114);
                    }
                    break;
                case TextBox or ComboBox or NumericUpDown or DateTimePicker or CheckedListBox:
                    control.BackColor = Surface;
                    break;
                case TabControl:
                    control.BackColor = Canvas;
                    break;
                case ChartPanel:
                    control.BackColor = Surface;
                    break;
                default:
                    control.BackColor = parent is TabControl ? Canvas : parent.BackColor;
                    break;
            }
            ApplyTheme(control);
        }
    }
    private static void StyleTabs(TabControl tabs, int width)
    {
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.ItemSize = new Size(width, 44);
        tabs.DrawItem += (_, e) =>
        {
            var selected = e.Index == tabs.SelectedIndex;
            var bounds = tabs.GetTabRect(e.Index);
            using var background = new SolidBrush(selected ? Surface : Pale);
            e.Graphics.FillRectangle(background, bounds);
            if (selected)
            {
                using var accent = new SolidBrush(Accent);
                e.Graphics.FillRectangle(accent, bounds.Left + 1, bounds.Bottom - 4, bounds.Width - 2, 4);
            }
            var icon = new Rectangle(bounds.Left + 13, bounds.Top + (bounds.Height - 18) / 2, 18, 18);
            DrawTabIcon(e.Graphics, icon, tabs.TabPages[e.Index].Text);
            var label = new Rectangle(icon.Right + 8, bounds.Top + 2, bounds.Right - icon.Right - 13, bounds.Height - 5);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, label, Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if ((e.State & DrawItemState.Focus) != 0 && tabs.Focused)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -5, -5));
        };
    }
    private static void DrawTabIcon(Graphics graphics, Rectangle area, string title)
    {
        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Accent, 1.8f);
        if (title.Contains("Zustand") || title.Contains("Auswertung") || title.Contains("condition", StringComparison.OrdinalIgnoreCase) || title == "Analysis")
        {
            var y = area.Top + area.Height / 2;
            graphics.DrawLines(pen, new[] { new Point(area.Left, y), new Point(area.Left + 4, y),
                new Point(area.Left + 7, y - 6), new Point(area.Left + 11, y + 5),
                new Point(area.Left + 14, y), new Point(area.Right, y) });
        }
        else if (title.Contains("Schlaf") || title.Contains("protokoll") || title == "Tag" || title.Contains("sleep", StringComparison.OrdinalIgnoreCase) || title.Contains("log", StringComparison.OrdinalIgnoreCase))
        {
            graphics.DrawEllipse(pen, area.Left + 2, area.Top + 2, 14, 14);
            graphics.DrawLine(pen, area.Left + 9, area.Top + 9, area.Left + 9, area.Top + 5);
            graphics.DrawLine(pen, area.Left + 9, area.Top + 9, area.Left + 13, area.Top + 11);
        }
        else if (title.Contains("Medikament") || title.Contains("Vorrat") || title == "Plan" || title.Contains("Medication", StringComparison.OrdinalIgnoreCase) || title.Contains("inventory", StringComparison.OrdinalIgnoreCase) || title.Contains("schedule", StringComparison.OrdinalIgnoreCase))
        {
            var state = graphics.Save();
            graphics.TranslateTransform(area.Left + 9, area.Top + 9);
            graphics.RotateTransform(-36);
            graphics.DrawEllipse(pen, -8, -5, 16, 10);
            graphics.DrawLine(pen, 0, -5, 0, 5);
            graphics.Restore(state);
        }
        else if (title.Contains("Maßnahme") || title.Contains("Intervention"))
        {
            graphics.DrawRectangle(pen, area.Left + 2, area.Top + 2, 14, 14);
            graphics.DrawLine(pen, area.Left + 9, area.Top + 5, area.Left + 9, area.Top + 13);
            graphics.DrawLine(pen, area.Left + 5, area.Top + 9, area.Left + 13, area.Top + 9);
        }
        else
        {
            for (int i = 0; i < 3; ++i)
                graphics.DrawLine(pen, area.Left + 2, area.Top + 4 + i * 5, area.Right - 2, area.Top + 4 + i * 5);
        }
        graphics.SmoothingMode = previous;
    }
    private void RestoreWindowSettings()
    {
        try
        {
            if (!File.Exists(WindowSettingsPath)) return;
            var saved = JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(WindowSettingsPath));
            if (saved is not null) foreach (var item in saved.Tables ?? new()) tableSettings[item.Key] = item.Value;
            startInTray = saved?.StartInTray == true;
            if (saved?.Language is "de" or "en") language = saved.Language;
            if (saved is null || saved.Width < MinimumSize.Width || saved.Height < MinimumSize.Height) return;
            var bounds = new Rectangle(saved.X, saved.Y, saved.Width, saved.Height);
            var screen = Screen.AllScreens.FirstOrDefault(s =>
            {
                var visible = Rectangle.Intersect(s.WorkingArea, bounds);
                return visible.Width >= 100 && visible.Height >= 100;
            });
            if (screen is null) return;
            var area = screen.WorkingArea;
            var width = Math.Min(saved.Width, Math.Max(MinimumSize.Width, area.Width));
            var height = Math.Min(saved.Height, Math.Max(MinimumSize.Height, area.Height));
            var x = Math.Clamp(saved.X, area.Left, Math.Max(area.Left, area.Right - width));
            var y = Math.Clamp(saved.Y, area.Top, Math.Max(area.Top, area.Bottom - height));
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(x, y, width, height);
            if (saved.Maximized) WindowState = FormWindowState.Maximized;
        }
        catch (IOException) { } // Damaged or unavailable settings should not prevent startup.
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
        catch (NotSupportedException) { }
    }
    private void SaveWindowSettings()
    {
        try
        {
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (bounds.Width < MinimumSize.Width || bounds.Height < MinimumSize.Height) return;
            var settings = new WindowSettings
            {
                X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height,
                Maximized = WindowState == FormWindowState.Maximized ||
                    (WindowState == FormWindowState.Minimized && restoreWindowState == FormWindowState.Maximized),
                StartInTray = startInTray
            };
            settings.Language = language;
            foreach (var (name, view) in PersistentTables())
            {
                settings.Tables[name] = new GridSettings
                {
                    ColumnWeights = view.Columns.Cast<DataGridViewColumn>().ToDictionary(column => column.Name, column => column.FillWeight),
                    SortColumn = view.SortedColumn?.Name ?? (tableSettings.TryGetValue(name, out var previous) ? previous.SortColumn : null),
                    SortDescending = view.SortedColumn is null && tableSettings.TryGetValue(name, out var previousSort) ? previousSort.SortDescending : view.SortOrder == SortOrder.Descending
                };
            }
            Directory.CreateDirectory(Store.Folder);
            var temporary = WindowSettingsPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
            File.Move(temporary, WindowSettingsPath, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    private static DateTimePicker Clock() => new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy HH:mm", Width = 215, ShowUpDown = false };
    private static ComboBox Choice(string[] values)
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, Width = 175 };
        box.ItemHeight = Math.Max(32, TextRenderer.MeasureText("Ägjpqy", box.Font).Height + 12);
        box.DrawItem += (_, e) =>
        {
            e.DrawBackground();
            if (e.Index >= 0)
            {
                var bounds = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top + 1, Math.Max(0, e.Bounds.Width - 8), Math.Max(0, e.Bounds.Height - 2));
                TextRenderer.DrawText(e.Graphics, box.GetItemText(box.Items[e.Index]), e.Font ?? box.Font,
                    bounds, e.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            e.DrawFocusRectangle();
        };
        box.Items.AddRange(values); return box;
    }
    private static Button Button(string label, Action action)
    {
        var b = new Button { Text = label, AutoSize = true, MinimumSize = new Size(105, 29), Margin = new Padding(3) };
        b.Click += (_,_) => action(); return b;
    }
    private static Label Label(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(4, 8, 6, 0) };
    private static void AlignRowControls(Control.ControlCollection controls)
    {
        if (controls.Count == 0) return;
        var lineHeight = controls.Cast<Control>().Max(control => control.Height);
        foreach (Control control in controls)
        {
            var margin = control.Margin;
            // NumericUpDown paints its text slightly above the visual center of its border.
            var top = Math.Max(0, (lineHeight - control.Height) / 2) + (control is NumericUpDown ? 3 : 0);
            if (margin.Top != top) control.Margin = new Padding(margin.Left, top, margin.Right, margin.Bottom);
        }
    }
    private static FlowLayoutPanel Row(params Control[] items)
    {
        var p = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 64, WrapContents = false };
        p.Controls.AddRange(items);
        AlignRowControls(p.Controls);
        p.FontChanged += (_, _) => AlignRowControls(p.Controls);
        return p;
    }
    private static TableLayoutPanel NoteFooter(TextBox note, string saveText, Action save, Action reset)
    {
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Margin = new Padding(0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.Controls.Add(new Label { Text = "Notiz:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(4, 0, 0, 0) }, 0, 0);
        note.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        note.Margin = new Padding(4, 0, 4, 0);
        footer.Controls.Add(note, 1, 0);
        var saveButton = Button(saveText, save);
        var newButton = Button("Neu", reset);
        foreach (var button in new[] { saveButton, newButton })
        {
            button.AutoSize = false;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(4, 10, 4, 10);
        }
        footer.Controls.Add(saveButton, 2, 0);
        footer.Controls.Add(newButton, 3, 0);
        return footer;
    }
    private static Panel Stack(params Control[] rows)
    {
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };
        for (int i = rows.Length - 1; i >= 0; --i) panel.Controls.Add(rows[i]);
        return panel;
    }
    private TabPage MakeStateTab()
    {
        var page = new TabPage("Zustand erfassen") { Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        page.Controls.Add(layout);
        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        var timeLabel = Label("Zeit:");
        timeLabel.Margin = new Padding(0, 0, 6, 0);
        var pulseLabel = Label("Puls (optional):");
        pulseLabel.Margin = new Padding(4, 0, 6, 0);
        header.Controls.AddRange([timeLabel, stateTime, Label("Allgemein:"), overall,
            Label("PEM:"), pem, crash, pulseLabel, pulse]);
        AlignRowControls(header.Controls);
        header.FontChanged += (_, _) => AlignRowControls(header.Controls);
        layout.Controls.Add(header, 0, 0);
        var symptomHeading = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0), ColumnCount = 2, RowCount = 1 };
        symptomHeading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        symptomHeading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 426));
        var symptomActions = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 48,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        foreach (var (caption, severity) in new[] { ("Alle: nicht beurteilt", -1), ("Alle: keine", 0) })
        {
            var action = Button(caption, () => { foreach (var box in symptoms.Values) box.SelectedIndex = severity + 1; });
            action.AutoSize = false; action.Width = 202; action.Height = 35; action.Margin = new Padding(4, 4, 4, 0);
            symptomActions.Controls.Add(action);
        }
        symptomHeading.Controls.Add(new Label { Text = "Symptome", Dock = DockStyle.Fill, Margin = new Padding(0),
            Padding = new Padding(0, 1, 0, 10), Font = new Font(Font.FontFamily, Font.SizeInPoints + 1.5f, FontStyle.Bold) }, 0, 0);
        symptomHeading.Controls.Add(symptomActions, 1, 0);
        layout.Controls.Add(symptomHeading, 0, 1);
        var symptomHost = new Panel { Dock = DockStyle.Fill, AutoScroll = false, Margin = new Padding(0) };
        var symptomGrid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 288, ColumnCount = 5, RowCount = 3, Margin = new Padding(0) };
        for (int i = 0; i < 5; i++) symptomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        for (int i = 0; i < 3; i++) symptomGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        for (int i = 0; i < SymptomNames.Length; i++)
        {
            string name = SymptomNames[i];
            var box = Choice(["nicht beurteilt", "keine", "leicht", "mittel", "stark", "extrem"]);
            box.SelectedIndex = 0;
            box.Dock = DockStyle.Top;
            box.Margin = new Padding(0, 5, 0, 0);
            symptoms[name] = box;
            var tile = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 14, 2) };
            int captionHeight = Math.Max(32, TextRenderer.MeasureText("Ägjpqy", Font).Height + 12);
            tile.RowStyles.Add(new RowStyle(SizeType.Absolute, captionHeight));
            tile.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tile.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, Margin = new Padding(0), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 0, 0, 4) }, 0, 0);
            tile.Controls.Add(box, 0, 1);
            symptomGrid.Controls.Add(tile, i % 5, i / 5);
        }
        symptomHost.Controls.Add(symptomGrid);
        layout.Controls.Add(symptomHost, 0, 2);
        var painArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
        painArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        painArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        painArea.Controls.Add(new Label { Text = "Schmerzorte", Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(0, 1, 0, 10), Font = new Font(Font.FontFamily, Font.SizeInPoints + 1.5f, FontStyle.Bold) }, 0, 0);
        var painFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true, Margin = new Padding(0) };
        foreach (string name in PainNames)
        {
            var check = new CheckBox { Text = name, Width = 135, Height = 34, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(5, 0, 2, 0) };
            painChecks[name] = check; painFlow.Controls.Add(check);
        }
        painArea.Controls.Add(painFlow, 0, 1);
        layout.Controls.Add(painArea, 0, 3);
        layout.Controls.Add(NoteFooter(stateNote, "Zustand speichern", SaveState, ResetEdit), 0, 4);
        return page;
    }
    private TabPage MakeIntervalTab()
    {
        var page = new TabPage("Aktivität, Ruhe und Schlaf") { Padding = new Padding(8) };
        intervalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        intervalLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        intervalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        intervalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        intervalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        intervalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        intervalLayout.Controls.Add(Row(Label("Typ:"), intervalKind, Label("Beginn:"), intervalStart, Label("Ende:"), intervalEnd, openActivity), 0, 0);
        activityFields.Controls.Add(dimensions);
        var dimensionBar = new Panel { Dock = DockStyle.Top, Height = 43 };
        var dimensionButton = Button("...", () => ManageChoiceOptions(intervalKind.SelectedIndex == 1 ? "rest_dimensions" : "activity_dimensions"));
        dimensionButton.AutoSize = false; dimensionButton.MinimumSize = Size.Empty; dimensionButton.Width = 48;
        dimensionOptionsButton = dimensionButton;
        dimensionButton.Dock = DockStyle.Right;
        dimensionBar.Controls.Add(dimensionButton);
        dimensionBar.Controls.Add(new Label { Text = "Belastungsarten / Ruheformen (Mehrfachauswahl):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft });
        activityFields.Controls.Add(dimensionBar);
        activityFields.Controls.Add(Row(Label("Intensität:"), intensity));
        intervalLayout.Controls.Add(activityFields, 0, 1);
        sleepFields.Controls.AddRange([Label("Erholung nach dem Schlaf:"), sleepRecovery]);
        sleepRecovery.SelectedIndex = 0;
        intervalLayout.Controls.Add(sleepFields, 0, 2);
        var protectionLabel = new Label { Text = "Akustischer Schutz:", AutoSize = false, Width = 174, Height = 38,
            Margin = new Padding(4, 6, 6, 0), TextAlign = ContentAlignment.MiddleLeft };
        foreach (var check in new[] { ncHeadphones, loopEarplugs, calmerEarplugs })
        {
            check.TextAlign = ContentAlignment.MiddleLeft;
            check.Margin = new Padding(3, 6, 3, 0);
        }
        intervalLayout.Controls.Add(Row(protectionLabel, ncHeadphones, loopEarplugs, calmerEarplugs), 0, 3);
        intervalLayout.Controls.Add(NoteFooter(intervalNote, "Zeitraum speichern", SaveInterval, ResetEdit), 0, 4);
        page.Controls.Add(intervalLayout);
        return page;
    }
    private TabPage MakeMeasureTab()
    {
        var page = new TabPage("Maßnahme") { Padding = new Padding(8) };
        var measureLayout = new TableLayoutPanel { Dock = DockStyle.Top, Height = 128, ColumnCount = 1, RowCount = 2 };
        measureLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        measureLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        var manageMeasures = Button("...", () => ManageChoiceOptions("measures"));
        manageMeasures.AutoSize = false; manageMeasures.MinimumSize = Size.Empty; manageMeasures.Width = 48;
        measureOptionsButton = manageMeasures;
        measureLayout.Controls.Add(Row(Label("Zeit:"), measureTime, Label("Maßnahme:"), measure, manageMeasures, Label("Dosis (optional):"), dose), 0, 0);
        measureLayout.Controls.Add(NoteFooter(measureNote, "Maßnahme speichern", SaveMeasure, ResetEdit), 0, 1);
        page.Controls.Add(measureLayout);
        return page;
    }
    private TabPage MakeMedicationTab()
    {
        var page = new TabPage("Medikamente und Supplemente") { Padding = new Padding(8) };
        page.Controls.Add(medicationTabs);
        var planPage = new TabPage("Einnahmeplan") { Padding = new Padding(6) };
        var dayPage = new TabPage("Tagesprotokoll") { Padding = new Padding(6) };
        medicationTabs.TabPages.Add(planPage); medicationTabs.TabPages.Add(dayPage); medicationTabs.TabPages.Add(stockPage);
        BuildStockPage();
        var planLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        planLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        planLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        planLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        planPage.Controls.Add(planLayout);
        planQuantity.Items.AddRange(["0,5", "1", "1,5", "2", "2,5", "3", "4", "5", "6", "7", "8", "9", "10", "15", "20", "30"]);
        planQuantity.Text = "1";
        blankPlanTime = planTime.Value.ToString("HH:mm");
        planForm.Items.AddRange(["Kapsel", "Tablette", "mg", "ml", "Spray", "Pflaster"]);
        planOngoing.CheckedChanged += (_, _) => planEnd.Enabled = !planOngoing.Checked;
        planStart.ValueChanged += (_, _) => planStart.Tag = null;
        planLayout.Controls.Add(Row(Label("Uhrzeit:"), planTime, Label("Präparat:"), planName, Label("Dosis:"), planDose,
            Label("Ab wann:"), planStart, Label("Bis wann:"), planEnd, planOngoing), 0, 0);
        planLayout.Controls.Add(Row(Label("Anzahl:"), planQuantity, Label("Form:"), planForm,
            Button("Warum ...", EditMedicationGoals), goalSummary,
            Button("Plan speichern", SaveMedicationPlan), Button("Aus Plan entfernen", DeleteMedicationPlan)), 0, 1);
        UpdateGoalSummary();
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        planLayout.Controls.Add(columns, 0, 2);
        foreach (var planView in new[] { planGrid, planGridRight })
        {
            planView.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Uhrzeit", FillWeight = 13 });
            planView.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Präparat", FillWeight = 37 });
            planView.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Dosis", FillWeight = 17 });
            planView.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Anzahl", FillWeight = 13 });
            planView.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Form", FillWeight = 17 });
            planView.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Warum", FillWeight = 16 });
            planView.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ab wann", FillWeight = 19 });
            planView.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bis wann", FillWeight = 19 });
            planView.CellClick += (_, e) => { if (e.RowIndex >= 0) EditMedicationPlan(planView); };
        }
        columns.Controls.Add(planGrid, 0, 0); columns.Controls.Add(planGridRight, 1, 0);
        var dayLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        dayLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        dayLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        dayLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        dayPage.Controls.Add(dayLayout);
        var dayToolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        dayToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        dayToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
        dayToolbar.Controls.Add(Row(Label("Einnahmen am:"), intakeDay,
            new Label { Text = "Offen bedeutet: keine Angabe zur tatsächlichen Einnahme.", AutoSize = true, Margin = new Padding(16, 9, 0, 0) }), 0, 0);
        var bulkActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        bulkActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bulkActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bulkActions.Controls.Add(Button("Alles genommen", () => SetAllIntakes(Localization.StatusLabel("taken", language))), 0, 0);
        bulkActions.Controls.Add(Button("Alles offen", () => SetAllIntakes(Localization.StatusLabel("pending", language))), 1, 0);
        foreach (Control button in bulkActions.Controls)
        {
            button.AutoSize = false;
            button.Dock = DockStyle.Top;
            button.Height = 42;
            button.Margin = new Padding(4, 10, 4, 0);
        }
        dayToolbar.Controls.Add(bulkActions, 1, 0);
        dayLayout.Controls.Add(dayToolbar, 0, 0);
        intakeGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "time", HeaderText = "Uhrzeit", ReadOnly = true, FillWeight = 12 });
        intakeGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Präparat", ReadOnly = true, FillWeight = 29 });
        intakeGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "planned", HeaderText = "Geplante Dosis", ReadOnly = true, FillWeight = 18 });
        intakeGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "quantity", HeaderText = "Anzahl", ReadOnly = true, FillWeight = 11 });
        intakeGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "form", HeaderText = "Form", ReadOnly = true, FillWeight = 14 });
        intakeGrid.Columns.Add(new DataGridViewComboBoxColumn { Name = "status", HeaderText = "Einnahme", DataSource = new[] { "Offen", "Genommen", "Ausgelassen" }, FillWeight = 16 });
        intakeGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "actual", HeaderText = "Tatsächliche Dosis", FillWeight = 19 });
        intakeGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "actualQuantity", HeaderText = "Tatsächl. Anzahl", FillWeight = 14 });
        intakeGrid.DataError += (_, e) => { e.ThrowException = false; };
        intakeGrid.CurrentCellDirtyStateChanged += (_, _) => { if (intakeGrid.IsCurrentCellDirty) intakeGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        var intakeHost = new Panel { Dock = DockStyle.Fill, BackColor = Surface };
        intakeGrid.Dock = DockStyle.Top;
        intakeHost.Controls.Add(intakeGrid);
        void FitWholeRows()
        {
            var available = intakeHost.ClientSize.Height;
            if (available <= 0) return;
            var headerHeight = intakeGrid.ColumnHeadersVisible ? intakeGrid.ColumnHeadersHeight : 0;
            var rowHeight = intakeGrid.Rows.Count > 0 ? intakeGrid.Rows[0].Height : intakeGrid.RowTemplate.Height;
            var fullRows = Math.Max(0, (available - headerHeight - 8) / Math.Max(1, rowHeight));
            intakeGrid.Height = Math.Min(available, headerHeight + fullRows * rowHeight + 2);
        }
        intakeHost.Resize += (_, _) => FitWholeRows();
        intakeGrid.RowsAdded += (_, _) => FitWholeRows();
        dayLayout.Controls.Add(intakeHost, 0, 1);
        dayLayout.Controls.Add(Row(Button("Einnahmen für diesen Tag speichern", SaveMedicationDay)), 0, 2);
        intakeDay.ValueChanged += (_, _) => LoadMedicationDay();
        return page;
    }
    private void BuildStockPage()
    {
        stockPage.Padding = new Padding(6);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        stockPage.Controls.Add(layout);
        layout.Controls.Add(Row(Label("Hersteller:"), manufacturer, Label("Lieferant:"), supplier,
            Label("Inhalt je Packung:"), packUnits, Label("Preis €:"), packPrice), 0, 0);
        layout.Controls.Add(Row(Button("Packungsdaten speichern", () => { SaveProductDetails(); }), Label("Gekaufte Packungen:"), purchasePacks,
            Button("Nachkauf erfassen", RecordPurchase)), 0, 1);
        layout.Controls.Add(Row(Label("Gezählter Bestand (Einheiten):"), stockTarget,
            Button("Bestand setzen", SetStock), Button("Buchungen anzeigen", ShowStockMovements)), 0, 2);
        layout.Controls.Add(stockSummary, 0, 3);
        productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Präparat", FillWeight = 26 });
        productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Form", FillWeight = 17 });
        productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Hersteller", FillWeight = 16 });
        productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Lieferant", FillWeight = 16 });
        productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Packung", FillWeight = 10 });
        productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Preis €", FillWeight = 10 });
        productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bestand", FillWeight = 10 });
        productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "7 Tage", FillWeight = 10 });
        productGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Hinweis", FillWeight = 15 });
        productGrid.CellClick += (_, e) => { if (e.RowIndex >= 0) SelectProduct(); };
        layout.Controls.Add(productGrid, 0, 4);
    }
    private void ResetDimensions()
    {
        dimensions.Items.Clear();
        if (intervalKind.SelectedIndex != 2)
            dimensions.Items.AddRange((intervalKind.SelectedIndex == 1 ? restOptions : activityOptions)
                .Select(T)
                .OrderBy(name => name, StringComparer.Create(CultureInfo.GetCultureInfo(language == "de" ? "de-DE" : "en-US"), true))
                .Cast<object>().ToArray());
        bool isSleep = intervalKind.SelectedIndex == 2;
        activityFields.Visible = !isSleep;
        sleepFields.Visible = isSleep;
        intervalLayout.RowStyles[1].SizeType = isSleep ? SizeType.Absolute : SizeType.Percent;
        intervalLayout.RowStyles[1].Height = isSleep ? 0 : 100;
        intervalLayout.RowStyles[2].Height = isSleep ? 64 : 0;
        intervalLayout.RowStyles[5].SizeType = isSleep ? SizeType.Percent : SizeType.Absolute;
        intervalLayout.RowStyles[5].Height = isSleep ? 100 : 0;
    }
    private void ReloadChoiceOptions()
    {
        activityOptions = store.ChoiceOptions("activity_dimensions", ActivityNames);
        restOptions = store.ChoiceOptions("rest_dimensions", RestNames);
        measureOptions = store.ChoiceOptions("measures", MeasureNames);
        var selected = Localization.Canonical(measure.Text);
        measure.Items.Clear();
        measure.Items.AddRange(measureOptions.Select(T).Cast<object>().ToArray());
        var index = measureOptions.FindIndex(name => string.Equals(name, selected, StringComparison.OrdinalIgnoreCase));
        measure.SelectedIndex = index >= 0 ? index : measure.Items.Count > 0 ? 0 : -1;
        ResetDimensions();
    }
    private void ManageChoiceOptions(string key)
    {
        var names = (key switch
        {
            "measures" => measureOptions,
            "rest_dimensions" => restOptions,
            _ => activityOptions
        }).ToList();
        var title = key switch
        {
            "measures" => language == "de" ? "Maßnahmen verwalten" : "Manage measures",
            "rest_dimensions" => language == "de" ? "Ruheformen verwalten" : "Manage rest types",
            _ => language == "de" ? "Belastungsarten verwalten" : "Manage activity types"
        };
        using var dialog = new Form { Text = title, StartPosition = FormStartPosition.CenterParent,
            Size = new Size(680, 720), MinimumSize = new Size(660, 500), Font = Font,
            ShowIcon = false, BackColor = SystemColors.Control };
        var header = new Panel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(18, 12, 18, 10) };
        header.Controls.Add(new PictureBox { Dock = DockStyle.Right, Width = 44, Image = Icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom });
        header.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, Font.SizeInPoints + 1.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });
        var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 42, BorderStyle = BorderStyle.None, BackColor = SystemColors.Window };
        list.DrawItem += (_, e) =>
        {
            e.DrawBackground();
            if (e.Index >= 0)
                TextRenderer.DrawText(e.Graphics, T(list.GetItemText(list.Items[e.Index])), e.Font ?? list.Font,
                    new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, e.Bounds.Width - 16, e.Bounds.Height), e.ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        };
        var input = new TextBox { Width = 210 };
        void RefreshList(string? selected = null)
        {
            list.Items.Clear();
            list.Items.AddRange(names.OrderBy(T, StringComparer.Create(CultureInfo.GetCultureInfo(language == "de" ? "de-DE" : "en-US"), true)).Cast<object>().ToArray());
            if (selected != null) list.SelectedItem = selected;
        }
        list.SelectedIndexChanged += (_, _) => { if (list.SelectedItem is string name) input.Text = T(name); };
        var message = language == "de" ? "Bitte einen eindeutigen Namen eingeben." : "Enter a unique name.";
        bool Valid(string name, string? current = null) => name.Length > 0 && !names.Any(existing => existing != current &&
            string.Equals(Localization.Canonical(existing), Localization.Canonical(name), StringComparison.OrdinalIgnoreCase));
        var add = Button(language == "de" ? "Hinzufügen" : "Add", () =>
        {
            var name = Localization.Canonical(input.Text.Trim());
            if (!Valid(name)) { MessageBox.Show(dialog, message); return; }
            names.Add(name); RefreshList(name);
        });
        add.AutoSize = false; add.Size = new Size(150, 46); add.Margin = new Padding(7, 4, 7, 4);
        var rename = Button(language == "de" ? "Umbenennen" : "Rename", () =>
        {
            if (list.SelectedItem is not string current) return;
            var name = Localization.Canonical(input.Text.Trim());
            if (!Valid(name, current)) { MessageBox.Show(dialog, message); return; }
            names[names.IndexOf(current)] = name; RefreshList(name);
        });
        rename.AutoSize = false; rename.Size = new Size(150, 46); rename.Margin = new Padding(7, 4, 7, 4);
        var remove = Button(language == "de" ? "Entfernen" : "Remove", () =>
        {
            if (list.SelectedItem is not string current) return;
            names.Remove(current); RefreshList(); input.Clear();
        });
        remove.AutoSize = false; remove.Size = new Size(180, 46); remove.Margin = new Padding(7, 4, 7, 4);
        var inputBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(14, 10, 14, 8), WrapContents = false };
        inputBar.Controls.AddRange([Label(language == "de" ? "Eintrag:" : "Option:"), input, add, rename]);
        var note = new Label { Dock = DockStyle.Bottom, Height = 57, Padding = new Padding(18, 8, 18, 6), BackColor = SystemColors.Window,
            Text = language == "de" ? "Änderungen gelten für künftige Einträge. Bereits gespeicherte Protokolle behalten ihre Bezeichnungen." :
                "Changes apply to future entries. Existing records keep their original labels." };
        var done = new Button { Text = language == "de" ? "Speichern" : "Save", DialogResult = DialogResult.OK,
            Width = 180, Height = 46, Margin = new Padding(7, 4, 7, 4) };
        var cancel = new Button { Text = language == "de" ? "Abbrechen" : "Cancel", DialogResult = DialogResult.Cancel,
            Width = 180, Height = 46, Margin = new Padding(7, 4, 7, 4) };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 74, Padding = new Padding(12, 8, 12, 8) };
        var right = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 388, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        right.Controls.Add(cancel); right.Controls.Add(done);
        footer.Controls.Add(right);
        var left = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 194, WrapContents = false };
        left.Controls.Add(remove); footer.Controls.Add(left);
        var middle = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 18, 0), BackColor = SystemColors.Window };
        middle.Controls.Add(list);
        dialog.Controls.Add(middle); dialog.Controls.Add(note); dialog.Controls.Add(footer); dialog.Controls.Add(inputBar); dialog.Controls.Add(header);
        dialog.AcceptButton = add; dialog.CancelButton = cancel;
        RefreshList();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var selectedMeasure = Localization.Canonical(measure.Text);
            var checkedNames = dimensions.CheckedItems.Cast<string>().Select(Localization.Canonical).ToList();
            store.SetChoiceOptions(key, names);
            ReloadChoiceOptions();
            if (editingId != 0 && editingKind == "Maßnahme")
            {
                if (!measure.Items.Cast<string>().Any(item => Localization.Canonical(item) == selectedMeasure)) measure.Items.Add(T(selectedMeasure));
                measure.SelectedItem = T(selectedMeasure);
            }
            if (editingId != 0 && editingKind is "Aktivität" or "Ruhe")
                foreach (var name in checkedNames)
                    if (!dimensions.Items.Cast<string>().Any(item => Localization.Canonical(item) == name)) dimensions.Items.Add(T(name));
            for (var i = 0; i < dimensions.Items.Count; i++)
                dimensions.SetItemChecked(i, checkedNames.Contains(Localization.Canonical(dimensions.Items[i]?.ToString() ?? "")));
        }
        catch (Exception ex) { Error(ex); }
    }
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static T Parse<T>(string json) where T : new() { try { return JsonSerializer.Deserialize<T>(json) ?? new(); } catch (JsonException) { return new(); } }
    private List<string> HearingProtection() => new[] { (ncHeadphones.Checked, "nc"), (loopEarplugs.Checked, "loop"), (calmerEarplugs.Checked, "calmer") }
        .Where(item => item.Item1).Select(item => item.Item2).ToList();
    private void SetHearingProtection(List<string>? selected)
    {
        ncHeadphones.Checked = selected?.Any(v => v is "nc" or "NC-Kopfhörer") == true;
        loopEarplugs.Checked = selected?.Any(v => v is "loop" or "Loop-Ohrstöpsel") == true;
        calmerEarplugs.Checked = selected?.Any(v => v is "calmer" or "Calmer-Ohrstöpsel") == true;
    }
    private void SaveState()
    {
        if (editingId != 0 && editingKind != "Zustand") { MessageBox.Show(this, T("Bitte zuerst die Bearbeitung mit Neu beenden.")); return; }
        Save(ConditionEntryFactory.Create(stateTime.Value, overall.SelectedIndex, pem.SelectedIndex,
            crash.Checked, pulse.Value == 0 ? null : (int)pulse.Value,
            symptoms.ToDictionary(pair => pair.Key, pair => pair.Value.SelectedIndex - 1),
            painChecks.Where(pair => pair.Value.Checked).Select(pair => pair.Key), stateNote.Text, editingId));
    }
    private void SaveInterval()
    {
        var kind = new[] { "Aktivität", "Ruhe", "Schlaf" }[Math.Clamp(intervalKind.SelectedIndex, 0, 2)];
        if (editingId != 0 && editingKind != kind) { MessageBox.Show(this, T("Bitte zuerst die Bearbeitung mit Neu beenden.")); return; }
        var running = openActivity.Checked;
        if (running && intervalStart.Value > DateTime.Now) { MessageBox.Show(this, T("Der Beginn eines laufenden Zeitraums darf nicht in der Zukunft liegen.")); return; }
        if (!running && intervalEnd.Value <= intervalStart.Value) { MessageBox.Show(this, T("Das Ende muss nach dem Beginn liegen.")); return; }
        if (kind == "Schlaf")
        {
            Save(new Entry { Id = editingId, Kind = "Schlaf", Start = intervalStart.Value, End = running ? null : intervalEnd.Value,
                Data = Json(new SleepData { Recovery = sleepRecovery.SelectedIndex, HearingProtection = HearingProtection() }), Note = intervalNote.Text });
            return;
        }
        var data = new IntervalData { Dimensions = dimensions.CheckedItems.Cast<string>().Select(Localization.Canonical).ToList(), Intensity = intensity.SelectedIndex + 1, HearingProtection = HearingProtection() };
        if (data.Dimensions.Count == 0) { MessageBox.Show(this, T("Bitte mindestens eine Belastungsart oder Ruheform wählen.")); return; }
        Save(new Entry { Id = editingId, Kind = kind, Start = intervalStart.Value, End = running ? null : intervalEnd.Value, Data = Json(data), Note = intervalNote.Text });
    }
    private void UpdateIntervalEndMode()
    {
        intervalEnd.Enabled = !openActivity.Checked;
    }
    private void SaveMeasure()
    {
        if (editingId != 0 && editingKind != "Maßnahme") { MessageBox.Show(this, T("Bitte zuerst die Bearbeitung mit Neu beenden.")); return; }
        if (measure.SelectedIndex < 0) { MessageBox.Show(this, language == "de" ? "Bitte zuerst eine Maßnahme in der Auswahlliste anlegen." : "Add a measure to the list first."); return; }
        Save(new Entry { Id = editingId, Kind = "Maßnahme", Start = measureTime.Value, Data = Json(new MeasureData { Name = Localization.Canonical(measure.Text), Dose = dose.Text }), Note = measureNote.Text });
    }
    private void LoadMedicationPlans()
    {
        try
        {
            planGrid.Rows.Clear(); planGridRight.Rows.Clear();
            var plans = store.MedicationPlans();
            for (var i = 0; i < plans.Count; ++i)
            {
                var plan = plans[i];
                var view = i % 2 == 0 ? planGrid : planGridRight;
                var index = view.Rows.Add(plan.Time, plan.Name, plan.Dose, plan.Quantity, T(plan.Form),
                    plan.Goals.Count == 0 ? "–" : plan.Goals.Count + (language == "de" ? " Ziele" : " goals"),
                    plan.StartDate == "0001-01-01" ? "bisher" : DateOnly.ParseExact(plan.StartDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).ToString("dd.MM.yyyy"),
                    plan.EndDate is null ? T("Läuft noch") : DateOnly.ParseExact(plan.EndDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).ToString("dd.MM.yyyy"));
                view.Rows[index].Tag = plan;
                view.Rows[index].Cells[5].ToolTipText = string.Join(", ", plan.Goals);
            }
            RestoreTableSort(planGrid); RestoreTableSort(planGridRight);
            planGrid.ClearSelection(); planGridRight.ClearSelection();
            LoadMedicationDay();
            RefreshInventory();
        }
        catch (Exception ex) { Error(ex); }
    }
    private void RefreshInventory()
    {
        var stocks = store.ProductStocks();
        productGrid.Rows.Clear();
        foreach (var item in stocks)
        {
            var p = item.Product;
            bool low = item.StockKnown && item.WeeklyNeed > 0 && item.Current <= item.WeeklyNeed;
            var index = productGrid.Rows.Add(p.Name, T(p.Form), p.Manufacturer, p.Supplier,
                p.PackUnits == 0 ? "–" : p.PackUnits.ToString("0.##"), p.PackPrice.ToString("0.00"),
                item.StockKnown ? item.Current.ToString("0.##") : T("nicht erfasst"), item.WeeklyNeed.ToString("0.##"), low ? T("Bald leer") : "");
            var row = productGrid.Rows[index]; row.Tag = item;
            if (low) row.DefaultCellStyle.BackColor = Color.MistyRose;
        }
        RestoreTableSort(productGrid);
        productGrid.ClearSelection();
        var again = productGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => (r.Tag as ProductStock)?.Product.Id == selectedProductId);
        if (again is not null) { productGrid.CurrentCell = again.Cells[0]; again.Selected = true; }
        int lowCount = stocks.Count(s => s.StockKnown && s.WeeklyNeed > 0 && s.Current <= s.WeeklyNeed);
        var weeklyCost = stocks.Where(s => s.Product.PackUnits > 0).Sum(s => s.WeeklyNeed / s.Product.PackUnits * s.Product.PackPrice);
        stockSummary.Text = language == "de"
            ? $"Dokumentierte Nachkäufe: {store.TotalPurchases():0.00} € · Geschätzter Wochenbedarf: {weeklyCost:0.00} € (mit hinterlegten Packungspreisen).\n" +
              (lowCount == 0 ? "Keine erfassten Vorräte unter dem Wochenbedarf." : $"Vorratswarnung: {lowCount} Präparat(e) reichen höchstens noch für sieben Tage.")
            : $"Recorded purchases: €{store.TotalPurchases():0.00} · Estimated weekly cost: €{weeklyCost:0.00} (where package prices are available).\n" +
              (lowCount == 0 ? "No recorded stock below one week's supply." : $"Low stock: {lowCount} product(s) have at most seven days of supply remaining.");
        stockPage.Text = lowCount == 0 ? T("Packungen und Vorrat") : language == "de" ? $"Vorrat ({lowCount} knapp)" : $"Inventory ({lowCount} low)";
    }
    private void SelectProduct()
    {
        if (productGrid.CurrentRow?.Tag is not ProductStock stock) return;
        var p = stock.Product;
        selectedProductId = p.Id; manufacturer.Text = p.Manufacturer; supplier.Text = p.Supplier;
        packUnits.Value = Math.Clamp(p.PackUnits, packUnits.Minimum, packUnits.Maximum);
        packPrice.Value = Math.Clamp(p.PackPrice, packPrice.Minimum, packPrice.Maximum);
        stockTarget.Value = Math.Clamp(stock.Current, stockTarget.Minimum, stockTarget.Maximum);
    }
    private bool SaveProductDetails()
    {
        if (selectedProductId == 0) { MessageBox.Show(this, T("Bitte zuerst ein Präparat in der Vorratsliste auswählen.")); return false; }
        try
        {
            var p = store.ProductStocks().First(s => s.Product.Id == selectedProductId).Product;
            p.Manufacturer = manufacturer.Text.Trim(); p.Supplier = supplier.Text.Trim();
            p.PackUnits = packUnits.Value; p.PackPrice = packPrice.Value;
            store.SaveProductDetails(p); RefreshInventory(); return true;
        }
        catch (Exception ex) { Error(ex); return false; }
    }
    private void RecordPurchase()
    {
        if (!SaveProductDetails()) return;
        try
        {
            var p = store.ProductStocks().First(s => s.Product.Id == selectedProductId).Product;
            store.RecordPurchase(p, purchasePacks.Value); RefreshInventory();
        }
        catch (Exception ex) { Error(ex); }
    }
    private void SetStock()
    {
        if (selectedProductId == 0) { MessageBox.Show(this, T("Bitte zuerst ein Präparat auswählen.")); return; }
        try { store.SetStock(selectedProductId, stockTarget.Value); RefreshInventory(); }
        catch (Exception ex) { Error(ex); }
    }
    private void ShowStockMovements()
    {
        if (selectedProductId == 0) { MessageBox.Show(this, T("Bitte zuerst ein Präparat auswählen.")); return; }
        using var dialog = new Form { Text = "Vorratsbuchungen", StartPosition = FormStartPosition.CenterParent,
            Size = new Size(800, 540), MinimumSize = new Size(640, 420), Font = Font, Icon = Icon };
        var history = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
        foreach (var title in new[] { "Tag", "Art", "Einheiten ±", "Packungen", "Kosten €" }) history.Columns.Add(title, title);
        foreach (DataGridViewColumn column in history.Columns) column.HeaderText = T(column.HeaderText);
        void Reload()
        {
            history.Rows.Clear();
            foreach (var movement in store.StockMovements(selectedProductId))
                history.Rows[history.Rows.Add(movement.Day, T(movement.Kind), movement.Units.ToString("0.##"),
                    movement.Packages.ToString("0.##"), movement.Cost.ToString("0.00"))].Tag = movement;
        }
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62 };
        bar.Controls.Add(Button("Buchung löschen", () =>
        {
            if (history.CurrentRow?.Tag is not StockMovement movement) return;
            if (movement.Kind == "Einnahme") { MessageBox.Show(dialog, T("Bitte die Einnahme im Tagesprotokoll ändern oder löschen.")); return; }
            if (MessageBox.Show(dialog, T("Diese Buchung löschen? Bestand und Ausgaben werden neu berechnet."), T("Buchung löschen"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            store.DeleteStockMovement(movement.Id); Reload(); RefreshInventory();
        }));
        dialog.Controls.Add(history); dialog.Controls.Add(bar);
        LocalizeDialog(dialog);
        Reload(); dialog.ShowDialog(this);
    }
    private void EditMedicationPlan(DataGridView view)
    {
        if (view.CurrentRow?.Tag is not MedicationPlan plan) return;
        if (plan.Id != editingPlanId && HasUnsavedPlanChanges())
        {
            var answer = MessageBox.Show(this, T("Die Änderungen am bisherigen Präparat sind noch nicht gespeichert. Jetzt speichern?"),
                T("Einnahmeplan"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) { RestorePlanSelection(); return; }
            if (answer == DialogResult.Yes && !TrySaveMedicationPlan()) { RestorePlanSelection(); return; }
            if (answer == DialogResult.No) ClearPlanEditor();
        }
        var selected = store.MedicationPlans().FirstOrDefault(p => p.Id == plan.Id);
        if (selected is null) return;
        var targetView = new[] { planGrid, planGridRight }.FirstOrDefault(g => g.Rows.Cast<DataGridViewRow>().Any(r => (r.Tag as MedicationPlan)?.Id == selected.Id));
        if (targetView is null) return;
        var targetRow = targetView.Rows.Cast<DataGridViewRow>().First(r => (r.Tag as MedicationPlan)?.Id == selected.Id);
        targetView.CurrentCell = targetRow.Cells[0]; targetRow.Selected = true;
        view = targetView;
        (view == planGrid ? planGridRight : planGrid).ClearSelection();
        editingPlanId = selected.Id;
        loadedPlanBaseline = selected;
        if (TimeSpan.TryParse(selected.Time, out var time)) planTime.Value = DateTime.Today.Add(time);
        planName.Text = selected.Name;
        planDose.Text = selected.Dose;
        planStart.Value = selected.StartDate == "0001-01-01" ? DateTime.Today : DateTime.ParseExact(selected.StartDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        planStart.Tag = selected.StartDate == "0001-01-01" ? "legacy" : null;
        planOngoing.Checked = selected.EndDate is null;
        planEnd.Value = selected.EndDate is null ? DateTime.Today : DateTime.ParseExact(selected.EndDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        planQuantity.Text = selected.Quantity;
        planForm.Text = T(selected.Form);
        selectedGoals = new List<string>(selected.Goals);
        UpdateGoalSummary();
    }
    private bool HasUnsavedPlanChanges()
    {
        if (loadedPlanBaseline is not { } p)
            return planName.Text.Trim().Length > 0 || planDose.Text.Trim().Length > 0 || planForm.Text.Trim().Length > 0
                || selectedGoals.Count > 0 || planQuantity.Text.Trim() != "1" || planTime.Value.ToString("HH:mm") != blankPlanTime;
        return planTime.Value.ToString("HH:mm") != p.Time || planName.Text.Trim() != p.Name || planDose.Text.Trim() != p.Dose
            || (planStart.Tag is "legacy" && planStart.Value.Date == DateTime.Today ? "0001-01-01" : planStart.Value.ToString("yyyy-MM-dd")) != p.StartDate
            || (planOngoing.Checked ? null : planEnd.Value.ToString("yyyy-MM-dd")) != p.EndDate
            || planQuantity.Text.Trim() != p.Quantity || Localization.Canonical(planForm.Text.Trim()) != Localization.Canonical(p.Form)
            || !selectedGoals.OrderBy(s => s).SequenceEqual(p.Goals.OrderBy(s => s));
    }
    private void RestorePlanSelection()
    {
        planGrid.ClearSelection(); planGridRight.ClearSelection();
        if (editingPlanId == 0) return;
        foreach (var view in new[] { planGrid, planGridRight })
        foreach (DataGridViewRow row in view.Rows)
        {
            if ((row.Tag as MedicationPlan)?.Id != editingPlanId) continue;
            view.CurrentCell = row.Cells[0]; row.Selected = true;
            return;
        }
    }
    private void ClearPlanEditor()
    {
        editingPlanId = 0; loadedPlanBaseline = null;
        planName.Clear(); planDose.Clear(); planQuantity.Text = "1"; planForm.Text = "";
        planStart.Value = DateTime.Today; planStart.Tag = null; planOngoing.Checked = true; planEnd.Value = DateTime.Today;
        selectedGoals.Clear(); UpdateGoalSummary();
        blankPlanTime = planTime.Value.ToString("HH:mm");
    }
    private void UpdateGoalSummary() => goalSummary.Text = selectedGoals.Count == 0 ? (language == "de" ? "kein Ziel" : "no goals") : selectedGoals.Count + (language == "de" ? selectedGoals.Count == 1 ? " Ziel" : " Ziele" : selectedGoals.Count == 1 ? " goal" : " goals");
    private void EditMedicationGoals()
    {
        using var dialog = new Form
        {
            Text = "Warum? · Behandlungsziele", StartPosition = FormStartPosition.CenterParent,
            Size = new Size(620, 720), MinimumSize = new Size(500, 500), Font = Font,
            ShowIcon = false, BackColor = SystemColors.Control
        };
        var header = new Panel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(18, 12, 18, 10) };
        var iconImage = new PictureBox { Dock = DockStyle.Right, Width = 44, Image = Icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom };
        header.Controls.Add(iconImage);
        header.Controls.Add(new Label { Text = "Behandlungsziele auswählen", Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, Font.SizeInPoints + 1.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });
        var choices = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(14, 10, 14, 10), BackColor = SystemColors.Window };
        var boxes = new List<CheckBox>();
        void PopulateChoices()
        {
            choices.Controls.Clear(); boxes.Clear();
            foreach (var name in store.GoalOptions().OrderBy(T, StringComparer.Create(CultureInfo.GetCultureInfo(language == "de" ? "de-DE" : "en-US"), true)))
            {
                var box = new CheckBox { Text = T(name), Tag = name, Checked = selectedGoals.Contains(name),
                    Height = 39, Width = 510, Margin = new Padding(4, 3, 4, 3), TextAlign = ContentAlignment.MiddleLeft };
                boxes.Add(box); choices.Controls.Add(box);
            }
        }
        PopulateChoices();
        choices.Resize += (_, _) =>
        {
            foreach (var box in boxes) box.Width = Math.Max(340, choices.ClientSize.Width - choices.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 12);
        };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 74, Padding = new Padding(12, 8, 12, 8) };
        var manage = new Button { Text = "Ziele verwalten ...", Width = 180, Height = 46, Margin = new Padding(7, 4, 7, 4) };
        manage.Click += (_, _) =>
        {
            selectedGoals = boxes.Where(box => box.Checked).Select(box => (string)box.Tag!).ToList();
            if (!ManageGoalOptions(dialog)) return;
            selectedGoals = selectedGoals.Where(store.GoalOptions().Contains).ToList();
            PopulateChoices();
            LoadMedicationPlans();
            if (editingPlanId != 0) loadedPlanBaseline = store.MedicationPlans().FirstOrDefault(p => p.Id == editingPlanId);
        };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 388, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Width = 180, Height = 46, Margin = new Padding(7, 4, 7, 4) };
        var accept = new Button { Text = "Übernehmen", DialogResult = DialogResult.OK, Width = 180, Height = 46, Margin = new Padding(7, 4, 7, 4) };
        buttons.Controls.Add(cancel); buttons.Controls.Add(accept);
        var manageContainer = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 194, WrapContents = false };
        manageContainer.Controls.Add(manage);
        footer.Controls.Add(buttons); footer.Controls.Add(manageContainer);
        dialog.Controls.Add(choices); dialog.Controls.Add(footer); dialog.Controls.Add(header);
        LocalizeDialog(dialog);
        dialog.AcceptButton = accept; dialog.CancelButton = cancel;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        selectedGoals = boxes.Where(box => box.Checked).Select(box => (string)box.Tag!).ToList();
        UpdateGoalSummary();
    }
    private bool ManageGoalOptions(Form owner)
    {
        bool modified = false;
        using var dialog = new Form { Text = "Behandlungsziele verwalten", StartPosition = FormStartPosition.CenterParent,
            Size = new Size(620, 720), MinimumSize = new Size(620, 500), Font = Font,
            ShowIcon = false, BackColor = SystemColors.Control };
        var header = new Panel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(18, 12, 18, 10) };
        header.Controls.Add(new PictureBox { Dock = DockStyle.Right, Width = 44, Image = Icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom });
        header.Controls.Add(new Label { Text = "Behandlungsziele verwalten", Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, Font.SizeInPoints + 1.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });
        var input = new TextBox { Width = 250 };
        var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 42, BorderStyle = BorderStyle.None, BackColor = SystemColors.Window };
        list.DrawItem += (_, e) =>
        {
            e.DrawBackground();
            if (e.Index >= 0)
                TextRenderer.DrawText(e.Graphics, T(list.GetItemText(list.Items[e.Index])), e.Font ?? list.Font,
                    new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, e.Bounds.Width - 16, e.Bounds.Height), e.ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        };
        void RefreshList(string? select = null)
        {
            list.Items.Clear(); list.Items.AddRange(store.GoalOptions().OrderBy(T, StringComparer.Create(CultureInfo.GetCultureInfo(language == "de" ? "de-DE" : "en-US"), true)).Cast<object>().ToArray());
            if (select is not null) list.SelectedItem = select;
        }
        var add = Button("Hinzufügen", () =>
        {
            try
            {
                var name = input.Text.Trim();
                if (!store.AddGoalOption(name)) { MessageBox.Show(dialog, T("Bitte einen neuen, eindeutigen Namen eingeben.")); return; }
                selectedGoals.Add(name); modified = true; input.Clear(); RefreshList(name);
            }
            catch (Exception ex) { Error(ex); }
        });
        add.AutoSize = false; add.Size = new Size(180, 46); add.Margin = new Padding(7, 4, 7, 4);
        var inputBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(14, 10, 14, 8), WrapContents = false };
        inputBar.Controls.AddRange([Label("Neues Ziel:"), input, add]);
        var listPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 18, 0), BackColor = SystemColors.Window };
        listPanel.Controls.Add(list);
        var remove = Button("Ziel löschen", () =>
        {
            if (list.SelectedItem is not string name) return;
            if (MessageBox.Show(dialog, language == "de" ? $"„{name}“ aus der Auswahlliste und aus aktuellen Einnahmeplänen entfernen? Bereits dokumentierte Einnahmen bleiben unverändert." :
                $"Remove ‘{T(name)}’ from the options and current medication schedules? Previously recorded intakes remain unchanged.",
                T("Behandlungsziel löschen"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { store.DeleteGoalOption(name); selectedGoals.RemoveAll(g => g == name); modified = true; RefreshList(); }
            catch (Exception ex) { Error(ex); }
        });
        remove.AutoSize = false; remove.Size = new Size(180, 46); remove.Margin = new Padding(7, 4, 7, 4);
        var close = new Button { Text = "Fertig", DialogResult = DialogResult.OK, Width = 180, Height = 46, Margin = new Padding(7, 4, 7, 4) };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 74, Padding = new Padding(12, 8, 12, 8) };
        var right = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 194, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        right.Controls.Add(close);
        footer.Controls.Add(right);
        var left = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 194, WrapContents = false };
        left.Controls.Add(remove);
        footer.Controls.Add(left);
        dialog.Controls.Add(listPanel); dialog.Controls.Add(footer); dialog.Controls.Add(inputBar); dialog.Controls.Add(header);
        LocalizeDialog(dialog);
        dialog.AcceptButton = add;
        RefreshList();
        dialog.ShowDialog(owner);
        return modified;
    }
    private void SaveMedicationPlan() => TrySaveMedicationPlan();
    private bool TrySaveMedicationPlan()
    {
        if (string.IsNullOrWhiteSpace(planName.Text) || string.IsNullOrWhiteSpace(planDose.Text) || string.IsNullOrWhiteSpace(planForm.Text)
            || !decimal.TryParse(planQuantity.Text, out var quantity) || quantity <= 0)
        {
            MessageBox.Show(this, T("Bitte Präparat, geplante Dosis, eine positive Anzahl und die Form eingeben.")); return false;
        }
        try
        {
            var start = planStart.Tag is "legacy" && planStart.Value.Date == DateTime.Today && editingPlanId != 0
                ? "0001-01-01" : planStart.Value.ToString("yyyy-MM-dd");
            var end = planOngoing.Checked ? null : planEnd.Value.ToString("yyyy-MM-dd");
            if (end is not null && string.CompareOrdinal(end, start) < 0)
            { MessageBox.Show(this, T("Das Ende darf nicht vor dem Beginn liegen.")); return false; }
            store.SaveMedicationPlan(new MedicationPlan { Id = editingPlanId, Time = planTime.Value.ToString("HH:mm"), Name = planName.Text.Trim(), Dose = planDose.Text.Trim(), Quantity = planQuantity.Text.Trim(), Form = Localization.Canonical(planForm.Text.Trim()), Goals = new List<string>(selectedGoals), StartDate = start, EndDate = end });
            ClearPlanEditor(); LoadMedicationPlans(); return true;
        }
        catch (Exception ex) { Error(ex); return false; }
    }
    private void DeleteMedicationPlan()
    {
        if (editingPlanId == 0) { MessageBox.Show(this, T("Bitte zuerst eine Zeile im Einnahmeplan auswählen.")); return; }
        if (MessageBox.Show(this, T("Dieses Präparat aus dem täglichen Plan entfernen? Bereits gespeicherte Einnahmen bleiben erhalten."), T("Einnahmeplan"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try { store.DeleteMedicationPlan(editingPlanId); ClearPlanEditor(); LoadMedicationPlans(); }
        catch (Exception ex) { Error(ex); }
    }
    private void LoadMedicationDay()
    {
        try
        {
            intakeGrid.Rows.Clear();
            var day = intakeDay.Value.Date;
            var historic = store.All().Where(e => e.Kind == "Einnahme" && e.Start.Date == day).ToList();
            var plans = store.MedicationPlans();
            foreach (var plan in plans.Where(p => p.IsActiveOn(DateOnly.FromDateTime(day))))
            {
                var old = historic.LastOrDefault(e => Parse<IntakeData>(e.Data).PlanId == plan.Id);
                var data = old is null ? null : Parse<IntakeData>(old.Data);
                var displayedPlan = old is null ? plan : new MedicationPlan
                {
                    Id = plan.Id, ProductId = data!.ProductId == 0 ? plan.ProductId : data.ProductId, Time = old.Start.ToString("HH:mm"), Name = data.Name, Dose = data.PlannedDose,
                    Quantity = string.IsNullOrWhiteSpace(data.Quantity) ? plan.Quantity : data.Quantity,
                    Form = string.IsNullOrWhiteSpace(data.Form) ? plan.Form : data.Form, Goals = data.Goals
                };
                AddIntakeRow(displayedPlan, data);
            }
            foreach (var old in historic)
            {
                var data = Parse<IntakeData>(old.Data);
                if (plans.Any(p => p.Id == data.PlanId && p.IsActiveOn(DateOnly.FromDateTime(day)))) continue;
                AddIntakeRow(new MedicationPlan { Id = data.PlanId, ProductId = data.ProductId, Time = old.Start.ToString("HH:mm"), Name = data.Name, Dose = data.PlannedDose, Quantity = data.Quantity, Form = data.Form, Goals = data.Goals }, data);
            }
            RestoreTableSort(intakeGrid);
            intakeGrid.ClearSelection();
        }
        catch (Exception ex) { Error(ex); }
    }
    private void AddIntakeRow(MedicationPlan plan, IntakeData? saved)
    {
        var status = Localization.StatusLabel(saved?.Status ?? "pending", language);
        var missingQuantity = saved is not null && string.IsNullOrWhiteSpace(saved.Quantity) && !string.IsNullOrWhiteSpace(plan.Quantity);
        var missingForm = saved is not null && string.IsNullOrWhiteSpace(saved.Form) && !string.IsNullOrWhiteSpace(plan.Form);
        var index = intakeGrid.Rows.Add(plan.Time, saved?.Name ?? plan.Name, saved?.PlannedDose ?? plan.Dose,
            missingQuantity ? plan.Quantity : saved?.Quantity ?? plan.Quantity,
            T(missingForm ? plan.Form : saved?.Form ?? plan.Form), status,
            saved?.ActualDose ?? plan.Dose, saved?.ActualQuantity is { Length: > 0 } ? saved.ActualQuantity : plan.Quantity);
        intakeGrid.Rows[index].Tag = plan;
        intakeGrid.Rows[index].Cells["name"].ToolTipText = string.Join(", ", saved?.Goals ?? plan.Goals);
        if (missingQuantity) intakeGrid.Rows[index].Cells["quantity"].ToolTipText = T("Im gespeicherten Eintrag fehlte die Anzahl; angezeigt wird der aktuelle Planwert.");
        if (missingForm) intakeGrid.Rows[index].Cells["form"].ToolTipText = T("Im gespeicherten Eintrag fehlte die Form; angezeigt wird der aktuelle Planwert.");
    }
    private void SetAllIntakes(string status)
    {
        intakeGrid.EndEdit();
        foreach (DataGridViewRow row in intakeGrid.Rows)
            if (row.Tag is MedicationPlan) row.Cells["status"].Value = status;
    }
    private void SaveMedicationDay()
    {
        try
        {
            intakeGrid.EndEdit();
            var day = intakeDay.Value.Date;
            var previous = store.All().Where(e => e.Kind == "Einnahme" && e.Start.Date == day).ToList();
            var replacements = new List<Entry>();
            foreach (DataGridViewRow row in intakeGrid.Rows)
            {
                if (row.Tag is not MedicationPlan plan) continue;
                var status = Localization.Canonical(Convert.ToString(row.Cells["status"].Value) ?? "");
                if (status is not ("taken" or "skipped")) continue;
                var actualDose = status == "taken" ? Convert.ToString(row.Cells["actual"].Value)?.Trim() ?? "" : "";
                if (status == "taken" && actualDose.Length == 0)
                {
                    MessageBox.Show(this, T("Bitte für jede bestätigte Einnahme die tatsächliche Dosis angeben.")); return;
                }
                var actualQuantity = status == "taken" ? Convert.ToString(row.Cells["actualQuantity"].Value)?.Trim() ?? "" : "";
                if (status == "taken" && (!decimal.TryParse(actualQuantity, out var count) || count <= 0))
                {
                    MessageBox.Show(this, T("Bitte für jede bestätigte Einnahme eine positive tatsächliche Anzahl eingeben.")); return;
                }
                var data = new IntakeData { PlanId = plan.Id, ProductId = plan.ProductId, Name = Convert.ToString(row.Cells["name"].Value) ?? plan.Name,
                    PlannedDose = Convert.ToString(row.Cells["planned"].Value) ?? plan.Dose,
                    Quantity = Convert.ToString(row.Cells["quantity"].Value) ?? plan.Quantity,
                    Form = Localization.Canonical(Convert.ToString(row.Cells["form"].Value) ?? plan.Form), Goals = new List<string>(plan.Goals), ActualDose = actualDose, ActualQuantity = actualQuantity, Status = status };
                replacements.Add(new Entry { Kind = "Einnahme", Start = day.Add(TimeSpan.Parse(plan.Time)), Data = Json(data) });
            }
            store.SaveIntakes(day, previous, replacements);
            LoadEntries(); LoadMedicationDay(); RefreshInventory();
            MessageBox.Show(this, T("Einnahmen für diesen Tag gespeichert."));
        }
        catch (Exception ex) { Error(ex); }
    }
    private void Save(Entry entry)
    {
        try { store.Save(entry); ResetEdit(); LoadEntries(); }
        catch (Exception ex) { Error(ex); }
    }
    private void ResetEdit()
    {
        editingId = 0; editingKind = "";
        stateTime.Value = DateTime.Now; measureTime.Value = DateTime.Now; intervalStart.Value = DateTime.Now; intervalEnd.Value = DateTime.Now.AddMinutes(30);
        openActivity.Checked = false;
        overall.SelectedIndex = 2; pem.SelectedIndex = 0; crash.Checked = false; pulse.Value = 0;
        ApplyPemFromTodaysEntries();
        foreach (var box in symptoms.Values) box.SelectedIndex = 0;
        foreach (var check in painChecks.Values) check.Checked = false;
        stateNote.Clear(); intervalNote.Clear(); measureNote.Clear(); dose.Clear();
        var selectedMeasure = Localization.Canonical(measure.Text);
        measure.Items.Clear(); measure.Items.AddRange(measureOptions.Select(T).Cast<object>().ToArray());
        measure.SelectedIndex = Math.Max(0, measureOptions.FindIndex(name => name == selectedMeasure));
        sleepRecovery.SelectedIndex = 0;
        SetHearingProtection(null);
        ResetDimensions();
    }
    private void ApplyPemFromTodaysEntries()
    {
        var latestMarked = store.All().Where(e => e.Kind == "Zustand" && e.Start.Date == stateTime.Value.Date)
            .Select(e => Parse<StateData>(e.Data)).FirstOrDefault(s => s.Pem > 0 || s.Crash);
        pem.SelectedIndex = Math.Clamp(latestMarked?.Pem ?? 0, 0, 2);
        crash.Checked = latestMarked?.Crash ?? false;
    }
    private void LoadEntries()
    {
        try
        {
            aiResult.Clear();
            entries = store.All(); grid.Rows.Clear();
            foreach (var e in entries)
            {
                var time = e.Start.ToString(language == "de" ? "dd.MM.yyyy HH:mm" : "MM/dd/yyyy HH:mm") +
                    (e.Kind is "Aktivität" or "Ruhe" or "Schlaf" && e.End is null ? " – " + T("läuft") : e.End is null ? "" : " – " + e.End.Value.ToString(language == "de" ? "dd.MM. HH:mm" : "MM/dd HH:mm"));
                int index = grid.Rows.Add(time, T(e.Kind), Describe(e), e.Note);
                grid.Rows[index].Tag = e;
                if (e.Kind is "Aktivität" or "Ruhe" or "Schlaf" && e.End is null)
                {
                    grid.Rows[index].DefaultCellStyle.BackColor = Color.FromArgb(255, 238, 237);
                    grid.Rows[index].DefaultCellStyle.SelectionBackColor = Color.FromArgb(241, 196, 191);
                }
            }
            RestoreTableSort(grid);
            grid.ClearSelection(); UpdateEndActivityButton(); UpdateAnalysis(); RestoreAiResponse();
        }
        catch (Exception ex) { Error(ex); }
    }
    private string Describe(Entry entry) => entry.Kind switch
    {
        "Zustand" => DescribeState(Parse<StateData>(entry.Data)),
        "Maßnahme" => DescribeMeasure(Parse<MeasureData>(entry.Data)),
        "Schlaf" => DescribeSleep(entry),
        "Einnahme" => DescribeIntake(Parse<IntakeData>(entry.Data)),
        _ => DescribeInterval(entry)
    };
    private string DescribeSleep(Entry entry)
    {
        var data = Parse<SleepData>(entry.Data);
        return T("Schlaf") + " · " + (entry.End is null ? T("läuft") :
            (entry.End.Value - entry.Start).ToString(@"hh\:mm") + (language == "de" ? " Std. · Erholung: " : " h · Recovery: ") + SleepRecoveryName(data.Recovery)) + ProtectionDescription(data.HearingProtection);
    }
    private string DescribeInterval(Entry entry)
    {
        var data = Parse<IntervalData>(entry.Data);
        return string.Join(", ", data.Dimensions.Select(T)) + " · " + T(entry.Kind).ToLowerInvariant() +
            " · " + (language == "de" ? "Intensität: " : "Intensity: ") +
            T(new[] { "gering", "mittel", "hoch", "sehr hoch" }[Math.Clamp(data.Intensity - 1, 0, 3)]) +
            (entry.End is null ? " · " + T("läuft") : "") + ProtectionDescription(data.HearingProtection);
    }
    private string ProtectionDescription(List<string>? protection) => protection is { Count: > 0 } ? " · " + string.Join(", ", protection.Select(v => T(v switch { "nc" => "NC-Kopfhörer", "loop" => "Loop-Ohrstöpsel", "calmer" => "Calmer-Ohrstöpsel", _ => v }))) : "";
    private string DescribeIntake(IntakeData data) => data.Name + " · " + Localization.StatusLabel(data.Status, language) +
        (Localization.IsTaken(data.Status) ? " · " + data.ActualDose : "") +
        (data.Quantity.Length > 0 || data.Form.Length > 0 ? " · " + string.Join(" ", new[] { data.Quantity, T(data.Form) }.Where(s => s.Length > 0)) : "") +
        (language == "de" ? " (Plan: " : " (Schedule: ") + data.PlannedDose + ")";
    private string SleepRecoveryName(int recovery) => T(new[] { "nicht bewertet", "keine", "gering", "mittel", "deutlich" }[Math.Clamp(recovery, 0, 4)]);
    private string DescribeMeasure(MeasureData m) => T(m.Name) + (m.Dose.Length > 0 ? " · " + m.Dose : "");
    private string DescribeState(StateData d)
    {
        string state = (language == "de" ? "Allgemein: " : "Overall: ") +
            T(OverallNames[Math.Clamp(d.Overall,0,4)]) +
            (d.Pem > 0 ? " · PEM: " + T(PemNames[Math.Clamp(d.Pem,0,2)]) : "") +
            (d.Crash ? " · Crash" : "") +
            (d.Pulse is > 0 ? " · " + (language == "de" ? "Puls: " : "Pulse: ") + d.Pulse : "");
        var active = SymptomNames.Where(name => d.SymptomSeverity(name) > 0).Select(name => T(name) + " " + d.SymptomSeverity(name));
        var none = SymptomNames.Count(name => d.SymptomSeverity(name) == 0);
        var unknown = SymptomNames.Count(name => d.SymptomSeverity(name) < 0);
        var details = active.ToList();
        if (none > 0) details.Add(none == SymptomNames.Length ? T("Alle Symptome: keine") :
            language == "de" ? $"{none} Symptome: keine" : $"{none} symptoms: none");
        if (unknown > 0) details.Add(language == "de" ? $"{unknown} nicht beurteilt" : $"{unknown} not assessed");
        return state + (details.Count > 0 ? " · " + string.Join(", ", details) : "");
    }
    private Entry? Selected() => grid.CurrentRow?.Selected == true ? grid.CurrentRow.Tag as Entry : null;
    private void UpdateEndActivityButton()
    {
        if (endActivityButton is null) return;
        var entry = Selected();
        endActivityButton.Enabled = entry is { Kind: "Aktivität" or "Ruhe" or "Schlaf", End: null };
    }
    private void EditSelected()
    {
        var e = Selected(); if (e is null) return;
        ResetEdit(); editingId = e.Id; editingKind = e.Kind;
        var tabs = topTabs;
        if (e.Kind == "Einnahme")
        {
            editingId = 0; editingKind = "";
            tabs.SelectedIndex = 3; medicationTabs.SelectedIndex = 1; intakeDay.Value = e.Start.Date; LoadMedicationDay();
            return;
        }
        if (e.Kind == "Zustand")
        {
            tabs.SelectedIndex = 0; stateTime.Value = e.Start; stateNote.Text = e.Note;
            var d = Parse<StateData>(e.Data); overall.SelectedIndex = Math.Clamp(d.Overall,0,4); pem.SelectedIndex = Math.Clamp(d.Pem,0,2); crash.Checked = d.Crash; pulse.Value = Math.Clamp(d.Pulse ?? 0,0,250);
            foreach (var p in symptoms) p.Value.SelectedIndex = Math.Clamp(d.SymptomSeverity(p.Key) + 1, 0, 5);
            foreach (var pair in painChecks) pair.Value.Checked = d.PainLocations.Contains(pair.Key);
        }
        else if (e.Kind == "Maßnahme")
        {
            tabs.SelectedIndex = 2; measureTime.Value = e.Start; measureNote.Text = e.Note;
            var d = Parse<MeasureData>(e.Data);
            if (!measure.Items.Cast<string>().Any(item => Localization.Canonical(item) == Localization.Canonical(d.Name))) measure.Items.Add(T(d.Name));
            measure.SelectedIndex = measure.Items.Cast<string>().ToList().FindIndex(item => Localization.Canonical(item) == Localization.Canonical(d.Name)); dose.Text = d.Dose;
        }
        else
        {
            tabs.SelectedIndex = 1; intervalKind.SelectedIndex = e.Kind == "Schlaf" ? 2 : e.Kind == "Ruhe" ? 1 : 0;
            intervalStart.Value = e.Start; intervalEnd.Value = e.End ?? DateTime.Now.AddMinutes(1); intervalNote.Text = e.Note;
            openActivity.Checked = e.End is null;
            if (e.Kind == "Schlaf")
            {
                var data = Parse<SleepData>(e.Data);
                sleepRecovery.SelectedIndex = Math.Clamp(data.Recovery, 0, 4);
                SetHearingProtection(data.HearingProtection);
            }
            else
            {
                var d = Parse<IntervalData>(e.Data); intensity.SelectedIndex = Math.Clamp(d.Intensity-1,0,3);
                SetHearingProtection(d.HearingProtection);
                foreach (var name in d.Dimensions)
                    if (!dimensions.Items.Cast<string>().Any(item => Localization.Canonical(item) == Localization.Canonical(name))) dimensions.Items.Add(T(name));
                for (int i = 0; i < dimensions.Items.Count; ++i) dimensions.SetItemChecked(i, d.Dimensions.Contains(Localization.Canonical(dimensions.Items[i]?.ToString() ?? "")));
            }
        }
    }
    private void EndSelectedActivity()
    {
        var entry = Selected();
        if (entry is not { Kind: "Aktivität" or "Ruhe" or "Schlaf", End: null }) return;
        var now = DateTime.Now;
        if (now <= entry.Start)
        {
            MessageBox.Show(this, T("Das Ende muss nach dem Beginn liegen."));
            return;
        }
        Save(new Entry { Id = entry.Id, Kind = entry.Kind, Start = entry.Start, End = now, Data = entry.Data, Note = entry.Note });
    }
    private void DeleteSelected()
    {
        var e = Selected(); if (e is null) return;
        if (MessageBox.Show(this, T("Diesen Eintrag dauerhaft löschen?"), T("Löschen"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try { store.Delete(e.Id); ResetEdit(); LoadEntries(); RefreshInventory(); } catch (Exception ex) { Error(ex); }
    }
    private void Export()
    {
        using var dialog = new SaveFileDialog { Filter = "CSV-Datei|*.csv", FileName = "PaceAtlas-Export.csv" };
        if (dialog.ShowDialog(this) == DialogResult.OK) try { store.ExportCsv(dialog.FileName); } catch (Exception ex) { Error(ex); }
    }
    private void Backup()
    {
        using var dialog = new SaveFileDialog { Filter = "SQLite-Backup|*.db", FileName = "PaceAtlas-Backup-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".db" };
        if (dialog.ShowDialog(this) == DialogResult.OK) try { store.Backup(dialog.FileName); } catch (Exception ex) { Error(ex); }
    }
    private void Restore()
    {
        using var dialog = new OpenFileDialog { Filter = "SQLite-Backup|*.db|Alle Dateien|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (MessageBox.Show(this, T("Das Backup ersetzt sämtliche aktuellen Einträge. Fortfahren?"), T("Wiederherstellen"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { store.Restore(dialog.FileName); ResetEdit(); LoadEntries(); LoadMedicationPlans(); } catch (Exception ex) { Error(ex); }
    }
    private void UpdateAnalysis()
    {
        DateTime threshold = period.SelectedIndex switch { 0 => DateTime.Now.AddDays(-7), 1 => DateTime.Now.AddDays(-30), 2 => DateTime.Now.AddMonths(-3), 3 => DateTime.Now.AddYears(-1), _ => DateTime.MinValue };
        var states = entries.Where(e => e.Kind == "Zustand" && e.Start >= threshold).OrderBy(e => e.Start).Select(e => (e.Start, Data: Parse<StateData>(e.Data))).ToList();
        var intervals = entries.Where(e => (e.Kind == "Ruhe" || e.Kind == "Aktivität") && e.Start >= threshold).ToList();
        int pemCount = states.Count(s => s.Data.Pem == 2), crashCount = states.Count(s => s.Data.Crash);
        var sleeps = entries.Where(e => e.Kind == "Schlaf" && e.End is not null && e.End > threshold)
            .Select(e => (Start: e.Start < threshold ? threshold : e.Start, End: e.End!.Value)).OrderBy(x => x.Start).ToList();
        TimeSpan sleepTime = ConditionAnalysis.TotalSleepTime(entries, threshold);
        summary.Text = language == "de"
            ? $"{states.Count} Zustände · {intervals.Count} Aktivität/Ruhe · {sleeps.Count} Schlafphasen ({sleepTime.TotalHours:0.#} h) · PEM erkannt: {pemCount} · Crash markiert: {crashCount}\n" +
              (states.Count == 0 ? "Noch keine Zustände im gewählten Zeitraum." : $"Mittlerer Allgemeinzustand: {states.Average(s => s.Data.Overall):0.0} von 4 (höher = schlechter). Linien zeigen dokumentierte Zustände, keine lückenlose Messung.")
            : $"{states.Count} conditions · {intervals.Count} activity/rest intervals · {sleeps.Count} sleep periods ({sleepTime.TotalHours:0.#} h) · PEM confirmed: {pemCount} · crashes marked: {crashCount}\n" +
              (states.Count == 0 ? "No conditions recorded in the selected period." : $"Average overall condition: {states.Average(s => s.Data.Overall):0.0} of 4 (higher = worse). Lines connect recorded conditions, not continuous measurements.");
        chart.States = states; chart.Invalidate();
        assessment.Text = ConditionAnalysis.BuildAssessment(states, entries, DateTime.Now, language, T);
        assessment.Select(0, 0);
        assessment.ScrollToCaret();
    }
    private void Error(Exception ex) => MessageBox.Show(this, ex.Message, "PaceAtlas", MessageBoxButtons.OK, MessageBoxIcon.Error);
}

public sealed class ChartPanel : Control
{
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string UiLanguage { get; set; } = "de";
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<(DateTime Start, StateData Data)> States { get; set; } = new();
    public ChartPanel() { DoubleBuffered = true; BackColor = Color.White; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(60, 25, Math.Max(10, Width - 90), Math.Max(10, Height - 72));
        using var axis = new Pen(Color.FromArgb(210,220,230));
        for (int i = 0; i <= 4; i++)
        {
            float y = bounds.Bottom - i * bounds.Height / 4f;
            g.DrawLine(axis, bounds.Left, y, bounds.Right, y);
            g.DrawString(i.ToString(), Font, Brushes.DimGray, 27, y - 8);
        }
        g.DrawString(Localization.Translate("gut", UiLanguage), Font, Brushes.DimGray, bounds.Right - 20, bounds.Bottom + 6);
        if (States.Count == 0) { g.DrawString(Localization.Translate("Hier erscheint dein Verlauf.", UiLanguage), Font, Brushes.DimGray, bounds.Left + 20, bounds.Top + 30); return; }
        long min = States[0].Start.Ticks, max = States[^1].Start.Ticks;
        float X(DateTime date) => max == min ? bounds.Left + bounds.Width/2f : bounds.Left + (float)((date.Ticks - min) / (double)(max - min) * bounds.Width);
        float Y(int value) => bounds.Bottom - Math.Clamp(value,0,4) * bounds.Height/4f;
        using var line = new Pen(Color.FromArgb(43,143,179), 2.4f);
        for (int i = 1; i < States.Count; ++i) g.DrawLine(line, X(States[i-1].Start), Y(States[i-1].Data.Overall), X(States[i].Start), Y(States[i].Data.Overall));
        foreach (var s in States)
        {
            var color = s.Data.Crash ? Color.FromArgb(222,75,80) : s.Data.Pem > 0 ? Color.FromArgb(232,158,57) : Color.FromArgb(43,143,179);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, X(s.Start)-4, Y(s.Data.Overall)-4, 8, 8);
        }
        g.DrawString(States[0].Start.ToString(UiLanguage == "de" ? "dd.MM.yy" : "MM/dd/yy"), Font, Brushes.DimGray, bounds.Left, bounds.Bottom + 7);
        g.DrawString(States[^1].Start.ToString(UiLanguage == "de" ? "dd.MM.yy" : "MM/dd/yy"), Font, Brushes.DimGray, bounds.Right - 65, bounds.Bottom + 7);
        g.DrawString(Localization.Translate("● Zustand     ● PEM vermutet/erkannt     ● Crash", UiLanguage), Font, Brushes.DimGray, bounds.Left + 110, bounds.Bottom + 7);
    }
}
