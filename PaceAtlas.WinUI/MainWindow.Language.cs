using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private static string LanguagePath => Path.Combine(WinUiSettingsFolder, "language.txt");
    private string selectedLanguage = "de";
    private bool languageInitialized;
    private readonly Dictionary<object, string> originalUiLabels = new();
    private readonly Dictionary<TextBox, string> originalPlaceholders = new();
    private readonly Dictionary<TextBlock, string> originalStatus = new();
    private bool applyingLanguage;
    private bool updatingStatus;

    private static readonly Dictionary<string, string> WinUiEnglish = new(StringComparer.Ordinal)
    {
        ["ME/CFS Verlauf"] = "ME/CFS tracker", ["Geräuscheempf."] = "Sound sensitivity",
        ["Ohrgeräusche"] = "Tinnitus",
        ["Aktiv (Min.):"] = "Active (min):", ["Pause (Min.):"] = "Break (min):",
        ["Zeitraum erfassen"] = "Record interval", ["Maßnahme erfassen"] = "Record intervention",
        ["Aktivität und Ruhe"] = "Activity and rest", ["Aktivität:"] = "Activity:",
        ["Aktivitäten verwalten"] = "Manage activities", ["Ruhequalität"] = "Rest quality",
        ["Belastungsarten"] = "Activity types",
        ["Ruheformen (Mehrfachauswahl)"] = "Rest types (select multiple)",
        ["Intensität"] = "Intensity", ["Akustischer Schutz"] = "Hearing protection", ["Schutzmaßnahmen"] = "Protective measures",
        ["Erholung nach dem Schlaf"] = "Recovery after sleep",
        ["Grund / betroffene Symptome (optional)"] = "Reason / affected symptoms (optional)",
        ["Welches Symptom schränkt dich gerade am meisten ein? (optional)"] =
            "Which symptom limits you most right now? (optional)",
        ["Keine Angabe"] = "No answer",
        ["Anderer Grund (optional)"] = "Other reason (optional)",
        ["Anderer Grund (optional):"] = "Other reason (optional):",
        ["Nur wenn kein Symptom passt"] = "Only if no symptom fits",
        ["Verlauf beobachten"] = "Track progress", ["Ziel / Problem"] = "Goal / issue",
        ["Was soll sich verbessern?"] = "What should improve?",
        ["Ausgangslage (optional)"] = "Baseline (optional)",
        ["Wie war es vor Beginn der Maßnahme?"] = "How was it before starting?",
        ["Rückblick geplant am (optional)"] = "Review planned for (optional)",
        ["Uhrzeit"] = "Time", ["Zeit"] = "Time", ["Ende"] = "End",
        ["Status"] = "Status", ["Läuft"] = "Active", ["Pausiert"] = "Paused",
        ["Abgeschlossen"] = "Completed", ["Beobachtetes Ergebnis"] = "Observed outcome",
        ["Noch nicht beurteilt"] = "Not assessed yet", ["Deutlich geholfen"] = "Helped a lot",
        ["Etwas geholfen"] = "Helped somewhat", ["Unverändert"] = "Unchanged",
        ["Verschlechtert"] = "Worsened", ["Unklar"] = "Unclear",
        ["Das Ergebnis beschreibt deine Beobachtung; zeitliche Nähe allein belegt keine Ursache."] =
            "The outcome records your observation; timing alone does not establish cause.",
        ["Rückblick / Beobachtungen"] = "Review / observations",
        ["Was hat sich verändert? Gab es unerwünschte Effekte oder andere gleichzeitige Änderungen?"] =
            "What changed? Were there unwanted effects or other changes at the same time?",
        ["Notiz zur Maßnahme"] = "Intervention note", ["Laufende Maßnahmen"] = "Ongoing interventions",
        ["Läuft noch – Ende später eintragen"] = "Ongoing – enter end later",
        ["Puls:"] = "Pulse:", ["PEM:"] = "PEM:",
        ["Was ist dir sonst aufgefallen?"] = "What else did you notice?",
        ["Neuer Eintrag"] = "New entry", ["Bisheriger Verlauf"] = "History so far",
        ["Bisherigen Verlauf ausklappen"] = "Expand history",
        ["Bisherigen Verlauf einklappen"] = "Collapse history",
        ["Nur laufende"] = "Ongoing only", ["Laufende Maßnahme abschließen"] = "Complete ongoing intervention",
        ["Medikamente/Einnahmen ausblenden"] = "Hide medication/intakes",
        ["KI"] = "AI", ["Automatische Einordnung"] = "Automated assessment",
        ["Verlauf"] = "Trend",
        ["Einzelsymptom"] = "Single symptom", ["Mehrfachsymptome"] = "Multiple symptoms",
        ["Am stärksten einschränkende Symptome"] = "Most limiting symptoms",
        ["Prägende Symptome"] = "Prominent symptoms", ["Eigene Einschätzung"] = "Your own assessment",
        ["Heatmap · Tage"] = "Heatmap · days", ["Heatmap · Wochendurchschnitt"] = "Heatmap · weekly average",
        ["Zustand fortführen"] = "Carry condition forward", ["24 Stunden"] = "24 hours", ["48 Stunden"] = "48 hours",
        ["Auswertung:"] = "Analysis:", ["7 Tage"] = "7 days", ["30 Tage"] = "30 days",
        ["3 Monate"] = "3 months", ["1 Jahr"] = "1 year", ["Gesamt"] = "All time",
        ["Einnahmen am (TT.MM.JJJJ)"] = "Intakes on (DD.MM.YYYY)",
        ["Einnahmen am"] = "Intakes on",
        ["Dokumentierte Nachkäufe:"] = "Recorded purchases:",
        ["Geschätzter Wochenbedarf:"] = "Estimated weekly need:",
        ["Keine erfassten Vorräte unter dem Wochenbedarf."] = "No recorded stock below weekly demand.",
        ["Neue Bezeichnung"] = "New name", ["Auswahl entfernen"] = "Remove selection",
        ["Nicht erholsamer Schlaf"] = "Unrefreshing sleep",
        ["Neu vorbereiten"] = "Prepare new entry", ["hoch"] = "high", ["sehr hoch"] = "very high",
        ["etwas"] = "some", ["Pacing starten"] = "Start pacing", ["Pacing beenden"] = "Stop pacing",
        ["Pause machen"] = "Take a break", ["Pause beenden"] = "End break",
        ["Timer aus"] = "Timer off", ["Schlaf"] = "Sleep",
        ["Dosierung"] = "Dosage", ["Eintrag löschen"] = "Delete entry",
        ["Eintrag speichern"] = "Save entry", ["Gespeicherter Einnahmeplan"] = "Saved medication schedule",
        ["Grund ergänzen"] = "Add reason", ["Täglicher Einnahmeplan"] = "Daily medication schedule",
        ["Einnahme bearbeiten"] = "Edit medication schedule entry",
        ["Ab wann (TT.MM.JJJJ)"] = "From (DD.MM.YYYY)", ["Bis wann (TT.MM.JJJJ)"] = "Until (DD.MM.YYYY)",
        ["Läuft noch"] = "Ongoing", ["Ab wann"] = "From", ["Bis wann"] = "Until",
        ["bisher"] = "Previously",
        ["Uhrzeit (HH:mm)"] = "Time (HH:mm)", ["Warum? (Mehrfachauswahl)"] = "Why? (select multiple)",
        ["Inhalt je Packung"] = "Units per package", ["Gekaufte Packungen"] = "Packages purchased",
        ["Gezählter Bestand"] = "Counted stock", ["Bestand unbekannt"] = "Stock unknown",
        ["Nachkauf prüfen"] = "Check reorder", ["nicht erfasst"] = "not recorded",
        ["Ruheformen verwalten"] = "Manage rest types", ["Belastungsarten verwalten"] = "Manage activity types",
        ["Eintrag löschen?"] = "Delete entry?", ["Dieser Eintrag wird dauerhaft gelöscht."] = "This entry will be permanently deleted.",
        ["Backup einspielen?"] = "Restore backup?",
        ["Das Backup ersetzt sämtliche aktuellen Einträge und Medikamentendaten."] =
            "The backup replaces all current entries and medication data.",
        ["Neuer Eintrag vorbereitet."] = "New entry ready.", ["Neuer Zeitraum vorbereitet."] = "New interval ready.",
        ["Bitte Zeit als TT.MM.JJJJ und HH:mm eingeben."] = "Enter time as DD.MM.YYYY and HH:mm.",
        ["Bitte Beginn als TT.MM.JJJJ und HH:mm eingeben."] = "Enter start as DD.MM.YYYY and HH:mm.",
        ["Bitte Ende als TT.MM.JJJJ und HH:mm eingeben."] = "Enter end as DD.MM.YYYY and HH:mm.",
        ["Bitte das Datum als TT.MM.JJJJ eingeben."] = "Enter the date as DD.MM.YYYY.",
        ["Bitte eine Maßnahme angeben."] = "Enter an intervention.",
        ["Bitte die Uhrzeit als HH:mm eingeben."] = "Enter the time as HH:mm.",
        ["Bitte ein Präparat angeben."] = "Enter a product.",
        ["Bei 'Genommen' bitte tatsächliche Dosis und positive Anzahl eintragen."] =
            "For taken intakes, enter the actual dose and a positive quantity.",
        ["Bitte gültige Packungsdaten angeben."] = "Enter valid package details.",
        ["Bitte Präparat, Packungsinhalt und Anzahl der Packungen angeben."] =
            "Enter the product, units per package and number of packages.",
        ["Bitte Präparat und gezählten Bestand angeben."] = "Enter the product and counted stock.",
        ["Maßnahme gespeichert."] = "Intervention saved.", ["Zeitraum gespeichert."] = "Interval saved.",
        ["Zeitraum beendet."] = "Interval ended.", ["Eintrag gespeichert."] = "Entry saved.",
        ["Eintrag gelöscht."] = "Entry deleted.", ["Einnahmeplan gespeichert."] = "Medication schedule saved.",
        ["Einnahmen gespeichert."] = "Intakes saved.", ["Packungsdaten gespeichert."] = "Package details saved.",
        ["Nachkauf erfasst."] = "Purchase recorded.", ["Bestand gesetzt."] = "Stock set.",
        ["CSV exportiert."] = "CSV exported.", ["Backup erstellt."] = "Backup created.",
        ["Backup eingespielt."] = "Backup restored.",
    };

    private string T(string german) => selectedLanguage == "de" ? german :
        WinUiEnglish.TryGetValue(german, out var english) ? english : Localization.Translate(german, "en");

    // Controls keep their German source labels. A second language switch never translates
    // English back through a potentially ambiguous value such as "Type" or "Taken".
    private string Localized(object control, string current)
    {
        if (!originalUiLabels.TryGetValue(control, out var original)) originalUiLabels[control] = original = current;
        else if (current != original && current != EnglishLabel(original))
            originalUiLabels[control] = original = current; // status text replaced by an event handler
        return T(original);
    }

    private static string EnglishLabel(string german) =>
        WinUiEnglish.TryGetValue(german, out var english) ? english : Localization.Translate(german, "en");

    private void LocalizeTree(object? node)
    {
        if (node is null || ReferenceEquals(node, LanguageChoice)) return;
        if (node is TextBlock label && label.Tag is not string && !originalStatus.ContainsKey(label) &&
            !ReferenceEquals(label, MottoQuestion) && !ReferenceEquals(label, MottoAnswer) &&
            !ReferenceEquals(label, PacingStatus) && label.Text is string text)
            label.Text = Localized(label, text);
        if (node is ComboBox combo && combo.Header is string comboHeader)
            combo.Header = Localized(combo, comboHeader);
        if (node is NumberBox number && number.Header is string numberHeader)
            number.Header = Localized(number, numberHeader);
        if (node is CalendarDatePicker datePicker && datePicker.Header is string dateHeader)
            datePicker.Header = Localized(datePicker, dateHeader);
        if (node is TabViewItem tab && tab.Header is string tabHeader)
            tab.Header = Localized(tab, tabHeader);
        if (node is TextBox box)
        {
            if (box.Header is string boxHeader) box.Header = Localized(box, boxHeader);
            if (box.PlaceholderText is string placeholder)
            {
                if (!originalPlaceholders.TryGetValue(box, out var german))
                    originalPlaceholders[box] = german = placeholder;
                box.PlaceholderText = T(german);
            }
        }
        if (node is ContentControl content)
        {
            if (content.Content is string caption)
            {
                if (content is FrameworkElement { Tag: string canonical })
                    originalUiLabels[content] = canonical;
                content.Content = Localized(content, caption);
            }
            else if (content.Content is UIElement child) LocalizeTree(child);
        }
        if (node is Panel panel) foreach (var child in panel.Children) LocalizeTree(child);
        if (node is Border border) LocalizeTree(border.Child);
        if (node is TabView tabs) foreach (var tabItem in tabs.TabItems) LocalizeTree(tabItem);
        if (node is ItemsControl items)
            foreach (var item in items.Items) if (item is UIElement child) LocalizeTree(child);
    }

    private static void SelectTranslated(ComboBox box, IEnumerable<string> values, string language)
    {
        int selection = box.SelectedIndex;
        box.ItemsSource = values.Select(value => language == "en" ?
            WinUiEnglish.TryGetValue(value, out var english) ? english : Localization.Translate(value, "en") : value).ToArray();
        box.SelectedIndex = selection;
    }

    private void SelectVisual(ComboBox box, IReadOnlyList<string> values, bool bars, bool symptom = false)
    {
        int selection = box.SelectedIndex;
        box.ItemsSource = values.Select((value, index) =>
        {
            var level = symptom ? Math.Max(0, index - 1) : bars ? index + 1 : index;
            var marker = symptom && index == 0 ? "?" : bars
                ? new string('▰', level) + new string('▱', 4 - level)
                : new string('●', level) + new string('○', 4 - level);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
            row.Children.Add(new TextBlock { Text = marker,
                Foreground = symptom && index == 0
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 110, 120, 130))
                    : VisualScaleBrush(level, bars),
                VerticalAlignment = VerticalAlignment.Center, FontSize = 13 });
            row.Children.Add(new TextBlock { Text = T(value), VerticalAlignment = VerticalAlignment.Center });
            return row;
        }).ToArray();
        box.SelectedIndex = selection;
    }

    private void LocalizeChoices()
    {
        SelectVisual(Overall, ["gut", "leicht eingeschränkt", "mittel", "schlecht", "sehr schlecht"], false);
        SelectTranslated(Pem, ["nein", "vermutet", "erkannt"], selectedLanguage);
        SelectTranslated(IntervalKind, ["Aktivität", "Ruhe"], selectedLanguage);
        SelectVisual(IntervalIntensity, ["gering", "mittel", "hoch", "sehr hoch"], true);
        SelectTranslated(SleepRecovery, ["nicht bewertet", "keine", "etwas", "mittel", "deutlich"], selectedLanguage);
        foreach (var box in symptoms.Values) SelectVisual(box, Severities, false, symptom: true);
        RefreshLimitingSymptomOptions();
        PopulatePlanForms();
        foreach (var row in intakeRows)
        {
            foreach (var item in row.Status.Items.OfType<ComboBoxItem>())
            {
                var canonical = item.Tag?.ToString() ?? "Offen";
                ToolTipService.SetToolTip(item, T(canonical));
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, T(canonical));
            }
            var description = T(IntakeStatusLabel(row.Status.SelectedIndex));
            ToolTipService.SetToolTip(row.Status, description);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(row.Status, description);
        }
    }

    private void InitializeLanguageChoice()
    {
        try
        {
            selectedLanguage = File.Exists(LanguagePath) ? File.ReadAllText(LanguagePath).Trim() :
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }
        catch { selectedLanguage = "de"; }
        if (selectedLanguage is not ("de" or "en")) selectedLanguage = "de";
        LanguageChoice.SelectedIndex = selectedLanguage == "en" ? 1 : 0;
        languageInitialized = true;
        ApplyHeaderLanguage();
    }

    private void LanguageChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!languageInitialized || LanguageChoice.SelectedItem is not ComboBoxItem item) return;
        selectedLanguage = item.Tag?.ToString() == "en" ? "en" : "de";
        try
        {
            Directory.CreateDirectory(WinUiSettingsFolder);
            File.WriteAllText(LanguagePath, selectedLanguage);
        }
        catch { /* The language remains available for this session. */ }
        ApplyHeaderLanguage();
        ApplyUiLanguage();
    }

    private void ApplyHeaderLanguage()
    {
        MottoQuestion.Text = selectedLanguage == "en"
            ? "“Can I do what I’m doing twice in a row?”"
            : "„Kann ich das, was ich tue, zweimal hintereinander tun?“";
        MottoAnswer.Text = selectedLanguage == "en"
            ? "– if the answer is “no”, don’t do it."
            : "– ist die Antwort „nein“, tue es nicht.";
        Title = "Pace Atlas";
    }

    private void ApplyUiLanguage()
    {
        if (applyingLanguage) return;
        applyingLanguage = true;
        try
        {
            LocalizeChoices();
            LocalizeTree(Content);
            RefreshActivityTemplateChoice();
            RefreshSortableHeaderCaptions();
            if (EntryList.ContextFlyout is MenuFlyout menu)
                foreach (var item in menu.Items.OfType<MenuFlyoutItem>())
                    item.Text = Localized(item, item.Text);
            RenderGoals();
            RenderPlans();
            RenderIntakes();
            RenderStock();
            DisplayEntries();
            LocalizeAnalysisPeriod();
            RefreshAnalysis();
            UpdateEditingIndicators();
            foreach (var status in originalStatus.Keys) TranslateStatus(status);
            UpdatePacingDisplay();
            UpdateEntriesToggleHint();
        }
        finally { applyingLanguage = false; }
    }

    private async Task<ContentDialogResult> ShowTranslatedDialogAsync(ContentDialog dialog)
    {
        if (dialog.Title is string title) dialog.Title = T(title);
        if (dialog.Content is string message) dialog.Content = T(message);
        else LocalizeTree(dialog.Content);
        dialog.PrimaryButtonText = T(dialog.PrimaryButtonText);
        dialog.CloseButtonText = T(dialog.CloseButtonText);
        return await dialog.ShowAsync();
    }

    private void AttachStatusLocalization()
    {
        foreach (var label in new[] { Status, IntervalStatus, MeasureStatus, IntakeStatus, StockStatus, PlanStatus })
        {
            originalStatus[label] = label.Text ?? "";
            label.RegisterPropertyChangedCallback(TextBlock.TextProperty, (sender, _) =>
            {
                if (updatingStatus) return;
                var status = (TextBlock)sender;
                if (status.Text == StatusTranslation(originalStatus[status])) return;
                originalStatus[status] = status.Text ?? "";
                TranslateStatus(status);
            });
        }
    }

    private string StatusTranslation(string source)
    {
        if (selectedLanguage != "en") return source;
        var translated = T(source);
        if (translated != source) return translated;
        foreach (var (german, english) in new[]
        {
            ("Speichern fehlgeschlagen: ", "Save failed: "),
            ("Löschen fehlgeschlagen: ", "Delete failed: "),
            ("Backup fehlgeschlagen: ", "Backup failed: "),
            ("Nachkauf fehlgeschlagen: ", "Purchase failed: "),
            ("Wiederherstellen fehlgeschlagen: ", "Restore failed: "),
            ("CSV-Export fehlgeschlagen: ", "CSV export failed: "),
            ("Bestand konnte nicht gesetzt werden: ", "Could not set stock: "),
            ("Auswahl konnte nicht gespeichert werden: ", "Could not save options: "),
            ("Einträge konnten nicht geladen werden: ", "Could not load entries: "),
            ("PaceAtlas-Daten konnten nicht geladen werden: ", "Could not load Pace Atlas data: "),
            ("Spaltenbreiten konnten nicht gespeichert werden: ", "Could not save column widths: ")
        })
            if (source.StartsWith(german, StringComparison.Ordinal)) return english + source[german.Length..];
        return source;
    }

    private void TranslateStatus(TextBlock status)
    {
        updatingStatus = true;
        try { status.Text = StatusTranslation(originalStatus[status]); }
        finally { updatingStatus = false; }
    }
}
