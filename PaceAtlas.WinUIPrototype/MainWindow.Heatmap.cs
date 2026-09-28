using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PaceAtlas;

namespace PaceAtlas.WinUIPrototype;

public sealed partial class MainWindow
{
    private void DrawAnalysisHeatmap()
    {
        if (AnalysisHeatmap is null || HeatmapLegend is null) return;
        const int maximumDays = 42;
        const double labelWidth = 45, minimumColumnWidth = 52, headerHeight = 36;
        var rowHeight = HeatmapRowHeight(DailyHeatmapViewport);
        var today = DateTime.Today;
        var threshold = AnalysisThreshold();
        var first = threshold == DateTime.MinValue
            ? entries.Where(e => e.Kind is "Zustand" or "Aktivität").Select(e => e.Start.Date)
                .DefaultIfEmpty(today).Min()
            : threshold.Date;
        first = first > today ? today : first;
        var requestedStart = first;
        var clipped = (today - first).TotalDays >= maximumDays;
        if (clipped) first = today.AddDays(1 - maximumDays);
        int days = (today - first).Days + 1;
        var columnWidth = Math.Max(minimumColumnWidth,
            (Math.Max(0, DailyHeatmapViewport.ActualWidth) - labelWidth - 10) / days);
        var english = selectedLanguage == "en";
        HeatmapLegend.Text = (english
            ? "Activity intensity: light blue = low, dark blue = very high · Condition: green = good, yellow = moderate, red = poor · Orange outline = PEM, red outline = crash · White = no entry. Time proximity does not establish cause."
            : "Aktivitätsintensität: hellblau = gering, dunkelblau = sehr hoch · Zustand: grün = gut, gelb = mittel, rot = schlecht · Orange Umrandung = PEM, rote Umrandung = Crash · Weiß = kein Eintrag. Zeitliche Nähe beweist keine Ursache.")
            + (CarryEnabled ? (english
                ? $" Pale tint and a narrow color stripe = assumed last condition for at most {CarryHours} hours; dots are recorded entries. PEM and crash are never assumed."
                : $" Blasse Fläche und schmaler Farbstreifen = letzter Zustand höchstens {CarryHours} Stunden angenommen; Punkte sind echte Einträge. PEM und Crash werden nie angenommen.") : "");
        HeatmapLimitNotice.Visibility = clipped ? Visibility.Visible : Visibility.Collapsed;
        HeatmapLimitNotice.Text = clipped
            ? (english
                ? $"The selected period begins on {requestedStart:MM/dd/yyyy}; this daily heatmap can show only its last {maximumDays} days ({first:MM/dd/yyyy}–{today:MM/dd/yyyy}). Earlier days are omitted here."
                : $"Der gewählte Zeitraum beginnt am {requestedStart:dd.MM.yyyy}; diese Tages-Heatmap zeigt nur seine letzten {maximumDays} Tage ({first:dd.MM.yyyy}–{today:dd.MM.yyyy}). Frühere Tage fehlen in dieser Ansicht.")
            : "";
        var canvas = AnalysisHeatmap;
        canvas.Children.Clear();
        canvas.Width = Math.Max(DailyHeatmapViewport.ActualWidth - 2,
            labelWidth + days * columnWidth + 8);
        canvas.Height = headerHeight + 24 * rowHeight + 2;
        var activityColors = new[] { "#DCEFF4", "#A4DBE8", "#55AEC7", "#146A89" };
        var stateColors = new[] { "#41A66A", "#93BD55", "#E9B44B", "#E77B39", "#C84550" };
        Brush BrushFrom(string hex) => new SolidColorBrush(
            Windows.UI.Color.FromArgb(255, Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16)));
        var borderBrush = BrushFrom("#E1E8ED");
        var captionBrush = BrushFrom("#344556");
        void Label(string value, double x, double y)
        {
            var label = new TextBlock { Text = value, FontSize = 11, Foreground = captionBrush };
            Canvas.SetLeft(label, x); Canvas.SetTop(label, y); canvas.Children.Add(label);
        }
        var activities = new List<(Entry Entry, int Intensity)>();
        foreach (var entry in entries.Where(e => e.Kind == "Aktivität" && e.Start < today.AddDays(1)
                     && (e.End ?? DateTime.Now) > first))
        {
            int intensity;
            try { intensity = JsonSerializer.Deserialize<IntervalData>(entry.Data)?.Intensity ?? 1; }
            catch (JsonException) { intensity = 1; }
            activities.Add((entry, Math.Clamp(intensity, 1, 4)));
        }
        var culture = CultureInfo.GetCultureInfo(english ? "en-US" : "de-DE");
        for (int day = 0; day < days; day++)
        {
            var date = first.AddDays(day);
            double x = labelWidth + day * columnWidth;
            Label(date.ToString("dd.MM", culture), x + columnWidth / 2 - 16, 3);
            Label(date.ToString("ddd", culture), x + columnWidth / 2 - 10, 18);
            for (int hour = 0; hour < 24; hour++)
            {
                var slotStart = date.AddHours(hour);
                var slotEnd = slotStart.AddHours(1);
                double y = headerHeight + hour * rowHeight;
                var overlapping = activities.Where(a => a.Entry.Start < slotEnd
                    && slotEnd > threshold
                    && (a.Entry.End ?? DateTime.Now) > slotStart).ToList();
                // A small opaque overlap hides antialiasing seams at fractional DPI scales.
                var cell = new Rectangle { Width = columnWidth, Height = rowHeight + 0.75,
                    Fill = overlapping.Count == 0 ? BrushFrom("#FFFFFF") :
                        BrushFrom(activityColors[overlapping.Max(a => a.Intensity) - 1]) };
                Canvas.SetLeft(cell, x); Canvas.SetTop(cell, y); canvas.Children.Add(cell);
                if (overlapping.Count > 0)
                {
                    var details = string.Join("\n", overlapping.Select(a =>
                        $"{a.Entry.Start:HH:mm}–{(a.Entry.End ?? DateTime.Now):HH:mm} · " +
                        (english ? "Intensity" : "Intensität") + $" {a.Intensity}/4"));
                    ToolTipService.SetToolTip(cell, details);
                }
            }
        }
        for (int day = 1; day < days; day++)
        {
            var x = labelWidth + day * columnWidth;
            canvas.Children.Add(new Line { X1 = x, Y1 = headerHeight, X2 = x,
                Y2 = headerHeight + 24 * rowHeight, Stroke = borderBrush, StrokeThickness = 1, Opacity = 0.65 });
        }
        for (int hour = 0; hour < 24; hour += 2)
            Label($"{hour:00}:00", 2, headerHeight + hour * rowHeight + 3);
        // Draw inferred condition as a pale wash and a narrow color stripe. Neither
        // becomes a new state entry; the real measurement markers remain on top.
        foreach (var segment in CarrySegments())
        {
            if (segment.End <= first || segment.Start >= today.AddDays(1)) continue;
            var color = BrushFrom(stateColors[Math.Clamp(segment.Overall, 0, 4)]);
            // One translucent rectangle per day keeps the color continuous across
            // hour boundaries; drawing a translucent rectangle per hour leaves seams.
            for (var from = segment.Start > first ? segment.Start : first;
                 from < segment.End && from < today.AddDays(1);)
            {
                var to = MinTime(segment.End, from.Date.AddDays(1));
                var x = labelWidth + (from.Date - first).Days * columnWidth;
                var top = headerHeight + from.TimeOfDay.TotalHours * rowHeight;
                var height = (to - from).TotalHours * rowHeight;
                var wash = new Rectangle { Width = columnWidth, Height = height, Fill = color,
                    Opacity = 0.12, IsHitTestVisible = false };
                Canvas.SetLeft(wash, x); Canvas.SetTop(wash, top); canvas.Children.Add(wash);
                var stripe = new Rectangle { Width = 8, Height = height, Fill = color, Opacity = 0.85 };
                Canvas.SetLeft(stripe, x); Canvas.SetTop(stripe, top); canvas.Children.Add(stripe);
                var hint = english
                    ? $"Assumed overall condition {segment.Overall}/4 from the entry at {segment.Start:MM/dd/yyyy HH:mm}; assumed until {segment.End:MM/dd/yyyy HH:mm}, maximum {CarryHours} hours. No new measurement."
                    : $"Angenommener Allgemeinzustand {segment.Overall}/4 aus dem Eintrag vom {segment.Start:dd.MM.yyyy HH:mm}; fortgeführt bis {segment.End:dd.MM.yyyy HH:mm}, höchstens {CarryHours} Stunden. Keine neue Messung.";
                ToolTipService.SetToolTip(stripe, hint);
                from = to;
            }
        }
        foreach (var state in analysisStates.Where(s => s.Start >= first && s.Start < today.AddDays(1)))
        {
            var x = labelWidth + (state.Start.Date - first).Days * columnWidth + columnWidth / 2;
            var y = headerHeight + (state.Start.TimeOfDay.TotalHours) * rowHeight;
            var marker = new Ellipse { Width = 13, Height = 13,
                Fill = BrushFrom(stateColors[Math.Clamp(state.Data.Overall, 0, 4)]),
                Stroke = state.Data.Crash ? BrushFrom("#B51F2E") :
                    state.Data.Pem > 0 ? BrushFrom("#D3740A") : BrushFrom("#FFFFFF"),
                StrokeThickness = state.Data.Crash || state.Data.Pem > 0 ? 2.5 : 1 };
            Canvas.SetLeft(marker, x - 6.5); Canvas.SetTop(marker, y - 6.5);
            canvas.Children.Add(marker);
            ToolTipService.SetToolTip(marker, $"{state.Start:dd.MM.yyyy HH:mm} · " +
                (english ? "Overall condition" : "Allgemeinzustand") + $" {state.Data.Overall}/4" +
                (state.Data.Pem > 0 ? " · PEM" : "") + (state.Data.Crash ? " · Crash" : ""));
        }
    }

    private static double HeatmapRowHeight(ScrollViewer viewport) =>
        Math.Max(9, ((viewport.ActualHeight > 0 ? viewport.ActualHeight : 400) - 42) / 24);

    private void HeatmapViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender == DailyHeatmapViewport) DrawAnalysisHeatmap();
        else if (sender == WeeklyHeatmapViewport) DrawWeeklyHeatmap();
    }

    private void DrawWeeklyHeatmap()
    {
        if (WeeklyHeatmap is null || WeeklyHeatmapLegend is null) return;
        var english = selectedLanguage == "en";
        WeeklyHeatmapLegend.Text = english
            ? "Days of the same weekday and hour are combined across the selected period · Blue = mean of each day's highest activity intensity · Dot = mean recorded condition · Orange/red number = recorded PEM/crash reports. White = no activity. Hover for counts; proximity does not establish cause."
                + (CarryEnabled ? " Pale tint and a narrow stripe = mean assumed condition; the dot still represents recorded entries only." : "")
            : "Gleiche Wochentage und Stunden werden über den gewählten Zeitraum zusammengefasst · Blau = Mittel der höchsten Aktivitätsintensität je Tag · Punkt = Mittel dokumentierter Zustände · Orange/rote Zahl = dokumentierte PEM-/Crash-Meldungen. Weiß = keine Aktivität. Details beim Darüberfahren; zeitliche Nähe beweist keine Ursache."
                + (CarryEnabled ? " Blasse Fläche und schmaler Streifen = mittlerer angenommener Zustand; der Punkt zeigt weiterhin nur echte Einträge." : "");
        const double left = 48, top = 36;
        var rowHeight = HeatmapRowHeight(WeeklyHeatmapViewport);
        var columnWidth = Math.Max(46, (Math.Max(420, WeeklyHeatmapViewport.ActualWidth) - left - 6) / 7);
        var canvas = WeeklyHeatmap;
        canvas.Children.Clear();
        canvas.Width = left + 7 * columnWidth + 2;
        canvas.Height = top + 24 * rowHeight + 2;
        var active = new List<int>[7, 24];
        var states = new List<StateData>[7, 24];
        var assumed = new List<(double Overall, double Hours)>[7, 24];
        for (int day = 0; day < 7; day++)
        for (int hour = 0; hour < 24; hour++)
        {
            active[day, hour] = new List<int>();
            states[day, hour] = new List<StateData>();
            assumed[day, hour] = new List<(double Overall, double Hours)>();
        }
        int Weekday(DateTime date) => ((int)date.DayOfWeek + 6) % 7;
        var now = DateTime.Now;
        var threshold = AnalysisThreshold();
        var activitySlots = new Dictionary<(DateTime Day, int Hour), int>();
        var recordedSlots = new HashSet<(DateTime Day, int Hour)>();
        foreach (var state in analysisStates.Where(s => s.Start <= now))
        {
            states[Weekday(state.Start), state.Start.Hour].Add(state.Data);
            recordedSlots.Add((state.Start.Date, state.Start.Hour));
        }
        // A recorded state takes precedence within its own day/hour; other hours
        // contribute at most their actual fraction of an hour, without duplicating entries.
        foreach (var segment in CarrySegments())
            for (var slot = new DateTime(segment.Start.Year, segment.Start.Month,
                     segment.Start.Day, segment.Start.Hour, 0, 0);
                 slot < segment.End; slot = slot.AddHours(1))
            {
                if (recordedSlots.Contains((slot.Date, slot.Hour))) continue;
                var from = segment.Start > slot ? segment.Start : slot;
                if (from < threshold) from = threshold;
                var to = segment.End < slot.AddHours(1) ? segment.End : slot.AddHours(1);
                if (to > from)
                    assumed[Weekday(slot), slot.Hour].Add((segment.Overall, (to - from).TotalHours));
            }
        foreach (var entry in entries.Where(e => e.Kind == "Aktivität" && e.Start <= now &&
                     (e.End ?? now) > threshold))
        {
            int intensity;
            try { intensity = JsonSerializer.Deserialize<IntervalData>(entry.Data)?.Intensity ?? 1; }
            catch (JsonException) { intensity = 1; }
            var start = entry.Start > threshold ? entry.Start : threshold;
            var end = entry.End is { } finish && finish < now ? finish : now;
            if (end <= start) continue;
            // Match the daily view: overlapping intervals use the highest intensity
            // within each actual date/hour; only then average the corresponding weekdays.
            for (var slot = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
                 slot < end; slot = slot.AddHours(1))
            {
                var key = (slot.Date, slot.Hour);
                var level = Math.Clamp(intensity, 1, 4);
                activitySlots[key] = activitySlots.TryGetValue(key, out var existing)
                    ? Math.Max(existing, level) : level;
            }
        }
        foreach (var slot in activitySlots)
            active[Weekday(slot.Key.Day), slot.Key.Hour].Add(slot.Value);
        var blues = new[] { "#DCEFF4", "#A4DBE8", "#55AEC7", "#146A89" };
        var conditions = new[] { "#41A66A", "#93BD55", "#E9B44B", "#E77B39", "#C84550" };
        Brush Color(string hex) => new SolidColorBrush(Windows.UI.Color.FromArgb(255,
            Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16)));
        Brush Tinted(string background, string tint, double tintOpacity = 0.12)
        {
            byte Component(int index) => (byte)Math.Round(
                Convert.ToByte(background.Substring(index, 2), 16) * (1 - tintOpacity) +
                Convert.ToByte(tint.Substring(index, 2), 16) * tintOpacity);
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255,
                Component(1), Component(3), Component(5)));
        }
        var culture = CultureInfo.GetCultureInfo(english ? "en-US" : "de-DE");
        for (int day = 0; day < 7; day++)
        {
            var heading = new TextBlock { Text = culture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)((day + 1) % 7)),
                FontSize = 12 };
            Canvas.SetLeft(heading, left + day * columnWidth + 7);
            Canvas.SetTop(heading, 9);
            canvas.Children.Add(heading);
            for (int hour = 0; hour < 24; hour++)
            {
                var a = active[day, hour];
                var s = states[day, hour];
                var inferred = assumed[day, hour];
                var assumedHours = inferred.Sum(value => value.Hours);
                var assumedMean = assumedHours == 0 ? double.NaN :
                    inferred.Sum(value => value.Overall * value.Hours) / assumedHours;
                var pem = s.Count(value => value.Pem > 0);
                var crash = s.Count(value => value.Crash);
                var x = left + day * columnWidth;
                var y = top + hour * rowHeight;
                var background = a.Count > 0 ? blues[Math.Clamp((int)Math.Round(a.Average()) - 1, 0, 3)] : "#FFFFFF";
                var inferredColor = double.IsNaN(assumedMean) ? null :
                    conditions[Math.Clamp((int)Math.Round(assumedMean), 0, 4)];
                var rectangle = new Rectangle { Width = columnWidth, Height = rowHeight + 0.75,
                    Fill = inferredColor is null ? Color(background) : Tinted(background, inferredColor) };
                Canvas.SetLeft(rectangle, x); Canvas.SetTop(rectangle, y);
                canvas.Children.Add(rectangle);
                var details = english
                    ? $"{hour:00}:00–{(hour + 1):00}:00 · Days with activity: {a.Count} · Mean daily peak intensity: {(a.Count == 0 ? "—" : a.Average().ToString("0.0", culture))}/4 · Recorded conditions: {s.Count} · Their mean: {(s.Count == 0 ? "—" : s.Average(v => v.Overall).ToString("0.0", culture))}/4 · Assumed hours: {assumedHours.ToString("0.#", culture)} · Their mean: {(double.IsNaN(assumedMean) ? "—" : assumedMean.ToString("0.0", culture))}/4 · Recorded PEM: {pem} · Recorded crash: {crash}"
                    : $"{hour:00}:00–{(hour + 1):00}:00 · Tage mit Aktivität: {a.Count} · Mittel der Tageshöchstintensität: {(a.Count == 0 ? "—" : a.Average().ToString("0.0", culture))}/4 · Dokumentierte Zustände: {s.Count} · Deren Mittel: {(s.Count == 0 ? "—" : s.Average(v => v.Overall).ToString("0.0", culture))}/4 · Angenommene Stunden: {assumedHours.ToString("0.#", culture)} · Deren Mittel: {(double.IsNaN(assumedMean) ? "—" : assumedMean.ToString("0.0", culture))}/4 · Dokumentiertes PEM: {pem} · Dokumentierter Crash: {crash}";
                ToolTipService.SetToolTip(rectangle, details);
                if (inferredColor is not null)
                {
                    var stripe = new Rectangle { Width = 8, Height = rowHeight + 0.75,
                        Fill = Tinted(background, inferredColor, 0.85) };
                    Canvas.SetLeft(stripe, x); Canvas.SetTop(stripe, y); canvas.Children.Add(stripe);
                    ToolTipService.SetToolTip(stripe, details);
                }
                if (s.Count == 0) continue;
                var average = Math.Clamp((int)Math.Round(s.Average(v => v.Overall)), 0, 4);
                var markerSize = Math.Min(12, rowHeight - 2);
                var marker = new Ellipse { Width = markerSize, Height = markerSize, Fill = Color(conditions[average]),
                    Stroke = Color(crash > 0 ? "#B51F2E" : pem > 0 ? "#D3740A" : "#FFFFFF"),
                    StrokeThickness = 2 };
                Canvas.SetLeft(marker, x + 5); Canvas.SetTop(marker, y + (rowHeight - markerSize) / 2);
                canvas.Children.Add(marker);
                ToolTipService.SetToolTip(marker, details);
                if (pem + crash == 0) continue;
                var badge = new TextBlock { Text = $"{pem}/{crash}", FontSize = 10,
                    Foreground = Color(crash > 0 ? "#A21F2D" : "#9A5600"), FontWeight = Microsoft.UI.Text.FontWeights.Bold };
                Canvas.SetLeft(badge, x + 20); Canvas.SetTop(badge, y + (rowHeight - 13) / 2);
                canvas.Children.Add(badge);
                ToolTipService.SetToolTip(badge, details);
            }
        }
        var guide = Color("#D9E3EA");
        for (int day = 1; day < 7; day++)
        {
            var x = left + day * columnWidth;
            canvas.Children.Add(new Line { X1 = x, Y1 = top, X2 = x,
                Y2 = top + 24 * rowHeight, Stroke = guide, StrokeThickness = 1, Opacity = 0.65 });
        }
        for (int hour = 0; hour < 24; hour += 2)
        {
            var tick = new TextBlock { Text = $"{hour:00}:00", FontSize = 11 };
            Canvas.SetLeft(tick, 3); Canvas.SetTop(tick, top + hour * rowHeight + 2);
            canvas.Children.Add(tick);
        }
    }
}
