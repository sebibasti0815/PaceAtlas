using System.Text.Json;

namespace PaceAtlas.WinUI;

internal sealed class AnalysisSortPreference
{
    public int Column { get; set; }
    public bool Descending { get; set; }
}

internal sealed class AnalysisPreferences
{
    // Same names as the previous analysis-display.json, for backwards compatibility.
    public bool Enabled { get; set; }
    public int Hours { get; set; } = 24;
    public int Period { get; set; } = 1;
    public string? Symptom { get; set; }
    public string? Activity { get; set; }
    public int SymptomSort { get; set; }
    public int ActivityWindow { get; set; } = 2;
    public int VisualTab { get; set; }
    public int AssessmentTab { get; set; }
    public string MatrixSort { get; set; } = "Gesamt";
    public bool MatrixDescending { get; set; } = true;
    public Dictionary<string, AnalysisSortPreference> LoadSort { get; set; } = new();

    public static AnalysisPreferences Load(string path)
    {
        try
        {
            var p = File.Exists(path) ? JsonSerializer.Deserialize<AnalysisPreferences>(File.ReadAllText(path)) ?? new() : new();
            p.Period = p.Period is >= 0 and <= 4 ? p.Period : 1;
            p.Hours = p.Hours == 48 ? 48 : 24;
            p.SymptomSort = p.SymptomSort is >= 0 and <= 3 ? p.SymptomSort : 0;
            p.ActivityWindow = p.ActivityWindow is >= 0 and <= 3 ? p.ActivityWindow : 2;
            p.LoadSort ??= new(); p.MatrixSort ??= "Gesamt";
            return p;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public bool Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this));
            File.Move(path + ".tmp", path, true); return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
