using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow : Window
{
    private bool aboutOpen;
    private bool databaseMaintenanceRunning;
    private readonly DispatcherTimer todaySummaryTimer = new() { Interval = TimeSpan.FromMinutes(1) };

    private static bool TryCalendarTime(CalendarDatePicker picker, TextBox time, out DateTime value)
    {
        value = default;
        if (picker.Date is not { } selected ||
            !TimeOnly.TryParseExact(time.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var clock))
            return false;
        value = selected.Date.Add(clock.ToTimeSpan());
        return true;
    }

    private void ShowAbout_Click(object sender, RoutedEventArgs e) => _ = ShowAboutAsync();

    private void ShowAboutFromTray()
    {
        RestoreFromPacingTray();
        DispatcherQueue.TryEnqueue(() => _ = ShowAboutAsync());
    }

    private async Task ShowAboutAsync()
    {
        if (aboutOpen) return;
        aboutOpen = true;
        try
        {
            var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "?";
            var english = selectedLanguage == "en";
            var aboutContent = new StackPanel { Spacing = 12 };
            aboutContent.Children.Add(new TextBlock { Text = $"Pace Atlas · Version {version} · ME/CFS",
                FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var slogan = new StackPanel { Spacing = 2 };
            slogan.Children.Add(new TextBlock { Text = MottoQuestion.Text, TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Segoe Script"), FontStyle = Windows.UI.Text.FontStyle.Italic });
            slogan.Children.Add(new TextBlock { Text = MottoAnswer.Text, TextWrapping = TextWrapping.Wrap });
            aboutContent.Children.Add(slogan);
            aboutContent.Children.Add(new TextBlock { Text = english ? "Pace Atlas license" : "Lizenz von Pace Atlas",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            aboutContent.Children.Add(new TextBlock { Text = english
                ? "Pace Atlas is licensed under the PolyForm Noncommercial License 1.0.0. Commercial use requires separate permission from the rights holder. The license text is included as LICENSE in the installation and source archive. Third-party data and components retain their own license terms."
                : "Pace Atlas steht unter der PolyForm Noncommercial License 1.0.0. Für eine kommerzielle Nutzung ist eine gesonderte Erlaubnis des Rechteinhabers erforderlich. Der Lizenztext liegt als LICENSE der Installation und dem Quellarchiv bei. Für Daten und Komponenten Dritter gelten deren eigene Lizenzbedingungen.",
                TextWrapping = TextWrapping.Wrap });
            aboutContent.Children.Add(new HyperlinkButton { Content = english ? "PolyForm license terms" : "PolyForm-Lizenzbedingungen",
                NavigateUri = new Uri("https://polyformproject.org/licenses/noncommercial/1.0.0/"), Padding = new Thickness(0) });
            aboutContent.Children.Add(new TextBlock { Text = english ? "Nutrition data: Bundeslebensmittelschlüssel (BLS) 4.0" :
                "Nährstoffdaten: Bundeslebensmittelschlüssel (BLS) 4.0",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            aboutContent.Children.Add(new TextBlock { Text =
                "Max Rubner-Institut (2025): Bundeslebensmittelschlüssel (BLS), Version 4.0 — Deutsche Nährstoffdatenbank. Karlsruhe. DOI: 10.25826/Data20251217-134202-0",
                TextWrapping = TextWrapping.Wrap });
            aboutContent.Children.Add(new TextBlock { Text = english
                ? "License: Creative Commons Attribution 4.0 International (CC BY 4.0). The original Excel data was converted into an offline catalog for this application. Credit the Max Rubner-Institut, link the license and indicate changes when reusing the data. This license applies to the BLS data, not your personal entries."
                : "Lizenz: Creative Commons Namensnennung 4.0 International (CC BY 4.0). Die ursprünglichen Excel-Daten wurden für diese Anwendung in einen Offlinekatalog umgewandelt. Bei Weiterverwendung der Daten das Max Rubner-Institut nennen, die Lizenz verlinken und Änderungen angeben. Die Lizenz betrifft die BLS-Daten, nicht deine persönlichen Einträge.",
                TextWrapping = TextWrapping.Wrap });
            var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            links.Children.Add(new HyperlinkButton { Content = english ? "BLS source" : "BLS-Quelle",
                NavigateUri = new Uri("https://blsdb.de/download"), Padding = new Thickness(0) });
            links.Children.Add(new HyperlinkButton { Content = english ? "License terms" : "Lizenzbedingungen",
                NavigateUri = new Uri("https://creativecommons.org/licenses/by/4.0/legalcode.de"), Padding = new Thickness(0) });
            aboutContent.Children.Add(links);
            aboutContent.Children.Add(new TextBlock { Text = english ? "Open Food Facts (imported products)" :
                "Open Food Facts (importierte Produkte)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            aboutContent.Children.Add(new TextBlock { Text = english
                ? "Product data: © Open Food Facts contributors, Open Database License (ODbL) 1.0; individual database contents: Database Contents License (DbCL) 1.0. Imported products are marked separately; personal entries and BLS data are kept separate. See LICENSE-OPEN-FOOD-FACTS.md for attribution and reuse terms."
                : "Produktdaten: © Mitwirkende von Open Food Facts, Open Database License (ODbL) 1.0; einzelne Datenbankinhalte: Database Contents License (DbCL) 1.0. Importierte Produkte sind getrennt gekennzeichnet; eigene Einträge und BLS-Daten bleiben separat. Hinweise zur Namensnennung und Weiterverwendung stehen in LICENSE-OPEN-FOOD-FACTS.md.",
                TextWrapping = TextWrapping.Wrap });
            aboutContent.Children.Add(new TextBlock { Text = english
                ? "Product and brand names are used solely to identify the listed products. All trademarks belong to their respective owners. Pace Atlas is not affiliated with, endorsed by, or sponsored by those owners."
                : "Produkt- und Markennamen dienen ausschließlich der Identifikation der aufgeführten Produkte. Die Marken gehören ihren jeweiligen Inhabern. Pace Atlas steht mit diesen Unternehmen nicht in Verbindung und wird von ihnen weder unterstützt noch gesponsert.",
                TextWrapping = TextWrapping.Wrap });
            var offLinks = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            offLinks.Children.Add(new HyperlinkButton { Content = english ? "OFF data export" : "OFF-Datenexport",
                NavigateUri = new Uri("https://world.openfoodfacts.org/data"), Padding = new Thickness(0) });
            offLinks.Children.Add(new HyperlinkButton { Content = english ? "ODbL license" : "ODbL-Lizenz",
                NavigateUri = new Uri("https://opendatacommons.org/licenses/odbl/1-0/"), Padding = new Thickness(0) });
            aboutContent.Children.Add(offLinks);
            var dialog = new ContentDialog
            {
                XamlRoot = ((FrameworkElement)Content).XamlRoot,
                Title = english ? "About Pace Atlas" : "Info zu Pace Atlas",
                Content = new ScrollViewer { Content = aboutContent, MaxHeight = 520,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                MinWidth = 550,
                PrimaryButtonText = english ? "Check for updates" : "Auf Updates prüfen",
                SecondaryButtonText = english ? "Optimize database" : "Datenbank optimieren",
                IsSecondaryButtonEnabled = !OffImportRunning && !databaseMaintenanceRunning,
                CloseButtonText = english ? "Close" : "Schließen"
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                await CheckForUpdatesAsync(true);
            else if (result == ContentDialogResult.Secondary)
                await ShowDatabaseMaintenanceAsync();
        }
        finally { aboutOpen = false; }
    }

    private string DatabaseMaintenanceError(Exception ex) => ex is IOException { Message: "VACUUM_DISK_SPACE" }
        ? N("Für die Datenbankwartung wird vorübergehend freier Speicherplatz von etwa der doppelten Datenbankgröße benötigt.",
            "Database maintenance temporarily needs free space of approximately twice the database size.")
        : ex.Message;

    private async Task ShowDatabaseMaintenanceAsync()
    {
        if (databaseMaintenanceRunning || OffImportRunning) return;
        databaseMaintenanceRunning = true;
        var status = new TextBlock { Text = N("Die Datenbank wird optimiert. Bitte warten …",
            "Optimizing the database. Please wait …"), TextWrapping = TextWrapping.Wrap };
        var body = new StackPanel { Spacing = 12, Width = 390 };
        body.Children.Add(new ProgressRing { IsActive = true, Width = 32, Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left });
        body.Children.Add(status);
        var progress = new ContentDialog { XamlRoot = ((FrameworkElement)Content).XamlRoot,
            Title = N("Datenbankwartung", "Database maintenance"), Content = body };
        var shown = progress.ShowAsync();
        try
        {
            var result = await Task.Run(() => store.MaintainDatabase(forceVacuum: true));
            status.Text = N($"Fertig. Datenbankgröße: {result.BeforeBytes / 1024d / 1024:0.#} → {result.AfterBytes / 1024d / 1024:0.#} MB.",
                $"Done. Database size: {result.BeforeBytes / 1024d / 1024:0.#} → {result.AfterBytes / 1024d / 1024:0.#} MB.");
        }
        catch (Exception ex) { status.Text = N("Datenbankwartung fehlgeschlagen: ",
            "Database maintenance failed: ") + DatabaseMaintenanceError(ex); }
        finally
        {
            databaseMaintenanceRunning = false;
            ((ProgressRing)body.Children[0]).IsActive = false;
            progress.CloseButtonText = N("Schließen", "Close");
        }
        await shown;
    }

    private static readonly string[] DefaultSymptomNames =
    [
        "Erschöpfung", "Brain Fog", "Schmerzen", "Geräuschempfindlichkeit", "Ohrgeräusche", "Lichtempfindlichkeit",
        "Atemprobleme", "Schwindel/Kreislauf", "Herzrasen", "Zittern", "Kältegefühl/Schüttelfrost",
        "Angst/Unruhe", "Hyperarousal", "Nicht erholsamer Schlaf", "Sehkraft"
    ];
    private static readonly string[] DefaultPainNames =
        ["Finger", "Hände", "Unterarme", "Oberarme", "Kopf", "Nacken", "Rücken", "Beine", "Füße", "Sonstige"];
    private static readonly string[] DefaultHearingProtection = ["NC-Kopfhörer", "Loop", "Calmer"];
    private string[] SymptomNames => choices.Symptoms.OrderBy(name => name, StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true)).ToArray();
    private string[] PainNames => choices.PainLocations.ToArray();
    private static readonly string[] Severities =
        ["nicht beurteilt", "keine", "leicht", "mittel", "stark", "extrem"];

    private static SolidColorBrush VisualScaleBrush(int level, bool intensity = false)
    {
        var colors = intensity
            ? new[] { "#187C90", "#187C90", "#A66A16", "#B75125" }
            : new[] { "#418066", "#638663", "#A66A16", "#B75125", "#BD3844" };
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255,
            Convert.ToByte(colors[Math.Clamp(level, 0, colors.Length - 1)][1..3], 16),
            Convert.ToByte(colors[Math.Clamp(level, 0, colors.Length - 1)][3..5], 16),
            Convert.ToByte(colors[Math.Clamp(level, 0, colors.Length - 1)][5..7], 16)));
    }

    private static string CircleScale(int level) => new string('●', Math.Clamp(level, 0, 4)) + new string('○', 4 - Math.Clamp(level, 0, 4));
    private static string BarScale(int level) => new string('▰', Math.Clamp(level, 0, 4)) + new string('▱', 4 - Math.Clamp(level, 0, 4));
    private static readonly string[] ActivityNames =
        ["Körperlich", "Kognitiv", "Sozial", "Akustisch", "Visuell", "Emotional/Stress", "Fahrt/Transport"];
    private static readonly string[] RestNames = ["Hingelegt", "Reizarm", "Augen geschlossen", "Geschlafen"];
    private static readonly string[] DefaultMeasures = ["Ibuprofen", "Naproxen", "Cannabis", "Benzodiazepin", "Sonstige"];
    private static readonly string[] DefaultGoals = ["Antidepressiv", "Angststörung", "Antioxidant", "Herzfrequenz", "Bluthochdruck", "ME/CFS (Mitochondrien)", "Schmerzen", "Leaky Gut", "Antihistamin", "ME/CFS (allg. Schmerzen)", "ME/CFS (Muskelschwäche)", "ME/CFS (Erschöpfung)", "ME/CFS (Verdauung)", "entzündungshemmend", "Borreliose", "Darmaufbau", "Wassereinlagerung", "ME/CFS (allg.)", "Gewicht", "Brain Fog", "Schwindel", "Testosteron", "Schlafstörung"];

    private static string WinUiSettingsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PaceAtlas.WinUI");

    private static void MigrateWinUiSettings()
    {
        var previous = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PaceAtlas.WinUIPrototype");
        if (!Directory.Exists(previous)) return;
        try
        {
            Directory.CreateDirectory(WinUiSettingsFolder);
            foreach (var name in new[] { "language.txt", "window-placement.json", "tab-order.json",
                         "column-widths.json", "entry-filters.json", "tray-settings.json",
                         "update-settings.json" })
            {
                var source = Path.Combine(previous, name);
                var target = Path.Combine(WinUiSettingsFolder, name);
                if (File.Exists(source) && !File.Exists(target)) File.Copy(source, target);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The shared PaceAtlas database is independent of these display settings.
        }
    }

    private sealed class WinUiChoices
    {
        public List<string> Measures { get; set; } = [..DefaultMeasures];
        public List<string> Goals { get; set; } = [..DefaultGoals];
        public List<string> Activities { get; set; } = [..ActivityNames];
        public List<string> Rests { get; set; } = [..RestNames];
        public List<string> Symptoms { get; set; } = [..DefaultSymptomNames];
        public List<string> PainLocations { get; set; } = [..DefaultPainNames];
        public List<string> HearingProtection { get; set; } = [..DefaultHearingProtection];
    }
    private sealed class MedicationPlan
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public string Time { get; set; } = "";
        public string StartDate { get; set; } = "0001-01-01";
        public string? EndDate { get; set; }
        public bool IsActiveOn(DateOnly day) =>
            string.CompareOrdinal(StartDate, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) <= 0 &&
            (EndDate is null || string.CompareOrdinal(EndDate, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) >= 0);
        public string Name { get; set; } = "";
        public string Quantity { get; set; } = "";
        public string Form { get; set; } = "";
        public string Dose { get; set; } = "";
        public List<string> Goals { get; set; } = new();
    }
    private sealed class IntakeRecord
    {
        public long PlanId { get; set; }
        public long ProductId { get; set; }
        public List<string> Goals { get; set; } = new();
        public string Day { get; set; } = "";
        public string Time { get; set; } = "";
        public string Name { get; set; } = "";
        public string Quantity { get; set; } = "";
        public string Form { get; set; } = "";
        public string Dose { get; set; } = "";
        public string Status { get; set; } = "Offen";
        public string ActualDose { get; set; } = "";
        public string ActualQuantity { get; set; } = "";
    }
    private sealed class StockProduct
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Form { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Supplier { get; set; } = "";
        public double PackUnits { get; set; }
        public double PackPrice { get; set; }
        public double Current { get; set; }
        public double WeeklyNeed { get; set; }
        public bool StockKnown { get; set; }
    }
    private sealed class IntakeRow
    {
        public required MedicationPlan Plan { get; init; }
        public required ComboBox Status { get; init; }
        public required TextBox Dose { get; init; }
        public required TextBox Quantity { get; init; }
        public required Grid Visual { get; init; }
    }
    private readonly WinUiChoices choices = new();
    private readonly Store store = new();
    private readonly List<MedicationPlan> plans = new();
    private readonly List<IntakeRecord> intakes = new();
    private readonly List<StockProduct> products = new();
    private readonly List<IntakeRow> intakeRows = new();
    private readonly Dictionary<ListView, List<(Grid Row, Brush? Base)>> listFeedbackRows = new();
    private Grid? hoveredListRow;
    private readonly Dictionary<StackPanel, List<(Grid Row, Brush? Base)>> panelFeedbackRows = new();
    private Grid? selectedPanelRow;
    private Grid? hoveredPanelRow;
    private IntakeRow? selectedIntakeRow;
    private IntakeRow? hoveredIntakeRow;
    private readonly List<StockProduct> displayedProducts = new();
    private long editingStockProductId;
    private bool renderingStock;
    private readonly List<MedicationPlan> displayedPlans = new();
    private readonly List<CheckBox> goalChecks = new();
    private readonly List<CheckBox> measureReasonChecks = new();
    private long editingPlanId;
    private static string ColumnWidthsPath => Path.Combine(WinUiSettingsFolder, "column-widths.json");
    private static string EntryFiltersPath => Path.Combine(WinUiSettingsFolder, "entry-filters.json");
    private sealed class EntryFilterSettings
    {
        public bool HideIntakes { get; set; }
    }
    private bool restoringEntryFilters;

    private readonly Dictionary<string, ComboBox> symptoms = new();
    private readonly Dictionary<string, CheckBox> painChecks = new();
    private readonly List<CheckBox> protectionChecks = new();
    private readonly ObservableCollection<Grid> visibleEntries = new();
    private readonly List<Entry> displayedEntries = new();
    private readonly List<Entry> entries = new();
    private long editingEntryId;
    private string editingEntryKind = "";
    private readonly List<CheckBox> intervalDimensionChecks = new();
    private int fieldColumns;
    private int painColumns;
    private readonly Dictionary<string, (int Column, bool Descending)> tableSort = new()
    {
        ["entries"] = (0, true), ["plans"] = (0, false), ["stock"] = (0, false), ["intakes"] = (0, false),
        ["foods"] = (0, false), ["rules"] = (0, false),
        ["ingredients"] = (0, false), ["meals"] = (0, true)
    };
    private readonly Dictionary<string, int[]> columnWidths = new()
    {
        ["entries"] = [180, 180, 115, 400, 340],
        ["plans"] = [85, 205, 125, 85, 76, 125, 125, 290],
        ["stock"] = [170, 76, 145, 145, 95, 95, 95, 90, 170],
        ["intakes"] = [85, 190, 145, 95, 76, 110, 165, 155],
        ["foods"] = [280, 130, 105, 130, 230, 250],
        ["rules"] = [230, 145, 95, 380, 230],
        ["ingredients"] = [260, 105, 125],
        ["meals"] = [170, 290, 125, 100, 110, 300]
    };
    private readonly Dictionary<string, Dictionary<int, string>> tableFilters = new();

    private bool MatchesTableFilters(string table, params string[] cells) =>
        !tableFilters.TryGetValue(table, out var filters) || filters.All(filter =>
            filter.Key < cells.Length && (table == "foods" && filter.Key == 0
                ? FoodNameMatches(cells[0], filter.Value)
                : cells[filter.Key].Contains(filter.Value, StringComparison.CurrentCultureIgnoreCase)));

    private static string SearchWords(string value)
    {
        var decomposed = value.Normalize(System.Text.NormalizationForm.FormD);
        var normalized = new System.Text.StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            else normalized.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        return normalized.ToString().Replace("ß", "ss");
    }

    private static bool NearWord(string candidate, string query)
    {
        if (candidate.Contains(query, StringComparison.Ordinal)) return true;
        if (query.Length < 5 || Math.Abs(candidate.Length - query.Length) > 1) return false;
        var edits = 0;
        for (int i = 0, j = 0; i < candidate.Length && j < query.Length;)
        {
            if (candidate[i] == query[j]) { i++; j++; continue; }
            if (++edits > 1) return false;
            if (candidate.Length >= query.Length) i++;
            if (candidate.Length <= query.Length) j++;
        }
        return true;
    }

    private static bool FoodNameMatches(string name, string search)
    {
        var words = SearchWords(name).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return SearchWords(search).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(term => words.Any(word => NearWord(word, term)));
    }

    private string[]? TableFilterChoices(string table, int column) => (table, column) switch
    {
        ("entries", 2) => ["Zustand", "Aktivität", "Ruhe", "Maßnahme", "Einnahme", "Mahlzeit"],
        ("intakes", 5) => ["Offen", "Genommen", "Ausgelassen"],
        ("rules", 1) => ["Verboten", "Vermeiden", "Bedingt erlaubt", "Erlaubt"],
        ("meals", 2) => ["geplant", "gegessen", "Vorlage"],
        _ => null
    };

    private void RefreshFilteredTable(string table)
    {
        switch (table)
        {
            case "entries": DisplayEntries(); break;
            case "plans": RenderPlans(); break;
            case "stock": RenderStock(); break;
            case "intakes": SortIntakeRows(); break;
            case "foods": RenderFoodCatalog(); break;
            case "rules": RenderFoodRules(); break;
            case "ingredients": RenderMealIngredients(); break;
            case "meals": RenderMealHistory(); break;
        }
        RefreshSortableHeaderCaptions();
    }

    private bool entriesExpanded;
    private bool planExpanded;

    private void TogglePlan_Click(object sender, RoutedEventArgs e) => SetPlanExpanded(!planExpanded);

    private void SetPlanExpanded(bool expanded)
    {
        if (planExpanded == expanded) return;
        planExpanded = expanded;
        PlanLayout.RowDefinitions[0].Height = expanded ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        PlanLayout.RowDefinitions[1].Height = expanded ? new GridLength(1, GridUnitType.Star) : new GridLength(170);
        ExpandPlanIcon.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        CollapsePlanIcon.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(TogglePlanButton, T(expanded ?
            "Hinterlegte Einnahmen einklappen" : "Hinterlegte Einnahmen ausklappen"));
    }

    private void ToggleEntries_Click(object sender, RoutedEventArgs e) => SetEntriesExpanded(!entriesExpanded);

    private void SetEntriesExpanded(bool expanded)
    {
        if (entriesExpanded == expanded) return;
        entriesExpanded = expanded;
        Grid.SetRow(EntriesPanel, entriesExpanded ? 0 : 1);
        Grid.SetRowSpan(EntriesPanel, entriesExpanded ? 2 : 1);
        Canvas.SetZIndex(EntriesPanel, entriesExpanded ? 1 : 0);
        ExpandEntriesIcon.Visibility = entriesExpanded ? Visibility.Collapsed : Visibility.Visible;
        CollapseEntriesIcon.Visibility = entriesExpanded ? Visibility.Visible : Visibility.Collapsed;
        UpdateEntriesToggleHint();
    }

    private void UpdateEntriesToggleHint() => ToolTipService.SetToolTip(ToggleEntriesButton,
        T(entriesExpanded ? "Bisherigen Verlauf einklappen" : "Bisherigen Verlauf ausklappen"));

    public MainWindow()
    {
        InitializeComponent();
        MigrateWinUiSettings();
        SetInitialWindowSize();
        RestoreWindowPlacement();
        RestoreTabOrder();
        var windowIcon = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(windowIcon)) AppWindow.SetIcon(windowIcon);
        InitializeLanguageChoice();
        InitializePacing();
        TrackWindowPlacement();
        InitializeReminder();
        ConfigureTabColors(MainTabs);
        ConfigureTabColors(MedicationTabs);
        ConfigureTabColors(AnalysisTabs);
        ConfigureTabColors(AnalysisVisualTabs);
        ConfigureTabColors(NutritionTabs);
        Overall.ItemsSource = new[] { "gut", "leicht eingeschränkt", "mittel", "schlecht", "sehr schlecht" };
        Pem.ItemsSource = new[] { "nein", "vermutet", "erkannt" };
        EntryList.ItemsSource = visibleEntries;
        RestoreEntryFilters();
        LoadColumnWidths();
        InitializeTableHeaders();
        LoadConditionChoices();
        BuildFields();
        RenderMeasureReasons();
        InitializeIntervals();
        InitializeMeasuresAndPlans();
        InitializeAnalysis();
        ResetForm();
        LoadEntries();
        InitializeNutrition();
        AttachStatusLocalization();
        ApplyUiLanguage();
        UpdateEditingIndicators();
        todaySummaryTimer.Tick += (_, _) => { RefreshTodaySummary(); RefreshMedicationDueIndicators(); RefreshMealDueIndicators(); };
        todaySummaryTimer.Start();
        ((FrameworkElement)Content).Loaded += async (_, _) =>
        {
            await CheckForUpdatesAsync(false);
            await CheckBlsDataAsync(false);
        };
    }

    private static void ConfigureTabColors(TabView view)
    {
        foreach (var tab in view.TabItems.OfType<TabViewItem>())
            tab.Resources["TabViewItemHeaderBackground"] =
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);

        void UpdateColors()
        {
            foreach (var tab in view.TabItems.OfType<TabViewItem>())
            {
                var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)tab.Resources["TabViewItemHeaderBackground"];
                brush.Color = ReferenceEquals(view.SelectedItem, tab)
                    ? Microsoft.UI.Colors.White
                    : Windows.UI.Color.FromArgb(255, 226, 237, 243);
            }
        }

        view.SelectionChanged += (_, _) => UpdateColors();
        view.Loaded += (_, _) => UpdateColors();
        UpdateColors();
    }

    private void InitializeMeasuresAndPlans()
    {
        try
        {
            choices.Measures = store.ChoiceOptions("measures", DefaultMeasures);
            choices.Goals = store.GoalOptions();
            choices.Activities = store.ChoiceOptions("activity_dimensions", ActivityNames);
            choices.Rests = store.ChoiceOptions("rest_dimensions", RestNames);
            LoadActivityTemplates();
            ReloadMedicationData();
        }
        catch (Exception ex)
        {
            PlanStatus.Text = "PaceAtlas-Daten konnten nicht geladen werden: " + ex.Message;
        }
        MeasureName.ItemsSource = choices.Measures.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (choices.Measures.Count > 0) MeasureName.SelectedIndex = 0;
        IntervalKind_SelectionChanged(IntervalKind, null!);
        PopulatePlanForms();
        PlanTime.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        PlanStartDate.Date = DateTimeOffset.Now;
        MeasureDate.Date = DateTimeOffset.Now;
        MeasureTime.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        RenderGoals();
        RenderPlans();
        IntakeDate.Date = DateTimeOffset.Now;
        RenderIntakes();
        RenderStock();
    }

    private void LoadConditionChoices()
    {
        try
        {
            choices.Symptoms = store.ChoiceOptions("symptoms", DefaultSymptomNames);
            choices.PainLocations = store.ChoiceOptions("pain_locations", DefaultPainNames);
            choices.HearingProtection = store.ChoiceOptions("hearing_protection", DefaultHearingProtection);
        }
        catch (Exception ex) { Status.Text = "Auswahllisten konnten nicht geladen werden: " + ex.Message; }
        RenderHearingProtection();
    }

    private void ReloadMedicationData()
    {
        plans.Clear();
        plans.AddRange(store.MedicationPlans().Select(plan => new MedicationPlan
        {
            Id = plan.Id, ProductId = plan.ProductId, Time = plan.Time, Name = plan.Name,
            Dose = plan.Dose, Quantity = plan.Quantity, Form = plan.Form, Goals = plan.Goals,
            StartDate = plan.StartDate, EndDate = plan.EndDate
        }));
        intakes.Clear();
        foreach (var entry in store.All().Where(entry => entry.Kind == "Einnahme"))
        {
            PaceAtlas.IntakeData? data;
            try { data = JsonSerializer.Deserialize<PaceAtlas.IntakeData>(entry.Data); }
            catch (JsonException) { continue; }
            if (data is null) continue;
            intakes.Add(new IntakeRecord { Day = entry.Start.ToString("yyyy-MM-dd"), Time = entry.Start.ToString("HH:mm"),
                PlanId = data.PlanId, ProductId = data.ProductId, Name = data.Name, Dose = data.PlannedDose,
                Quantity = data.Quantity, Form = data.Form, Goals = data.Goals,
                Status = data.Status is "taken" or "Taken" or "Genommen" ? "Genommen" : "Ausgelassen",
                ActualDose = data.ActualDose, ActualQuantity = data.ActualQuantity });
        }
        products.Clear();
        products.AddRange(store.ProductStocks().Select(stock => new StockProduct
        {
            Id = stock.Product.Id, Name = stock.Product.Name, Form = stock.Product.Form,
            Manufacturer = stock.Product.Manufacturer, Supplier = stock.Product.Supplier,
            PackUnits = (double)stock.Product.PackUnits, PackPrice = (double)stock.Product.PackPrice,
            Current = (double)stock.Current, WeeklyNeed = (double)stock.WeeklyNeed,
            StockKnown = stock.StockKnown
        }));
    }

    private Grid TableRow((string Text, int Width)[] cells, bool header = false, bool compact = false, string? table = null,
        IReadOnlyList<(string Symbol, SolidColorBrush Brush)>? badges = null, string? contentDescription = null,
        string? contentForm = null, int recoveryLevel = -1)
    {
        var grid = new Grid { MinHeight = header ? 30 : 27 };
        foreach (var (value, width) in cells)
        {
            int index = grid.ColumnDefinitions.Count;
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(table is not null
                ? columnWidths[table][index] : width) });
            if (!header && (table == "plans" && index == 4 || table == "stock" && index == 1))
            {
                var formIcon = MedicationFormCell(value);
                Grid.SetColumn(formIcon, index);
                grid.Children.Add(formIcon);
                continue;
            }
            var label = new TextBlock { Text = table == "entries" && (index is 1 or 2 or 3) ? T(value) :
                table == "stock" && index == 8 ? T(value) : value, Margin = new Thickness(8, 1, 8, 1),
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = header ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal };
            if (table == "entries" && index == 3 && badges is { Count: > 0 })
            {
                label.Text = "";
                foreach (var (symbol, brush) in badges)
                    label.Inlines.Add(new Run { Text = symbol + "  ", Foreground = brush });
                label.Inlines.Add(new Run { Text = value });
                ToolTipService.SetToolTip(label, contentDescription ?? value);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(label, contentDescription ?? value);
            }
            if (table == "entries" && index == 3 && recoveryLevel >= 0)
            {
                var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6,
                    VerticalAlignment = VerticalAlignment.Center };
                content.Children.Add(RecoveryBatteryIcon(recoveryLevel));
                content.Children.Add(label);
                Grid.SetColumn(content, index);
                grid.Children.Add(content);
            }
            else if (table == "entries" && index == 3 && !string.IsNullOrWhiteSpace(contentForm))
            {
                var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4,
                    VerticalAlignment = VerticalAlignment.Center };
                content.Children.Add(MedicationFormCell(contentForm));
                content.Children.Add(label);
                Grid.SetColumn(content, index);
                grid.Children.Add(content);
            }
            else
            {
                Grid.SetColumn(label, index);
                grid.Children.Add(label);
            }
        }
        return grid;
    }

    private void AddEntryDetails(Grid row, string detail)
    {
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var label = row.Children.OfType<FrameworkElement>().First(child => Grid.GetColumn(child) == 3);
        row.Children.Remove(label);
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var details = new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 2, 12, 8), Visibility = Visibility.Collapsed };
        Grid.SetColumn(details, 3); Grid.SetColumnSpan(details, 2); Grid.SetRow(details, 1);
        row.Children.Add(details);
        var toggle = new Button { Content = "▸", Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0), Padding = new Thickness(5, 1, 3, 1),
            VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(toggle, N("Details anzeigen", "Show details"));
        toggle.Click += (_, _) =>
        {
            details.Visibility = details.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            toggle.Content = details.Visibility == Visibility.Visible ? "▾" : "▸";
            ToolTipService.SetToolTip(toggle, details.Visibility == Visibility.Visible
                ? N("Details ausblenden", "Hide details") : N("Details anzeigen", "Show details"));
        };
        content.Children.Add(toggle);
        Grid.SetColumn(label, 1); content.Children.Add(label);
        Grid.SetColumn(content, 3); row.Children.Add(content);
    }

    private void LoadColumnWidths()
    {
        try
        {
            if (!File.Exists(ColumnWidthsPath)) return;
            var saved = JsonSerializer.Deserialize<Dictionary<string, int[]>>(File.ReadAllText(ColumnWidthsPath));
            if (saved is null) return;
            foreach (var (table, widths) in saved)
                if (widths.Length is > 0 and <= 64 && widths.All(width => width is >= 55 and <= 1200) &&
                    (!columnWidths.TryGetValue(table, out var defaults) || widths.Length == defaults.Length))
                    columnWidths[table] = widths;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    private int[] AnalysisWidths(string table, params int[] defaults)
    {
        if (!columnWidths.TryGetValue(table, out var widths) || widths.Length != defaults.Length)
            columnWidths[table] = widths = defaults;
        return widths;
    }

    private void ResizeAnalysisColumn(StackPanel panel, string table, int column, double change)
    {
        var widths = columnWidths[table];
        widths[column] = Math.Clamp(widths[column] + (int)Math.Round(change), 55, 1200);
        foreach (var row in panel.Children.OfType<Grid>())
            if (column < row.ColumnDefinitions.Count)
                row.ColumnDefinitions[column].Width = new GridLength(widths[column]);
    }

    private void SaveAnalysisWidths()
    {
        try { SaveJson(ColumnWidthsPath, columnWidths); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Status.Text = "Spaltenbreiten konnten nicht gespeichert werden: " + ex.Message; }
    }

    private void ApplyColumnWidths(string table)
    {
        var header = table switch
        {
            "entries" => EntryHeader, "plans" => PlanHeader, "stock" => StockHeader,
            "foods" => FoodHeader, "rules" => RuleHeader,
            "ingredients" => MealIngredientsHeader, "meals" => MealHistoryHeader, _ => IntakeHeader
        };
        var rows = table switch
        {
            "entries" => visibleEntries.Cast<Grid>().ToArray(),
            "plans" => PlanList.Items.Cast<Grid>().ToArray(),
            "stock" => StockList.Items.Cast<Grid>().ToArray(),
            "foods" => FoodCatalogList.Items.Cast<Grid>().ToArray(),
            "rules" => FoodRulesList.Items.Cast<Grid>().ToArray(),
            "ingredients" => MealIngredientsList.Items.Cast<Grid>().ToArray(),
            "meals" => MealHistoryList.Items.Cast<Grid>().ToArray(),
            _ => intakeRows.Select(row => row.Visual).ToArray()
        };
        foreach (var grid in rows.Prepend((Grid)header.Children[0]))
            for (int i = 0; i < grid.ColumnDefinitions.Count; i++)
                grid.ColumnDefinitions[i].Width = new GridLength(columnWidths[table][i]);
        if (table == "ingredients") FillMealIngredientsWidth();
    }

    private void MealIngredientsViewport_SizeChanged(object sender, SizeChangedEventArgs e) => FillMealIngredientsWidth();

    private void FillMealIngredientsWidth()
    {
        if (MealIngredientsViewport is null || MealIngredientsHeader?.Children.FirstOrDefault() is not Grid header)
            return;
        var widths = columnWidths["ingredients"];
        var last = widths.Length - 1;
        var remaining = MealIngredientsViewport.ActualWidth - widths.Take(last).Sum() - 2;
        var width = Math.Max(widths[last], remaining);
        foreach (var grid in MealIngredientsList.Items.Cast<Grid>().Prepend(header))
            grid.ColumnDefinitions[last].Width = new GridLength(width);
    }

    private Grid SortableHeader(string table, (string Text, int Width)[] cells)
    {
        var headerBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 224, 236, 242));
        var grid = new Grid { MinHeight = 34, Background = headerBrush };
        for (int i = 0; i < cells.Length; i++)
        {
            int column = i;
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(columnWidths[table][i]) });
            var selected = tableSort[table];
            var title = cells[i].Text + (selected.Column == i ? (selected.Descending ? "  ↓" : "  ↑") : "");
            var heading = new Border { Padding = new Thickness(7, 6, 31, 6), Background = headerBrush,
                Child = new TextBlock { Text = title, Tag = cells[i].Text, TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold } };
            heading.Tapped += (_, _) => SortTable(table, column);
            Grid.SetColumn(heading, i);
            grid.Children.Add(heading);
            var filterButton = new Button { Content = new FontIcon { Glyph = "\uE721", FontSize = 13 }, Width = 26, Height = 26,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0), Margin = new Thickness(0, 0, 10, 0),
                Background = headerBrush, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(13), Tag = i };
            ToolTipService.SetToolTip(filterButton, N("Diese Spalte durchsuchen", "Filter this column"));
            var filterBox = new TextBox { Width = 240, PlaceholderText = N("Suchbegriff", "Search term") };
            var term = tableFilters.TryGetValue(table, out var active) && active.TryGetValue(i, out var saved)
                ? saved : "";
            var filterFlyout = new Flyout();
            var filterPanel = new StackPanel { Spacing = 6 };
            var choices = TableFilterChoices(table, i);
            ComboBox? choiceBox = null;
            if (choices is null)
            {
                filterBox.Text = term;
                filterPanel.Children.Add(filterBox);
            }
            else
            {
                choiceBox = new ComboBox { Width = 240 };
                choiceBox.Items.Add(N("Alle", "All"));
                foreach (var choice in choices) choiceBox.Items.Add(T(choice));
                choiceBox.SelectedIndex = Math.Max(0, Array.FindIndex(choices,
                    choice => T(choice).Equals(term, StringComparison.CurrentCultureIgnoreCase)) + 1);
                filterPanel.Children.Add(choiceBox);
            }
            void SetFilter(string value)
            {
                if (!tableFilters.TryGetValue(table, out var terms))
                    tableFilters[table] = terms = new Dictionary<int, string>();
                if (string.IsNullOrWhiteSpace(value)) terms.Remove(column);
                else terms[column] = value.Trim();
                RefreshFilteredTable(table);
            }
            var clear = new Button { Content = N("Filter löschen", "Clear filter") };
            clear.Click += (_, _) => { if (choiceBox is null) filterBox.Text = ""; else choiceBox.SelectedIndex = 0; };
            filterPanel.Children.Add(clear);
            filterFlyout.Content = filterPanel;
            filterButton.Flyout = filterFlyout;
            if (choiceBox is null) filterBox.TextChanged += (_, _) => SetFilter(filterBox.Text);
            else choiceBox.SelectionChanged += (_, _) => SetFilter(choiceBox.SelectedIndex <= 0
                ? "" : choiceBox.SelectedItem?.ToString() ?? "");
            Grid.SetColumn(filterButton, i);
            grid.Children.Add(filterButton);
            var grip = new Thumb { Width = 9, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch, Background = headerBrush };
            grip.DragDelta += (_, args) =>
            {
                columnWidths[table][column] = Math.Clamp(
                    columnWidths[table][column] + (int)Math.Round(args.HorizontalChange), 55, 1200);
                ApplyColumnWidths(table);
            };
            grip.DragCompleted += (_, _) =>
            {
                try { SaveJson(ColumnWidthsPath, columnWidths); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { Status.Text = "Spaltenbreiten konnten nicht gespeichert werden: " + ex.Message; }
            };
            Grid.SetColumn(grip, i);
            grid.Children.Add(grip);
        }
        return grid;
    }

    private void SortTable(string table, int column)
    {
        var current = tableSort[table];
        tableSort[table] = (column, current.Column == column ? !current.Descending : false);
        RefreshSortableHeaderCaptions();
        switch (table)
        {
            case "entries": DisplayEntries(); break;
            case "plans": RenderPlans(); break;
            case "stock": RenderStock(); break;
            case "intakes": SortIntakeRows(); break;
            case "foods": RenderFoodCatalog(); break;
            case "rules": RenderFoodRules(); break;
            case "ingredients": RenderMealIngredients(); break;
            case "meals": RenderMealHistory(); break;
        }
    }

    private void RefreshSortableHeaderCaptions()
    {
        foreach (var (table, host) in new[] { ("entries", EntryHeader), ("plans", PlanHeader),
                     ("stock", StockHeader), ("intakes", IntakeHeader),
                     ("foods", FoodHeader), ("rules", RuleHeader),
                     ("ingredients", MealIngredientsHeader), ("meals", MealHistoryHeader) })
        {
            if (host.Children.FirstOrDefault() is not Grid headerGrid) continue;
            foreach (var heading in headerGrid.Children.OfType<Border>().Select((border, index) => (border, index)))
            {
                if (heading.border.Child is not TextBlock { Tag: string canonical } caption) continue;
                var sort = tableSort[table];
                caption.Text = T(canonical) + (sort.Column == heading.index
                    ? (sort.Descending ? "  ↓" : "  ↑") : "");
            }
            foreach (var button in headerGrid.Children.OfType<Button>())
            {
                var column = (int)button.Tag;
                var filtered = tableFilters.TryGetValue(table, out var filters) && filters.ContainsKey(column);
                button.Background = filtered
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 119, 137))
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 224, 236, 242));
                button.Foreground = new SolidColorBrush(filtered ? Microsoft.UI.Colors.White :
                    Windows.UI.Color.FromArgb(255, 45, 65, 78));
                button.FontWeight = filtered
                    ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
                if (button.Content is FontIcon icon)
                {
                    icon.Foreground = button.Foreground;
                    icon.FontWeight = filtered ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
                    icon.FontSize = filtered ? 16 : 13;
                }
            }
        }
    }

    private void InitializeTableHeaders()
    {
        PlanHeader.Children.Add(SortableHeader("plans", [("Uhrzeit", 85), ("Präparat", 205), ("Dosis", 125),
            ("Anzahl", 85), ("Form", 120), ("Ab wann", 125), ("Bis wann", 125), ("Warum", 290)]));
        StockHeader.Children.Add(SortableHeader("stock", [("Präparat", 170), ("Form", 100), ("Hersteller", 145),
            ("Lieferant", 145), ("Packung", 95), ("Preis €", 95), ("Bestand", 95),
            ("7 Tage", 90), ("Hinweis", 170)]));
        EntryHeader.Children.Add(SortableHeader("entries", [("Zeit", 180), ("Ende", 180), ("Typ", 115),
            ("Inhalt", 400), ("Notiz", 340)]));

        var headings = new[] { "Uhrzeit", "Präparat", "Geplante Dosis", "Anzahl", "Form",
            "Einnahme", "Tatsächliche Dosis", "Tatsächl. Anzahl" };
        IntakeHeader.Children.Add(SortableHeader("intakes", headings.Select((text, index) =>
            (text, columnWidths["intakes"][index])).ToArray()));
        FoodHeader.Children.Add(SortableHeader("foods", [("Bezeichnung", 280), ("KH / 100 g", 130),
            ("GI", 105), ("GL / 100 g", 130), ("Quelle", 230), ("Notiz", 250)]));
        RuleHeader.Children.Add(SortableHeader("rules", [("Suchbegriff", 230), ("Einstufung", 145),
            ("Priorität", 95), ("Bedingung / Begründung", 380), ("Quelle", 230)]));
        MealIngredientsHeader.Children.Add(SortableHeader("ingredients", [("Zutat", 260), ("Menge", 105),
            ("KH-Gehalt", 125)]));
        MealHistoryHeader.Children.Add(SortableHeader("meals", [("Datum", 170), ("Mahlzeit / Getränk", 290),
            ("Status", 125), ("Zutaten", 100), ("KH-Gehalt", 110), ("Notiz", 300)]));
    }

    private static void SaveJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value));
        File.Move(temporary, path, true);
    }

    private void RenderGoals(IEnumerable<string>? selected = null)
    {
        var selectedNames = selected?.ToHashSet(StringComparer.OrdinalIgnoreCase) ??
            goalChecks.Where(check => check.IsChecked == true).Select(check => check.Tag?.ToString() ?? check.Content.ToString()!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        goalChecks.Clear();
        foreach (var name in choices.Goals.OrderBy(name => name, StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true)))
            goalChecks.Add(new CheckBox { Content = T(name), Tag = name, IsChecked = selectedNames.Contains(name) });
        PlanGoals.ItemsSource = goalChecks.ToArray();
    }

    private void RenderPlans()
    {
        displayedPlans.Clear();
        var (column, descending) = tableSort["plans"];
        string Key(MedicationPlan plan) => column switch
        {
            0 => plan.Time, 1 => plan.Name, 2 => plan.Dose,
            3 => ParseNumber(plan.Quantity).ToString("000000000000.000", CultureInfo.InvariantCulture),
            4 => plan.Form, 5 => plan.StartDate, 6 => plan.EndDate ?? "9999-12-31",
            _ => string.Join(", ", plan.Goals)
        };
        var filtered = plans.Where(plan => MatchesTableFilters("plans", plan.Time, plan.Name, plan.Dose,
            plan.Quantity, plan.Form, plan.StartDate == "0001-01-01" ? T("bisher") : plan.StartDate,
            plan.EndDate ?? T("Läuft noch"), string.Join(", ", plan.Goals)));
        displayedPlans.AddRange(descending
            ? filtered.OrderByDescending(Key, StringComparer.CurrentCultureIgnoreCase).ThenBy(plan => plan.Id)
            : filtered.OrderBy(Key, StringComparer.CurrentCultureIgnoreCase).ThenBy(plan => plan.Id));
        PlanList.ItemsSource = displayedPlans.Select(plan => TableRow([
            (plan.Time, 85), (plan.Name, 205), (plan.Dose, 125), (plan.Quantity, 85),
            (plan.Form, 120), (plan.StartDate == "0001-01-01" ? T("bisher") : DateOnly.ParseExact(plan.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd.MM.yyyy"), 125),
            (plan.EndDate is null ? T("Läuft noch") : DateOnly.ParseExact(plan.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd.MM.yyyy"), 125),
            (string.Join(", ", plan.Goals), 290)], table: "plans")).ToArray();
        ConfigureListFeedback(PlanList);
    }

    private void ConfigureListFeedback(ListView list)
    {
        var rows = list.Items.OfType<Grid>().Select(row => (Row: row, Base: (Brush?)row.Background)).ToList();
        listFeedbackRows[list] = rows;
        foreach (var (row, _) in rows)
        {
            row.Background ??= new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            row.Tapped += (_, _) => list.SelectedItem = row;
            row.PointerEntered += (_, _) => { hoveredListRow = row; UpdateListFeedback(list); };
            row.PointerExited += (_, _) =>
            {
                if (ReferenceEquals(hoveredListRow, row)) hoveredListRow = null;
                UpdateListFeedback(list);
            };
        }
        UpdateListFeedback(list);
    }

    private void UpdateListFeedback(ListView list)
    {
        if (!listFeedbackRows.TryGetValue(list, out var rows)) return;
        foreach (var (row, original) in rows)
        {
            var selected = ReferenceEquals(list.SelectedItem, row);
            var hovered = ReferenceEquals(hoveredListRow, row);
            var red = original is SolidColorBrush brush && brush.Color.R > brush.Color.G + 12 &&
                brush.Color.R > brush.Color.B + 12;
            row.Background = selected
                ? new SolidColorBrush(red ? Windows.UI.Color.FromArgb(255, 255, 195, 195) :
                    Windows.UI.Color.FromArgb(255, 216, 234, 243))
                : hovered
                    ? new SolidColorBrush(red ? Windows.UI.Color.FromArgb(255, 255, 216, 216) :
                        Windows.UI.Color.FromArgb(255, 239, 246, 250))
                    : original ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private void ConfigurePanelFeedback(StackPanel panel)
    {
        var rows = panel.Children.OfType<Grid>().Skip(1).Select(row => (Row: row, Base: (Brush?)row.Background)).ToList();
        panelFeedbackRows[panel] = rows;
        foreach (var (row, _) in rows)
        {
            row.Background ??= new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            row.Tapped += (_, _) => { selectedPanelRow = row; RefreshPanelFeedback(); };
            row.PointerEntered += (_, _) => { hoveredPanelRow = row; RefreshPanelFeedback(); };
            row.PointerExited += (_, _) =>
            {
                if (ReferenceEquals(hoveredPanelRow, row)) hoveredPanelRow = null;
                RefreshPanelFeedback();
            };
        }
        UpdatePanelFeedback(panel);
    }

    private void RefreshPanelFeedback()
    {
        foreach (var panel in panelFeedbackRows.Keys) UpdatePanelFeedback(panel);
    }

    private void UpdatePanelFeedback(StackPanel panel)
    {
        if (!panelFeedbackRows.TryGetValue(panel, out var rows)) return;
        foreach (var (row, original) in rows)
            row.Background = ReferenceEquals(selectedPanelRow, row)
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 216, 234, 243))
                : ReferenceEquals(hoveredPanelRow, row)
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 246, 250))
                    : original ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private async void ManageMeasures_Click(object sender, RoutedEventArgs e)
    {
        var selected = MeasureName.SelectedItem?.ToString();
        if (!await EditChoicesAsync("Maßnahmen verwalten", choices.Measures)) return;
        MeasureName.ItemsSource = choices.Measures.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();
        MeasureName.SelectedItem = selected is not null && choices.Measures.Contains(selected) ? selected :
            choices.Measures.FirstOrDefault();
    }

    private async void ManageDimensions_Click(object sender, RoutedEventArgs e)
    {
        bool rest = IntervalKind.SelectedIndex == 1;
        if (!await EditChoicesAsync(rest ? "Ruheformen verwalten" : "Belastungsarten verwalten",
            rest ? choices.Rests : choices.Activities)) return;
        IntervalKind_SelectionChanged(IntervalKind, null!);
    }

    private async void ManageSymptoms_Click(object sender, RoutedEventArgs e)
    {
        if (!await EditChoicesAsync("Symptome verwalten", choices.Symptoms)) return;
        BuildFields();
        RenderMeasureReasons();
        RefreshAnalysis();
    }

    private async void ManagePainLocations_Click(object sender, RoutedEventArgs e)
    {
        if (await EditChoicesAsync("Schmerzorte verwalten", choices.PainLocations)) BuildFields();
    }

    private async void ManageHearingProtection_Click(object sender, RoutedEventArgs e)
    {
        if (await EditChoicesAsync("Schutzmaßnahmen verwalten", choices.HearingProtection)) RenderHearingProtection();
    }

    private static string CanonicalProtection(string name) => name.ToLowerInvariant() switch
    {
        "nc" => "NC-Kopfhörer", "loop-ohrstöpsel" => "Loop", "calmer-ohrstöpsel" => "Calmer", _ => name
    };

    private void RenderHearingProtection()
    {
        var selected = protectionChecks.Where(c => c.IsChecked == true).Select(c => c.Tag?.ToString() ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        protectionChecks.Clear();
        foreach (var name in choices.HearingProtection.OrderBy(name => name,
            StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true)))
            protectionChecks.Add(new CheckBox { Content = T(name), Tag = name,
                IsChecked = selected.Contains(name) });
        IntervalHearingProtection.ItemsSource = protectionChecks.ToArray();
    }

    private async Task<bool> EditChoicesAsync(string title, List<string> target)
    {
        var draft = target.ToList();
        var list = new ListView { Height = 300, ItemsSource = draft.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray() };
        var input = new TextBox { PlaceholderText = "Neue Bezeichnung" };
        var add = new Button { Content = "Hinzufügen" };
        var remove = new Button { Content = "Auswahl entfernen" };
        add.Click += (_, _) =>
        {
            var name = input.Text.Trim();
            if (name.Length == 0 || draft.Contains(name, StringComparer.CurrentCultureIgnoreCase)) return;
            draft.Add(name);
            list.ItemsSource = draft.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();
            list.SelectedItem = name;
            input.Text = "";
        };
        remove.Click += (_, _) =>
        {
            if (list.SelectedItem is not string name) return;
            draft.Remove(name);
            list.ItemsSource = draft.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(add); buttons.Children.Add(remove);
        var body = new StackPanel { Spacing = 10, Width = 420 };
        body.Children.Add(input); body.Children.Add(buttons); body.Children.Add(list);
        var dialog = new ContentDialog { Title = title, Content = body, PrimaryButtonText = "Übernehmen",
            CloseButtonText = "Abbrechen", DefaultButton = ContentDialogButton.Primary,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot };
        if (await ShowTranslatedDialogAsync(dialog) != ContentDialogResult.Primary) return false;
        var previous = target.ToList();
        target.Clear(); target.AddRange(draft);
        try
        {
            store.SetChoiceOptions(ReferenceEquals(target, choices.Measures) ? "measures" :
                ReferenceEquals(target, choices.Rests) ? "rest_dimensions" :
                ReferenceEquals(target, choices.Symptoms) ? "symptoms" :
                ReferenceEquals(target, choices.PainLocations) ? "pain_locations" :
                ReferenceEquals(target, choices.HearingProtection) ? "hearing_protection" : "activity_dimensions", target);
            return true;
        }
        catch (Exception ex)
        {
            target.Clear(); target.AddRange(previous);
            MeasureStatus.Text = IntervalStatus.Text = "Auswahl konnte nicht gespeichert werden: " + ex.Message;
            return false;
        }
    }

    private void RenderMeasureReasons(IEnumerable<string>? selected = null)
    {
        var selectedNames = selected?.ToHashSet(StringComparer.OrdinalIgnoreCase) ??
            measureReasonChecks.Where(check => check.IsChecked == true)
                .Select(check => check.Tag?.ToString() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
        measureReasonChecks.Clear();
        foreach (var name in choices.Symptoms.Concat(selectedNames)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name,
                StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true)))
            measureReasonChecks.Add(new CheckBox { Content = T(name), Tag = name,
                IsChecked = selectedNames.Contains(name) });
        MeasureReasons.ItemsSource = measureReasonChecks.ToArray();
    }

    private void MeasureTrack_Changed(object sender, RoutedEventArgs e)
    {
        if (MeasureTrackingFields is not null)
            MeasureTrackingFields.Visibility = MeasureTrack.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string MeasureSelection(ComboBox box) =>
        (box.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

    private static void SelectMeasureValue(ComboBox box, string value)
    {
        box.SelectedItem = box.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == value) ?? box.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }

    private void SaveMeasure_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCalendarTime(MeasureDate, MeasureTime, out var at))
        { MeasureStatus.Text = "Bitte Zeit als TT.MM.JJJJ und HH:mm eingeben."; return; }
        var name = MeasureName.SelectedItem?.ToString()?.Trim() ?? "";
        if (name.Length == 0) { MeasureStatus.Text = "Bitte eine Maßnahme angeben."; return; }
        var tracking = MeasureTrack.IsChecked == true;
        DateTime? reviewAt = null;
        if (tracking && MeasureReviewDate.Date is not null)
        {
            if (!TryCalendarTime(MeasureReviewDate, MeasureReviewTime, out var planned))
            { MeasureStatus.Text = "Bitte die Uhrzeit des Rückblicks als HH:mm eingeben."; return; }
            if (planned < at)
            { MeasureStatus.Text = "Der Rückblick darf nicht vor dem Beginn liegen."; return; }
            reviewAt = planned;
        }
        else if (tracking && MeasureReviewTime.Text.Trim().Length > 0)
        { MeasureStatus.Text = "Bitte zum Rückblick auch ein Datum auswählen."; return; }
        if (tracking && string.IsNullOrWhiteSpace(MeasureGoal.Text))
        { MeasureStatus.Text = "Bitte ein Ziel oder Problem für die laufende Maßnahme angeben."; return; }
        var status = tracking ? MeasureSelection(MeasureProgressStatus) : "active";
        var outcome = tracking ? MeasureSelection(MeasureOutcome) : "";
        if (tracking && status == "completed" && outcome.Length == 0)
        { MeasureStatus.Text = "Bitte beim Abschluss das beobachtete Ergebnis auswählen."; return; }
        var previous = editingEntryKind == "Maßnahme" ? entries.FirstOrDefault(item => item.Id == editingEntryId) : null;
        MeasureData? oldData = previous is null ? null : JsonSerializer.Deserialize<MeasureData>(previous.Data);
        var data = new MeasureData
        {
            Name = name, Dose = MeasureDose.Text.Trim(), TrackProgress = tracking,
            ReasonSymptoms = measureReasonChecks.Where(check => check.IsChecked == true)
                .Select(check => check.Tag?.ToString() ?? "").ToList(),
            ReasonOther = MeasureReasonOther.Text.Trim(),
            Goal = tracking ? MeasureGoal.Text.Trim() : "",
            Baseline = tracking ? MeasureBaseline.Text.Trim() : "",
            ReviewDate = reviewAt,
            Status = status, Outcome = outcome,
            ReviewNote = tracking ? MeasureReviewNote.Text.Trim() : "",
            ReviewedAt = tracking && outcome.Length > 0
                ? oldData?.Outcome == outcome && oldData?.ReviewNote == MeasureReviewNote.Text.Trim() && oldData.ReviewedAt is not null
                    ? oldData.ReviewedAt : DateTime.Now : null
        };
        var entry = new Entry { Id = previous?.Id ?? 0, Kind = "Maßnahme", Start = at,
            Data = JsonSerializer.Serialize(data), Note = MeasureNote.Text };
        try { store.Save(entry); ResetMeasure_Click(sender, e); LoadEntries(); MeasureStatus.Text = "Maßnahme gespeichert. Neuer Eintrag vorbereitet."; }
        catch (Exception ex) { MeasureStatus.Text = "Speichern fehlgeschlagen: " + ex.Message; }
    }

    private void ResetMeasure_Click(object sender, RoutedEventArgs e)
    {
        ClearEditing();
        MeasureDate.Date = DateTimeOffset.Now;
        MeasureTime.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        MeasureDose.Text = MeasureNote.Text = MeasureGoal.Text = MeasureBaseline.Text = MeasureReviewNote.Text = MeasureReasonOther.Text = "";
        RenderMeasureReasons([]);
        MeasureReviewDate.Date = null;
        MeasureReviewTime.Text = "";
        MeasureTrack.IsChecked = false;
        MeasureProgressStatus.SelectedIndex = 0;
        MeasureOutcome.SelectedIndex = 0;
        MeasureStatus.Text = "Neuer Eintrag vorbereitet.";
    }

    private void OpenOngoingMeasure_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: long id }) return;
        var entry = entries.FirstOrDefault(item => item.Id == id);
        if (entry is not null) EditEntry(entry);
    }

    private string MeasureDescription(MeasureData? measure)
    {
        if (measure is null) return "";
        var name = $"{T(measure.Name)} {measure.Dose}".Trim();
        var reasons = (measure.ReasonSymptoms ?? []).Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Select(T).ToList();
        if (!string.IsNullOrWhiteSpace(measure.ReasonOther)) reasons.Add(measure.ReasonOther.Trim());
        return reasons.Count == 0 ? name : name + (selectedLanguage == "en" ? " for " : " wegen ") +
            string.Join(", ", reasons);
    }

    private static string MeasureOutcomeLabel(string outcome) => outcome switch
    {
        "much" => "deutlich geholfen", "some" => "etwas geholfen", "unchanged" => "unverändert",
        "worse" => "verschlechtert", "unclear" => "unklar", _ => ""
    };

    private void RenderOngoingMeasures()
    {
        if (OngoingMeasures is null) return;
        OngoingMeasures.Children.Clear();
        var ongoing = entries.Where(item => item.Kind == "Maßnahme").Select(item =>
        {
            try { return (Entry: item, Data: JsonSerializer.Deserialize<MeasureData>(item.Data)); }
            catch (JsonException) { return (Entry: item, Data: (MeasureData?)null); }
        }).Where(item => item.Data?.TrackProgress == true && item.Data.Status != "completed")
          .OrderBy(item => item.Data!.ReviewDate ?? DateTime.MaxValue).ThenBy(item => item.Entry.Start);
        foreach (var item in ongoing)
        {
            var due = item.Data!.ReviewDate is { } date && date <= DateTime.Now;
            var label = $"{MeasureDescription(item.Data)} · {item.Entry.Start:dd.MM.yyyy} · " +
                (item.Data.Status == "paused" ? T("Pausiert") : T("Läuft")) +
                (item.Data.ReviewDate is { } review
                    ? N($" · Rückblick {review:dd.MM.yyyy HH:mm}", $" · Review {review:dd.MM.yyyy HH:mm}") +
                      (due ? N(" fällig", " due") : "") : "");
            var button = new Button { Content = label, Tag = item.Entry.Id,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(item.Data.Status == "paused"
                    ? Windows.UI.Color.FromArgb(255, 255, 244, 216)
                    : Windows.UI.Color.FromArgb(255, 226, 244, 232)),
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 51, 55)),
                BorderThickness = new Thickness(0) };
            button.Click += OpenOngoingMeasure_Click;
            OngoingMeasures.Children.Add(button);
        }
        if (OngoingMeasures.Children.Count == 0)
            OngoingMeasures.Children.Add(new TextBlock { Text = N("Keine laufenden Maßnahmen.", "No ongoing interventions.") });
    }

    private void AddGoal_Click(object sender, RoutedEventArgs e)
    {
        var name = NewGoalName.Text.Trim();
        if (name.Length == 0 || choices.Goals.Contains(name, StringComparer.CurrentCultureIgnoreCase)) return;
        choices.Goals.Add(name);
        try { store.AddGoalOption(name); }
        catch (Exception ex)
        { choices.Goals.Remove(name); PlanStatus.Text = ex.Message; return; }
        RenderGoals([name, ..goalChecks.Where(check => check.IsChecked == true).Select(check => check.Tag?.ToString() ?? check.Content.ToString()!)]);
        NewGoalName.Text = "";
    }

    private void SavePlan_Click(object sender, RoutedEventArgs e)
    {
        if (!TimeOnly.TryParseExact(PlanTime.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var at))
        { PlanStatus.Text = "Bitte die Uhrzeit als HH:mm eingeben."; return; }
        if (string.IsNullOrWhiteSpace(PlanName.Text))
        { PlanStatus.Text = "Bitte ein Präparat angeben."; return; }
        var start = PlanStartDate.Date is { } startDate
            ? DateOnly.FromDateTime(startDate.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : plans.Any(plan => plan.Id == editingPlanId && plan.StartDate == "0001-01-01") ? "0001-01-01" : "";
        if (start.Length == 0) { PlanStatus.Text = "Bitte den Beginn angeben."; return; }
        string? end = null;
        if (PlanOngoing.IsChecked != true)
        {
            if (PlanEndDate.Date is not { } endDate)
            { PlanStatus.Text = "Bitte das Ende im Kalender auswählen."; return; }
            end = DateOnly.FromDateTime(endDate.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (string.CompareOrdinal(end, start) < 0)
            { PlanStatus.Text = "Das Ende darf nicht vor dem Beginn liegen."; return; }
        }
        var newPlan = new MedicationPlan { Id = editingPlanId,
            Time = at.ToString("HH:mm", CultureInfo.InvariantCulture), Name = PlanName.Text.Trim(),
            StartDate = start, EndDate = end,
            Quantity = PlanQuantity.Text.Trim(), Form = (PlanForm.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Kapsel", Dose = PlanDose.Text.Trim(),
            Goals = goalChecks.Where(check => check.IsChecked == true).Select(check => check.Tag?.ToString() ?? check.Content.ToString()!).ToList() };
        try
        {
            store.SaveMedicationPlan(new PaceAtlas.MedicationPlan { Id = editingPlanId,
                Time = newPlan.Time, Name = newPlan.Name, Quantity = newPlan.Quantity,
                Form = newPlan.Form, Dose = newPlan.Dose, Goals = newPlan.Goals,
                StartDate = newPlan.StartDate, EndDate = newPlan.EndDate });
            ReloadMedicationData(); RenderPlans(); RenderIntakes(); RenderStock(); ResetPlan(); RefreshTodaySummary();
            PlanStatus.Text = "Einnahmeplan gespeichert.";
        }
        catch (Exception ex) { PlanStatus.Text = "Speichern fehlgeschlagen: " + ex.Message; }
    }

    private void PlanList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateListFeedback(PlanList);
        var index = PlanList.SelectedIndex;
        if (index < 0 || index >= displayedPlans.Count) return;
        if (editingPlanId != 0 && editingPlanId != displayedPlans[index].Id)
            ResetPlan(clearSelection: false);
    }

    private void PlanList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var index = PlanList.SelectedIndex;
        if (index < 0 || index >= displayedPlans.Count) return;
        SetPlanExpanded(false);
        var plan = displayedPlans[index];
        editingPlanId = plan.Id;
        UpdateEditingIndicators();
        PlanTime.Text = plan.Time;
        PlanName.Text = plan.Name;
        PlanQuantity.Text = plan.Quantity;
        PopulatePlanForms(plan.Form);
        PlanDose.Text = plan.Dose;
        PlanStartDate.Date = plan.StartDate == "0001-01-01" ? null :
            new DateTimeOffset(DateOnly.ParseExact(plan.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToDateTime(TimeOnly.MinValue));
        PlanOngoing.IsChecked = plan.EndDate is null;
        PlanEndDate.Date = plan.EndDate is null ? null :
            new DateTimeOffset(DateOnly.ParseExact(plan.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToDateTime(TimeOnly.MinValue));
        RenderGoals(plan.Goals);
    }

    private void ResetPlan(bool clearSelection = true)
    {
        editingPlanId = 0;
        UpdateEditingIndicators();
        if (clearSelection) PlanList.SelectedIndex = -1;
        PlanTime.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        PlanStartDate.Date = DateTimeOffset.Now;
        PlanOngoing.IsChecked = true;
        PlanEndDate.Date = null;
        PlanName.Text = PlanQuantity.Text = PlanDose.Text = "";
        PopulatePlanForms(MedicationForms[0]);
        RenderGoals([]);
    }

    private void ResetPlan_Click(object sender, RoutedEventArgs e)
    { ResetPlan(); PlanStatus.Text = "Neuer Eintrag vorbereitet."; }

    private void PlanOngoing_Changed(object sender, RoutedEventArgs e)
    {
        if (PlanEndDate is not null) PlanEndDate.IsEnabled = PlanOngoing.IsChecked != true;
    }

    private void DeletePlan_Click(object sender, RoutedEventArgs e)
    {
        var plan = plans.FirstOrDefault(plan => plan.Id == editingPlanId);
        if (plan is null) return;
        try { store.DeleteMedicationPlan(plan.Id); ReloadMedicationData(); RenderPlans(); RenderIntakes(); RenderStock(); ResetPlan(); RefreshTodaySummary(); PlanStatus.Text = "Eintrag gelöscht."; }
        catch (Exception ex) { PlanStatus.Text = "Löschen fehlgeschlagen: " + ex.Message; }
    }

    private void ResetInterval_Click(object sender, RoutedEventArgs e)
    {
        ClearEditing();
        var now = DateTime.Now;
        IntervalStartDate.Date = new DateTimeOffset(now);
        IntervalEndDate.Date = new DateTimeOffset(now.AddMinutes(30));
        IntervalStartTime.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        IntervalEndTime.Text = now.AddMinutes(30).ToString("HH:mm", CultureInfo.InvariantCulture);
        IntervalRunning.IsChecked = false;
        IntervalKind.SelectedIndex = 0;
        IntervalIntensity.SelectedIndex = 1;
        SleepRecovery.SelectedIndex = 0;
        ActivityTemplateChoice.SelectedIndex = 0;
        foreach (var check in protectionChecks) check.IsChecked = false;
        foreach (var check in intervalDimensionChecks) check.IsChecked = false;
        IntervalNote.Text = "";
        IntervalStatus.Text = "Neuer Zeitraum vorbereitet.";
    }

    private bool TryIntakeDay(out DateTime day)
    {
        if (IntakeDate.Date is { } selected) { day = selected.Date; return true; }
        day = default;
        IntakeStatus.Text = "Bitte einen Tag im Kalender auswählen.";
        return false;
    }

    private void IntakeDate_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs e)
    {
        if (intakeRows is not null && IntakeRows is not null) RenderIntakes();
    }

    private void RenderIntakes()
    {
        if (!TryIntakeDay(out var day)) return;
        selectedIntakeRow = null;
        hoveredIntakeRow = null;
        intakeRows.Clear();
        IntakeRows.Children.Clear();
        var key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dayRecords = intakes.Where(item => item.Day == key).ToList();
        var dayPlans = plans.Where(plan => plan.IsActiveOn(DateOnly.FromDateTime(day))).Select(plan =>
            {
                var record = dayRecords.LastOrDefault(item => item.PlanId == plan.Id);
                return (plan: record is null ? plan : new MedicationPlan
                {
                    Id = plan.Id, ProductId = record.ProductId == 0 ? plan.ProductId : record.ProductId,
                    Time = record.Time, Name = record.Name, Dose = record.Dose,
                    Quantity = string.IsNullOrWhiteSpace(record.Quantity) ? plan.Quantity : record.Quantity,
                    Form = string.IsNullOrWhiteSpace(record.Form) ? plan.Form : record.Form,
                    Goals = record.Goals
                }, record);
            })
            .Concat(dayRecords.Where(item => plans.All(plan => plan.Id != item.PlanId || !plan.IsActiveOn(DateOnly.FromDateTime(day))))
                .Select(item => (plan: new MedicationPlan { Id = item.PlanId, Time = item.Time, Name = item.Name,
                    ProductId = item.ProductId, Goals = item.Goals,
                    Quantity = item.Quantity, Dose = item.Dose, Form = item.Form }, record: (IntakeRecord?)item)))
            .OrderBy(pair => pair.plan.Time).ThenBy(pair => pair.plan.Name);
        foreach (var (plan, record) in dayPlans)
        {
            var row = new Grid { Height = 36 };
            foreach (var width in columnWidths["intakes"])
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
            var values = new[] { record?.Time ?? plan.Time, record?.Name ?? plan.Name,
                record?.Dose ?? plan.Dose, record?.Quantity ?? plan.Quantity, record?.Form ?? plan.Form };
            for (int i = 0; i < values.Length; i++)
            {
                if (i == 4)
                {
                    var formIcon = MedicationFormCell(values[i]);
                    Grid.SetColumn(formIcon, i);
                    row.Children.Add(formIcon);
                    continue;
                }
                var label = new TextBlock { Text = values[i], Margin = new Thickness(8, 3, 8, 3),
                    VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(label, i);
                row.Children.Add(label);
            }
            var status = new ComboBox { Margin = new Thickness(2, 1, 2, 1), Height = 32,
                VerticalAlignment = VerticalAlignment.Center };
            PopulateIntakeStatus(status, record?.Status == "Genommen" ? 1 : record?.Status == "Ausgelassen" ? 2 : 0);
            var actualDose = new TextBox { Margin = new Thickness(2, 1, 2, 1), Height = 32, Text = record?.ActualDose ?? plan.Dose,
                VerticalAlignment = VerticalAlignment.Center };
            var actualQuantity = new TextBox { Margin = new Thickness(2, 1, 2, 1), Height = 32, Text = record?.ActualQuantity ?? plan.Quantity,
                VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(status, 5); Grid.SetColumn(actualDose, 6); Grid.SetColumn(actualQuantity, 7);
            row.Children.Add(status);
            row.Children.Add(actualDose);
            row.Children.Add(actualQuantity);
            IntakeRows.Children.Add(row);
            var intakeRow = new IntakeRow { Plan = plan, Status = status, Dose = actualDose, Quantity = actualQuantity,
                Visual = row };
            intakeRows.Add(intakeRow);
            row.Tapped += (_, _) => { selectedIntakeRow = intakeRow; RefreshMedicationDueIndicators(); };
            row.PointerEntered += (_, _) => { hoveredIntakeRow = intakeRow; RefreshMedicationDueIndicators(); };
            row.PointerExited += (_, _) =>
            {
                if (ReferenceEquals(hoveredIntakeRow, intakeRow)) hoveredIntakeRow = null;
                RefreshMedicationDueIndicators();
            };
            status.SelectionChanged += (_, _) =>
            {
                RefreshMedicationDueIndicators();
                UpdateTakeSelectedTimeButton();
            };
        }
        SortIntakeRows();
        var selectedTime = IntakeBulkTime.SelectedItem?.ToString();
        var times = intakeRows.Select(row => row.Plan.Time).Distinct(StringComparer.Ordinal)
            .OrderBy(time => time, StringComparer.Ordinal).ToArray();
        IntakeBulkTime.ItemsSource = times;
        IntakeBulkTime.SelectedItem = selectedTime is not null && times.Contains(selectedTime) ? selectedTime : times.FirstOrDefault();
        UpdateTakeSelectedTimeButton();
        RefreshMedicationDueIndicators();
    }

    private void IntakeBulkTime_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateTakeSelectedTimeButton();

    private void UpdateTakeSelectedTimeButton()
    {
        if (TakeSelectedTimeButton is null || IntakeBulkTime is null || IntakeDate is null) return;
        var time = IntakeBulkTime.SelectedItem?.ToString();
        var day = IntakeDate.Date?.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        TakeSelectedTimeButton.IsEnabled = time is not null && day is not null &&
            intakeRows.Any(row => row.Plan.Time == time &&
                (row.Status.SelectedIndex != 1 ||
                 !intakes.Any(saved => saved.Day == day && saved.PlanId == row.Plan.Id &&
                     saved.Status == "Genommen")));
    }

    private static bool IntakeIsDue(DateTime day, string time, DateTime now) =>
        TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var clock) &&
        day.Date.Add(clock.ToTimeSpan()) <= now;

    private void RefreshMedicationDueIndicators()
    {
        var now = DateTime.Now;
        var today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var selectedDay = IntakeDate.Date?.Date;
        foreach (var row in intakeRows)
        {
            var due = selectedDay is { } day && row.Status.SelectedIndex == 0 &&
                IntakeIsDue(day, row.Plan.Time, now);
            var selected = ReferenceEquals(row, selectedIntakeRow);
            var hovered = ReferenceEquals(row, hoveredIntakeRow);
            row.Visual.Background = new SolidColorBrush(due
                ? selected ? Windows.UI.Color.FromArgb(255, 255, 195, 195) :
                    hovered ? Windows.UI.Color.FromArgb(255, 255, 216, 216) : Windows.UI.Color.FromArgb(255, 255, 229, 229)
                : selected ? Windows.UI.Color.FromArgb(255, 216, 234, 243) :
                    hovered ? Windows.UI.Color.FromArgb(255, 239, 246, 250) : Microsoft.UI.Colors.Transparent);
        }

        var displayedToday = selectedDay == now.Date;
        var hasDue = displayedToday
            ? intakeRows.Any(row => row.Status.SelectedIndex == 0 && IntakeIsDue(now.Date, row.Plan.Time, now))
            : plans.Any(plan => plan.IsActiveOn(DateOnly.FromDateTime(now)) &&
                IntakeIsDue(now.Date, plan.Time, now) &&
                !intakes.Any(item => item.Day == today && item.PlanId == plan.Id));
        MedicationDueIcon.Visibility = IntakeDueIcon.Visibility = hasDue ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void TakeSelectedTime_Click(object sender, RoutedEventArgs e)
    {
        var time = IntakeBulkTime.SelectedItem?.ToString();
        if (time is null) { IntakeStatus.Text = "Bitte zuerst eine Uhrzeit auswählen."; return; }
        UpdateTakeSelectedTimeButton();
        if (!TakeSelectedTimeButton.IsEnabled) return;
        var count = 0;
        foreach (var row in intakeRows.Where(row => row.Plan.Time == time))
        {
            row.Status.SelectedIndex = 1;
            count++;
        }
        IntakeStatus.Text = N($"{count} Einnahme(n) um {time} als genommen markiert. Zum Übernehmen den Tag speichern.",
            $"{count} intake(s) at {time} marked as taken. Save the day to apply.");
        if (count == 0) return;
        var date = IntakeDate.Date?.Date.ToString("dd.MM.yyyy") ?? N("dem ausgewählten Tag", "the selected day");
        var dialog = new ContentDialog
        {
            Title = N("Einnahmen jetzt speichern?", "Save intakes now?"),
            Content = N($"Die Einnahmen um {time} Uhr wurden als genommen markiert. Möchtest du alle Angaben für den {date} jetzt speichern?",
                $"The intakes at {time} were marked as taken. Save all entries for {date} now?"),
            PrimaryButtonText = N("Tag speichern", "Save day"),
            CloseButtonText = N("Später", "Later"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };
        TakeSelectedTimeButton.IsEnabled = false;
        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                SaveIntakes_Click(sender, e);
        }
        finally { UpdateTakeSelectedTimeButton(); }
    }

    private void SortIntakeRows()
    {
        var (column, descending) = tableSort["intakes"];
        string Key(IntakeRow row) => column switch
        {
            0 => row.Plan.Time, 1 => row.Plan.Name, 2 => row.Plan.Dose,
            3 => Numeric(ParseNumber(row.Plan.Quantity)), 4 => row.Plan.Form,
            5 => IntakeStatusLabel(row.Status.SelectedIndex), 6 => row.Dose.Text,
            _ => Numeric(ParseNumber(row.Quantity.Text))
        };
        var filtered = intakeRows.Where(row => MatchesTableFilters("intakes", row.Plan.Time,
            row.Plan.Name, row.Plan.Dose, row.Plan.Quantity, row.Plan.Form,
            IntakeStatusLabel(row.Status.SelectedIndex), row.Dose.Text, row.Quantity.Text));
        var ordered = (descending ? filtered.OrderByDescending(Key, StringComparer.CurrentCultureIgnoreCase)
            : filtered.OrderBy(Key, StringComparer.CurrentCultureIgnoreCase)).ToArray();
        IntakeRows.Children.Clear();
        foreach (var row in ordered) IntakeRows.Children.Add(row.Visual);
    }

    private void AllTaken_Click(object sender, RoutedEventArgs e)
    { foreach (var row in intakeRows) row.Status.SelectedIndex = 1; }

    private void AllPending_Click(object sender, RoutedEventArgs e)
    { foreach (var row in intakeRows) row.Status.SelectedIndex = 0; }

    private void SaveIntakes_Click(object sender, RoutedEventArgs e)
    {
        if (!TryIntakeDay(out var day)) return;
        var key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var replacements = new List<IntakeRecord>();
        foreach (var row in intakeRows)
        {
            var status = IntakeStatusLabel(row.Status.SelectedIndex);
            if (status == "Offen") continue;
            var quantity = row.Quantity.Text.Trim();
            if (status == "Genommen" && (row.Dose.Text.Trim().Length == 0 ||
                !double.TryParse(quantity, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out var count) || count <= 0))
            { IntakeStatus.Text = "Bei 'Genommen' bitte tatsächliche Dosis und positive Anzahl eintragen."; return; }
            var plan = row.Plan;
            replacements.Add(new IntakeRecord { PlanId = plan.Id, ProductId = plan.ProductId, Goals = plan.Goals,
                Day = key, Time = plan.Time,
                Name = plan.Name, Quantity = plan.Quantity, Form = plan.Form, Dose = plan.Dose,
                Status = status, ActualDose = status == "Genommen" ? row.Dose.Text.Trim() : "",
                ActualQuantity = status == "Genommen" ? quantity : "" });
        }
        try
        {
            var previous = store.All().Where(item => item.Kind == "Einnahme" && item.Start.Date == day.Date).ToList();
            var written = replacements.Select(item => new Entry { Kind = "Einnahme",
                Start = day.Date.Add(TimeSpan.ParseExact(item.Time, "hh\\:mm", CultureInfo.InvariantCulture)),
                Data = JsonSerializer.Serialize(new PaceAtlas.IntakeData
                {
                    PlanId = item.PlanId, ProductId = item.ProductId, Name = item.Name,
                    PlannedDose = item.Dose, Quantity = item.Quantity, Form = item.Form,
                    Goals = item.Goals, ActualDose = item.ActualDose,
                    ActualQuantity = item.ActualQuantity,
                    Status = item.Status == "Genommen" ? "taken" : "skipped"
                }) }).ToList();
            store.SaveIntakes(day.Date, previous, written);
            ReloadMedicationData(); RenderIntakes(); RenderStock(); LoadEntries();
            IntakeStatus.Text = "Einnahmen gespeichert.";
        }
        catch (Exception ex) { IntakeStatus.Text = "Speichern fehlgeschlagen: " + ex.Message; }
    }

    private static string ProductKey(string name, string form) => name.Trim().ToUpperInvariant() + "\u001F" + form.Trim().ToUpperInvariant();

    private void RenderStock()
    {
        long selected = StockList.SelectedIndex >= 0 && StockList.SelectedIndex < displayedProducts.Count
            ? displayedProducts[StockList.SelectedIndex].Id : 0;
        var (column, descending) = tableSort["stock"];
        string Key(StockProduct product) => column switch
        {
            0 => product.Name, 1 => product.Form, 2 => product.Manufacturer, 3 => product.Supplier,
            4 => Numeric(product.PackUnits), 5 => Numeric(product.PackPrice),
            6 => Numeric(product.Current), 7 => Numeric(product.WeeklyNeed),
            _ => !product.StockKnown ? "Bestand unbekannt" : product.Current <= product.WeeklyNeed ? "Nachkauf prüfen" : ""
        };
        renderingStock = true;
        displayedProducts.Clear();
        var filtered = products.Where(product => MatchesTableFilters("stock", product.Name, product.Form,
            product.Manufacturer, product.Supplier, product.PackUnits.ToString("G"), product.PackPrice.ToString("N2"),
            product.StockKnown ? product.Current.ToString("G") : T("nicht erfasst"),
            product.WeeklyNeed.ToString("G"), !product.StockKnown ? T("Bestand unbekannt") :
            product.Current <= product.WeeklyNeed ? T("Nachkauf prüfen") : ""));
        displayedProducts.AddRange(descending ? filtered.OrderByDescending(Key, StringComparer.CurrentCultureIgnoreCase)
            : filtered.OrderBy(Key, StringComparer.CurrentCultureIgnoreCase));
        StockList.ItemsSource = displayedProducts.Select(product =>
        {
            var note = product.StockKnown && product.WeeklyNeed > 0 && product.Current <= product.WeeklyNeed
                ? "Nachkauf prüfen" : product.StockKnown ? "" : "Bestand unbekannt";
            return TableRow([ (product.Name, 170), (product.Form, 100), (product.Manufacturer, 145),
                (product.Supplier, 145), (product.PackUnits.ToString("G"), 95),
                (product.PackPrice.ToString("N2"), 95),
                (product.StockKnown ? product.Current.ToString("G") : T("nicht erfasst"), 95),
                (product.WeeklyNeed.ToString("G"), 90), (note, 170) ], table: "stock");
        }).ToArray();
        ConfigureListFeedback(StockList);
        var weeklyCost = products.Where(product => product.PackUnits > 0)
            .Sum(product => product.WeeklyNeed / product.PackUnits * product.PackPrice);
        var lowCount = products.Count(product => product.StockKnown && product.WeeklyNeed > 0 &&
            product.Current <= product.WeeklyNeed);
        StockSummary.Text = selectedLanguage == "en"
            ? $"Recorded purchases: {store.TotalPurchases():N2} € · Estimated weekly demand: {weeklyCost:N2} € (with saved package prices).\n" +
              (lowCount == 0 ? "No recorded stock below weekly demand." :
                  $"Stock warning: {lowCount} product(s) have at most seven days remaining.")
            : $"Dokumentierte Nachkäufe: {store.TotalPurchases():N2} € · " +
              $"Geschätzter Wochenbedarf: {weeklyCost:N2} € (mit hinterlegten Packungspreisen).\n" +
              (lowCount == 0 ? "Keine erfassten Vorräte unter dem Wochenbedarf." :
                  $"Vorratswarnung: {lowCount} Präparat(e) reichen höchstens noch für sieben Tage.");
        if (selected != 0)
        {
            int index = displayedProducts.FindIndex(product => product.Id == selected);
            if (index >= 0) StockList.SelectedIndex = index;
        }
        renderingStock = false;
        if (!products.Any(product => product.Id == editingStockProductId)) editingStockProductId = 0;
        UpdateStockEditingBanner();
    }

    private static double ParseNumber(string value) =>
        double.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out var number)
            ? number : 0;
    private static string Numeric(double value) => value.ToString("+000000000000.000;-000000000000.000", CultureInfo.InvariantCulture);

    private StockProduct? SelectedStockProduct() => StockList.SelectedIndex >= 0 &&
        StockList.SelectedIndex < displayedProducts.Count ? displayedProducts[StockList.SelectedIndex] : null;

    private void StockList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateListFeedback(StockList);
        if (renderingStock) return;
        var product = SelectedStockProduct();
        if (product?.Id != editingStockProductId)
        {
            editingStockProductId = 0;
            StockManufacturer.Text = StockSupplier.Text = "";
            StockPackUnits.Value = StockPackPrice.Value = double.NaN;
        }
        UpdateStockEditingBanner();
    }

    private void StockList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var product = SelectedStockProduct();
        if (product is null) return;
        editingStockProductId = product.Id;
        StockManufacturer.Text = product.Manufacturer;
        StockSupplier.Text = product.Supplier;
        StockPackUnits.Value = product.PackUnits;
        StockPackPrice.Value = product.PackPrice;
        StockCounted.Value = product.Current;
        UpdateStockEditingBanner();
    }

    private void UpdateStockEditingBanner()
    {
        if (StockEditingBanner is null || StockEditingText is null) return;
        var product = products.FirstOrDefault(item => item.Id == editingStockProductId);
        StockEditingBanner.Visibility = product is null ? Visibility.Collapsed : Visibility.Visible;
        if (product is null) return;
        StockEditingText.Text = selectedLanguage == "en"
            ? $"You are editing {product.Name}. Saving package details updates this product."
            : $"Du bearbeitest {product.Name}. Das Speichern der Packungsdaten ändert dieses Präparat.";
    }

    private void RefreshProducts()
    {
        products.Clear();
        products.AddRange(store.ProductStocks().Select(stock => new StockProduct
        {
            Id = stock.Product.Id, Name = stock.Product.Name, Form = stock.Product.Form,
            Manufacturer = stock.Product.Manufacturer, Supplier = stock.Product.Supplier,
            PackUnits = (double)stock.Product.PackUnits, PackPrice = (double)stock.Product.PackPrice,
            Current = (double)stock.Current, WeeklyNeed = (double)stock.WeeklyNeed,
            StockKnown = stock.StockKnown
        }));
        RenderStock();
        UpdateStockSuggestions(StockManufacturer, products.Select(p => p.Manufacturer));
        UpdateStockSuggestions(StockSupplier, products.Select(p => p.Supplier));
    }

    private void StockManufacturer_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            UpdateStockSuggestions(sender, products.Select(p => p.Manufacturer));
    }

    private void StockSupplier_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            UpdateStockSuggestions(sender, products.Select(p => p.Supplier));
    }

    private static void UpdateStockSuggestions(AutoSuggestBox box, IEnumerable<string> previous)
    {
        var query = box.Text.Trim();
        box.ItemsSource = previous.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()).Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Where(value => query.Length == 0 || value.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private void SaveStockProduct_Click(object sender, RoutedEventArgs e)
    {
        var product = products.FirstOrDefault(item => item.Id == editingStockProductId);
        if (product is null) { StockStatus.Text = selectedLanguage == "en" ? "Double-click a product to edit it." : "Bitte ein Präparat zur Bearbeitung doppelt anklicken."; return; }
        if (double.IsNaN(StockPackUnits.Value) || double.IsNaN(StockPackPrice.Value) ||
            StockPackUnits.Value < 0 || StockPackPrice.Value < 0)
        { StockStatus.Text = "Bitte gültige Packungsdaten angeben."; return; }
        try
        {
            store.SaveProductDetails(new PaceAtlas.MedicationProduct { Id = product.Id, Name = product.Name,
                Form = product.Form, Manufacturer = StockManufacturer.Text.Trim(), Supplier = StockSupplier.Text.Trim(),
                PackUnits = (decimal)StockPackUnits.Value, PackPrice = (decimal)StockPackPrice.Value });
            RefreshProducts();
            editingStockProductId = 0;
            StockList.SelectedIndex = -1;
            StockManufacturer.Text = StockSupplier.Text = "";
            StockPackUnits.Value = StockPackPrice.Value = double.NaN;
            StockCounted.Value = double.NaN;
            UpdateStockEditingBanner();
            StockStatus.Text = "Packungsdaten gespeichert.";
        }
        catch (Exception ex) { StockStatus.Text = "Speichern fehlgeschlagen: " + ex.Message; }
    }

    private void RecordPurchase_Click(object sender, RoutedEventArgs e)
    {
        var product = SelectedStockProduct();
        if (product is null || product.PackUnits <= 0 || double.IsNaN(StockPurchasePacks.Value) || StockPurchasePacks.Value <= 0)
        { StockStatus.Text = "Bitte Präparat, Packungsinhalt und Anzahl der Packungen angeben."; return; }
        try
        {
            store.RecordPurchase(new PaceAtlas.MedicationProduct { Id = product.Id, PackUnits = (decimal)product.PackUnits,
                PackPrice = (decimal)product.PackPrice }, (decimal)StockPurchasePacks.Value);
            RefreshProducts(); StockStatus.Text = "Nachkauf erfasst.";
        }
        catch (Exception ex) { StockStatus.Text = "Nachkauf fehlgeschlagen: " + ex.Message; }
    }

    private void SetStock_Click(object sender, RoutedEventArgs e)
    {
        var product = SelectedStockProduct();
        if (product is null || double.IsNaN(StockCounted.Value) || StockCounted.Value < 0)
        { StockStatus.Text = "Bitte Präparat und gezählten Bestand angeben."; return; }
        try { store.SetStock(product.Id, (decimal)StockCounted.Value); RefreshProducts(); StockStatus.Text = "Bestand gesetzt."; }
        catch (Exception ex) { StockStatus.Text = "Bestand konnte nicht gesetzt werden: " + ex.Message; }
    }

    private void InitializeIntervals()
    {
        IntervalKind.ItemsSource = new[] { "Aktivität", "Ruhe" };
        IntervalIntensity.ItemsSource = new[] { "gering", "mittel", "hoch", "sehr hoch" };
        PopulateRecoveryChoices();
        IntervalIntensity.SelectedIndex = 1;
        SleepRecovery.SelectedIndex = 0;
        var now = DateTime.Now;
        IntervalStartDate.Date = IntervalEndDate.Date = new DateTimeOffset(now);
        IntervalStartTime.Text = IntervalEndTime.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        IntervalKind.SelectedIndex = 0;
    }

    private void IntervalKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IntervalDimensions is null || IntervalIntensity is null || SleepRecovery is null) return;
        var selected = IntervalDimensions.Items.OfType<CheckBox>()
            .Where(check => check.IsChecked == true)
            .Select(check => check.Tag?.ToString() ?? check.Content?.ToString())
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        // ItemsSource must not point to a List that is cleared while its old checkboxes
        // are still displayed. Otherwise the visible selection can differ from this list.
        IntervalDimensions.ItemsSource = null;
        intervalDimensionChecks.Clear();
        bool rest = IntervalKind.SelectedIndex == 1;
        ActivityTemplatePanel.Visibility = rest ? Visibility.Collapsed : Visibility.Visible;
        IntervalIntensity.Visibility = rest ? Visibility.Collapsed : Visibility.Visible;
        SleepRecovery.Visibility = rest ? Visibility.Visible : Visibility.Collapsed;
        IntervalDimensionsTitle.Text = IntervalKind.SelectedIndex == 1
            ? T("Ruheformen (Mehrfachauswahl)") : T("Belastungsarten");
        foreach (var name in (rest ? choices.Rests : choices.Activities)
            .OrderBy(n => n, StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true)))
            intervalDimensionChecks.Add(new CheckBox { Content = T(name), Tag = name, IsChecked = selected.Contains(name) });
        IntervalDimensions.ItemsSource = intervalDimensionChecks.ToArray();
    }

    private void IntervalRunning_Changed(object sender, RoutedEventArgs e)
    {
        if (IntervalEndDate is null || IntervalEndTime is null) return;
        IntervalEndDate.IsEnabled = IntervalEndTime.IsEnabled = IntervalRunning.IsChecked != true;
    }

    private void SaveInterval_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCalendarTime(IntervalStartDate, IntervalStartTime, out var start))
        {
            IntervalStatus.Text = "Bitte Beginn als TT.MM.JJJJ und HH:mm eingeben.";
            return;
        }
        bool running = IntervalRunning.IsChecked == true;
        DateTime end = default;
        if (!running && !TryCalendarTime(IntervalEndDate, IntervalEndTime, out end))
        {
            IntervalStatus.Text = "Bitte Ende als TT.MM.JJJJ und HH:mm eingeben.";
            return;
        }
        if (running && start > DateTime.Now)
        {
            IntervalStatus.Text = "Der Beginn eines laufenden Zeitraums darf nicht in der Zukunft liegen.";
            return;
        }
        if (!running && end <= start)
        {
            IntervalStatus.Text = "Das Ende muss nach dem Beginn liegen.";
            return;
        }
        string kind = IntervalKind.SelectedIndex == 1 ? "Ruhe" : "Aktivität";
        if (editingEntryId != 0 && (editingEntryKind is "Aktivität" or "Ruhe" or "Schlaf") && editingEntryKind != kind &&
            !(editingEntryKind == "Schlaf" && kind == "Ruhe"))
        {
            IntervalStatus.Text = selectedLanguage == "en"
                ? "You are editing a different interval type. Choose the original type or press New entry."
                : "Du bearbeitest einen anderen Zeitraumtyp. Wähle den ursprünglichen Typ oder drücke „Neuer Eintrag“.";
            return;
        }
        var protection = protectionChecks
            .Where(check => check.IsChecked == true).Select(check => check.Tag?.ToString() ?? check.Content.ToString()!).ToList();
        if (editingEntryId != 0 && (editingEntryKind == kind || editingEntryKind == "Schlaf" && kind == "Ruhe") &&
            entries.FirstOrDefault(e => e.Id == editingEntryId) is { } previousInterval)
        {
            var oldProtection = previousInterval.Kind == "Schlaf"
                ? (JsonSerializer.Deserialize<SleepData>(previousInterval.Data)?.HearingProtection ?? [])
                : (JsonSerializer.Deserialize<IntervalData>(previousInterval.Data)?.HearingProtection ?? []);
            foreach (var oldName in oldProtection)
                if (!choices.HearingProtection.Contains(CanonicalProtection(oldName), StringComparer.OrdinalIgnoreCase) &&
                    !protection.Contains(oldName, StringComparer.OrdinalIgnoreCase)) protection.Add(oldName);
        }
        var dimensions = IntervalDimensions.Items.OfType<CheckBox>().Where(check => check.IsChecked == true)
            .Select(check => check.Tag?.ToString() ?? check.Content.ToString()!).ToList();
        if (dimensions.Count == 0)
        {
            IntervalStatus.Text = "Bitte mindestens eine Belastungsart oder Ruheform wählen.";
            return;
        }
        var data = JsonSerializer.Serialize(new IntervalData { Dimensions = dimensions,
            Intensity = IntervalIntensity.SelectedIndex + 1, HearingProtection = protection,
            Recovery = kind == "Ruhe" ? SleepRecovery.SelectedIndex : 0 });
        try
        {
            store.Save(new Entry { Id = editingEntryKind == kind || editingEntryKind == "Schlaf" && kind == "Ruhe" ? editingEntryId : 0,
                Kind = kind, Start = start, End = running ? null : end,
                Data = data, Note = IntervalNote.Text });
            ResetInterval_Click(sender, e);
            LoadEntries();
            IntervalStatus.Text = "Zeitraum gespeichert. Neuer Zeitraum vorbereitet.";
        }
        catch (Exception ex)
        {
            IntervalStatus.Text = "Speichern fehlgeschlagen: " + ex.Message;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private void SetInitialWindowSize()
    {
        // Size the native window before App.OnLaunched calls Activate, so its first frame
        // is already at the requested size. AppWindow.Resize expects physical pixels.
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        uint dpi = GetDpiForWindow(hwnd);
        double scale = (dpi == 0 ? 96 : dpi) / 96.0;
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        int width = Math.Min((int)Math.Round(1500 * scale), (int)(workArea.Width * 0.95));
        int height = Math.Min((int)Math.Round(940 * scale), (int)(workArea.Height * 0.95));
        AppWindow.Resize(new SizeInt32(Math.Max(1, width), Math.Max(1, height)));
    }

    private void BuildFields()
    {
        var previousSymptoms = symptoms.ToDictionary(pair => pair.Key, pair => pair.Value.SelectedIndex);
        var previousPain = painChecks.Where(pair => pair.Value.IsChecked == true)
            .Select(pair => pair.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        symptoms.Clear(); painChecks.Clear();
        SymptomGrid.Children.Clear(); PainGrid.Children.Clear();
        for (int i = 0; i < SymptomNames.Length; i++)
        {
            var name = SymptomNames[i];
            var choice = new ComboBox { ItemsSource = Severities, SelectedIndex = previousSymptoms.GetValueOrDefault(name, 0),
                Width = 192, Height = 34, VerticalAlignment = VerticalAlignment.Center };
            var field = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Height = 38 };
            var label = new TextBlock { Text = name == "Geräuschempfindlichkeit" ? "Geräuscheempf." : name,
                Width = 110, FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center };
            field.Children.Add(label);
            field.Children.Add(choice);
            SymptomGrid.Children.Add(field);
            symptoms.Add(name, choice);
            choice.SelectionChanged += (_, _) => RefreshLimitingSymptomOptions();
        }
        RefreshLimitingSymptomOptions();

        for (int i = 0; i < PainNames.Length; i++)
        {
            var check = new CheckBox { Content = T(PainNames[i]), Tag = PainNames[i], IsChecked = previousPain.Contains(PainNames[i]) };
            PainGrid.Children.Add(check);
            painChecks.Add(PainNames[i], check);
        }
        fieldColumns = 0;
        painColumns = 0;
        ArrangeFields(SymptomGrid.ActualWidth switch
        {
            < 640 => 1,
            < 960 => 2,
            < 1280 => 3,
            < 1600 => 4,
            _ => 5
        });
        ArrangePainFields(PainGrid.ActualWidth switch
        {
            < 360 => 2,
            < 600 => 4,
            < 900 => 6,
            < 1200 => 8,
            _ => 10
        });
    }

    private void RefreshLimitingSymptomOptions(string? selected = null)
    {
        if (MostLimitingSymptom is null) return;
        selected ??= (MostLimitingSymptom.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var names = symptoms.Where(pair => pair.Value.SelectedIndex > 1)
            .Select(pair => pair.Key).OrderBy(name => T(name), StringComparer.CurrentCultureIgnoreCase).ToArray();
        MostLimitingSymptom.Items.Clear();
        MostLimitingSymptom.Items.Add(new ComboBoxItem { Content = T("Keine Angabe"), Tag = "" });
        foreach (var name in names)
            MostLimitingSymptom.Items.Add(new ComboBoxItem { Content = T(name), Tag = name });
        MostLimitingSymptom.SelectedItem = MostLimitingSymptom.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == selected) ?? MostLimitingSymptom.Items[0];
    }

    private void Fields_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Die Anordnung ändert sich nur beim Überschreiten eines Breitenbereichs.
        // Während des Ziehens bleiben die Steuerelemente sonst unangetastet.
        int columns = e.NewSize.Width switch
        {
            < 640 => 1,
            < 960 => 2,
            < 1280 => 3,
            < 1600 => 4,
            _ => 5
        };
        ArrangeFields(columns);
    }

    private void PainGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        int columns = e.NewSize.Width switch
        {
            < 360 => 2,
            < 600 => 4,
            < 900 => 6,
            < 1200 => 8,
            _ => 10
        };
        ArrangePainFields(columns);
    }

    private void ArrangePainFields(int columns)
    {
        if (painColumns == columns) return;
        painColumns = columns;
        PainGrid.ColumnDefinitions.Clear();
        PainGrid.RowDefinitions.Clear();
        for (int i = 0; i < columns; i++)
            PainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < (PainNames.Length + columns - 1) / columns; i++)
            PainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < PainGrid.Children.Count; i++)
        {
            if (PainGrid.Children[i] is FrameworkElement child)
            {
                Grid.SetRow(child, i / columns);
                Grid.SetColumn(child, i % columns);
            }
        }
    }

    private void ArrangeFields(int columns)
    {
        if (fieldColumns == columns) return;
        fieldColumns = columns;
        var grid = SymptomGrid;
        int count = SymptomNames.Length;
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (int column = 0; column < columns; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int row = 0; row < (count + columns - 1) / columns; row++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < grid.Children.Count; i++)
        {
            if (grid.Children[i] is FrameworkElement child)
            {
                Grid.SetRow(child, i / columns);
                Grid.SetColumn(child, i % columns);
            }
        }
    }

    private void ResetForm()
    {
        var now = DateTime.Now;
        StateDate.Date = new DateTimeOffset(now);
        StateTime.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        Overall.SelectedIndex = 2;
        Pem.SelectedIndex = 0;
        Crash.IsChecked = false;
        Pulse.Value = 0;
        Note.Text = "";
        foreach (var field in symptoms.Values) field.SelectedIndex = 0;
        RefreshLimitingSymptomOptions("");
        foreach (var check in painChecks.Values) check.IsChecked = false;
    }

    private void MarkNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var field in symptoms.Values) field.SelectedIndex = 1;
    }

    private void MarkUnassessed_Click(object sender, RoutedEventArgs e)
    {
        foreach (var field in symptoms.Values) field.SelectedIndex = 0;
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ClearEditing();
        ResetForm();
        Status.Text = "Neuer Eintrag vorbereitet.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!TryCalendarTime(StateDate, StateTime, out var at))
            {
                Status.Text = "Bitte Zeit als TT.MM.JJJJ und HH:mm eingeben.";
                return;
            }
            int? pulse = !double.IsNaN(Pulse.Value) && Pulse.Value > 0
                ? (int)Math.Clamp(Pulse.Value, 1, 250) : null;
            var entry = ConditionEntryFactory.Create(at, Overall.SelectedIndex, Pem.SelectedIndex,
                Crash.IsChecked == true, pulse,
                symptoms.ToDictionary(pair => pair.Key, pair => pair.Value.SelectedIndex - 1),
                painChecks.Where(pair => pair.Value.IsChecked == true).Select(pair => pair.Key), Note.Text);
            entry.Id = editingEntryKind == "Zustand" ? editingEntryId : 0;
            var newData = JsonSerializer.Deserialize<StateData>(entry.Data) ?? new();
            var limiting = (MostLimitingSymptom.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            newData.MostLimitingSymptom = limiting is not null &&
                newData.SymptomSeverity(limiting) > 0 ? limiting : null;
            if (entry.Id != 0 && entries.FirstOrDefault(e => e.Id == entry.Id) is { } original)
            {
                var oldData = JsonSerializer.Deserialize<StateData>(original.Data) ?? new();
                foreach (var name in oldData.Symptoms.Keys)
                    if (!newData.Symptoms.ContainsKey(name)) newData.Symptoms[name] = oldData.SymptomSeverity(name);
                foreach (var location in oldData.PainLocations)
                    if (!painChecks.ContainsKey(location) && !newData.PainLocations.Contains(location))
                        newData.PainLocations.Add(location);
            }
            entry.Data = JsonSerializer.Serialize(newData);
            store.Save(entry);
            ClearEditing();
            ResetForm();
            LoadEntries();
            Status.Text = "Eintrag gespeichert. Neuer Eintrag vorbereitet.";
        }
        catch (Exception ex)
        {
            Status.Text = "Speichern fehlgeschlagen: " + ex.Message;
        }
    }

    private void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateListFeedback(EntryList);
        UpdateEndAction();
    }

    private void UpdateEndAction()
    {
        if (EndRunningButton is null) return;
        var selected = SelectedEntry();
        var measure = selected is { Kind: "Maßnahme" } && IsRunningEntry(selected);
        EndRunningButton.IsEnabled = selected is not null && IsRunningEntry(selected);
        var caption = measure ? "Laufende Maßnahme abschließen" : "Laufenden Zeitraum jetzt beenden";
        originalUiLabels[EndRunningButton] = caption;
        EndRunningButton.Content = T(caption);
        if (EndEntryMenu is not null) EndEntryMenu.Text = T(caption);
    }

    private void ClearEditing() { editingEntryId = 0; editingEntryKind = ""; UpdateEditingIndicators(); }

    private Entry? SelectedEntry() => EntryList.SelectedIndex is var index &&
        index >= 0 && index < displayedEntries.Count ? displayedEntries[index] : null;

    private bool SelectEntryAt(DependencyObject? source)
    {
        var node = source;
        while (node is not null && node is not ListViewItem)
            node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
        if (node is not ListViewItem item) return false;
        EntryList.SelectedItem = item.Content;
        return true;
    }

    private void EntryList_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        SelectEntryAt(e.OriginalSource as DependencyObject);
    }

    private void EntryList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (!SelectEntryAt(e.OriginalSource as DependencyObject)) return;
        EditEntry_Click(sender, null!);
        e.Handled = true;
    }

    private void EntryMenu_Opening(object sender, object e)
    {
        var entry = SelectedEntry();
        EditEntryMenu.IsEnabled = entry is not null;
        DeleteEntryMenu.IsEnabled = entry is not null;
        EndEntryMenu.IsEnabled = entry is not null && IsRunningEntry(entry);
        UpdateEndAction();
    }

    private void EditEntry_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntry();
        if (entry is not null) EditEntry(entry);
    }

    private void EditEntry(Entry entry)
    {
        if (entry.Kind == "Mahlzeit")
        {
            var meal = nutritionMeals.FirstOrDefault(item => item.Id == -entry.Id);
            if (meal is not null) EditMeal(meal);
            return;
        }
        SetEntriesExpanded(false);
        if (entry.Kind == "Einnahme")
        {
            ClearEditing();
            MainTabs.SelectedItem = MedicationTab;
            MedicationTabs.SelectedItem = IntakeTab;
            IntakeDate.Date = new DateTimeOffset(entry.Start);
            RenderIntakes();
            return;
        }
        editingEntryId = entry.Id;
        editingEntryKind = entry.Kind;
        UpdateEditingIndicators();
        var culture = CultureInfo.GetCultureInfo("de-DE");
        if (entry.Kind == "Zustand")
        {
            MainTabs.SelectedItem = StateTab;
            var data = JsonSerializer.Deserialize<StateData>(entry.Data) ?? new();
            StateDate.Date = new DateTimeOffset(entry.Start);
            StateTime.Text = entry.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
            Overall.SelectedIndex = Math.Clamp(data.Overall, 0, 4);
            Pem.SelectedIndex = Math.Clamp(data.Pem, 0, 2);
            Crash.IsChecked = data.Crash;
            Pulse.Value = data.Pulse ?? 0;
            Note.Text = entry.Note;
            foreach (var (name, field) in symptoms)
                field.SelectedIndex = Math.Clamp(data.SymptomSeverity(name) + 1, 0, Severities.Length - 1);
            RefreshLimitingSymptomOptions(data.MostLimitingSymptom ?? "");
            foreach (var (name, check) in painChecks)
                check.IsChecked = data.PainLocations.Contains(name);
        }
        else if (entry.Kind == "Maßnahme")
        {
            MainTabs.SelectedItem = MeasureTab;
            var data = JsonSerializer.Deserialize<PaceAtlas.MeasureData>(entry.Data) ?? new();
            MeasureDate.Date = new DateTimeOffset(entry.Start);
            MeasureTime.Text = entry.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (!choices.Measures.Contains(data.Name, StringComparer.OrdinalIgnoreCase))
            {
                choices.Measures.Add(data.Name);
                MeasureName.ItemsSource = choices.Measures.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();
            }
            MeasureName.SelectedItem = data.Name;
            MeasureDose.Text = data.Dose;
            MeasureNote.Text = entry.Note;
            RenderMeasureReasons(data.ReasonSymptoms ?? []);
            MeasureReasonOther.Text = data.ReasonOther ?? "";
            MeasureTrack.IsChecked = data.TrackProgress;
            MeasureGoal.Text = data.Goal;
            MeasureBaseline.Text = data.Baseline;
            MeasureReviewDate.Date = data.ReviewDate is { } review ? new DateTimeOffset(review) : null;
            MeasureReviewTime.Text = data.ReviewDate?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "";
            SelectMeasureValue(MeasureProgressStatus, data.Status);
            SelectMeasureValue(MeasureOutcome, data.Outcome);
            MeasureReviewNote.Text = data.ReviewNote;
        }
        else if (entry.Kind is "Aktivität" or "Ruhe" or "Schlaf")
        {
            MainTabs.SelectedItem = IntervalTab;
            IntervalKind.SelectedIndex = entry.Kind is "Schlaf" or "Ruhe" ? 1 : 0;
            IntervalStartDate.Date = new DateTimeOffset(entry.Start);
            IntervalStartTime.Text = entry.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
            var end = entry.End ?? DateTime.Now.AddMinutes(1);
            IntervalEndDate.Date = new DateTimeOffset(end);
            IntervalEndTime.Text = end.ToString("HH:mm", CultureInfo.InvariantCulture);
            IntervalRunning.IsChecked = entry.End is null;
            IntervalNote.Text = entry.Note;
            if (entry.Kind == "Schlaf")
            {
                var data = JsonSerializer.Deserialize<SleepData>(entry.Data) ?? new();
                SleepRecovery.SelectedIndex = Math.Clamp(data.Recovery, 0, 4);
                if (!choices.Rests.Contains("Geschlafen", StringComparer.OrdinalIgnoreCase)) choices.Rests.Add("Geschlafen");
                IntervalKind_SelectionChanged(IntervalKind, null!);
                foreach (var check in intervalDimensionChecks) check.IsChecked = check.Tag?.ToString() == "Geschlafen";
                SetHearingProtection(data.HearingProtection);
            }
            else
            {
                var data = JsonSerializer.Deserialize<IntervalData>(entry.Data) ?? new();
                IntervalIntensity.SelectedIndex = Math.Clamp(data.Intensity - 1, 0, 3);
                SleepRecovery.SelectedIndex = Math.Clamp(data.Recovery, 0, 4);
                foreach (var name in data.Dimensions)
                {
                    var list = entry.Kind == "Ruhe" ? choices.Rests : choices.Activities;
                    if (!list.Contains(name, StringComparer.OrdinalIgnoreCase)) list.Add(name);
                }
                IntervalKind_SelectionChanged(IntervalKind, null!);
                foreach (var check in intervalDimensionChecks)
                    check.IsChecked = data.Dimensions.Contains(check.Tag?.ToString(), StringComparer.OrdinalIgnoreCase);
                SetHearingProtection(data.HearingProtection);
            }
        }
        DispatcherQueue.TryEnqueue(() =>
        {
            if (entry.Kind == "Zustand") { StateTime.Focus(FocusState.Programmatic); FormScroller.ChangeView(null, 0, null); }
            else if (entry.Kind == "Maßnahme") { MeasureTime.Focus(FocusState.Programmatic); MeasureFormScroller.ChangeView(null, 0, null); }
            else if (entry.Kind is "Aktivität" or "Ruhe" or "Schlaf")
            { IntervalStartTime.Focus(FocusState.Programmatic); IntervalFormScroller.ChangeView(null, 0, null); }
        });
    }

    private void SetHearingProtection(IEnumerable<string>? protection)
    {
        var set = (protection ?? []).Select(CanonicalProtection).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var check in protectionChecks)
            check.IsChecked = set.Contains(check.Tag?.ToString() ?? "");
    }

    private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntry();
        if (entry is null) return;
        var dialog = new ContentDialog { Title = "Eintrag löschen?",
            Content = "Dieser Eintrag wird dauerhaft gelöscht.", PrimaryButtonText = "Löschen",
            CloseButtonText = "Abbrechen", XamlRoot = (Content as FrameworkElement)?.XamlRoot };
        if (await ShowTranslatedDialogAsync(dialog) != ContentDialogResult.Primary) return;
        try
        {
            if (entry.Kind == "Mahlzeit")
            {
                store.DeleteMeal(-entry.Id);
                if (editingMealId == -entry.Id) ResetMeal();
                ReloadMealData();
                return;
            }
            store.Delete(entry.Id);
            if (editingEntryId == entry.Id) ClearEditing();
            ReloadMedicationData(); RenderIntakes(); RenderStock(); LoadEntries();
        }
        catch (Exception ex) { Status.Text = "Löschen fehlgeschlagen: " + ex.Message; }
    }

    private async Task<string?> PickFileAsync(bool save, string extension, string suggestedName)
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (save)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker { SuggestedFileName = suggestedName };
            picker.FileTypeChoices.Add(extension == ".csv" ? "CSV-Datei" : "SQLite-Backup", [extension]);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, handle);
            return (await picker.PickSaveFileAsync())?.Path;
        }
        else
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(extension);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, handle);
            return (await picker.PickSingleFileAsync())?.Path;
        }
    }

    private async void ExportEntries_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await PickFileAsync(true, ".csv", "PaceAtlas-Export");
            if (path is not null) { store.ExportCsv(path); Status.Text = "CSV exportiert."; }
        }
        catch (Exception ex) { Status.Text = "CSV-Export fehlgeschlagen: " + ex.Message; }
    }

    private async void BackupEntries_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await PickFileAsync(true, ".db", "PaceAtlas-Backup-" + DateTime.Now.ToString("yyyyMMdd-HHmm"));
            if (path is not null) { store.Backup(path); Status.Text = "Backup erstellt."; }
        }
        catch (Exception ex) { Status.Text = "Backup fehlgeschlagen: " + ex.Message; }
    }

    private async void RestoreEntries_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await PickFileAsync(false, ".db", "");
            if (path is null) return;
            var dialog = new ContentDialog { Title = "Backup einspielen?",
                Content = "Das Backup ersetzt sämtliche aktuellen Einträge und Medikamentendaten.",
                PrimaryButtonText = "Backup einspielen", CloseButtonText = "Abbrechen",
                XamlRoot = (Content as FrameworkElement)?.XamlRoot };
            if (await ShowTranslatedDialogAsync(dialog) != ContentDialogResult.Primary) return;
            store.Restore(path);
            ClearEditing();
            ReloadMedicationData(); RenderPlans(); RenderIntakes(); RenderStock(); LoadEntries(); ReloadNutrition();
            Status.Text = "Backup eingespielt.";
        }
        catch (Exception ex) { Status.Text = "Wiederherstellen fehlgeschlagen: " + ex.Message; }
    }

    private void EndRunning_Click(object sender, RoutedEventArgs e)
    {
        int index = EntryList.SelectedIndex;
        if (index < 0 || index >= displayedEntries.Count) return;
        var entry = displayedEntries[index];
        if (entry.Kind == "Maßnahme" && IsRunningEntry(entry))
        {
            EditEntry(entry);
            MeasureProgressStatus.SelectedIndex = 2;
            MeasureStatus.Text = "Bitte das beobachtete Ergebnis auswählen und die Maßnahme speichern.";
            return;
        }
        if (entry.End is not null || entry.Kind is not ("Aktivität" or "Ruhe" or "Schlaf")) return;
        var now = DateTime.Now;
        if (now <= entry.Start) { IntervalStatus.Text = "Das Ende muss nach dem Beginn liegen."; return; }
        entry.End = now;
        try
        {
            store.Save(entry);
            LoadEntries();
            IntervalStatus.Text = "Zeitraum beendet.";
        }
        catch (Exception ex)
        {
            entry.End = null;
            IntervalStatus.Text = "Speichern fehlgeschlagen: " + ex.Message;
        }
    }

    private void LoadEntries()
    {
        try
        {
            entries.Clear();
            entries.AddRange(store.All());
            DisplayEntries();
            RenderOngoingMeasures();
            RefreshAnalysis();
            if (FoodAnalysisRows is not null) RenderFoodAnalysis();
        }
        catch (Exception ex)
        {
            Status.Text = "Einträge konnten nicht geladen werden: " + ex.Message;
        }
    }

    private void RefreshTodaySummary()
    {
        if (TodaySummary is null) return;
        var today = DateTime.Today;
        var latest = entries.Where(entry => entry.Kind == "Zustand" && entry.Start.Date == today)
            .OrderByDescending(entry => entry.Start).FirstOrDefault();
        StateData? state = null;
        if (latest is not null)
            try { state = JsonSerializer.Deserialize<StateData>(latest.Data); }
            catch (JsonException) { }
        var running = entries.Count(IsRunningEntry);
        var nextPlan = plans.Where(plan => plan.IsActiveOn(DateOnly.FromDateTime(today)) &&
                TimeOnly.TryParseExact(plan.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) &&
                at >= TimeOnly.FromDateTime(DateTime.Now))
            .OrderBy(plan => plan.Time).FirstOrDefault();
        var condition = state is null ? (selectedLanguage == "en" ? "no condition today" : "heute kein Zustand") :
            (selectedLanguage == "en" ? "overall: " : "Allgemein: ") +
            T(new[] { "gut", "leicht eingeschränkt", "mittel", "schlecht", "sehr schlecht" }[Math.Clamp(state.Overall, 0, 4)]);
        var status = state is null ? "" :
            (state.Pem > 0 ? " · ◆ PEM: " + T(state.Pem == 1 ? "vermutet" : "erkannt") : "") +
            (state.Crash ? " · ✖ Crash" : "");
        var ongoing = selectedLanguage == "en" ? $"◷ {running} ongoing" : $"◷ {running} laufend";
        var next = nextPlan is null ? (selectedLanguage == "en" ? "no further intake scheduled" : "keine weitere Einnahme geplant") :
            (selectedLanguage == "en" ? "next scheduled intake: " : "nächste geplante Einnahme: ") +
            $"{nextPlan.Time} {nextPlan.Name}";
        TodaySummary.Text = (selectedLanguage == "en" ? "Today: " : "Heute: ") + condition + status + "  ·  " + ongoing + "  ·  " + next;
    }

    private void EntryFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (restoringEntryFilters || EntryList is null || HideIntakesFilter is null) return;
        DisplayEntries();
        try
        {
            SaveJson(EntryFiltersPath, new EntryFilterSettings
            {
                HideIntakes = HideIntakesFilter.IsChecked == true
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status.Text = selectedLanguage == "en" ? "Could not save filters: " + ex.Message :
                "Filter konnten nicht gespeichert werden: " + ex.Message;
        }
    }

    private void RestoreEntryFilters()
    {
        restoringEntryFilters = true;
        try
        {
            if (!File.Exists(EntryFiltersPath)) return;
            var saved = JsonSerializer.Deserialize<EntryFilterSettings>(File.ReadAllText(EntryFiltersPath));
            HideIntakesFilter.IsChecked = saved?.HideIntakes == true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Invalid or unavailable preferences leave both filters at their defaults.
        }
        finally { restoringEntryFilters = false; }
    }

    private static bool IsRunningEntry(Entry entry)
    {
        if (entry.Kind is "Aktivität" or "Ruhe" or "Schlaf") return entry.End is null;
        if (entry.Kind != "Maßnahme") return false;
        try
        {
            var measure = JsonSerializer.Deserialize<MeasureData>(entry.Data);
            return measure?.TrackProgress == true && measure.Status == "active";
        }
        catch (JsonException) { return false; }
    }

    private void DisplayEntries()
    {
        long selectedId = SelectedEntry()?.Id ?? 0;
        visibleEntries.Clear();
        displayedEntries.Clear();
        var (column, descending) = tableSort["entries"];
        string SortKey(Entry entry) => column switch
        {
            0 => entry.Start.ToString("O"), 1 => entry.End?.ToString("O") ?? "",
            2 => entry.Kind, 3 => EntryContent(entry), _ => entry.Note
        };
        var source = entries.AsEnumerable().Concat(nutritionMeals
            .Where(meal => meal.Status == "consumed")
            .Select(meal => new Entry { Id = -meal.Id, Kind = "Mahlzeit", Start = meal.At,
                Data = meal.Name, Note = meal.Note }));
        if (HideIntakesFilter.IsChecked == true)
            source = source.Where(entry => entry.Kind != "Einnahme");
        source = source.Where(entry => MatchesTableFilters("entries", entry.Start.ToString("dd.MM.yyyy HH:mm"),
            entry.End?.ToString("dd.MM.yyyy HH:mm") ?? "", T(entry.Kind == "Schlaf" ? "Ruhe" : entry.Kind),
            EntryContent(entry), entry.Note));
        var sorted = descending
            ? source.OrderByDescending(IsRunningEntry).ThenByDescending(SortKey, StringComparer.CurrentCultureIgnoreCase)
            : source.OrderByDescending(IsRunningEntry).ThenBy(SortKey, StringComparer.CurrentCultureIgnoreCase);
        foreach (var entry in sorted)
        {
            if (entry.Kind == "Mahlzeit")
            {
                var meal = nutritionMeals.FirstOrDefault(item => item.Id == -entry.Id);
                if (meal is null) continue;
                displayedEntries.Add(entry);
                visibleEntries.Add(MealEntryRow(entry, meal));
                continue;
            }
            if (entry.Kind == "Einnahme")
            {
                PaceAtlas.IntakeData? intake;
                try { intake = JsonSerializer.Deserialize<PaceAtlas.IntakeData>(entry.Data); }
                catch (JsonException) { intake = null; }
                var statusIndex = Localization.IsTaken(intake?.Status ?? "") ? 1 :
                    Localization.IsSkipped(intake?.Status ?? "") ? 2 : 0;
                var intakeDetails = $"{intake?.Name} {intake?.ActualDose}".Trim();
                displayedEntries.Add(entry);
                visibleEntries.Add(TableRow([(entry.Start.ToString("dd.MM.yyyy HH:mm"), 180), ("", 180),
                    ("Einnahme", 115), (intakeDetails, 400),
                    (entry.Note, 340)], compact: true, table: "entries",
                    badges: [(IntakeStatusIcon(statusIndex), IntakeStatusBrush(statusIndex))],
                    contentDescription: intakeDetails + " · " + T(IntakeStatusLabel(statusIndex)),
                    contentForm: intake?.Form));
                continue;
            }
            if (entry.Kind == "Maßnahme")
            {
                displayedEntries.Add(entry);
                MeasureData? measure;
                try { measure = JsonSerializer.Deserialize<MeasureData>(entry.Data); }
                catch (JsonException) { measure = null; }
                var runningMeasure = measure?.TrackProgress == true && measure.Status == "active";
                var measureContent = MeasureDescription(measure) +
                    (measure?.TrackProgress == true ? " · " + (measure.Status == "completed" ? "abgeschlossen" :
                        measure.Status == "paused" ? "pausiert" : "läuft") +
                        (!string.IsNullOrEmpty(measure.Outcome) ? " · " + MeasureOutcomeLabel(measure.Outcome) : "") : "");
                var measureEnd = runningMeasure ? "läuft" :
                    measure?.TrackProgress == true && measure.Status == "paused" ? "pausiert" : "";
                var measureRow = TableRow([(entry.Start.ToString("dd.MM.yyyy HH:mm"), 180), (measureEnd, 180),
                    ("Maßnahme", 115), (measureContent, 400),
                    (string.Join(" · ", new[] { entry.Note, measure?.ReviewNote }.Where(text => !string.IsNullOrWhiteSpace(text))), 340)],
                    compact: true, table: "entries",
                    badges: runningMeasure ? [("◷", VisualScaleBrush(3))] : null,
                    contentDescription: measureContent);
                if (measure?.TrackProgress == true && measure.Status != "completed")
                    measureRow.Background = new SolidColorBrush(measure.Status == "paused"
                        ? Windows.UI.Color.FromArgb(255, 255, 244, 216)
                        : Windows.UI.Color.FromArgb(255, 226, 244, 232));
                visibleEntries.Add(measureRow);
                continue;
            }
            if (entry.Kind is "Aktivität" or "Ruhe" or "Schlaf")
            {
                displayedEntries.Add(entry);
                string details;
                int intensityLevel = 1;
                int recoveryLevel = -1;
                IReadOnlyList<string> protection = [];
                try
                {
                    if (entry.Kind == "Schlaf")
                    {
                        var sleep = JsonSerializer.Deserialize<SleepData>(entry.Data);
                        protection = sleep?.HearingProtection ?? [];
                        recoveryLevel = Math.Clamp(sleep?.Recovery ?? 0, 0, 4);
                        details = T("Geschlafen");
                    }
                    else
                    {
                        var interval = JsonSerializer.Deserialize<IntervalData>(entry.Data);
                        protection = interval?.HearingProtection ?? [];
                        intensityLevel = Math.Clamp(interval?.Intensity ?? 1, 1, 4);
                        details = string.Join(", ", (interval?.Dimensions ?? []).Select(T));
                        if (entry.Kind == "Ruhe")
                            recoveryLevel = Math.Clamp(interval?.Recovery ?? 0, 0, 4);
                    }
                }
                catch (JsonException) { details = entry.Kind; }
                var intervalRow = TableRow([(entry.Start.ToString("dd.MM.yyyy HH:mm"), 180),
                    (entry.End is DateTime end ? end.ToString("dd.MM.yyyy HH:mm") : "läuft", 180),
                    (entry.Kind == "Schlaf" ? "Ruhe" : entry.Kind, 115), (details, 400), (entry.Note, 340)], compact: true, table: "entries",
                    badges: entry.Kind is "Schlaf" or "Ruhe"
                        ? entry.End is null ? [("◷", VisualScaleBrush(3))] : null
                        : entry.End is null
                            ? [("◷", VisualScaleBrush(3)), (BarScale(intensityLevel), VisualScaleBrush(intensityLevel - 1, true))]
                            : [(BarScale(intensityLevel), VisualScaleBrush(intensityLevel - 1, true))],
                    contentDescription: entry.Kind is "Schlaf" or "Ruhe" ?
                        details + " · " + RecoveryDescription(recoveryLevel) :
                        (details.Length == 0 ? "" : details + " · ") +
                        (selectedLanguage == "en" ? "Intensity: " : "Intensität: ") +
                        T(new[] { "gering", "mittel", "hoch", "sehr hoch" }[intensityLevel - 1]),
                    recoveryLevel: recoveryLevel);
                if (entry.End is null)
                    intervalRow.Background = (Microsoft.UI.Xaml.Media.Brush)
                        ((FrameworkElement)Content).Resources["RunningIntervalBrush"];
                if (protection.Count > 0)
                    AddEntryDetails(intervalRow, T("Schutzmaßnahmen") + ": " +
                        string.Join(" · ", protection.Select(name => T(CanonicalProtection(name)))
                            .Distinct(StringComparer.CurrentCultureIgnoreCase)));
                visibleEntries.Add(intervalRow);
                continue;
            }
            if (entry.Kind != "Zustand") continue;
            displayedEntries.Add(entry);
            var data = JsonSerializer.Deserialize<StateData>(entry.Data);
            var condition = data?.Overall switch
            {
                0 => "gut", 1 => "leicht eingeschränkt", 2 => "mittel",
                3 => "schlecht", 4 => "sehr schlecht", _ => "unbekannt"
            };
            var stateDetails = (selectedLanguage == "en" ? "Overall: " : "Allgemein: ") + T(condition);
            var symptomCount = data is null ? 0 : data.Symptoms.Keys.Count(name => data.SymptomSeverity(name) > 0);
            stateDetails += selectedLanguage == "en"
                ? $" · {symptomCount} {(symptomCount == 1 ? "symptom" : "symptoms")}" 
                : $" · {symptomCount} {(symptomCount == 1 ? "Symptom" : "Symptome")}";
            if (data is { Pem: > 0 })
                stateDetails += " · PEM: " + T(data.Pem == 1 ? "vermutet" : "erkannt");
            if (data?.Crash == true) stateDetails += " · Crash";
            if (data is { Pulse: > 0 })
                stateDetails += " · " + (selectedLanguage == "en" ? "Pulse: " : "Puls: ") + data.Pulse;
            var stateRow = TableRow([(entry.Start.ToString("dd.MM.yyyy HH:mm"), 180), ("", 180),
                ("Zustand", 115), (stateDetails, 400),
                (entry.Note, 340)], compact: true, table: "entries",
                badges: [(CircleScale(Math.Clamp(data?.Overall ?? 0, 0, 4)), VisualScaleBrush(data?.Overall ?? 0)),
                    ..(data is { Pem: > 0 } ? new[] { ("◆ PEM", VisualScaleBrush(2)) } : []),
                    ..(data?.Crash == true ? new[] { ("✖ Crash", VisualScaleBrush(4)) } : [])]);
            var presentSymptoms = data?.Symptoms.Keys
                .Select(name => (Name: name, Severity: data.SymptomSeverity(name)))
                .Where(item => item.Severity > 0)
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => $"{T(item.Name)}: {item.Severity}/4 ({T(Severities[item.Severity + 1])})")
                .ToArray() ?? [];
            AddEntryDetails(stateRow, presentSymptoms.Length == 0
                ? N("Keine Symptome dokumentiert.", "No symptoms recorded.")
                : string.Join(" · ", presentSymptoms));
            visibleEntries.Add(stateRow);
        }
        var selectedIndex = selectedId == 0 ? -1 : displayedEntries.FindIndex(entry => entry.Id == selectedId);
        EntryList.SelectedIndex = selectedIndex;
        ConfigureListFeedback(EntryList);
        UpdateEndAction();
        RefreshTodaySummary();
    }

    private static string EntryContent(Entry entry)
    {
        try
        {
            return entry.Kind switch
            {
                "Maßnahme" => JsonSerializer.Deserialize<MeasureData>(entry.Data)?.Name ?? "",
                "Einnahme" => JsonSerializer.Deserialize<PaceAtlas.IntakeData>(entry.Data)?.Name ?? "",
                "Schlaf" => "Schlaf",
                "Aktivität" or "Ruhe" => string.Join(", ",
                    JsonSerializer.Deserialize<IntervalData>(entry.Data)?.Dimensions ?? []),
                "Zustand" => (JsonSerializer.Deserialize<StateData>(entry.Data)?.Overall ?? -1).ToString(),
                "Mahlzeit" => entry.Data,
                _ => ""
            };
        }
        catch (JsonException) { return ""; }
    }
}
