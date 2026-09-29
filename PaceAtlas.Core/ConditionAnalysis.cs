using System.Globalization;

namespace PaceAtlas;

/// <summary>Analysis that can be reused from WinForms and a future WinUI UI.</summary>
public static class ConditionAnalysis
{
    private static readonly string[] OverallNames = ["gut", "leicht eingeschränkt", "mittel", "schlecht", "sehr schlecht"];
    private static readonly string[] PemNames = ["nein", "vermutet", "erkannt"];

    public static TimeSpan TotalSleepTime(IEnumerable<Entry> sleeps, DateTime threshold)
    {
        TimeSpan total = TimeSpan.Zero;
        DateTime? coveredUntil = null;
        foreach (var sleep in sleeps.Where(e => IsSleep(e) && e.End is not null && e.End > threshold)
                     .Select(e => (Start: e.Start < threshold ? threshold : e.Start, End: e.End!.Value))
                     .OrderBy(e => e.Start))
        {
            if (sleep.End <= sleep.Start) continue;
            if (coveredUntil is null || sleep.Start > coveredUntil.Value)
            {
                total += sleep.End - sleep.Start;
                coveredUntil = sleep.End;
            }
            else if (sleep.End > coveredUntil.Value)
            {
                total += sleep.End - coveredUntil.Value;
                coveredUntil = sleep.End;
            }
        }
        return total;
    }

    public static bool IsSleep(Entry entry)
    {
        if (entry.Kind == "Schlaf") return true;
        if (entry.Kind != "Ruhe") return false;
        try
        {
            var data = System.Text.Json.JsonSerializer.Deserialize<IntervalData>(entry.Data);
            return data?.Dimensions.Any(name => name is "Geschlafen" or "Schlaf") == true;
        }
        catch (System.Text.Json.JsonException) { return false; }
    }

    public static string BuildAssessment(IReadOnlyList<(DateTime Start, StateData Data)> states,
        IReadOnlyList<Entry> entries, DateTime now, string language, Func<string, string> translate)
    {
        bool german = language == "de";
        var latest = states.Where(s => s.Start <= now).OrderByDescending(s => s.Start).FirstOrDefault();
        string first;
        if (latest.Data is null)
            first = german ? "Für diesen Zeitraum ist kein Zustand dokumentiert." : "No condition has been recorded for this period.";
        else
        {
            var when = latest.Start.ToString(german ? "dd.MM.yyyy HH:mm" : "MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture);
            var condition = translate(OverallNames[Math.Clamp(latest.Data.Overall, 0, OverallNames.Length - 1)]);
            var marked = latest.Data.Crash ? (german ? " (Crash markiert)" : " (crash marked)")
                : latest.Data.Pem > 0 ? $" (PEM {translate(PemNames[Math.Clamp(latest.Data.Pem, 0, 2)])})" : "";
            first = german
                ? $"Zuletzt dokumentiert am {when}: Allgemeinzustand {condition}{marked}."
                : $"Last recorded on {when}: overall condition {condition}{marked}.";
            if (now - latest.Start > TimeSpan.FromHours(24))
                first = first.TrimEnd('.') + (german ? "; für den aktuellen Zustand fehlen neuere Angaben." : "; there is no newer information about your current condition.");
        }
        var observed = states.Where(s => s.Start <= now).ToList();
        var markedStates = observed.Where(s => s.Data.Pem > 0 || s.Data.Crash).ToList();
        var second = german
            ? $"Im gewählten Zeitraum sind {observed.Count} Zustände und {markedStates.Count} Einträge mit PEM oder Crash erfasst."
            : $"The selected period contains {observed.Count} condition records and {markedStates.Count} records marked PEM or crash.";
        string third;
        if (markedStates.Count > 0)
        {
            var mark = markedStates[^1].Start;
            var activities = entries.Count(e => e.Kind == "Aktivität" && e.Start <= mark && (e.End ?? now) >= mark.AddHours(-48));
            third = activities > 0
                ? german ? $"Vor der letzten Markierung sind innerhalb von 48 Stunden {activities} Aktivitäten dokumentiert; daraus lässt sich keine Ursache ableiten." : $"In the 48 hours before the last marked record, {activities} activities were documented; this does not establish a cause."
                : german ? "Vor der letzten Markierung sind in den vorherigen 48 Stunden keine Aktivitäten dokumentiert; fehlende Einträge bleiben unbekannt." : "No activities were documented in the 48 hours before the last marked record; missing entries remain unknown.";
        }
        else
            third = german ? "Zwischen den Einträgen ist der Zustand unbekannt; fehlende Angaben gelten nicht als Beschwerdefreiheit." : "Condition between records is unknown; missing entries do not mean absence of symptoms.";
        return string.Join(Environment.NewLine + Environment.NewLine, first, second, third);
    }
}
