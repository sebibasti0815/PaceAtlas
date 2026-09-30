using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private readonly Dictionary<string, (int Column, bool Descending)> loadSort = new()
    {
        ["duration"] = (0, false), ["intensity"] = (0, false),
        ["cumulative"] = (0, false), ["activities"] = (0, true)
    };

    private sealed record LoadObservation(DateTime Start, DateTime End, string[] Dimensions,
        int Intensity, double Minutes, bool Mature, bool HasFollowup, bool Pem, double CumulativeMinutes = 0);
    private sealed record LoadGroup(string Label, int Order, int Count, int PemCount, int Valid, int Pending, int Missing);

    private void RefreshLoadAnalysis()
    {
        if (LoadDurationRows is null || LoadIntensityRows is null || LoadCumulativeRows is null || LoadActivityRows is null) return;
        var english = selectedLanguage == "en";
        var culture = CultureInfo.GetCultureInfo(english ? "en-US" : "de-DE");
        LoadDurationRows.Children.Clear();
        LoadIntensityRows.Children.Clear();
        LoadCumulativeRows.Children.Clear();
        LoadActivityRows.Children.Clear();
        LoadAnalysisNote.Text = english
            ? "The condition 12–48 hours after an activity is considered. The PEM fraction includes only activities at least 48 hours old with a condition entry in that interval; PEM must be explicitly confirmed there. Minutes × intensity (1–4) is an exploratory comparison, not a medical scale. Overlapping activity periods count once toward the 24-hour total. Several activities may share a later condition entry."
            : "Betrachtet wird der Zustand 12-48h nach einer Aktivität. Für die PEM-Anteile zählen nur Aktivitäten, die mindestens 48 Stunden zurückliegen und für die in diesem Zeitraum ein Zustandseintrag vorhanden ist. PEM muss darin ausdrücklich als „erkannt“ stehen. Minuten × Intensität (1–4) ist ein experimenteller Vergleich, keine medizinische Skala. Überlappende Aktivitäten zählen in der 24-Stunden-Summe nur einmal. Derselbe Zustand kann mehreren Aktivitäten zugeordnet sein.";

        var threshold = AnalysisThreshold();
        var now = DateTime.Now;
        var observations = new List<LoadObservation>();
        foreach (var entry in entries.Where(e => e.Kind == "Aktivität" && e.End is not null &&
                     e.End >= threshold && e.End <= now))
        {
            var minutes = (entry.End!.Value - entry.Start).TotalMinutes;
            if (minutes <= 0) continue;
            IntervalData? data;
            try { data = JsonSerializer.Deserialize<IntervalData>(entry.Data); }
            catch (JsonException) { continue; }
            var intensity = Math.Clamp(data?.Intensity ?? 1, 1, 4);
            var followups = analysisStates.Where(s =>
            {
                var hours = (s.Start - entry.End.Value).TotalHours;
                return hours >= 12 && hours < 48;
            }).ToArray();
            observations.Add(new LoadObservation(entry.Start, entry.End.Value,
                data?.Dimensions?.Distinct().ToArray() ?? [], intensity, minutes,
                entry.End.Value <= now.AddHours(-48), followups.Length > 0,
                followups.Any(s => s.Data.Pem == 2)));
        }
        var allPeriods = entries.Where(e => e.Kind == "Aktivität" && e.End is not null && e.End > e.Start)
            .Select(e => (e.Start, End: e.End!.Value)).ToArray();
        observations = observations.Select(o => o with { CumulativeMinutes = UnionMinutes(
            allPeriods, o.End.AddHours(-24), o.End) }).ToList();
        if (observations.Count == 0)
        {
            LoadDurationRows.Children.Add(new TextBlock { Text = english
                ? "No completed activities in the selected period." : "Keine abgeschlossenen Aktivitäten im gewählten Zeitraum." });
            return;
        }
        var showMissing = observations.Any(o => o.Mature && !o.HasFollowup);

        var durationGroups = new (string Label, Func<double, bool> Match)[]
        {
            (english ? "Up to 5 min" : "Bis 5 Min.", m => m <= 5),
            (english ? "Over 5–20 min" : "Über 5–20 Min.", m => m > 5 && m <= 20),
            (english ? "Over 20–60 min" : "Über 20–60 Min.", m => m > 20 && m <= 60),
            (english ? "Over 60 min" : "Über 60 Min.", m => m > 60)
        };
        var duration = durationGroups.Select((group, index) => Summarize(group.Label, index,
            observations.Where(o => group.Match(o.Minutes)).ToArray())).ToArray();
        RenderGroups(LoadDurationRows, "duration", english ? "Duration" : "Dauer", duration);
        var intensityNames = english
            ? new[] { "Low", "Moderate", "High", "Very high" }
            : new[] { "Gering", "Mittel", "Hoch", "Sehr hoch" };
        var intensityGroups = Enumerable.Range(1, 4).Select(level => Summarize(
            $"{level} · {intensityNames[level - 1]}", level - 1,
            observations.Where(o => o.Intensity == level).ToArray())).ToArray();
        RenderGroups(LoadIntensityRows, "intensity", english ? "Intensity" : "Intensität", intensityGroups);
        var cumulativeGroups = new (string Label, Func<double, bool> Match)[]
        {
            (english ? "Up to 20 min" : "Bis 20 Min.", m => m <= 20),
            (english ? "Over 20–60 min" : "Über 20–60 Min.", m => m > 20 && m <= 60),
            (english ? "Over 60–120 min" : "Über 60–120 Min.", m => m > 60 && m <= 120),
            (english ? "Over 120 min" : "Über 120 Min.", m => m > 120)
        };
        var cumulative = cumulativeGroups.Select((group, index) => Summarize(group.Label, index,
            observations.Where(o => group.Match(o.CumulativeMinutes)).ToArray())).ToArray();
        RenderGroups(LoadCumulativeRows, "cumulative", english ? "Activity time" : "Aktivitätszeit", cumulative);

        AddActivityHeader();
        var activityWidths = AnalysisWidths("load:activities", 135, 200, 170, 105, 125, 220);
        var activityOrder = loadSort["activities"];
        var orderedActivities = observations.OrderBy(o => activityOrder.Column switch
        {
            0 => (IComparable)o.End, 1 => string.Join(", ", o.Dimensions.Select(T)),
            2 => o.Minutes, 3 => o.Minutes * o.Intensity,
            4 => o.CumulativeMinutes, _ => !o.Mature ? 2 : !o.HasFollowup ? 1 : o.Pem ? 3 : 0
        });
        foreach (var observation in (activityOrder.Descending ? orderedActivities.Reverse() : orderedActivities).Take(20))
        {
            var score = observation.Minutes * observation.Intensity;
            var outcome = !observation.Mature ? (english ? "48 h not yet elapsed" : "48 Std. noch nicht vergangen") :
                !observation.HasFollowup ? (english ? "no condition entry 12–48 h afterward" :
                    "kein Zustandseintrag 12–48 h danach") :
                observation.Pem ? (english ? "PEM recorded" : "PEM erfasst") :
                    (english ? "no PEM in later condition entry" : "kein PEM im späteren Zustandseintrag");
            var kinds = observation.Dimensions.Length == 0 ? (english ? "Activity" : "Aktivität") :
                string.Join(", ", observation.Dimensions.Select(T));
            AddTableRow(LoadActivityRows, [
                observation.End.ToString(english ? "MM/dd HH:mm" : "dd.MM. HH:mm"), kinds,
                $"{observation.Minutes.ToString("0.#", culture)} × {observation.Intensity}",
                score.ToString("0.#", culture), observation.CumulativeMinutes.ToString("0.#", culture), outcome],
                activityWidths);
        }
        if (observations.Count > 20)
            LoadActivityRows.Children.Add(new TextBlock { Text = english ?
                "Showing 20 activities in the selected order." : "20 Aktivitäten in der gewählten Sortierung werden angezeigt." });
        ConfigurePanelFeedback(LoadDurationRows);
        ConfigurePanelFeedback(LoadIntensityRows);
        ConfigurePanelFeedback(LoadCumulativeRows);
        ConfigurePanelFeedback(LoadActivityRows);

        LoadGroup Summarize(string label, int order, LoadObservation[] group)
        {
            var valid = group.Where(o => o.Mature && o.HasFollowup).ToArray();
            return new LoadGroup(label, order, group.Length, valid.Count(o => o.Pem), valid.Length,
                group.Count(o => !o.Mature), group.Count(o => o.Mature && !o.HasFollowup));
        }

        void RenderGroups(StackPanel panel, string section, string first, LoadGroup[] groups)
        {
            var cells = new List<string> { first, english ? "Activities" : "Aktivitäten",
                english ? "PEM / assessed" : "PEM / auswertbar",
                english ? "Pending" : "Noch offen" };
            if (showMissing) cells.Add(english ? "No condition 12–48 h later" : "Kein Zustand 12–48 Std. später");
            var widths = AnalysisWidths("load:" + section, showMissing ? [220, 120, 180, 115, 215] : [220, 120, 180, 115]);
            AddTableRow(panel, cells.ToArray(), widths, true, section);
            var (column, descending) = loadSort[section];
            var sorted = groups.OrderBy(g => column switch
            {
                0 => (double)g.Order, 1 => g.Count,
                2 => g.Valid == 0 ? -1 : (double)g.PemCount / g.Valid,
                3 => g.Pending, _ => g.Missing
            });
            foreach (var group in descending ? sorted.Reverse() : sorted)
            {
                var pem = group.Valid == 0 ? (english ? "Not evaluable" : "Nicht auswertbar") : $"{group.PemCount}/{group.Valid}";
                var row = new List<string> { group.Label, group.Count.ToString(culture), pem,
                    group.Pending.ToString(culture) };
                if (showMissing) row.Add(group.Missing == 0 ? "" : group.Missing.ToString(culture));
                AddTableRow(panel, row.ToArray(), widths);
            }
        }

        void AddActivityHeader() => AddTableRow(LoadActivityRows,
            [english ? "End" : "Ende", english ? "Activity type" : "Aktivitätsart",
             english ? "Minutes × intensity" : "Minuten × Intensität",
             english ? "Weighted" : "Gewichtet",
             english ? "Past 24 h (min)" : "Letzte 24 Std. (Min.)",
             english ? "Condition 12–48 h later" : "Zustand 12–48 Std. später"],
            AnalysisWidths("load:activities", 135, 200, 170, 105, 125, 220), true, "activities");

        void AddTableRow(StackPanel panel, string[] cells, int[] widths, bool header = false, string? section = null)
        {
            if (header && section is not null) panel.Tag = section;
            if (!header && panel.Tag is string tableSection &&
                !MatchesTableFilters("load:" + tableSection, cells)) return;
            var grid = new Grid { Background = new SolidColorBrush(header ?
                Color.FromArgb(255, 224, 236, 242) :
                panel.Children.Count % 2 == 0 ? Color.FromArgb(255, 246, 249, 251) : Color.FromArgb(255, 255, 255, 255)) };
            for (var i = 0; i < cells.Length; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[i]) });
                FrameworkElement cell;
                if (header && section is not null)
                {
                    var column = i;
                    var sort = loadSort[section];
                    var caption = cells[i] + (sort.Column == i ? (sort.Descending ? "  ↓" : "  ↑") : "");
                    var heading = new Border { Padding = new Thickness(8, 6, 32, 6),
                        Child = new TextBlock { Text = caption, TextWrapping = TextWrapping.NoWrap,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        Background = new SolidColorBrush(Color.FromArgb(255, 224, 236, 242)),
                        BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(0) };
                    heading.Tapped += (_, _) =>
                    {
                        var current = loadSort[section];
                        loadSort[section] = (column, current.Column == column ? !current.Descending : false);
                        RefreshLoadAnalysis();
                    };
                    cell = heading;
                }
                else cell = new TextBlock { Text = cells[i], TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(8, 6, 8, 6) };
                Grid.SetColumn(cell, i);
                grid.Children.Add(cell);
                if (header && section is not null)
                {
                    var column = i;
                    var key = "load:" + section;
                    var filterButton = new Button { Content = new FontIcon { Glyph = "\uE721", FontSize = 13 }, Width = 25, Height = 26,
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(0), Margin = new Thickness(0, 0, 10, 0),
                        Background = new SolidColorBrush(Color.FromArgb(255, 224, 236, 242)),
                        BorderThickness = new Thickness(0) };
                    ToolTipService.SetToolTip(filterButton, N("Diese Spalte durchsuchen", "Filter this column"));
                    var filterBox = new TextBox { Width = 240, PlaceholderText = N("Suchbegriff", "Search term") };
                    if (tableFilters.TryGetValue(key, out var active) && active.TryGetValue(i, out var term))
                        filterBox.Text = term;
                    filterButton.Foreground = new SolidColorBrush(active is not null && active.ContainsKey(i)
                        ? Color.FromArgb(255, 16, 111, 128) : Color.FromArgb(255, 45, 65, 78));
                    var flyout = new Flyout();
                    var content = new StackPanel { Spacing = 6 };
                    content.Children.Add(filterBox);
                    var clear = new Button { Content = N("Filter löschen", "Clear filter") };
                    clear.Click += (_, _) => filterBox.Text = "";
                    content.Children.Add(clear);
                    flyout.Content = content;
                    filterButton.Flyout = flyout;
                    filterBox.TextChanged += (_, _) =>
                    {
                        if (!tableFilters.TryGetValue(key, out var terms))
                            tableFilters[key] = terms = new Dictionary<int, string>();
                        if (string.IsNullOrWhiteSpace(filterBox.Text)) terms.Remove(column);
                        else terms[column] = filterBox.Text.Trim();
                        filterButton.Foreground = new SolidColorBrush(terms.ContainsKey(column)
                            ? Color.FromArgb(255, 16, 111, 128) : Color.FromArgb(255, 45, 65, 78));
                        foreach (var dataRow in panel.Children.OfType<Grid>().Skip(1))
                            if (dataRow.Tag is string[] values)
                                dataRow.Visibility = MatchesTableFilters(key, values) ? Visibility.Visible : Visibility.Collapsed;
                    };
                    Grid.SetColumn(filterButton, i);
                    grid.Children.Add(filterButton);
                    var grip = new Thumb { Width = 9, HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Background = new SolidColorBrush(Color.FromArgb(255, 224, 236, 242)) };
                    grip.DragDelta += (_, args) => ResizeAnalysisColumn(panel, key, column, args.HorizontalChange);
                    grip.DragCompleted += (_, _) => SaveAnalysisWidths();
                    Grid.SetColumn(grip, i);
                    grid.Children.Add(grip);
                }
            }
            if (!header) grid.Tag = cells;
            panel.Children.Add(grid);
        }
    }

    private static double UnionMinutes(IEnumerable<(DateTime Start, DateTime End)> periods,
        DateTime windowStart, DateTime windowEnd)
    {
        var clipped = periods.Select(p => (Start: p.Start < windowStart ? windowStart : p.Start,
                End: p.End > windowEnd ? windowEnd : p.End))
            .Where(p => p.End > p.Start).OrderBy(p => p.Start).ToArray();
        if (clipped.Length == 0) return 0;
        var begin = clipped[0].Start;
        var finish = clipped[0].End;
        double minutes = 0;
        foreach (var period in clipped.Skip(1))
        {
            if (period.Start <= finish)
            {
                if (period.End > finish) finish = period.End;
            }
            else
            {
                minutes += (finish - begin).TotalMinutes;
                begin = period.Start;
                finish = period.End;
            }
        }
        return minutes + (finish - begin).TotalMinutes;
    }
}
