using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private sealed class TabOrderSettings
    {
        public List<string> Main { get; set; } = [];
        public List<string> Medication { get; set; } = [];
    }

    private static string TabOrderPath => System.IO.Path.Combine(WinUiSettingsFolder, "tab-order.json");
    private bool restoringTabOrder;

    private void RestoreTabOrder()
    {
        restoringTabOrder = true;
        try
        {
            if (!File.Exists(TabOrderPath)) return;
            var settings = JsonSerializer.Deserialize<TabOrderSettings>(File.ReadAllText(TabOrderPath));
            if (settings is null) return;
            RestoreOrder(MainTabs, settings.Main);
            RestoreOrder(MedicationTabs, settings.Medication);
            // A rearranged tab strip must not make a different form the startup page.
            MainTabs.SelectedItem = StateTab;
            MedicationTabs.SelectedItem = IntakeTab;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Keep the default order when saved preferences are unavailable.
        }
        finally { restoringTabOrder = false; }
    }

    private static void RestoreOrder(TabView view, IEnumerable<string>? saved)
    {
        if (saved is null) return;
        var tabs = view.TabItems.OfType<TabViewItem>().ToDictionary(tab => tab.Name);
        int target = 0;
        foreach (var name in saved.Distinct(StringComparer.Ordinal))
        {
            if (!tabs.TryGetValue(name, out var tab)) continue;
            int current = view.TabItems.IndexOf(tab);
            if (current != target)
            {
                view.TabItems.RemoveAt(current);
                view.TabItems.Insert(target, tab);
            }
            target++;
        }
    }

    private void MainTabs_TabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
    {
        if (!restoringTabOrder) DispatcherQueue.TryEnqueue(SaveTabOrder);
    }

    private void MedicationTabs_TabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
    {
        if (!restoringTabOrder) DispatcherQueue.TryEnqueue(SaveTabOrder);
    }

    private void SaveTabOrder()
    {
        try
        {
            SaveJson(TabOrderPath, new TabOrderSettings
            {
                Main = MainTabs.TabItems.OfType<TabViewItem>().Select(tab => tab.Name).ToList(),
                Medication = MedicationTabs.TabItems.OfType<TabViewItem>().Select(tab => tab.Name).ToList()
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status.Text = selectedLanguage == "en" ? "Could not save tab order: " + ex.Message :
                "Tab-Reihenfolge konnte nicht gespeichert werden: " + ex.Message;
        }
    }
}
