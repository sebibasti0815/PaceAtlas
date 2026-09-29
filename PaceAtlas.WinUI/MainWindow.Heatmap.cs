using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PaceAtlas;

namespace PaceAtlas.WinUI;

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
            ? "Each hour is split: left = highest activity intensity (light to dark blue), right = worst recorded overall condition (green to red). White = no entry. Timing does not establish cause."
            : "Jede Stunde ist geteilt: links höchste Aktivitätsintensität (hell- bis dunkelblau), rechts schlechtester dokumentierter Allgemeinzustand (grün bis rot). Weiß = kein Eintrag. Zeitliche Nähe beweist keine Ursache.")
            + (CarryEnabled ? (english
                ? " Pale color on the right = assumed condition, not a new measurement. PEM and crash are never assumed."
                : " Blasse Farbe rechts = angenommener Zustand, keine neue Messung. PEM und Crash werden nie angenommen.") : "");
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
            var dateLabel = new TextBlock { Text = date.ToString("dd.MM", culture), Width = columnWidth,
                TextAlignment = TextAlignment.Center, FontSize = 11, Foreground = captionBrush };
            Canvas.SetLeft(dateLabel, x); Canvas.SetTop(dateLabel, 3); canvas.Children.Add(dateLabel);
            var dayLabel = new TextBlock { Text = date.ToString("ddd", culture), Width = columnWidth,
                TextAlignment = TextAlignment.Center, FontSize = 11, Foreground = captionBrush };
            Canvas.SetLeft(dayLabel, x); Canvas.SetTop(dayLabel, 18); canvas.Children.Add(dayLabel);
            for (int hour = 0; hour < 24; hour++)
            {
                var slotStart = date.AddHours(hour);
                var slotEnd = slotStart.AddHours(1);
                double y = headerHeight + hour * rowHeight;
                var overlapping = activities.Where(a => a.Entry.Start < slotEnd
                    && slotEnd > threshold
                    && (a.Entry.End ?? DateTime.Now) > slotStart).ToList();
                // A small opaque overlap hides antialiasing seams at fractional DPI scales.
                var cell = new Rectangle { Width = columnWidth / 2, Height = rowHeight + 0.75,
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
        for (int hour = 0; hour < 24; hour += 2)
            Label($"{hour:00}:00", 2, headerHeight + hour * rowHeight + 3);
        // Assumed condition is a pale area on the right. It is not a new measurement.
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
                var x = labelWidth + (from.Date - first).Days * columnWidth + columnWidth / 2;
                var top = headerHeight + from.TimeOfDay.TotalHours * rowHeight;
                var height = (to - from).TotalHours * rowHeight;
                var wash = new Rectangle { Width = columnWidth / 2, Height = height, Fill = color,
                    Opacity = 0.12 };
                Canvas.SetLeft(wash, x); Canvas.SetTop(wash, top); canvas.Children.Add(wash);
                var hint = english
                    ? $"Assumed overall condition {segment.Overall}/4 from the entry at {segment.Start:MM/dd/yyyy HH:mm}; assumed until {segment.End:MM/dd/yyyy HH:mm}, maximum {CarryHours} hours. No new measurement."
                    : $"Angenommener Allgemeinzustand {segment.Overall}/4 aus dem Eintrag vom {segment.Start:dd.MM.yyyy HH:mm}; fortgeführt bis {segment.End:dd.MM.yyyy HH:mm}, höchstens {CarryHours} Stunden. Keine neue Messung.";
                ToolTipService.SetToolTip(wash, hint);
                from = to;
            }
        }
        // Recorded values occupy the right half of their actual hour. Several
        // entries in one hour use the worse overall value; event dots reflect any entry.
        foreach (var slot in analysisStates.Where(s => s.Start >= first && s.Start < today.AddDays(1))
                     .GroupBy(s => (Day: s.Start.Date, Hour: s.Start.Hour)))
        {
            var x = labelWidth + (slot.Key.Day - first).Days * columnWidth + columnWidth / 2;
            var y = headerHeight + slot.Key.Hour * rowHeight;
            var worst = Math.Clamp(slot.Max(s => s.Data.Overall), 0, 4);
            var pem = slot.Any(s => s.Data.Pem > 0);
            var crash = slot.Any(s => s.Data.Crash);
            var details = string.Join("\n", slot.Select(state =>
                $"{state.Start:dd.MM.yyyy HH:mm} · " +
                (english ? "Overall condition" : "Allgemeinzustand") + $" {state.Data.Overall}/4" +
                (state.Data.Pem > 0 ? " · PEM" : "") + (state.Data.Crash ? " · Crash" : "")));
            var condition = new Rectangle { Width = columnWidth / 2, Height = rowHeight + 0.75,
                Fill = BrushFrom(stateColors[worst]) };
            Canvas.SetLeft(condition, x); Canvas.SetTop(condition, y); canvas.Children.Add(condition);
            ToolTipService.SetToolTip(condition, details);
            void EventDot(double offset, string color)
            {
                var dot = new Ellipse { Width = 7, Height = 7, Fill = BrushFrom(color),
                    Stroke = BrushFrom("#FFFFFF"), StrokeThickness = 1 };
                Canvas.SetLeft(dot, x + offset); Canvas.SetTop(dot, y + Math.Max(1, (rowHeight - 7) / 2));
                canvas.Children.Add(dot);
                ToolTipService.SetToolTip(dot, details);
            }
            if (pem) EventDot(3, "#D3740A");
            if (crash) EventDot(pem ? 12 : 3, "#B51F2E");
        }
        var dayBorder = BrushFrom("#AFC2CE");
        for (int day = 1; day < days; day++)
        {
            var x = labelWidth + day * columnWidth;
            canvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x,
                Y2 = headerHeight + 24 * rowHeight, Stroke = dayBorder, StrokeThickness = 2.5 });
        }
        for (int day = 0; day < days; day++)
        {
            var x = labelWidth + (day + 0.5) * columnWidth;
            canvas.Children.Add(new Line { X1 = x, Y1 = headerHeight, X2 = x,
                Y2 = headerHeight + 24 * rowHeight, Stroke = borderBrush, StrokeThickness = 1 });
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
            ? "Each weekday/hour is split: left = mean daily peak activity intensity (blue), right = mean recorded overall condition (green to red). Hover for counts. Pale right half = assumed condition only when no recorded value exists. Timing does not establish cause."
            : "Jeder Wochentag und jede Stunde ist geteilt: links mittlere Tageshöchstintensität der Aktivität (blau), rechts mittlerer dokumentierter Allgemeinzustand (grün bis rot). Anzahl beim Darüberfahren. Blasse rechte Hälfte = angenommener Zustand nur ohne dokumentierten Wert. Zeitliche Nähe beweist keine Ursache.";
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
                Width = columnWidth, TextAlignment = TextAlignment.Center, FontSize = 12 };
            Canvas.SetLeft(heading, left + day * columnWidth);
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
                var activityColor = a.Count > 0 ? blues[Math.Clamp((int)Math.Round(a.Average()) - 1, 0, 3)] : "#FFFFFF";
                var conditionColor = s.Count > 0 ? conditions[Math.Clamp((int)Math.Round(s.Average(v => v.Overall)), 0, 4)] : null;
                var assumedColor = double.IsNaN(assumedMean) ? null :
                    conditions[Math.Clamp((int)Math.Round(assumedMean), 0, 4)];
                var details = english
                    ? $"{hour:00}:00–{(hour + 1):00}:00 · Days with activity: {a.Count} · Mean daily peak intensity: {(a.Count == 0 ? "—" : a.Average().ToString("0.0", culture))}/4 · Recorded conditions: {s.Count} · Their mean: {(s.Count == 0 ? "—" : s.Average(v => v.Overall).ToString("0.0", culture))}/4 · Assumed hours: {assumedHours.ToString("0.#", culture)} · Recorded PEM: {pem} · Recorded crash: {crash}"
                    : $"{hour:00}:00–{(hour + 1):00}:00 · Tage mit Aktivität: {a.Count} · Mittlere Tageshöchstintensität: {(a.Count == 0 ? "—" : a.Average().ToString("0.0", culture))}/4 · Dokumentierte Zustände: {s.Count} · Deren Mittel: {(s.Count == 0 ? "—" : s.Average(v => v.Overall).ToString("0.0", culture))}/4 · Angenommene Stunden: {assumedHours.ToString("0.#", culture)} · Dokumentiertes PEM: {pem} · Dokumentierter Crash: {crash}";
                var leftHalf = new Rectangle { Width = columnWidth / 2, Height = rowHeight + 0.75,
                    Fill = Color(activityColor) };
                Canvas.SetLeft(leftHalf, x); Canvas.SetTop(leftHalf, y); canvas.Children.Add(leftHalf);
                ToolTipService.SetToolTip(leftHalf, details);
                var rightHalf = new Rectangle { Width = columnWidth / 2, Height = rowHeight + 0.75,
                    Fill = conditionColor is not null ? Color(conditionColor) :
                        assumedColor is not null ? Tinted("#FFFFFF", assumedColor, 0.22) : Color("#FFFFFF") };
                Canvas.SetLeft(rightHalf, x + columnWidth / 2); Canvas.SetTop(rightHalf, y);
                canvas.Children.Add(rightHalf);
                ToolTipService.SetToolTip(rightHalf, details);
                void EventDot(double offset, string color)
                {
                    var dot = new Ellipse { Width = 7, Height = 7, Fill = Color(color),
                        Stroke = Color("#FFFFFF"), StrokeThickness = 1 };
                    Canvas.SetLeft(dot, x + columnWidth / 2 + offset);
                    Canvas.SetTop(dot, y + Math.Max(1, (rowHeight - 7) / 2));
                    canvas.Children.Add(dot);
                    ToolTipService.SetToolTip(dot, details);
                }
                if (pem > 0) EventDot(3, "#D3740A");
                if (crash > 0) EventDot(pem > 0 ? 12 : 3, "#B51F2E");
            }
        }
        var guide = Color("#D9E3EA");
        var dayBorder = Color("#AFC2CE");
        for (int day = 1; day < 7; day++)
        {
            var x = left + day * columnWidth;
            canvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x,
                Y2 = top + 24 * rowHeight, Stroke = dayBorder, StrokeThickness = 2.5 });
        }
        for (int day = 0; day < 7; day++)
        {
            var x = left + (day + 0.5) * columnWidth;
            canvas.Children.Add(new Line { X1 = x, Y1 = top, X2 = x,
                Y2 = top + 24 * rowHeight, Stroke = guide, StrokeThickness = 1 });
        }
        for (int hour = 0; hour < 24; hour += 2)
        {
            var tick = new TextBlock { Text = $"{hour:00}:00", FontSize = 11 };
            Canvas.SetLeft(tick, 3); Canvas.SetTop(tick, top + hour * rowHeight + 2);
            canvas.Children.Add(tick);
        }
    }
}
