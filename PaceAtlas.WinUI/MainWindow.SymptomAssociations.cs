using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private readonly List<string> patternActivityNames = new();
    private bool updatingPatternActivities;

    private void PatternActivity_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!updatingPatternActivities && PatternSymptom.SelectedIndex >= 0 && PatternSymptom.SelectedIndex < SymptomNames.Length)
            RefreshSymptomAssociations(SymptomNames[PatternSymptom.SelectedIndex]);
    }

    private void RefreshSymptomAssociations(string symptom)
    {
        if (PatternActivity is null || PatternCombinations is null) return;
        var english = selectedLanguage == "en";
        var activities = new List<(DateTime End, string[] Dimensions)>();
        foreach (var entry in entries.Where(e => e.Kind == "Aktivität" && e.End is not null))
        {
            try
            {
                var data = JsonSerializer.Deserialize<IntervalData>(entry.Data);
                if (data?.Dimensions is { Count: > 0 })
                    activities.Add((entry.End!.Value, data.Dimensions.Distinct().ToArray()));
            }
            catch (JsonException) { /* Malformed historical data is excluded. */ }
        }
        var names = activities.SelectMany(a => a.Dimensions).Distinct()
            .OrderBy(n => T(n), StringComparer.CurrentCultureIgnoreCase).ToArray();
        var previous = PatternActivity.SelectedIndex >= 0 && PatternActivity.SelectedIndex < patternActivityNames.Count
            ? patternActivityNames[PatternActivity.SelectedIndex] : null;
        if (!patternActivityNames.SequenceEqual(names) || PatternActivity.Items.Count != names.Length ||
            names.Where((name, index) => PatternActivity.Items[index]?.ToString() != T(name)).Any())
        {
            updatingPatternActivities = true;
            try
            {
                patternActivityNames.Clear();
                patternActivityNames.AddRange(names);
                PatternActivity.Items.Clear();
                foreach (var name in names) PatternActivity.Items.Add(T(name));
                var index = Array.IndexOf(names, previous);
                if (index < 0) index = Array.IndexOf(names, "Körperlich");
                PatternActivity.SelectedIndex = index < 0 && names.Length > 0 ? 0 : index;
            }
            finally { updatingPatternActivities = false; }
        }

        PatternActivityResults.Children.Clear();
        PatternActivityNote.Text = english
            ? "Each row counts assessed condition entries at a given delay after a completed activity. The comparison includes entries without this activity in that particular window. Small counts and overlapping activities limit interpretation; timing does not establish cause."
            : "Jede Zeile zählt beurteilte Zustandseinträge mit dem angegebenen Abstand nach dem Ende einer Aktivität. Der Vergleich umfasst Einträge ohne diese Aktivität im jeweiligen Zeitfenster. Kleine Fallzahlen und überlappende Aktivitäten schränken die Aussagekraft ein; zeitliche Nähe belegt keine Ursache.";
        if (PatternActivity.SelectedIndex >= 0 && PatternActivity.SelectedIndex < names.Length)
        {
            var name = names[PatternActivity.SelectedIndex];
            var ends = activities.Where(a => a.Dimensions.Contains(name)).Select(a => a.End).ToArray();
            var assessed = analysisStates.Select(s => (s.Start, Value: s.Data.SymptomSeverity(symptom)))
                .Where(s => s.Value >= 0).ToArray();
            if (assessed.Length == 0)
                PatternActivityResults.Children.Add(new TextBlock { Text = english
                    ? "No assessed entries for the selected symptom." :
                      "Für das gewählte Symptom liegen keine beurteilten Einträge vor." });
            foreach (var (lower, upper) in new[] { (0, 6), (6, 24), (24, 48), (48, 72) })
            {
                if (assessed.Length == 0) break;
                var with = assessed.Where(s => ends.Any(end =>
                    (s.Start - end).TotalHours >= lower && (s.Start - end).TotalHours < upper)).ToArray();
                var without = assessed.Length - with.Length;
                var withSymptoms = with.Count(s => s.Value > 0);
                var withoutSymptoms = assessed.Count(s => s.Value > 0) - withSymptoms;
                var description = with.Length == 0
                    ? (english ? $"{lower}–{upper} hours afterward: no assessed condition entries."
                        : $"{lower}–{upper} Stunden danach: keine beurteilten Zustandseinträge.")
                    : (english
                        ? $"{lower}–{upper} hours afterward: {withSymptoms} of {with.Length} entries with {T(symptom)}; outside this window: {withoutSymptoms} of {without}."
                        : $"{lower}–{upper} Stunden danach: {withSymptoms} von {with.Length} Einträgen mit {T(symptom)}; ohne diese Aktivität im Zeitfenster: {withoutSymptoms} von {without}.");
                PatternActivityResults.Children.Add(new TextBlock { Text = description,
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap, Margin = new Microsoft.UI.Xaml.Thickness(0, 3, 0, 3) });
            }
        }
        else
            PatternActivityResults.Children.Add(new TextBlock { Text = english
                ? "No completed activities with a recorded type." : "Keine abgeschlossenen Aktivitäten mit erfasster Art." });

        PatternCombinations.Children.Clear();
        PatternCombinationNote.Text = english
            ? "Other symptoms in the same condition entry as the selected symptom. The denominator includes only entries where both were assessed. This shows co-occurrence, not a causal link."
            : "Andere Beschwerden im selben Zustandseintrag wie das gewählte Symptom. Der Nenner enthält nur Einträge, in denen beide beurteilt wurden. Gemeinsames Auftreten ist kein ursächlicher Zusammenhang.";
        var combinations = SymptomNames.Where(other => other != symptom).Select(other =>
        {
            var assessed = analysisStates.Where(s => s.Data.SymptomSeverity(symptom) > 0 &&
                s.Data.SymptomSeverity(other) >= 0).ToArray();
            return new { Name = other, Denominator = assessed.Length,
                Count = assessed.Count(s => s.Data.SymptomSeverity(other) > 0) };
        }).Where(c => c.Count > 0).OrderByDescending(c => c.Count)
            .ThenByDescending(c => (double)c.Count / c.Denominator).Take(12).ToArray();
        foreach (var combination in combinations)
            PatternCombinations.Children.Add(PatternRow(T(combination.Name),
                english ? $"{combination.Count}/{combination.Denominator} shared entries" :
                    $"{combination.Count}/{combination.Denominator} gemeinsame Einträge",
                100.0 * combination.Count / combination.Denominator));
        if (combinations.Length == 0)
            PatternCombinations.Children.Add(new TextBlock { Text = english
                ? "No co-occurring symptoms recorded." : "Keine gemeinsam auftretenden Symptome erfasst." });
    }
}
