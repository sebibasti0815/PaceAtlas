using System.Text.Json;

namespace PaceAtlas;

public sealed class InterventionPhase
{
    public string Name { get; set; } = "";
    public string Dose { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime? End { get; set; }
    public bool Paused { get; set; }
    public string Note { get; set; } = "";
}

public sealed record InterventionDay(DateTime Date, double? Overall, IReadOnlyDictionary<string, double> Symptoms,
    double? ActivityMinutes, int PemPositive, int PemObserved, int ActivityCount)
{
    public double? DelayedPemPercent => PemObserved == 0 ? null : 100.0 * PemPositive / PemObserved;
}

// Descriptive distribution of observed daily values, not a confidence interval or a causal estimate.
public sealed record InterventionSummary(int Count, double Mean, double Min, double Max);
public sealed record InterventionPeriod(DateTime Start, DateTime End, IReadOnlyList<InterventionDay> Days)
{
    public static InterventionSummary? Summarize(IEnumerable<double?> values)
    {
        var observed = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        return observed.Length == 0 ? null : new(observed.Length, observed.Average(), observed.Min(), observed.Max());
    }
}

public static class InterventionAnalysis
{
    // Inclusive calendar bounds for the daily analysis. Phase end itself is exclusive.
    public static (DateTime From, DateTime Through)? PhaseDates(InterventionPhase phase, DateTime now)
    {
        var from = phase.Start.Date;
        if (phase.Start > now || phase.End <= phase.Start) return null;
        var through = phase.End is { } end
            ? end.TimeOfDay == TimeSpan.Zero ? end.Date.AddDays(-1) : end.Date
            : now.Date;
        if (through > now.Date) through = now.Date;
        return through < from ? null : (from, through);
    }

    public static InterventionPhase? ActivePhase(MeasureData? measure, DateTime now) =>
        measure?.Phases?.Where(p => p.Start <= now && (p.End is null || now < p.End))
            .OrderByDescending(p => p.Start).FirstOrDefault();

    public static T? Read<T>(Entry entry) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(entry.Data); }
        catch (JsonException) { return null; }
    }

    public static string? ValidatePhases(IEnumerable<InterventionPhase> phases)
    {
        var ordered = phases.OrderBy(p => p.Start).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            if (string.IsNullOrWhiteSpace(p.Name)) return "name";
            if (p.End <= p.Start) return "end";
            if (i > 0 && (ordered[i - 1].End is null || ordered[i - 1].End > p.Start)) return "overlap";
        }
        return null;
    }

    // Both date bounds are inclusive. An unrecorded day stays null, never zero.
    public static InterventionPeriod Build(IEnumerable<Entry> source, DateTime from, DateTime through,
        int lagFromHours, int lagToHours, DateTime now)
    {
        from = from.Date; through = through.Date;
        if (through < from || (through - from).TotalDays > 3660 || through > now.Date)
            throw new ArgumentException("Invalid date range (maximum 3661 days, no future dates).");
        if (lagFromHours < 0 || lagToHours <= lagFromHours || lagToHours > 168)
            throw new ArgumentException("Invalid follow-up window.");
        var entries = source.ToList();
        var states = entries.Where(e => e.Kind == "Zustand" && e.Start <= now)
            .Select(e => (e.Start, Data: Read<StateData>(e))).Where(s => s.Data is not null).ToList();
        // Ongoing intervals are excluded: their eventual duration is not yet known.
        var activities = entries.Where(e => e.Kind == "Aktivität" && e.End is { } end && end > e.Start && end <= now).ToList();
        var days = new List<InterventionDay>();
        for (var date = from; date <= through; date = date.AddDays(1))
        {
            var next = date.AddDays(1);
            var dayStates = states.Where(s => s.Start >= date && s.Start < next).ToList();
            var symptomValues = new Dictionary<string, double>();
            foreach (var key in dayStates.SelectMany(s => s.Data!.Symptoms?.Keys.AsEnumerable() ?? Enumerable.Empty<string>()).Distinct())
            {
                var values = dayStates.Where(s => s.Data!.Symptoms is not null).Select(s => s.Data!.SymptomSeverity(key)).Where(v => v >= 0).ToArray();
                if (values.Length > 0) symptomValues[key] = values.Average();
            }
            // Clip at midnight and union overlapping intervals to avoid double-counting minutes.
            var spans = activities.Where(e => e.Start < next && e.End > date)
                .Select(e => (Start: e.Start < date ? date : e.Start, End: e.End!.Value > next ? next : e.End.Value))
                .OrderBy(e => e.Start).ToList();
            double minutes = 0; DateTime? covered = null;
            foreach (var span in spans)
            {
                var start = covered is { } c && c > span.Start ? c : span.Start;
                if (span.End > start) minutes += (span.End - start).TotalMinutes;
                if (covered is null || span.End > covered) covered = span.End;
            }
            int observed = 0, positive = 0;
            var ended = activities.Where(e => e.End >= date && e.End < next).ToList();
            foreach (var activity in ended)
            {
                var lower = activity.End!.Value.AddHours(lagFromHours);
                var upper = activity.End.Value.AddHours(lagToHours);
                if (upper > now) continue; // Do not compare a partially elapsed window with completed ones.
                var follow = states.Where(s => s.Start >= lower && s.Start < upper).ToList();
                if (follow.Count == 0) continue;
                observed++;
                if (follow.Any(s => s.Data!.Pem == 2 || s.Data!.Crash)) positive++;
            }
            days.Add(new(date, dayStates.Count == 0 ? null : dayStates.Average(s => s.Data!.Overall),
                symptomValues, spans.Count == 0 ? null : minutes, positive, observed, ended.Count));
        }
        return new(from, through, days);
    }
}
