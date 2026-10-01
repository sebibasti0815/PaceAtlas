using Microsoft.UI.Xaml;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private void UpdateEditingIndicators()
    {
        bool english = selectedLanguage == "en";
        bool state = editingEntryId > 0 && editingEntryKind == "Zustand";
        bool interval = editingEntryId > 0 && (editingEntryKind is "Aktivität" or "Ruhe" or "Schlaf");
        bool measure = editingEntryId > 0 && editingEntryKind == "Maßnahme";
        bool plan = editingPlanId > 0;
        var editedEntry = editingEntryId > 0 ? entries.FirstOrDefault(item => item.Id == editingEntryId) : null;
        var when = editedEntry?.Start.ToString("dd.MM.yyyy, HH:mm") ?? "";

        StateEditingBanner.Visibility = state ? Visibility.Visible : Visibility.Collapsed;
        IntervalEditingBanner.Visibility = interval ? Visibility.Visible : Visibility.Collapsed;
        MeasureEditingBanner.Visibility = measure ? Visibility.Visible : Visibility.Collapsed;
        PlanEditingBanner.Visibility = plan ? Visibility.Visible : Visibility.Collapsed;

        if (state) StateEditingText.Text = english
            ? $"You are editing the condition entry from {when}. Save will update this entry."
            : $"Du bearbeitest den Zustandseintrag vom {when} Uhr. Speichern ändert diesen Eintrag.";
        if (interval) IntervalEditingText.Text = english
            ? $"You are editing the interval from {when}. Save will update this entry."
            : $"Du bearbeitest den Zeitraum vom {when} Uhr. Speichern ändert diesen Eintrag.";
        if (measure) MeasureEditingText.Text = english
            ? $"You are editing the measure from {when}. Save will update this entry."
            : $"Du bearbeitest die Maßnahme vom {when} Uhr. Speichern ändert diesen Eintrag.";
        if (plan) PlanEditingText.Text = english
            ? "Editing a saved medication plan. Save will update it. Press New entry for another plan."
            : "Du bearbeitest einen gespeicherten Einnahmeplan. Speichern ändert ihn. Für einen neuen Plan drücke „Neuer Eintrag“.";

        StateSaveButton.Content = state ? (english ? "Save changes" : "Änderungen speichern") :
            (english ? "Save condition" : "Zustand speichern");
        IntervalSaveButton.Content = interval ? (english ? "Save changes" : "Änderungen speichern") :
            (english ? "Save interval" : "Zeitraum speichern");
        MeasureSaveButton.Content = measure ? (english ? "Save changes" : "Änderungen speichern") :
            (english ? "Save measure" : "Maßnahme speichern");
        PlanSaveButton.Content = plan ? (english ? "Save changes" : "Änderungen speichern") :
            (english ? "Save entry" : "Eintrag speichern");
    }
}
