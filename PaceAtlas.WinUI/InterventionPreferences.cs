using System.Text.Json;

namespace PaceAtlas.WinUI;

internal sealed class InterventionPhaseReference
{
    public DateTime Start { get; set; }
    public string Name { get; set; } = "";
}

internal sealed class InterventionPeriodSelection
{
    public InterventionPhaseReference? Phase { get; set; }
    public DateTime? From { get; set; }
    public DateTime? Through { get; set; }
}

internal sealed class InterventionSelection
{
    public InterventionPeriodSelection Baseline { get; set; } = new();
    public InterventionPeriodSelection Comparison { get; set; } = new();
}

internal sealed class InterventionPreferences
{
    public long MeasureId { get; set; }
    public int PemWindowIndex { get; set; } = 1;
    public Dictionary<long, InterventionSelection> Measures { get; set; } = new();

    public static InterventionPreferences Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new();
            var value = JsonSerializer.Deserialize<InterventionPreferences>(File.ReadAllText(path)) ?? new();
            value.PemWindowIndex = value.PemWindowIndex is >= 0 and <= 2 ? value.PemWindowIndex : 1;
            value.Measures ??= new();
            // Ignore malformed preference objects; no recorded health data is changed.
            foreach (var key in value.Measures.Where(p => p.Value is null || p.Value.Baseline is null || p.Value.Comparison is null).Select(p => p.Key).ToList())
                value.Measures.Remove(key);
            return value;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public bool Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Replace only after the complete new file has been written.
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this));
            File.Move(path + ".tmp", path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
