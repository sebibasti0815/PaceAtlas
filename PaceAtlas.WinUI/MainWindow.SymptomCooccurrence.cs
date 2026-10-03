using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private void RefreshSymptomCooccurrence()
    {
        if (SymptomCooccurrenceTable is null || SymptomCooccurrenceNote is null) return;
        var english = selectedLanguage == "en";
        SymptomCooccurrenceTable.Children.Clear();
        SymptomCooccurrenceNote.Text = english
            ? "Each cell counts condition entries where both symptoms were present at the same recording. X marks the diagonal. White means no shared occurrence; darker blue means more entries in the selected period. Unassessed symptoms do not count. Letters match the row labels; this does not imply causation."
            : "Jedes Feld zählt Zustandseinträge, in denen beide Symptome bei derselben Erfassung vorhanden waren. X markiert die Diagonale. Weiß bedeutet kein gemeinsames Auftreten; dunkleres Blau steht für mehr Einträge im gewählten Zeitraum. Nicht beurteilte Symptome zählen nicht. Die Buchstaben entsprechen den Zeilen; daraus folgt kein ursächlicher Zusammenhang.";

        var names = SymptomNames.Concat(analysisStates.SelectMany(state => state.Data.Symptoms.Keys))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(T, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (names.Length == 0)
        {
            SymptomCooccurrenceTable.Children.Add(new TextBlock { Text = english
                ? "No symptoms available." : "Keine Symptome vorhanden." });
            return;
        }

        var counts = new int[names.Length, names.Length];
        foreach (var (_, data) in analysisStates)
        {
            var present = Enumerable.Range(0, names.Length)
                .Where(index => data.SymptomSeverity(names[index]) > 0).ToArray();
            foreach (var row in present)
                foreach (var column in present)
                    counts[row, column]++;
        }
        var maximum = 0;
        for (var row = 0; row < names.Length; row++)
            for (var column = 0; column < names.Length; column++)
                if (row != column) maximum = Math.Max(maximum, counts[row, column]);

        var header = CooccurrenceRow(names.Length);
        CooccurrenceCell(header, 0, english ? "Symptom" : "Symptom", "", 0, maximum, true, true);
        for (var column = 0; column < names.Length; column++)
            CooccurrenceCell(header, column + 1, CooccurrenceLetter(column), T(names[column]), 0, maximum, true, false);
        SymptomCooccurrenceTable.Children.Add(header);

        for (var row = 0; row < names.Length; row++)
        {
            var line = CooccurrenceRow(names.Length);
            var title = $"{CooccurrenceLetter(row)}  {T(names[row])}";
            CooccurrenceCell(line, 0, title, title, 0, maximum, true, true);
            for (var column = 0; column < names.Length; column++)
            {
                var count = counts[row, column];
                var diagonal = row == column;
                var tooltip = diagonal
                    ? T(names[row])
                    : (english ? $"{T(names[row])} + {T(names[column])}: {count} shared entries"
                        : $"{T(names[row])} + {T(names[column])}: {count} gemeinsame Einträge");
                CooccurrenceCell(line, column + 1, diagonal ? "X" : count.ToString(), tooltip,
                    diagonal ? 0 : count, maximum, false, false, diagonal);
            }
            SymptomCooccurrenceTable.Children.Add(line);
        }
    }

    private static string CooccurrenceLetter(int index)
    {
        var label = "";
        do
        {
            label = (char)('A' + index % 26) + label;
            index = index / 26 - 1;
        } while (index >= 0);
        return label;
    }

    private static Grid CooccurrenceRow(int count)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        for (var index = 0; index < count; index++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        return grid;
    }

    private static void CooccurrenceCell(Grid row, int column, string label, string tooltip,
        int count, int maximum, bool heading, bool leftAligned, bool diagonal = false)
    {
        var strength = maximum == 0 ? 0.0 : (double)count / maximum;
        var background = diagonal ? Windows.UI.Color.FromArgb(255, 255, 255, 255) : heading
            ? Windows.UI.Color.FromArgb(255, 224, 237, 244)
            : count == 0 ? Windows.UI.Color.FromArgb(255, 255, 255, 255)
            : Windows.UI.Color.FromArgb(255, (byte)(227 - 186 * strength),
                (byte)(241 - 113 * strength), (byte)(248 - 94 * strength));
        var cell = new Border
        {
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 222, 232, 238)),
            BorderThickness = new Thickness(0, 0, 1, 1),
            MinHeight = 34,
            Padding = new Thickness(leftAligned ? 8 : 2, 5, 2, 5),
            Child = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = leftAligned ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                Foreground = diagonal && Application.Current.Resources.TryGetValue("TextFillColorDisabledBrush", out var disabledBrush)
                    && disabledBrush is Brush brush ? brush
                    : new SolidColorBrush(diagonal
                        ? Windows.UI.Color.FromArgb(92, 0, 0, 0)
                        : Windows.UI.Color.FromArgb(255, 22, 39, 51)),
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
        if (!string.IsNullOrEmpty(tooltip)) ToolTipService.SetToolTip(cell, tooltip);
        Grid.SetColumn(cell, column);
        row.Children.Add(cell);
    }
}
