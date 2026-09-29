using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private static string AiModelPath => System.IO.Path.Combine(Store.Folder, "ai-settings.json");
    private static string AnalysisDisplayPath => System.IO.Path.Combine(Store.Folder, "analysis-display.json");
    private static readonly string[] AnalysisPeriods = ["7 Tage", "30 Tage", "3 Monate", "1 Jahr", "Gesamt"];
    private static readonly string[] CarryDurations = ["24 Stunden", "48 Stunden"];
    private readonly List<(DateTime Start, StateData Data)> analysisStates = new();
    private bool analysisDisplayInitialized;
    private string aiModel = "gpt-6-sol";
    private bool aiBusy;

    private void InitializeAnalysis()
    {
        try
        {
            if (File.Exists(AiModelPath))
            {
                var saved = JsonSerializer.Deserialize<string>(File.ReadAllText(AiModelPath));
                if (saved is "gpt-6-sol" or "gpt-6-astra" or "gpt-6-luna") aiModel = saved;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        AnalysisPeriod.ItemsSource = AnalysisPeriods;
        AnalysisPeriod.SelectedIndex = 1;
        CarryDuration.ItemsSource = CarryDurations.Select(T).ToArray();
        CarryDuration.SelectedIndex = 0;
        try
        {
            if (File.Exists(AnalysisDisplayPath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(AnalysisDisplayPath));
                var root = document.RootElement;
                if (root.TryGetProperty("Hours", out var hours) && hours.TryGetInt32(out var value))
                    CarryDuration.SelectedIndex = value == 48 ? 1 : 0;
                if (root.TryGetProperty("Enabled", out var enabled) &&
                    enabled.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    CarryCondition.IsChecked = enabled.GetBoolean();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        CarryDuration.IsEnabled = CarryCondition.IsChecked == true;
        analysisDisplayInitialized = true;
    }

    private void LocalizeAnalysisPeriod()
    {
        var index = AnalysisPeriod.SelectedIndex;
        AnalysisPeriod.ItemsSource = AnalysisPeriods.Select(T).ToArray();
        AnalysisPeriod.SelectedIndex = index < 0 ? 1 : index;
        var duration = CarryDuration.SelectedIndex;
        CarryDuration.ItemsSource = CarryDurations.Select(T).ToArray();
        CarryDuration.SelectedIndex = duration < 0 ? 0 : duration;
    }

    private bool CarryEnabled => CarryCondition?.IsChecked == true;
    private int CarryHours => CarryDuration?.SelectedIndex == 1 ? 48 : 24;

    private void CarryCondition_Changed(object sender, RoutedEventArgs e)
    {
        if (!analysisDisplayInitialized) return;
        CarryDuration.IsEnabled = CarryEnabled;
        SaveAnalysisDisplay();
        UpdateCarryPresentation();
    }

    private void CarryDuration_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!analysisDisplayInitialized || CarryDuration.SelectedIndex < 0) return;
        SaveAnalysisDisplay();
        UpdateCarryPresentation();
    }

    private void SaveAnalysisDisplay()
    {
        try { File.WriteAllText(AnalysisDisplayPath, JsonSerializer.Serialize(new { Enabled = CarryEnabled, Hours = CarryHours })); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void UpdateCarryPresentation()
    {
        UpdateAnalysisSummary();
        DrawAnalysisChart();
        DrawAnalysisHeatmap();
        DrawWeeklyHeatmap();
        RestoreAiResponse();
    }

    private void AnalysisPeriod_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (AnalysisPeriod.SelectedIndex >= 0) RefreshAnalysis();
    }

    private DateTime AnalysisThreshold() => AnalysisPeriod.SelectedIndex switch
    {
        0 => DateTime.Now.AddDays(-7), 1 => DateTime.Now.AddDays(-30),
        2 => DateTime.Now.AddMonths(-3), 3 => DateTime.Now.AddYears(-1), _ => DateTime.MinValue
    };

    private void RefreshAnalysis()
    {
        if (AnalysisPeriod is null || AnalysisSummary is null || AnalysisPeriod.SelectedIndex < 0) return;
        var threshold = AnalysisThreshold();
        analysisStates.Clear();
        foreach (var entry in entries.Where(e => e.Kind == "Zustand" && e.Start >= threshold).OrderBy(e => e.Start))
        {
            StateData data;
            try { data = JsonSerializer.Deserialize<StateData>(entry.Data) ?? new StateData(); }
            catch (JsonException) { data = new StateData(); }
            analysisStates.Add((entry.Start, data));
        }
        var intervals = entries.Count(e => (e.Kind is "Ruhe" or "Aktivität") && e.Start >= threshold);
        var sleepCount = entries.Count(e => ConditionAnalysis.IsSleep(e) && e.End is not null && e.End > threshold);
        var sleepHours = ConditionAnalysis.TotalSleepTime(entries, threshold).TotalHours;
        var pemCount = analysisStates.Count(s => s.Data.Pem == 2);
        var crashCount = analysisStates.Count(s => s.Data.Crash);
        var culture = CultureInfo.GetCultureInfo(selectedLanguage == "en" ? "en-US" : "de-DE");
        AnalysisCounts.Text = selectedLanguage == "en"
            ? $"{analysisStates.Count} conditions · {intervals} activity/rest intervals · {sleepCount} sleep periods ({sleepHours.ToString("0.#", culture)} h) · PEM confirmed: {pemCount} · crashes marked: {crashCount}"
            : $"{analysisStates.Count} Zustände · {intervals} Aktivität/Ruhe · {sleepCount} Schlafphasen ({sleepHours.ToString("0.#", culture)} h) · PEM erkannt: {pemCount} · Crash markiert: {crashCount}";
        UpdateAnalysisSummary();
        LocalAssessment.Text = ConditionAnalysis.BuildAssessment(analysisStates, entries, DateTime.Now, selectedLanguage, T);
        DrawAnalysisChart();
        DrawAnalysisHeatmap();
        DrawWeeklyHeatmap();
        RestoreAiResponse();
        RefreshSymptomPatterns();
        RefreshActivityMatrix();
        RefreshLoadAnalysis();
        RefreshSymptomTrend();
    }

    private void UpdateAnalysisSummary()
    {
        var culture = CultureInfo.GetCultureInfo(selectedLanguage == "en" ? "en-US" : "de-DE");
        AnalysisSummary.Text = selectedLanguage == "en"
            ? (analysisStates.Count == 0 ? "No conditions recorded in the selected period." :
                  $"Average recorded overall condition: {analysisStates.Average(s => s.Data.Overall).ToString("0.0", culture)} of 4 (higher = worse). The chart starts at the first recorded condition ({analysisStates[0].Start:MM/dd/yyyy}); earlier days are not shown. " +
                  (CarryEnabled ? $"Dashed steps assume the last condition for at most {CarryHours} hours, up to the next entry; gaps beyond that remain unknown. The last recorded condition was on {analysisStates[^1].Start:MM/dd/yyyy}; later days without an assumption are not shown. PEM and crash are not carried forward."
                      : "Lines connect observations, not continuous measurements."))
            : (analysisStates.Count == 0 ? "Noch keine Zustände im gewählten Zeitraum." :
                  $"Mittlerer dokumentierter Allgemeinzustand: {analysisStates.Average(s => s.Data.Overall).ToString("0.0", culture)} von 4 (höher = schlechter). Der Graph beginnt bei der ersten Zustandsangabe ({analysisStates[0].Start:dd.MM.yyyy}); frühere Tage fehlen. " +
                  (CarryEnabled ? $"Gestrichelte Stufen nehmen den letzten Zustand höchstens {CarryHours} Stunden bis zum nächsten Eintrag an; längere Lücken bleiben unbekannt. Der letzte Eintrag ist vom {analysisStates[^1].Start:dd.MM.yyyy}; spätere Tage ohne Annahme fehlen im Graphen. PEM und Crash werden nicht fortgeführt."
                      : "Die Linie verbindet Messpunkte, sie ist keine lückenlose Messung."));
    }

    private void AnalysisChart_SizeChanged(object sender, SizeChangedEventArgs e) => DrawAnalysisChart();

    private void AnalysisChartBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (AnalysisChart is null) return;
        var height = Math.Max(120, e.NewSize.Height - AnalysisChartBorder.Padding.Top -
            AnalysisChartBorder.Padding.Bottom - AnalysisChartBorder.BorderThickness.Top -
            AnalysisChartBorder.BorderThickness.Bottom);
        if (double.IsNaN(AnalysisChart.Height) || Math.Abs(AnalysisChart.Height - height) > 0.5)
            AnalysisChart.Height = height;
    }

    private void AnalysisLayout_Loaded(object sender, RoutedEventArgs e) => StretchAnalysisLayout();

    private void AnalysisHeader_SizeChanged(object sender, SizeChangedEventArgs e) => StretchAnalysisLayout();

    private void MainContentBorder_SizeChanged(object sender, SizeChangedEventArgs e) => StretchAnalysisLayout();

    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(StretchAnalysisLayout);
    }

    private void StretchAnalysisLayout()
    {
        if (AnalysisLayout.XamlRoot is null || MainContentBorder.ActualHeight <= 0) return;
        // The surrounding panel provides the available height for the full-width analysis tabs.
        var available = Math.Max(280, MainContentBorder.ActualHeight -
            MainContentBorder.Padding.Top - MainContentBorder.Padding.Bottom -
            MainContentBorder.BorderThickness.Top - MainContentBorder.BorderThickness.Bottom -
            52 - AnalysisLayout.Margin.Top - AnalysisLayout.Margin.Bottom);
        if (double.IsNaN(AnalysisLayout.Height) || Math.Abs(AnalysisLayout.Height - available) > 0.5)
            AnalysisLayout.Height = available;
        // The graph canvas has no natural height before it is drawn; give the TabView
        // the remaining space so all four views fill the same usable area.
        var visualHeight = Math.Max(220, available - AnalysisHeader.ActualHeight - 12);
        if (double.IsNaN(AnalysisVisualTabs.Height) || Math.Abs(AnalysisVisualTabs.Height - visualHeight) > 0.5)
            AnalysisVisualTabs.Height = visualHeight;
    }

    private void DrawAnalysisChart()
    {
        if (AnalysisChart is null || AnalysisChart.ActualWidth < 140 || AnalysisChart.ActualHeight < 100) return;
        var canvas = AnalysisChart;
        canvas.Children.Clear();
        var axis = (Brush)canvas.Resources["ChartAxisBrush"];
        var normal = (Brush)canvas.Resources["ChartLineBrush"];
        var pem = (Brush)canvas.Resources["ChartPemBrush"];
        var crash = (Brush)canvas.Resources["ChartCrashBrush"];
        double left = 45, top = 16, right = Math.Max(left + 20, canvas.ActualWidth - 20),
            bottom = Math.Max(top + 20, canvas.ActualHeight - 63);
        for (int value = 0; value <= 4; value++)
        {
            double y = bottom - value * (bottom - top) / 4;
            canvas.Children.Add(new Line { X1 = left, Y1 = y, X2 = right, Y2 = y, Stroke = axis, StrokeThickness = 1 });
            AddChartLabel(value.ToString(CultureInfo.InvariantCulture), 13, y - 9);
        }
        if (analysisStates.Count == 0)
        {
            AddChartLabel(T("Hier erscheint dein Verlauf."), left + 20, top + 30);
            return;
        }
        var now = DateTime.Now;
        var chartEnd = CarryEnabled && analysisStates[^1].Start < now
            ? MinTime(now, analysisStates[^1].Start.AddHours(CarryHours))
            : analysisStates[^1].Start;
        long min = analysisStates[0].Start.Ticks, max = chartEnd.Ticks;
        double X(DateTime at) => max == min ? (left + right) / 2 :
            left + (at.Ticks - min) / (double)(max - min) * (right - left);
        double Y(int value) => bottom - Math.Clamp(value, 0, 4) * (bottom - top) / 4;
        if (CarryEnabled)
        {
            foreach (var segment in CarrySegments())
            {
                var from = X(segment.Start);
                var to = X(segment.End);
                // Short strokes distinguish an assumed interval from recorded points.
                for (double x = from; x < to; x += 12)
                    canvas.Children.Add(new Line { X1 = x, Y1 = Y(segment.Overall),
                        X2 = Math.Min(x + 7, to), Y2 = Y(segment.Overall),
                        Stroke = normal, StrokeThickness = 2.4 });
                if (segment.NextOverall is { } nextOverall && segment.End > segment.Start)
                {
                    canvas.Children.Add(new Line { X1 = to, Y1 = Y(segment.Overall),
                        X2 = to, Y2 = Y(nextOverall), Stroke = normal, StrokeThickness = 1.4 });
                }
            }
        }
        else for (int i = 1; i < analysisStates.Count; i++)
            canvas.Children.Add(new Line { X1 = X(analysisStates[i - 1].Start), Y1 = Y(analysisStates[i - 1].Data.Overall),
                X2 = X(analysisStates[i].Start), Y2 = Y(analysisStates[i].Data.Overall),
                Stroke = normal, StrokeThickness = 2.4 });
        foreach (var state in analysisStates)
        {
            var dot = new Ellipse { Width = 8, Height = 8,
                Fill = state.Data.Crash ? crash : state.Data.Pem > 0 ? pem : normal };
            Canvas.SetLeft(dot, X(state.Start) - 4);
            Canvas.SetTop(dot, Y(state.Data.Overall) - 4);
            canvas.Children.Add(dot);
        }
        var format = selectedLanguage == "en" ? "MM/dd/yy" : "dd.MM.yy";
        AddChartLabel(analysisStates[0].Start.ToString(format), left, bottom + 5);
        AddChartLabel(chartEnd.ToString(format), Math.Max(left + 70, right - 70), bottom + 5);
        AddChartLegend(left, bottom + 29, normal, selectedLanguage == "en" ? "Condition" : "Zustand");
        AddChartLegend(left + 110, bottom + 29, pem, selectedLanguage == "en" ? "PEM suspected/confirmed" : "PEM vermutet/erkannt");
        AddChartLegend(left + 338, bottom + 29, crash, "Crash");
        if (CarryEnabled)
        {
            for (double x = left; x < left + 25; x += 10)
                canvas.Children.Add(new Line { X1 = x, Y1 = bottom + 52,
                    X2 = x + 6, Y2 = bottom + 52, Stroke = normal, StrokeThickness = 2 });
            AddChartLabel(selectedLanguage == "en" ? "Dashed line = carried condition" :
                "Gestrichelte Linie = Fortführung", left + 31, bottom + 44);
        }
    }

    private void AddChartLegend(double x, double y, Brush color, string label)
    {
        var dot = new Ellipse { Width = 8, Height = 8, Fill = color };
        Canvas.SetLeft(dot, x);
        Canvas.SetTop(dot, y + 4);
        AnalysisChart.Children.Add(dot);
        AddChartLabel(label, x + 13, y);
    }

    private void AddChartLabel(string text, double x, double y)
    {
        var label = new TextBlock { Text = text, FontSize = 11, Foreground = (Brush)AnalysisChart.Resources["ChartLineBrush"] };
        Canvas.SetLeft(label, x); Canvas.SetTop(label, y);
        AnalysisChart.Children.Add(label);
    }

    private static DateTime MinTime(DateTime first, DateTime second) => first < second ? first : second;

    private IEnumerable<(DateTime Start, DateTime End, int Overall, int? NextOverall)> CarrySegments()
    {
        if (!CarryEnabled) yield break;
        var now = DateTime.Now;
        for (int i = 0; i < analysisStates.Count; i++)
        {
            var state = analysisStates[i];
            if (state.Start >= now) continue;
            var next = i + 1 < analysisStates.Count ? analysisStates[i + 1].Start : now;
            var end = MinTime(MinTime(next, state.Start.AddHours(CarryHours)), now);
            if (end <= state.Start) continue;
            int? nextOverall = i + 1 < analysisStates.Count && end == next ? analysisStates[i + 1].Data.Overall : null;
            yield return (state.Start, end, state.Data.Overall, nextOverall);
        }
    }

    private List<Entry> AiEntries()
    {
        var threshold = AnalysisThreshold();
        return entries.Where(e => e.Start >= threshold && e.Start <= DateTime.Now)
            .OrderBy(e => e.Start).ThenBy(e => e.Id).ToList();
    }

    private static string AiSnapshot(IEnumerable<Entry> selected) => JsonSerializer.Serialize(selected.Select(e =>
        new { Type = e.Kind, e.Start, e.End, e.Data, e.Note }), new JsonSerializerOptions { WriteIndented = true });

    private static string AiSnapshotHash(string snapshot) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));

    private string AiAnalysisSignature(string approvedRecords) =>
        AiSnapshotHash(approvedRecords + (CarryEnabled ? $"\nConditionCarryHours={CarryHours}" : ""));

    private void RestoreAiResponse()
    {
        if (aiBusy || AnalysisPeriod.SelectedIndex < 0) return;
        var saved = store.LatestAiAnalysis(AnalysisPeriod.SelectedIndex, selectedLanguage);
        AiResult.Blocks.Clear();
        if (saved is null)
        {
            AiInfo.Text = selectedLanguage == "en" ? "No AI response is saved for this period." :
                "Für diesen Zeitraum ist noch keine KI-Antwort gespeichert.";
            return;
        }
        ShowAiResponse(saved.Response);
        var current = AiAnalysisSignature(AiSnapshot(AiEntries().TakeLast(300)));
        bool outdated = saved.SnapshotHash != current;
        AiInfo.Text = selectedLanguage == "en"
            ? $"Saved on {saved.Created.ToLocalTime():MM/dd/yyyy HH:mm} · {saved.Model}" +
              (outdated ? "\nThe analyzed data has changed since this response." : "")
            : $"Gespeichert am {saved.Created.ToLocalTime():dd.MM.yyyy HH:mm} · {saved.Model}" +
              (outdated ? "\nSeit dieser Auswertung haben sich die ausgewerteten Daten geändert." : "");
    }

    private void ShowAiResponse(string markdown)
    {
        AiResult.Blocks.Clear();
        var response = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        response = Regex.Replace(response, @"(?m)^(#{1,6}\s+[^\n]+?)(\s*-\s+\*\*)", "$1\n$2");
        foreach (var raw in response.Split('\n'))
        {
            var heading = Regex.Match(raw, @"^\s*#{1,6}\s+(.+)$");
            var content = heading.Success ? heading.Groups[1].Value : Regex.Replace(raw, @"^\s*[-*]\s+", "  • ");
            var paragraph = new Paragraph();
            if (heading.Success)
            {
                var boldHeading = new Bold();
                boldHeading.Inlines.Add(new Run { Text = content.Trim() });
                paragraph.Inlines.Add(boldHeading);
            }
            else
            {
                foreach (var part in Regex.Split(content, @"(\*\*[^*]+\*\*)"))
                {
                    if (part.StartsWith("**", StringComparison.Ordinal) && part.EndsWith("**", StringComparison.Ordinal) && part.Length > 4)
                    {
                        var bold = new Bold(); bold.Inlines.Add(new Run { Text = part[2..^2] });
                        paragraph.Inlines.Add(bold);
                    }
                    else paragraph.Inlines.Add(new Run { Text = part.Replace("**", "") });
                }
            }
            AiResult.Blocks.Add(paragraph);
        }
    }

    private async void ConfigureAi_Click(object sender, RoutedEventArgs e) => await ConfigureAiAsync();

    private async Task ConfigureAiAsync()
    {
        var de = selectedLanguage == "de";
        var key = new PasswordBox { Password = AiConnection.ReadKey() ?? "" };
        var model = new ComboBox { ItemsSource = new[] { "gpt-6-sol", "gpt-6-astra", "gpt-6-luna" },
            SelectedItem = aiModel, Width = 200 };
        var fields = new StackPanel { Spacing = 10, Width = 490 };
        fields.Children.Add(new TextBlock { Text = de ? "OpenAI API-Schlüssel:" : "OpenAI API key:" });
        fields.Children.Add(key);
        fields.Children.Add(new TextBlock { Text = de ? "Modell:" : "Model:" });
        fields.Children.Add(model);
        fields.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap,
            Text = de ? "Der Schlüssel wird in der Windows-Anmeldeinformationsverwaltung gespeichert. Protokolldaten werden nur nach deiner Bestätigung übertragen. Die API-Nutzung wird separat berechnet. Leer speichern entfernt den Schlüssel." :
                "The key is saved in Windows Credential Manager. Log data is sent only after your confirmation. API usage is billed separately. Saving an empty field removes the key." });
        var dialog = new ContentDialog { Title = de ? "KI-Verbindung einrichten" : "Set up AI connection",
            Content = fields, PrimaryButtonText = de ? "Speichern" : "Save", CloseButtonText = de ? "Abbrechen" : "Cancel",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = ((FrameworkElement)Content).XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            AiConnection.SaveKey(key.Password);
            aiModel = model.SelectedItem?.ToString() ?? "gpt-6-sol";
            Directory.CreateDirectory(Store.Folder);
            File.WriteAllText(AiModelPath, JsonSerializer.Serialize(aiModel));
        }
        catch (Exception ex) { AiInfo.Text = ex.Message; }
    }

    private async void AnalyzeAi_Click(object sender, RoutedEventArgs e)
    {
        if (aiBusy) return;
        var key = AiConnection.ReadKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            await ConfigureAiAsync();
            key = AiConnection.ReadKey();
            if (string.IsNullOrWhiteSpace(key)) return;
        }
        bool de = selectedLanguage == "de";
        var carryHoursForRequest = CarryEnabled ? CarryHours : 0;
        var selected = AiEntries();
        if (selected.Count == 0)
        {
            AiInfo.Text = de ? "Im gewählten Zeitraum liegen keine Einträge vor." : "No records in the selected period.";
            AnalysisTabs.SelectedIndex = 1;
            return;
        }
        var recent = selected.TakeLast(300).ToList();
        var snapshot = AiSnapshot(recent);
        var preview = new RichEditBox { Height = 300, TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"), FontSize = 13 };
        var body = new StackPanel { Spacing = 10, Width = 440, MaxWidth = 440 };
        body.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap,
            Text = de ? $"{recent.Count} von {selected.Count} Einträgen (höchstens die letzten 300) inklusive Notizen und Medikamentenangaben. Du kannst den Text vor dem Senden bearbeiten. Die Anfrage kann API-Kosten verursachen." :
                $"{recent.Count} of {selected.Count} records (at most the latest 300), including notes and medication details. You may edit the text before sending. API charges may apply." });
        body.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap,
            Text = carryHoursForRequest > 0
                ? (de ? $"KI-Hinweis: Der letzte Allgemeinzustand darf höchstens {carryHoursForRequest} Stunden bis zur nächsten Angabe als Annahme fortgeführt werden. PEM und Crash nicht fortführen. Die JSON-Daten bleiben Originaleinträge."
                    : $"AI guidance: The last overall condition may be assumed for up to {carryHoursForRequest} hours until the next entry. Do not carry PEM or crash. The JSON contains only original records.")
                : (de ? "Zustandsfortführung ist ausgeschaltet; die KI erhält nur die dokumentierten Zustände."
                    : "Condition carry-forward is off; the AI receives only recorded conditions.") });
        body.Children.Add(preview);
        var dialog = new ContentDialog { Title = de ? "Daten für die KI-Anfrage prüfen" : "Review data for AI request",
            Content = body, PrimaryButtonText = de ? "An OpenAI senden" : "Send to OpenAI",
            CloseButtonText = de ? "Abbrechen" : "Cancel", XamlRoot = ((FrameworkElement)Content).XamlRoot };
        // Populate the document once the editor is attached to the dialog's visual tree.
        dialog.Opened += (_, _) => preview.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, snapshot);
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        preview.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out var editedText);
        var approvedText = editedText.TrimEnd('\r', '\n').Replace("\r\n", "\n").Replace('\r', '\n');
        try
        {
            using var document = JsonDocument.Parse(approvedText);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
                throw new JsonException("The data preview contains no entries.");
        }
        catch (JsonException)
        {
            AnalysisTabs.SelectedIndex = 1;
            AiInfo.Text = de ? "Die Datenvorschau enthält keine gültigen Einträge. Die Anfrage wurde nicht gesendet." :
                "The data preview contains no valid records. The request was not sent.";
            return;
        }
        aiBusy = true;
        AnalysisPeriod.IsEnabled = false;
        CarryCondition.IsEnabled = false;
        CarryDuration.IsEnabled = false;
        AnalyzeAiButton.IsEnabled = false;
        AnalysisTabs.SelectedIndex = 1;
        AiInfo.Text = de ? "Neue Auswertung läuft ..." : "Generating a new analysis ...";
        AiResult.Blocks.Clear();
        ShowAiResponse(de ? "Analyse läuft ..." : "Analyzing ...");
        try
        {
            var guidance = carryHoursForRequest > 0
                ? (de ? $"Falls zwischen dokumentierten Zustandsangaben keine weitere Angabe vorliegt, darfst du den letzten Allgemeinzustand für höchstens {carryHoursForRequest} Stunden oder bis zum nächsten Eintrag als ANNAHME fortführen. Danach ist der Zustand unbekannt. Kennzeichne angenommene Abschnitte ausdrücklich, unterscheide sie in Auswertungen von echten Einträgen und leite daraus keine zusätzlichen PEM- oder Crash-Ereignisse ab. Beginne keine Annahme vor dem ersten übermittelten Zustand. "
                    : $"Where no condition entry exists between recorded states, you may ASSUME the last overall condition for at most {carryHoursForRequest} hours or until the next entry. Thereafter the condition is unknown. Label inferred periods explicitly, distinguish them from recorded entries in analyses, and never infer additional PEM or crash events. Do not assume a state before the first supplied condition entry. ")
                : (de ? "Nimm keine Zustandsfortführung für Zeiten ohne Eintrag vor. " :
                    "Do not carry conditions forward into times without an entry. ");
            var question = de ? "Analysiere die dokumentierte Lage und mögliche Muster für künftiges Pacing. Antworte auf Deutsch. " + guidance + "Die folgenden Daten wurden von mir zur Übertragung freigegeben:\n" :
                "Analyze documented condition and potential patterns for future pacing. Reply in English. " + guidance + "I approved sending these records:\n";
            var response = await AiConnection.AnalyzeAsync(key, aiModel, question + approvedText);
            var signature = AiSnapshotHash(approvedText + (carryHoursForRequest > 0 ? $"\nConditionCarryHours={carryHoursForRequest}" : ""));
            store.SaveAiAnalysis(AnalysisPeriod.SelectedIndex, selectedLanguage, aiModel, signature, response);
        }
        catch (Exception ex)
        {
            AiResult.Blocks.Clear();
            ShowAiResponse((de ? "Die KI-Anfrage ist fehlgeschlagen: " : "The AI request failed: ") + ex.Message);
            AiInfo.Text = de ? "Keine neue Antwort gespeichert." : "No new response was saved.";
            return;
        }
        finally
        {
            aiBusy = false;
            AnalysisPeriod.IsEnabled = true;
            CarryCondition.IsEnabled = true;
            CarryDuration.IsEnabled = CarryEnabled;
            AnalyzeAiButton.IsEnabled = true;
        }
        RestoreAiResponse();
    }
}
