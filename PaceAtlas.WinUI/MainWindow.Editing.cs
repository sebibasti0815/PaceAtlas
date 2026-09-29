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

        StateEditingBanner.Visibility = state ? Visibility.Visible : Visibility.Collapsed;
        IntervalEditingBanner.Visibility = interval ? Visibility.Visible : Visibility.Collapsed;
        MeasureEditingBanner.Visibility = measure ? Visibility.Visible : Visibility.Collapsed;
        PlanEditingBanner.Visibility = plan ? Visibility.Visible : Visibility.Collapsed;

        if (state) StateEditingText.Text = english
            ? "Editing a saved condition. Save will update this entry. Press New entry to create a separate one."
            : "Du bearbeitest einen gespeicherten Zustand. Speichern ändert diesen Eintrag. Für einen neuen Eintrag drücke „Neuer Eintrag“.";
        if (interval) IntervalEditingText.Text = english
            ? "Editing a saved interval. Save will update it. Press New entry for a separate interval."
            : "Du bearbeitest einen gespeicherten Zeitraum. Speichern ändert ihn. Für einen neuen Zeitraum drücke „Neuer Eintrag“.";
        if (measure) MeasureEditingText.Text = english
            ? "Editing a saved measure. Save will update it. Press New entry to create another."
            : "Du bearbeitest eine gespeicherte Maßnahme. Speichern ändert diesen Eintrag. Für eine neue Maßnahme drücke „Neuer Eintrag“.";
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
