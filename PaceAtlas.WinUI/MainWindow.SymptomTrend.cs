using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private void RefreshSymptomTrend()
    {
        if (SymptomTrendNeedle is null || entries is null) return;
        var today = DateTime.Today;
        var daily = new List<(DateTime Day, double Score, double Coverage)>();
        var measurements = new List<(DateTime Day, double Score, int Coverage)>();
        foreach (var entry in entries.Where(entry => entry.Kind == "Zustand" &&
                     entry.Start.Date >= today.AddDays(-13) && entry.Start.Date <= today))
        {
            StateData? state;
            try { state = JsonSerializer.Deserialize<StateData>(entry.Data); }
            catch (JsonException) { continue; }
            if (state is null) continue;
            var values = state.Symptoms.Keys.Select(state.SymptomSeverity).Where(value => value >= 0).ToArray();
            if (values.Length > 0) measurements.Add((entry.Start.Date, values.Average(), values.Length));
        }
        foreach (var group in measurements.GroupBy(value => value.Day))
            daily.Add((group.Key, group.Average(value => value.Score), group.Average(value => value.Coverage)));

        var boundary = today.AddDays(-6);
        var previous = daily.Where(day => day.Day < boundary).ToArray();
        var current = daily.Where(day => day.Day >= boundary).ToArray();
        var enough = previous.Length >= 2 && current.Length >= 2;
        var oldScore = enough ? previous.Average(day => day.Score) : 0;
        var newScore = enough ? current.Average(day => day.Score) : 0;
        var oldCoverage = enough ? previous.Average(day => day.Coverage) : 0;
        var newCoverage = enough ? current.Average(day => day.Coverage) : 0;
        enough &= oldCoverage > 0 && newCoverage >= oldCoverage * 0.5 && oldCoverage >= newCoverage * 0.5;
        var difference = newScore - oldScore;
        var change = !enough ? 0 : difference <= -0.3 ? -1 : difference >= 0.3 ? 1 : 0;
        var color = !enough ? "#D8E2E9" : change < 0 ? "#8DD7A0" : change > 0 ? "#F09A79" : "#F4BC68";
        SymptomTrendNeedle.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255,
            Convert.ToByte(color.Substring(1, 2), 16), Convert.ToByte(color.Substring(3, 2), 16),
            Convert.ToByte(color.Substring(5, 2), 16)));
        SymptomTrendRotation.Angle = !enough || change == 0 ? 90 : change < 0 ? 0 : 180;
        var english = selectedLanguage == "en";
        var description = !enough
            ? (english ? "Trend unclear: too few comparable days or too few assessed symptoms."
                : "Tendenz unklar: zu wenige vergleichbare Tage oder beurteilte Symptome.")
            : (english
                ? $"{(change < 0 ? "Improving" : change > 0 ? "Worsening" : "Stable")} · Last 7 days compared with the preceding 7 days. Average severity per assessed symptom: {oldScore:0.0} → {newScore:0.0} (0–4)."
                : $"{(change < 0 ? "Wird besser" : change > 0 ? "Wird schlechter" : "Gleichbleibend")} · Letzte 7 Tage verglichen mit den 7 Tagen davor. Mittlere Stärke je beurteiltem Symptom: {oldScore:0.0} → {newScore:0.0} (0–4).")
              + (english ? " This is an estimate from your entries, not a forecast."
                  : " Das ist eine Einschätzung aus deinen Einträgen, keine Vorhersage.");
        ToolTipService.SetToolTip(SymptomTrendNeedle, description);
    }
}
