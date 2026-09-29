using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private double patternViewportWidth;
    private bool updatingPatternChoices;
    private void PatternSymptom_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!updatingPatternChoices) RefreshSymptomPatterns();
    }
    private void PatternSort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!updatingPatternChoices) RefreshSymptomPatterns();
    }
    private void PatternTrendViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Math.Abs(patternViewportWidth - e.NewSize.Width) < 2) return;
        patternViewportWidth = e.NewSize.Width;
        RefreshSymptomPatterns();
    }

    private void RefreshSymptomPatterns()
    {
        if (updatingPatternChoices || PatternSymptom is null || PatternFrequency is null) return;
        var selected = PatternSymptom.SelectedIndex;
        if (SymptomNames.Length == 0)
        {
            updatingPatternChoices = true;
            try { PatternSymptom.Items.Clear(); }
            finally { updatingPatternChoices = false; }
            PatternFrequency.Children.Clear();
            PatternTrend.Children.Clear();
            return;
        }
        if (PatternSymptom.Items.Count != SymptomNames.Length ||
            (selected >= 0 && selected < PatternSymptom.Items.Count &&
             PatternSymptom.Items[selected]?.ToString() != T(SymptomNames[selected])))
        {
            updatingPatternChoices = true;
            try
            {
                PatternSymptom.Items.Clear();
                foreach (var name in SymptomNames) PatternSymptom.Items.Add(T(name));
                PatternSymptom.SelectedIndex = selected < 0 || selected >= SymptomNames.Length ? 0 : selected;
            }
            finally { updatingPatternChoices = false; }
            selected = PatternSymptom.SelectedIndex;
        }
        if (selected < 0 || selected >= SymptomNames.Length) return;

        var english = selectedLanguage == "en";
        var sortIndex = PatternSort.SelectedIndex;
        var sortNames = english
            ? new[] { "Frequency", "Severity", "Total severity", "Name" }
            : new[] { "Häufigkeit", "Schwere", "Gesamtbelastung", "Name" };
        if (PatternSort.Items.Count != sortNames.Length ||
            (sortIndex >= 0 && sortIndex < PatternSort.Items.Count &&
             PatternSort.Items[sortIndex]?.ToString() != sortNames[sortIndex]))
        {
            updatingPatternChoices = true;
            try
            {
                PatternSort.Items.Clear();
                foreach (var sortName in sortNames) PatternSort.Items.Add(sortName);
                PatternSort.SelectedIndex = sortIndex < 0 || sortIndex >= sortNames.Length ? 0 : sortIndex;
            }
            finally { updatingPatternChoices = false; }
            sortIndex = PatternSort.SelectedIndex;
        }
        if (sortIndex < 0) return;
        var culture = CultureInfo.GetCultureInfo(english ? "en-US" : "de-DE");
        PatternPatternNote();
        PatternOverviewNote.Text = english
            ? "All symptoms in the selected period, independent of the symptom chosen in Symptom patterns. Only symptoms recorded at least once are shown. Severity sorts by average severity when present; total severity adds the recorded values. Unassessed values are excluded."
            : "Alle Symptome im gewählten Zeitraum, unabhängig von der Auswahl in Symptommuster. Nur mindestens einmal erfasste Symptome werden angezeigt. Schwere sortiert nach mittlerer Stärke beim Auftreten, Gesamtbelastung nach der Summe der erfassten Stärken. Nicht beurteilte Werte bleiben außen vor.";
        PatternFrequency.Children.Clear();
        var ranked = SymptomNames.Select(name =>
        {
            var assessed = analysisStates.Select(s => s.Data.SymptomSeverity(name)).Where(v => v >= 0).ToArray();
            var present = assessed.Count(v => v > 0);
            return new { Name = name, Assessed = assessed.Length, Present = present,
                Severe = assessed.Count(v => v >= 3), Sum = assessed.Sum(),
                Mean = present == 0 ? 0 : assessed.Where(v => v > 0).Average() };
        }).Where(s => s.Present > 0);
        var sorted = sortIndex switch
        {
            1 => ranked.OrderByDescending(s => s.Mean).ThenByDescending(s => s.Present),
            2 => ranked.OrderByDescending(s => s.Sum).ThenByDescending(s => s.Present),
            3 => ranked.OrderBy(s => T(s.Name), StringComparer.CurrentCultureIgnoreCase),
            _ => ranked.OrderByDescending(s => s.Present).ThenByDescending(s => s.Mean)
        };
        foreach (var item in sorted)
        {
            var percent = 100.0 * item.Present / item.Assessed;
            var label = english
                ? $"{item.Present}/{item.Assessed} ({percent.ToString("0", culture)}%) · " +
                  (item.Severe == 0 ? "no entries at severity 3–4" : $"{item.Severe} entries at severity 3–4")
                : $"{item.Present}/{item.Assessed} ({percent.ToString("0", culture)} %) · " +
                  (item.Severe == 0 ? "keine Einträge mit Stärke 3–4" : $"{item.Severe} Einträge mit Stärke 3–4");
            PatternFrequency.Children.Add(PatternRow(T(item.Name), label, percent));
        }
        if (PatternFrequency.Children.Count == 0)
            PatternFrequency.Children.Add(new TextBlock { Text = english ?
                "No symptoms recorded in the selected period." : "Im gewählten Zeitraum wurden keine Symptome erfasst." });

        var symptom = SymptomNames[selected];
        var selectedValues = analysisStates.Select(s => (s.Start, Severity: s.Data.SymptomSeverity(symptom)))
            .Where(s => s.Severity >= 0).ToArray();
        RefreshSymptomAssociations(symptom);
        PatternTrend.Children.Clear();
        var daily = selectedValues.GroupBy(s => s.Start.Date)
            .ToDictionary(g => g.Key, g => g.Max(s => s.Severity));
        if (analysisStates.Count == 0)
        {
            PatternTrend.Children.Add(new TextBlock { Text = english ? "No assessed entries for this symptom." :
                "Für dieses Symptom liegen keine beurteilten Einträge vor." });
            return;
        }
        PatternTrend.Children.Add(new TextBlock { Text = english
            ? "Newest weeks on the left. Gray: no assessment · pale green: explicitly absent · yellow to red: severity 1–4. Hover for details."
            : "Neueste Wochen links. Grau: nicht beurteilt · Hellgrün: ausdrücklich keine Beschwerden · Gelb bis Rot: Stärke 1–4. Details beim Darüberfahren." });
        var first = AnalysisThreshold() == DateTime.MinValue ? analysisStates[0].Start.Date : AnalysisThreshold().Date;
        var last = DateTime.Today;
        var latestMonday = last.AddDays(-((int)last.DayOfWeek + 6) % 7);
        var earliestMonday = first.AddDays(-((int)first.DayOfWeek + 6) % 7);
        var weekCount = (latestMonday - earliestMonday).Days / 7 + 1;
        const double labelWidth = 34, gap = 4;
        var available = Math.Max(0, PatternTrendViewport.ActualWidth - labelWidth - gap * weekCount - 28);
        var weekWidth = Math.Max(42, available / weekCount);
        var calendar = new Grid { ColumnSpacing = 4, RowSpacing = 4, HorizontalAlignment = HorizontalAlignment.Left };
        calendar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
        for (var week = 0; week < weekCount; week++)
            calendar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weekWidth) });
        for (var row = 0; row < 8; row++)
            calendar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var weekday = 0; weekday < 7; weekday++)
        {
            var weekdayLabel = new TextBlock { Text = culture.DateTimeFormat.AbbreviatedDayNames[(weekday + 1) % 7],
                VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
            Grid.SetRow(weekdayLabel, weekday + 1);
            calendar.Children.Add(weekdayLabel);
        }
        for (var week = 0; week < weekCount; week++)
        {
            var monday = latestMonday.AddDays(-7 * week);
            var weekLabel = new TextBlock { Text = monday.ToString("dd.MM", culture), FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center };
            ToolTipService.SetToolTip(weekLabel, monday.ToString("D", culture));
            Grid.SetColumn(weekLabel, week + 1);
            calendar.Children.Add(weekLabel);
            for (var weekday = 0; weekday < 7; weekday++)
            {
                var date = monday.AddDays(weekday);
                if (date < first || date > last) continue;
                var known = daily.TryGetValue(date, out var value);
                var hex = !known ? "#E6EAEE" : value switch
                {
                    0 => "#D8EBD9", 1 => "#F4DC80", 2 => "#EDA94D", 3 => "#DC7049", _ => "#B94350"
                };
                var tile = new Border { Width = weekWidth, Height = 28, CornerRadius = new CornerRadius(4),
                    Background = PatternBrush(hex), Child = new TextBlock { Text = date.Day.ToString(culture),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                        Foreground = PatternBrush(known && value == 4 ? "#FFFFFF" : "#243348") } };
                ToolTipService.SetToolTip(tile, date.ToString(english ? "MM/dd/yyyy" : "dd.MM.yyyy", culture) + " · " +
                    (!known ? (english ? "not assessed" : "nicht beurteilt") :
                     value == 0 ? T("keine") : (english ? $"Severity {value}/4" : $"Stärke {value}/4")));
                Grid.SetColumn(tile, week + 1);
                Grid.SetRow(tile, weekday + 1);
                calendar.Children.Add(tile);
            }
        }
        PatternTrend.Children.Add(calendar);

        void PatternPatternNote() => SymptomPatternNote.Text = english
            ? "The following analyses refer to the selected symptom and the period chosen above. Unassessed values are excluded; 0 means explicitly absent."
            : "Die folgenden Auswertungen beziehen sich auf das gewählte Symptom und den oben eingestellten Zeitraum. Nicht beurteilte Werte bleiben außen vor; 0 bedeutet ausdrücklich keine Beschwerden.";
    }

    private static SolidColorBrush PatternBrush(string hex) => new(Windows.UI.Color.FromArgb(255,
        Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16),
        Convert.ToByte(hex.Substring(5, 2), 16)));

    private static Grid PatternRow(string heading, string detail, double percent)
    {
        var row = new Grid { ColumnSpacing = 12, MinHeight = 28, Margin = new Thickness(0, 0, 8, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var title = new TextBlock { Text = heading, VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap };
        var description = new TextBlock { Text = detail, VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(description, 1);
        var track = new Grid { Height = 9, VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 226, 237, 243)) };
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Clamp(percent, 0, 100), GridUnitType.Star) });
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - Math.Clamp(percent, 0, 100), GridUnitType.Star) });
        var fill = new Border { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 119, 137)) };
        track.Children.Add(fill);
        Grid.SetColumn(track, 2);
        row.Children.Add(title);
        row.Children.Add(description);
        row.Children.Add(track);
        return row;
    }
}
