namespace PaceAtlas;

// Shared wire format. Do not rename these properties: existing SQLite rows contain
// the JSON representation of StateData and must stay readable by both UIs.
public sealed class Entry
{
    public long Id { get; set; }
    public string Kind { get; set; } = "Zustand";
    public DateTime Start { get; set; } = DateTime.Now;
    public DateTime? End { get; set; }
    public string Data { get; set; } = "{}";
    public string Note { get; set; } = "";
}

public sealed class StateData
{
    public int Overall { get; set; } = 2;
    public int Pem { get; set; }
    public bool Crash { get; set; }
    // Version 1 stored default zeroes even when a symptom was never assessed.
    // Version 2: -1 = not assessed, 0 = explicitly absent, 1–4 = severity.
    public int SymptomValuesVersion { get; set; }
    public Dictionary<string, int> Symptoms { get; set; } = new();
    public int SymptomSeverity(string name)
    {
        if (!Symptoms.TryGetValue(name, out var value)) return -1;
        if (SymptomValuesVersion < 2 && value == 0) return -1;
        return Math.Clamp(value, -1, 4);
    }
    public List<string> PainLocations { get; set; } = new();
    // Optional self-report. Null means the question was left unanswered.
    public string? MostLimitingSymptom { get; set; }
    public int? Pulse { get; set; }
}

public sealed class IntervalData
{
    public List<string> Dimensions { get; set; } = new();
    public List<string> HearingProtection { get; set; } = new();
    public int Intensity { get; set; } = 1;
    public int Recovery { get; set; }
}

public sealed class ActivityTemplate
{
    public string Name { get; set; } = "";
    public List<string> Dimensions { get; set; } = new();
    public int Intensity { get; set; } = 2;
    public List<string> HearingProtection { get; set; } = new();
}

public sealed class SleepData
{
    public int Recovery { get; set; }
    public List<string> HearingProtection { get; set; } = new();
}
