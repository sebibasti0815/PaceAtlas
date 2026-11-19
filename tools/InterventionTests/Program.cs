using PaceAtlas;
using PaceAtlas.WinUI;
using System.Text.Json;

int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
var day = new DateTime(2026, 1, 1);
Entry State(int hours, int severity, int pem = 0, bool crash = false) => new() { Kind = "Zustand", Start = day.AddHours(hours),
    Data = JsonSerializer.Serialize(new StateData { Overall = severity, Pem = pem, Crash = crash, SymptomValuesVersion = 2,
        Symptoms = new() { ["Fatigue"] = severity, ["Unknown"] = -1 } }) };
Entry Activity(int start, int? end) => new() { Kind = "Aktivität", Start = day.AddHours(start), End = end is { } e ? day.AddHours(e) : null };
InterventionPeriod Build(List<Entry> data, int from = 0, int to = 2, int nowHours = 240) =>
    InterventionAnalysis.Build(data, day.AddDays(from), day.AddDays(to), 24, 72, day.AddHours(nowHours));

var empty = Build([]);
Check(empty.Days.Count == 3 && empty.Days.All(d => d.Overall is null && d.ActivityMinutes is null && d.DelayedPemPercent is null), "Missing days must stay unknown");
Check(InterventionPeriod.Summarize(empty.Days.Select(d => d.Overall)) is null, "Empty summary");
var states = Build([State(1, 0), State(2, 4), State(25, 4)]);
Check(states.Days[0].Symptoms["Fatigue"] == 2 && !states.Days[0].Symptoms.ContainsKey("Unknown"), "Daily means and unassessed symptoms");
Check(InterventionPeriod.Summarize(states.Days.Select(d => d.Overall)) is { Mean: 3, Count: 2, Min: 2, Max: 4 }, "Equal weighting of days, observed range");
var legacy = State(1, 0); legacy.Data = "{\"Symptoms\":{\"OldZero\":0},\"SymptomValuesVersion\":1}";
Check(!Build([legacy]).Days[0].Symptoms.ContainsKey("OldZero"), "Legacy default zero is not symptom absence");
var intervals = Build([Activity(23, 26), Activity(24, 27), Activity(28, null), Activity(35, 34)]);
Check(intervals.Days[0].ActivityMinutes == 60 && intervals.Days[1].ActivityMinutes == 180, "Midnight clipping, union overlaps, ignore ongoing/invalid");
var followup = Build([Activity(0, 1), State(25, 1, 2)]);
Check(followup.Days[0].PemObserved == 1 && followup.Days[0].PemPositive == 1, "Inclusive lower lag boundary");
Check(Build([Activity(0, 1), State(73, 1, 2)]).Days[0].PemObserved == 0, "Exclusive upper lag boundary");
Check(Build([Activity(0, 1), State(25, 1, 1)]).Days[0] is { PemObserved: 1, PemPositive: 0 }, "Suspected PEM is not counted as recognized");
Check(Build([Activity(0, 1), State(25, 1, 0, true)]).Days[0].PemPositive == 1, "Crash counted");
Check(Build([Activity(0, 1), State(25, 1, 2)], nowHours: 48).Days[0].PemObserved == 0, "Incomplete follow-up excluded");
Check(Build([Activity(0, 1), State(25, 1, 2)], to: 0).Days[0].PemPositive == 1, "Follow-up beyond selected period retained");
var windowData = new List<Entry> { Activity(0, 1), State(26, 1), State(60, 2, 2) };
var shortWindow = InterventionAnalysis.Build(windowData, day, day.AddDays(2), 24, 48, day.AddDays(10));
var longWindow = InterventionAnalysis.Build(windowData, day, day.AddDays(2), 24, 72, day.AddDays(10));
Check(shortWindow.Days[0].DelayedPemPercent == 0 && longWindow.Days[0].DelayedPemPercent == 100,
    "Extending follow-up window includes later PEM and changes the PEM result");
Check(shortWindow.Days.Select(d => d.Overall).SequenceEqual(longWindow.Days.Select(d => d.Overall)) &&
    shortWindow.Days.Select(d => d.ActivityMinutes).SequenceEqual(longWindow.Days.Select(d => d.ActivityMinutes)),
    "Changing PEM window leaves overall condition and activity averages unchanged");
var repeatedPem = new List<Entry> { Activity(0, 1), State(26, 1, 2), State(60, 2, 2) };
Check(InterventionAnalysis.Build(repeatedPem, day, day, 24, 48, day.AddDays(10)).Days[0].DelayedPemPercent ==
    InterventionAnalysis.Build(repeatedPem, day, day, 24, 72, day.AddDays(10)).Days[0].DelayedPemPercent,
    "Different windows legitimately give the same result when both contain recognized PEM");
Check(Build([new Entry { Kind = "Zustand", Start = day, Data = "broken" }]).Days[0].Overall is null, "Malformed record excluded");
var phase = new InterventionPhase { Name = "7 mg", Dose = "7 mg", Start = day, End = day.AddDays(7) };
Check(InterventionAnalysis.ValidatePhases([phase, new() { Name = "Pause", Start = day.AddDays(7), Paused = true }]) is null, "Adjacent phases allowed");
Check(InterventionAnalysis.ValidatePhases([phase, new() { Name = "Overlap", Start = day.AddDays(6) }]) == "overlap", "Overlaps rejected");
Check(InterventionAnalysis.ValidatePhases([new() { Name = "Open", Start = day }, new() { Name = "Second", Start = day.AddDays(1) }]) == "overlap", "Open phase must be last");
Check(InterventionAnalysis.ValidatePhases([new() { Name = "Bad", Start = day, End = day }]) == "end", "Empty phase rejected");
var measure = new MeasureData { Name = "Trial", Phases = [phase] };
Check(InterventionAnalysis.ActivePhase(measure, day) == phase, "Phase active at inclusive start");
Check(InterventionAnalysis.ActivePhase(measure, day.AddDays(-1)) is null, "Future phase not active");
Check(InterventionAnalysis.ActivePhase(measure, day.AddDays(7)) is null, "Phase inactive at exclusive end");
var pause = new InterventionPhase { Name = "Pause", Paused = true, Start = day.AddDays(7) };
measure.Phases.Add(pause);
Check(InterventionAnalysis.ActivePhase(measure, day.AddDays(7)) == pause, "Adjacent pause becomes active at boundary");
Check(InterventionAnalysis.ActivePhase(new MeasureData(), day) is null, "Old measure without phases has no active phase");
Check(InterventionAnalysis.PhaseDates(phase, day.AddDays(10)) == (day, day.AddDays(6)), "Exclusive midnight phase end becomes previous inclusive day");
Check(InterventionAnalysis.PhaseDates(pause, day.AddDays(10).AddHours(12)) == (day.AddDays(7), day.AddDays(10)), "Open phase extends through today");
Check(InterventionAnalysis.PhaseDates(new() {Start=day.AddDays(11)}, day.AddDays(10)) is null, "Future phase has no elapsed dates");
Check(InterventionAnalysis.PhaseDates(new() {Start=day.AddHours(18)}, day.AddHours(12)) is null, "Later today is also a future phase");
Check(InterventionAnalysis.PhaseDates(new() {Start=day,End=day.AddDays(1).AddHours(12)}, day.AddDays(10)) == (day, day.AddDays(1)), "Partial final day retained");
Check(InterventionAnalysis.PhaseDates(new() {Start=day,End=day.AddDays(20)}, day.AddDays(10)) == (day, day.AddDays(10)), "Future phase end clipped to today");
Check(JsonSerializer.Deserialize<MeasureData>(JsonSerializer.Serialize(measure))!.Phases[0].Dose == "7 mg", "Phases round trip in existing entry JSON");
Check(JsonSerializer.Deserialize<MeasureData>("{\"Name\":\"Old\"}")!.Phases.Count == 0, "Old measure remains readable");
bool rejected = false;
try { InterventionAnalysis.Build([], day.AddDays(2), day, 24, 72, day.AddDays(10)); } catch (ArgumentException) { rejected = true; }
Check(rejected, "Reverse dates rejected");
var preferenceDirectory = Path.Combine(Path.GetTempPath(), "PaceAtlas-InterventionPreferences-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(preferenceDirectory);
var preferencePath = Path.Combine(preferenceDirectory, "selection.json");
try
{
    Check(InterventionPreferences.Load(preferencePath).PemWindowIndex == 1, "Absent preferences use default PEM window");
    var preferences = new InterventionPreferences { MeasureId = 42, PemWindowIndex = 0 };
    preferences.Measures[42] = new() {
        Baseline = new() { From = day, Through = day.AddDays(6), Phase = new() {Start = day, Name = "7 mg"} },
        Comparison = new() { From = day.AddDays(7), Through = day.AddDays(13) } };
    preferences.Measures[99] = new() {Baseline = new() {From = day.AddDays(-30), Through = day.AddDays(-20)}};
    Check(preferences.Save(preferencePath), "Preferences can be saved");
    var restored = InterventionPreferences.Load(preferencePath);
    Check(restored.MeasureId == 42 && restored.PemWindowIndex == 0, "Last measure and PEM window restored from disk");
    Check(restored.Measures[42].Baseline.Phase is {Name: "7 mg"} && restored.Measures[42].Baseline.Phase!.Start == day,
        "Selected phase identity round trips");
    Check(restored.Measures[42].Comparison.Phase is null && restored.Measures[42].Comparison.From == day.AddDays(7) && restored.Measures[42].Comparison.Through == day.AddDays(13),
        "Free period mode and inclusive dates round trip");
    restored.MeasureId = 99; restored.PemWindowIndex = 2;
    Check(restored.Save(preferencePath) && InterventionPreferences.Load(preferencePath).Measures[42].Baseline.From == day,
        "Replacing preference file retains other measure selections");
    Check(!File.Exists(preferencePath + ".tmp"), "No temporary preference file remains after successful save");
    File.WriteAllText(preferencePath, "{\"PemWindowIndex\":98,\"Measures\":null}");
    Check(InterventionPreferences.Load(preferencePath) is {PemWindowIndex: 1} normalized && normalized.Measures.Count == 0,
        "Invalid window index and null measure map normalized");
    File.WriteAllText(preferencePath, "invalid JSON");
    Check(InterventionPreferences.Load(preferencePath).Measures.Count == 0, "Corrupt preference file safely falls back");
    Check(!preferences.Save(preferenceDirectory), "Write failure is reported instead of throwing");
    File.WriteAllText(preferencePath, "{\"Enabled\":true,\"Hours\":48}");
    var analysisPrefs = AnalysisPreferences.Load(preferencePath);
    Check(analysisPrefs.Enabled && analysisPrefs.Hours == 48 && analysisPrefs.Period == 1,
        "Legacy carry-condition settings preserved when loading extended analysis preferences");
    analysisPrefs.Period = 4; analysisPrefs.Symptom = "Brain Fog"; analysisPrefs.Activity = "Körperlich";
    analysisPrefs.SymptomSort = 2; analysisPrefs.ActivityWindow = 3; analysisPrefs.VisualTab = 7;
    analysisPrefs.AssessmentTab = 1; analysisPrefs.MatrixSort = "Schmerzen"; analysisPrefs.MatrixDescending = false;
    analysisPrefs.LoadSort["activities"] = new() {Column = 2, Descending = true};
    Check(analysisPrefs.Save(preferencePath), "Analysis display preferences save successfully");
    var restoredAnalysis = AnalysisPreferences.Load(preferencePath);
    Check(restoredAnalysis.Period == 4 && restoredAnalysis.Enabled && restoredAnalysis.Hours == 48,
        "Global analysis period and carry state round trip");
    Check(restoredAnalysis.Symptom == "Brain Fog" && restoredAnalysis.Activity == "Körperlich" && restoredAnalysis.SymptomSort == 2 && restoredAnalysis.ActivityWindow == 3,
        "Canonical symptom and activity names, sorting and follow-up window round trip");
    Check(restoredAnalysis.VisualTab == 7 && restoredAnalysis.AssessmentTab == 1 && restoredAnalysis.MatrixSort == "Schmerzen" &&
        !restoredAnalysis.MatrixDescending && restoredAnalysis.LoadSort["activities"] is {Column: 2, Descending: true},
        "Analysis tab and table sort preferences round trip");
    File.WriteAllText(preferencePath, "{\"Period\":100,\"Hours\":99,\"SymptomSort\":42,\"ActivityWindow\":-1,\"LoadSort\":null}");
    var normalizedAnalysis = AnalysisPreferences.Load(preferencePath);
    Check(normalizedAnalysis is {Period: 1, Hours: 24, SymptomSort: 0, ActivityWindow: 2} && normalizedAnalysis.LoadSort.Count == 0,
        "Invalid analysis display choices use safe defaults");
    File.WriteAllText(preferencePath, "bad JSON");
    Check(AnalysisPreferences.Load(preferencePath).Period == 1, "Corrupt display preference file falls back safely");
}
finally
{
    // Only exact files and the unique empty test directory created above; no recursive cleanup.
    if (File.Exists(preferencePath)) File.Delete(preferencePath);
    if (File.Exists(preferencePath + ".tmp")) File.Delete(preferencePath + ".tmp");
    if (File.Exists(preferenceDirectory + ".tmp")) File.Delete(preferenceDirectory + ".tmp");
    Directory.Delete(preferenceDirectory);
}
Console.WriteLine($"PASS {checks} intervention analysis and preference persistence checks (no user database accessed)");
