using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private bool updatingActivityMatrixLag;
    private string activityMatrixSortKey = "Gesamt";
    private bool activityMatrixSortDescending = true;
    private static readonly (int Lower, int Upper)[] ActivityMatrixWindows =
        [(0, 6), (6, 24), (24, 48), (48, 72)];

    private void ActivityMatrixLag_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!updatingActivityMatrixLag) RefreshActivityMatrix();
    }

    private void RefreshActivityMatrix()
    {
        if (ActivityMatrixLag is null || ActivityMatrixTable is null) return;
        var english = selectedLanguage == "en";
        var labels = ActivityMatrixWindows.Select(w => english
            ? $"{w.Lower}–{w.Upper} hours" : $"{w.Lower}–{w.Upper} Stunden").ToArray();
        var selected = ActivityMatrixLag.SelectedIndex;
        if (ActivityMatrixLag.Items.Count != labels.Length ||
            (selected >= 0 && ActivityMatrixLag.Items[selected]?.ToString() != labels[selected]))
        {
            updatingActivityMatrixLag = true;
            try
            {
                ActivityMatrixLag.Items.Clear();
                foreach (var label in labels) ActivityMatrixLag.Items.Add(label);
                ActivityMatrixLag.SelectedIndex = selected < 0 ? 2 : selected;
            }
            finally { updatingActivityMatrixLag = false; }
        }
        if (ActivityMatrixLag.SelectedIndex < 0) return;

        var (lower, upper) = ActivityMatrixWindows[ActivityMatrixLag.SelectedIndex];
        ActivityMatrixNote.Text = english
            ? "Each cell shows condition entries with the symptom / entries where it was assessed, in the chosen interval after a completed activity. The Total column counts each condition entry once, even if several activity types apply. Click a heading to sort by the symptom count before the slash. Counts indicate timing, not cause."
            : "Jedes Feld zeigt Zustandseinträge mit Symptom / Einträge, in denen es beurteilt wurde, im gewählten Abstand nach einer abgeschlossenen Aktivität. Gesamt zählt jeden Zustandseintrag nur einmal, auch wenn mehrere Aktivitätsarten passen. Ein Klick auf die Überschrift sortiert nach der Zahl vor dem Schrägstrich. Die Zahlen zeigen zeitliche Nähe, keine Ursache.";
        ActivityMatrixTable.Children.Clear();
        if (analysisStates.Count == 0)
        {
            ActivityMatrixTable.Children.Add(new TextBlock { Text = english
                ? "No condition entries in the selected period." : "Keine Zustandseinträge im gewählten Zeitraum." });
            return;
        }

        var exposed = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        var start = analysisStates[0].Start.AddHours(-upper);
        var end = analysisStates[^1].Start;
        foreach (var entry in entries.Where(e => e.Kind == "Aktivität" && e.End is { } finish &&
                     finish >= start && finish <= end))
        {
            IntervalData? data;
            try { data = JsonSerializer.Deserialize<IntervalData>(entry.Data); }
            catch (JsonException) { continue; }
            if (data?.Dimensions is not { Count: > 0 }) continue;
            for (var index = 0; index < analysisStates.Count; index++)
            {
                var hours = (analysisStates[index].Start - entry.End!.Value).TotalHours;
                if (hours < lower || hours >= upper) continue;
                foreach (var dimension in data.Dimensions.Distinct())
                {
                    if (!exposed.TryGetValue(dimension, out var indices))
                        exposed[dimension] = indices = new HashSet<int>();
                    indices.Add(index);
                }
            }
        }
        var dimensions = exposed.Keys.OrderBy(T, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (dimensions.Length == 0)
        {
            ActivityMatrixTable.Children.Add(new TextBlock { Text = english
                ? "No assessed condition entries after a completed activity in this interval."
                : "In diesem Zeitfenster folgen keine Zustandseinträge auf eine abgeschlossene Aktivität." });
            return;
        }
        var allIndices = exposed.Values.SelectMany(set => set).ToHashSet();
        var rows = SymptomNames.Select(name =>
        {
            var assessed = allIndices.Count(i => analysisStates[i].Data.SymptomSeverity(name) >= 0);
            var present = allIndices.Count(i => analysisStates[i].Data.SymptomSeverity(name) > 0);
            var counts = dimensions.Select(dimension =>
            {
                var indices = exposed[dimension];
                return (Present: indices.Count(i => analysisStates[i].Data.SymptomSeverity(name) > 0),
                    Assessed: indices.Count(i => analysisStates[i].Data.SymptomSeverity(name) >= 0));
            }).ToArray();
            return new { Name = name, Assessed = assessed, Present = present, Counts = counts };
        }).Where(row => row.Present > 0).ToArray();
        if (activityMatrixSortKey != "Symptom" && activityMatrixSortKey != "Gesamt" &&
            !dimensions.Contains(activityMatrixSortKey))
        { activityMatrixSortKey = "Gesamt"; activityMatrixSortDescending = true; }
        var sortDimension = Array.IndexOf(dimensions, activityMatrixSortKey);
        var orderedRows = activityMatrixSortKey == "Symptom"
            ? (activityMatrixSortDescending
                ? rows.OrderByDescending(row => T(row.Name), StringComparer.CurrentCultureIgnoreCase)
                : rows.OrderBy(row => T(row.Name), StringComparer.CurrentCultureIgnoreCase))
            : (activityMatrixSortDescending
                ? rows.OrderByDescending(row => sortDimension < 0 ? row.Present : row.Counts[sortDimension].Present)
                : rows.OrderBy(row => sortDimension < 0 ? row.Present : row.Counts[sortDimension].Present))
                .ThenBy(row => T(row.Name), StringComparer.CurrentCultureIgnoreCase);

        var widths = AnalysisWidths("matrix:" + string.Join("|", dimensions),
            new[] { 230, 105 }.Concat(dimensions.Select(d => Math.Clamp(38 + T(d).Length * 9, 100, 220))).ToArray());
        widths[1] = Math.Max(widths[1], 100);
        var tableKey = "matrix:" + string.Join("|", dimensions);
        ActivityMatrixTable.Children.Add(MatrixRow("Symptom",
            english ? "Total" : "Gesamt", dimensions.Select(T).ToArray(), widths, true, tableKey,
            new[] { "Symptom", "Gesamt" }.Concat(dimensions).ToArray()));
        foreach (var row in orderedRows)
        {
            var values = row.Counts.Select(count => count.Assessed == 0 ? "–" :
                $"{count.Present}/{count.Assessed}").ToArray();
            ActivityMatrixTable.Children.Add(MatrixRow(T(row.Name), $"{row.Present}/{row.Assessed}", values, widths, false, tableKey));
        }
        if (rows.Length == 0)
            ActivityMatrixTable.Children.Add(new TextBlock { Text = english
                ? "No positive symptom values in these condition entries."
                : "In diesen Zustandseinträgen wurden keine Symptome mit Stärke über 0 erfasst." });
    }

    private Grid MatrixRow(string symptom, string total, string[] values, int[] widths, bool header, string tableKey,
        string[]? sortKeys = null)
    {
        var headerBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 224, 236, 242));
        var grid = new Grid { MinHeight = 34, Background = header ? headerBrush : null };
        foreach (var width in widths) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        var labels = new[] { symptom, total }.Concat(values).ToArray();
        for (var index = 0; index < labels.Length; index++)
        {
            FrameworkElement cell;
            if (header && sortKeys is not null)
            {
                var key = sortKeys[index];
                var caption = labels[index] + (activityMatrixSortKey == key ?
                    (activityMatrixSortDescending ? "  ↓" : "  ↑") : "");
                var heading = new Border { Padding = new Thickness(9, 6, 12, 6), Background = headerBrush,
                    Child = new TextBlock { Text = caption, TextWrapping = TextWrapping.NoWrap,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold } };
                heading.Tapped += (_, _) =>
                {
                    activityMatrixSortDescending = activityMatrixSortKey == key ? !activityMatrixSortDescending : key != "Symptom";
                    activityMatrixSortKey = key;
                    RefreshActivityMatrix();
                };
                cell = heading;
            }
            else cell = new Border { Padding = new Thickness(9, 6, 9, 6),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 247, 250, 252)),
                Child = new TextBlock { Text = labels[index], TextWrapping = TextWrapping.Wrap } };
            Grid.SetColumn(cell, index);
            grid.Children.Add(cell);
            if (header)
            {
                var column = index;
                var grip = new Thumb { Width = 9, HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = headerBrush };
                grip.DragDelta += (_, args) => ResizeAnalysisColumn(ActivityMatrixTable, tableKey, column, args.HorizontalChange);
                grip.DragCompleted += (_, _) => SaveAnalysisWidths();
                Grid.SetColumn(grip, index);
                grid.Children.Add(grip);
            }
        }
        return grid;
    }
}
