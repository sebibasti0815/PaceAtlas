using System.Text.Json;

namespace PaceAtlas;

/// <summary>Creates the existing condition-entry format without depending on a UI framework.</summary>
public static class ConditionEntryFactory
{
    public static Entry Create(DateTime time, int overall, int pem, bool crash, int? pulse,
        IReadOnlyDictionary<string, int> symptoms, IEnumerable<string> painLocations,
        string? note, long id = 0)
    {
        ArgumentNullException.ThrowIfNull(symptoms);
        ArgumentNullException.ThrowIfNull(painLocations);
        var data = new StateData
        {
            Overall = overall,
            Pem = pem,
            Crash = crash,
            Pulse = pulse,
            SymptomValuesVersion = 2,
            Symptoms = symptoms.ToDictionary(pair => pair.Key, pair => pair.Value),
            PainLocations = painLocations.ToList()
        };
        return new Entry { Id = id, Kind = "Zustand", Start = time,
            Data = JsonSerializer.Serialize(data), Note = note ?? "" };
    }
}
