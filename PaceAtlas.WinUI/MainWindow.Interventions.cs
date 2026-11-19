using System.Globalization;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private List<InterventionPhase> measurePhaseDraft = new();
    private bool refreshingInterventions;
    private readonly DispatcherTimer interventionEvaluationTimer = new() {Interval = TimeSpan.FromMilliseconds(200)};
    private bool interventionEvaluationTimerInitialized;
    private InterventionPreferences? interventionPreferences;
    private static string InterventionPreferencesPath => System.IO.Path.Combine(WinUiSettingsFolder, "intervention-analysis.json");
    private Grid? interventionComparisonGrid;
    private readonly List<string> unavailableInterventionComparisons = new();
    private string phaseHistorySignature = "";
    private string PhaseHistorySignature() => string.Join(";", entries.Where(e => e.Kind == "Maßnahme")
        .Select(e => { var data = InterventionAnalysis.Read<MeasureData>(e); var phase = InterventionAnalysis.ActivePhase(data, DateTime.Now);
            return $"{e.Id}:{phase?.Start:O}:{phase?.End:O}"; }));

    private void RefreshPhaseHistory()
    {
        if (phaseHistorySignature != PhaseHistorySignature()) { DisplayEntries(); RefreshInterventionMeasures(); }
    }

    private string MeasurePhaseHistoryText(MeasureData? measure)
    {
        var phases = measure?.Phases?.OrderBy(p => p.Start).ToList();
        if (phases is null || phases.Count == 0) return "";
        var active = InterventionAnalysis.ActivePhase(measure, DateTime.Now);
        if (active is null) return N($"{phases.Count} Phasen · aktuell keine Phase aktiv", $"{phases.Count} phases · no phase currently active");
        var index = phases.IndexOf(active) + 1;
        return N($"Phase {index} „{active.Name}“ läuft", $"Phase {index} ‘{active.Name}’ ongoing") +
            (active.Paused ? N(" · Pause / ohne Maßnahme", " · pause / no intervention") :
                string.IsNullOrWhiteSpace(active.Dose) ? "" : " · " + active.Dose);
    }
    private sealed record InterventionChoice(Entry Entry, MeasureData Data, string PhaseDescription)
    {
        public override string ToString() => $"{Data.Name} {Data.Dose}".Trim() + $" · {Entry.Start:g}" +
            (PhaseDescription.Length == 0 ? "" : " · " + PhaseDescription);
    }

    private void RenderMeasurePhases()
    {
        if (MeasurePhaseRows is null) return;
        MeasurePhaseRows.Children.Clear();
        foreach (var phase in measurePhaseDraft.OrderBy(p => p.Start))
        {
            var row = new Grid { ColumnSpacing = 20, HorizontalAlignment = HorizontalAlignment.Stretch };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var details = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
            var heading = InterventionText(phase.Name);
            heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            heading.FontSize = 16;
            details.Children.Add(heading);
            var variant = phase.Paused ? N("Pause / ohne Maßnahme", "Pause / no intervention") : phase.Dose;
            if (!string.IsNullOrWhiteSpace(variant)) details.Children.Add(InterventionText(variant));
            var dates = InterventionText($"{phase.Start:g} – {(phase.End is { } end ? end.ToString("g") : N("offen", "ongoing"))}");
            dates.Opacity = 0.75; details.Children.Add(dates);
            if (!string.IsNullOrWhiteSpace(phase.Note)) details.Children.Add(InterventionText(phase.Note));
            row.Children.Add(details);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var edit = new Button { Content = N("Bearbeiten", "Edit") };
            edit.Click += async (_, _) => await EditMeasurePhaseAsync(phase);
            var remove = new Button { Content = N("Entfernen", "Remove") };
            remove.Click += (_, _) => { measurePhaseDraft.Remove(phase); RenderMeasurePhases(); };
            ToolTipService.SetToolTip(edit, N("Phase bearbeiten: ", "Edit phase: ") + phase.Name);
            ToolTipService.SetToolTip(remove, N("Phase entfernen: ", "Remove phase: ") + phase.Name);
            buttons.Children.Add(edit); buttons.Children.Add(remove); Grid.SetColumn(buttons, 1); row.Children.Add(buttons);
            MeasurePhaseRows.Children.Add(new Border { Child = row, Padding = new Thickness(14, 12, 14, 12),
                BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Colors.LightGray),
                CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Stretch });
        }
    }

    private string PhaseCaption(InterventionPhase p) =>
        $"{p.Name} · {(p.Paused ? N("Pause", "Pause") : p.Dose)} · {p.Start:g} – {(p.End is { } end ? end.ToString("g") : N("offen", "ongoing"))}";

    private async void AddMeasurePhase_Click(object sender, RoutedEventArgs e) => await EditMeasurePhaseAsync(null);

    private async Task EditMeasurePhaseAsync(InterventionPhase? original)
    {
        var name = new TextBox { Header = N("Phase / Variante", "Phase / variant"), Text = original?.Name ?? "" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(name, "InterventionPhaseName");
        var dose = new TextBox { Header = N("Dosis (optional)", "Dose (optional)"), Text = original?.Dose ?? MeasureDose.Text };
        var start = new CalendarDatePicker { Width = 180, PlaceholderText = N("Datum wählen", "Choose date"), Date = new DateTimeOffset(original?.Start ?? MeasureDate.Date?.DateTime ?? DateTime.Today) };
        var startTime = new TextBox { Width = 96, MaxLength = 5, PlaceholderText = "HH:mm", Text = original?.Start.ToString("HH:mm") ?? "00:00" };
        var end = new CalendarDatePicker { Width = 180, PlaceholderText = N("Datum wählen", "Choose date"), Date = original?.End is { } at ? new DateTimeOffset(at) : null };
        var endTime = new TextBox { Width = 96, MaxLength = 5, PlaceholderText = "HH:mm", Text = original?.End?.ToString("HH:mm") ?? "" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(start, N("Beginn – Datum", "Start date"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(startTime, N("Beginn – Uhrzeit", "Start time"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(end, N("Ende – Datum (optional)", "End date (optional)"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(endTime, N("Ende – Uhrzeit", "End time"));
        var paused = new CheckBox { Content = N("Pause / ohne Maßnahme", "Pause / no intervention"), IsChecked = original?.Paused ?? false };
        var note = new TextBox { Header = N("Notiz / weitere Änderungen", "Note / other changes"), Text = original?.Note ?? "",
            PlaceholderText = N("Beobachtungen oder weitere Änderungen …", "Observations or other changes …"),
            Height = 100, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        var error = InterventionText(""); error.Visibility = Visibility.Collapsed;
        var panel = new StackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Stretch };
        var identity = new Grid { ColumnSpacing = 24 };
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        identity.Children.Add(name); Grid.SetColumn(dose, 1); identity.Children.Add(dose);
        StackPanel DateGroup(string label, CalendarDatePicker date, TextBox time)
        {
            var group = new StackPanel { Spacing = 8 };
            group.Children.Add(InterventionText(label));
            var fields = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            fields.Children.Add(date); fields.Children.Add(time); group.Children.Add(fields);
            return group;
        }
        var dates = new Grid { ColumnSpacing = 24 };
        dates.ColumnDefinitions.Add(new ColumnDefinition()); dates.ColumnDefinitions.Add(new ColumnDefinition());
        dates.Children.Add(DateGroup(N("Beginn", "Start"), start, startTime));
        var endGroup = DateGroup(N("Ende (optional)", "End (optional)"), end, endTime);
        Grid.SetColumn(endGroup, 1); dates.Children.Add(endGroup);
        foreach (var control in new UIElement[] { identity, dates, paused, note, error }) panel.Children.Add(control);
        InterventionPhase? result = null;
        var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = N("Phase bearbeiten", "Edit phase"),
            Width = Math.Min(900, Math.Max(320, ((FrameworkElement)Content).ActualWidth - 48)),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = panel, PrimaryButtonText = N("Übernehmen", "Apply"), CloseButtonText = N("Abbrechen", "Cancel") };
        dialog.Resources["ContentDialogMaxWidth"] = 900d;
        // Keep the native dialog and keyboard behavior, but align its actions like the approved mockup.
        dialog.Opened += (_, _) =>
        {
            FrameworkElement? Part(DependencyObject parent, string partName)
            {
                if (parent is FrameworkElement element && element.Name == partName) return element;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                    if (Part(VisualTreeHelper.GetChild(parent, i), partName) is { } found) return found;
                return null;
            }
            if (Part(dialog, "CommandSpace") is Grid commands && commands.ColumnDefinitions.Count == 5 &&
                Part(dialog, "PrimaryButton") is Button apply && Part(dialog, "CloseButton") is Button cancel)
            {
                commands.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                commands.ColumnDefinitions[1].Width = new GridLength(0);
                commands.ColumnDefinitions[2].Width = new GridLength(160);
                commands.ColumnDefinitions[3].Width = new GridLength(12);
                commands.ColumnDefinitions[4].Width = new GridLength(160);
                Grid.SetColumn(apply, 2); Grid.SetColumn(cancel, 4);
                apply.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 119, 137));
                apply.Foreground = new SolidColorBrush(Colors.White);
            }
        };
        dialog.PrimaryButtonClick += (dialogSender, args) =>
        {
            if (!TryCalendarTime(start, startTime, out var from) ||
                (end.Date is not null && !TryCalendarTime(end, endTime, out _)))
            { error.Visibility = Visibility.Visible; error.Text = N("Bitte Datum und Uhrzeit prüfen.", "Check dates and times."); args.Cancel = true; return; }
            DateTime? until = null;
            if (end.Date is not null) { TryCalendarTime(end, endTime, out var value); until = value; }
            var candidate = new InterventionPhase { Name = name.Text.Trim(), Dose = dose.Text.Trim(), Start = from,
                End = until, Paused = paused.IsChecked == true, Note = note.Text.Trim() };
            if (InterventionAnalysis.ValidatePhases(measurePhaseDraft.Where(p => p != original).Append(candidate)) is not null)
            { error.Visibility = Visibility.Visible; error.Text = N("Name erforderlich. Ende muss nach Beginn liegen; Phasen dürfen sich nicht überschneiden. Eine offene Phase muss zuletzt stehen.",
                "Name required. End must follow start; phases must not overlap. An open phase must be last."); args.Cancel = true; return; }
            result = candidate;
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || result is null) return;
        if (original is not null) measurePhaseDraft.Remove(original);
        measurePhaseDraft.Add(result); RenderMeasurePhases();
        MeasureStatus.Text = N("Phase vorbereitet. Bitte Maßnahme speichern.", "Phase prepared. Save the intervention to keep it.");
    }

    private void RefreshInterventionMeasures()
    {
        if (InterventionMeasure is null || InterventionResults is null) return;
        bool firstLoad = interventionPreferences is null;
        interventionPreferences ??= InterventionPreferences.Load(InterventionPreferencesPath);
        var selectedId = (InterventionMeasure.SelectedItem as InterventionChoice)?.Entry.Id ?? interventionPreferences.MeasureId;
        refreshingInterventions = true;
        try
        {
            var options = entries.Where(e => e.Kind == "Maßnahme").OrderByDescending(e => e.Start)
                .Select(e => (Entry: e, Data: InterventionAnalysis.Read<MeasureData>(e))).Where(x => x.Data is not null)
                .Select(x => new InterventionChoice(x.Entry, x.Data!, MeasurePhaseHistoryText(x.Data))).ToList();
            InterventionMeasure.ItemsSource = options;
            InterventionMeasure.SelectedItem = options.FirstOrDefault(x => x.Entry.Id == selectedId) ?? options.FirstOrDefault();
            SetInterventionPhaseOptions();
            RestoreInterventionSelection();
            if (firstLoad) InterventionLag.SelectedIndex = interventionPreferences.PemWindowIndex;
            InterventionResults.Children.Clear();
            InterventionMessage.Visibility = Visibility.Visible;
            InterventionMessage.Text = options.Count == 0 ? N("Bitte zuerst eine Maßnahme erfassen.", "Record an intervention first.") :
                N("Auswertung wird aktualisiert …", "Updating analysis …");
        }
        finally { refreshingInterventions = false; }
        UpdateInterventionContext();
        RenderMeasurePhases();
        QueueInterventionEvaluation();
    }

    private void SetInterventionPhaseOptions()
    {
        foreach (var box in new[] { InterventionBaselinePhase, InterventionFollowPhase })
        {
            box.Items.Clear();
            box.Items.Add(new ComboBoxItem { Content = N("Freier Zeitraum", "Custom period") });
            if (InterventionMeasure.SelectedItem is InterventionChoice choice)
            {
                int index = 0;
                foreach (var phase in (choice.Data.Phases ?? []).OrderBy(p => p.Start))
                {
                    index++;
                    box.Items.Add(new ComboBoxItem { Content = $"Phase {index} · " + PhaseCaption(phase), Tag = phase });
                }
            }
            box.SelectedIndex = 0;
        }
    }

    private static void RestoreInterventionPhase(ComboBox box, InterventionPhaseReference? previous)
    {
        if (previous is null) return;
        box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(item => item.Tag is InterventionPhase p &&
            p.Start == previous.Start && p.Name == previous.Name) ?? box.Items[0];
    }

    private void InterventionMeasure_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (refreshingInterventions || InterventionBaselinePhase is null || InterventionFollowPhase is null || InterventionResults is null) return;
        refreshingInterventions = true;
        SetInterventionPhaseOptions();
        RestoreInterventionSelection();
        refreshingInterventions = false;
        UpdateInterventionContext(); InvalidateInterventionResult();
    }

    private void RestoreInterventionSelection()
    {
        if (InterventionMeasure.SelectedItem is not InterventionChoice choice) return;
        if (interventionPreferences is { } preferences && preferences.Measures.TryGetValue(choice.Entry.Id, out var saved))
        {
            RestoreInterventionPhase(InterventionBaselinePhase, saved.Baseline.Phase);
            RestoreInterventionPhase(InterventionFollowPhase, saved.Comparison.Phase);
            DateTimeOffset? Date(DateTime? value) => value is { } at ? new DateTimeOffset(at.Date) : null;
            InterventionBaselineFrom.Date = Date(saved.Baseline.From); InterventionBaselineTo.Date = Date(saved.Baseline.Through);
            InterventionFollowFrom.Date = Date(saved.Comparison.From); InterventionFollowTo.Date = Date(saved.Comparison.Through);
        }
        else SuggestInterventionDates(choice.Entry.Start);
        // Phase dates stay current (including ongoing phases through today). Deleted phases fall back to custom dates.
        ApplyInterventionPhase(InterventionBaselinePhase); ApplyInterventionPhase(InterventionFollowPhase);
    }

    private bool SaveInterventionSelection()
    {
        if (refreshingInterventions || interventionPreferences is null) return true;
        interventionPreferences.PemWindowIndex = InterventionLag.SelectedIndex is >= 0 and <= 2 ? InterventionLag.SelectedIndex : 1;
        if (InterventionMeasure.SelectedItem is InterventionChoice choice)
        {
            InterventionPeriodSelection Period(ComboBox box, CalendarDatePicker from, CalendarDatePicker to)
            {
                var phase = (box.SelectedItem as ComboBoxItem)?.Tag as InterventionPhase;
                return new() { From = from.Date?.Date, Through = to.Date?.Date,
                    Phase = phase is null ? null : new() { Start = phase.Start, Name = phase.Name } };
            }
            interventionPreferences.MeasureId = choice.Entry.Id;
            interventionPreferences.Measures[choice.Entry.Id] = new() {
                Baseline = Period(InterventionBaselinePhase, InterventionBaselineFrom, InterventionBaselineTo),
                Comparison = Period(InterventionFollowPhase, InterventionFollowFrom, InterventionFollowTo) };
        }
        return interventionPreferences.Save(InterventionPreferencesPath);
    }

    private string InterventionSaveWarning => N("Die Auswahl konnte nicht gespeichert werden. Bitte die Schreibrechte im Einstellungsordner prüfen.",
        "Could not save the selection. Check write permissions for the settings folder.");

    private void InterventionPhase_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (refreshingInterventions || InterventionResults is null || sender is not ComboBox box) return;
        refreshingInterventions = true;
        try { ApplyInterventionPhase(box); }
        finally { refreshingInterventions = false; }
        InvalidateInterventionResult();
    }

    private void ApplyInterventionPhase(ComboBox box)
    {
        bool baseline = ReferenceEquals(box, InterventionBaselinePhase);
        var from = baseline ? InterventionBaselineFrom : InterventionFollowFrom;
        var to = baseline ? InterventionBaselineTo : InterventionFollowTo;
        var hint = baseline ? InterventionBaselinePhaseHint : InterventionFollowPhaseHint;
        if ((box.SelectedItem as ComboBoxItem)?.Tag is not InterventionPhase phase)
        {
            from.IsEnabled = to.IsEnabled = true;
            hint.Text = N("Datumswerte frei wählbar", "Choose your own dates");
            return; // Switching to custom keeps the copied dates as a starting point.
        }
        from.IsEnabled = to.IsEnabled = false;
        if (InterventionAnalysis.PhaseDates(phase, DateTime.Now) is not { } range)
        {
            from.Date = to.Date = null;
            hint.Text = N("Diese Phase hat noch keinen auswertbaren Zeitraum.", "This phase has no elapsed period to evaluate yet.");
            return;
        }
        from.Date = new DateTimeOffset(range.From); to.Date = new DateTimeOffset(range.Through);
        hint.Text = N("Zeitraum aus Phase übernommen", "Date range copied from phase") +
            (phase.End is null || phase.End.Value.Date > DateTime.Today ? N(" · bis heute", " · through today") : "");
        if (phase.Start.TimeOfDay != TimeSpan.Zero || phase.End is { TimeOfDay: var time } && time != TimeSpan.Zero)
            hint.Text += N(" · Tagesauswertung einschließlich angebrochener Tage", " · daily analysis includes partial days");
    }

    private void SuggestInterventionDates(DateTime start)
    {
        var from = start.Date > DateTime.Today ? DateTime.Today : start.Date;
        var through = from.AddDays(27);
        if (through > DateTime.Today) through = DateTime.Today;
        if (through < from) through = from;
        var length = Math.Min(3661, (through - from).Days + 1);
        InterventionFollowFrom.Date = new DateTimeOffset(from); InterventionFollowTo.Date = new DateTimeOffset(through);
        InterventionBaselineTo.Date = new DateTimeOffset(from.AddDays(-1));
        InterventionBaselineFrom.Date = new DateTimeOffset(from.AddDays(-length));
    }

    private void UpdateInterventionContext()
    {
        InterventionContextLeft.Children.Clear(); InterventionContextRight.Children.Clear();
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(InterventionMeasure, N("Maßnahme", "Intervention"));
        if (InterventionMeasure.SelectedItem is not InterventionChoice choice) return;
        void Field(StackPanel column, string label, string value)
        {
            var group = new StackPanel { Spacing = 6 };
            var heading = InterventionText(label); heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            heading.FontSize = 16; heading.Tag = "LocalizedContent";
            var body = InterventionText(string.IsNullOrWhiteSpace(value) ? "—" : value); body.Tag = "UserContent";
            group.Children.Add(heading); group.Children.Add(body); column.Children.Add(group);
        }
        Field(InterventionContextLeft, N("Ziel", "Goal"), choice.Data.Goal);
        Field(InterventionContextLeft, N("Ausgangslage", "Starting situation"), choice.Data.Baseline);
        Field(InterventionContextLeft, N("Rückblick", "Review"), string.IsNullOrWhiteSpace(choice.Data.ReviewNote)
            ? N("Noch nicht festgehalten", "Not recorded yet") : choice.Data.ReviewNote);
        Field(InterventionContextRight, N("Notizen zur Maßnahme", "Intervention notes"), choice.Entry.Note);
        var phases = (choice.Data.Phases ?? []).OrderBy(p => p.Start).ToList();
        var phaseGroup = new StackPanel { Spacing = 10 };
        var phaseHeading = InterventionText(N("Phasen", "Phases")); phaseHeading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        phaseHeading.FontSize = 16; phaseHeading.Tag = "LocalizedContent"; phaseGroup.Children.Add(phaseHeading);
        for (int index = 0; index < phases.Count; index++)
        {
            var phase = phases[index];
            var details = new StackPanel { Spacing = 4 };
            var variant = phase.Paused ? N("Pause / ohne Maßnahme", "Pause / no intervention") : phase.Dose;
            var name = InterventionText($"Phase {index + 1} · {phase.Name}" + (string.IsNullOrWhiteSpace(variant) ? "" : " · " + variant));
            name.Tag = "UserContent"; details.Children.Add(name);
            var dates = InterventionText($"{phase.Start:g} – {(phase.End is { } end ? end.ToString("g") : N("offen", "ongoing"))}");
            dates.Opacity = 0.75; dates.Tag = "LocalizedContent"; details.Children.Add(dates);
            if (!string.IsNullOrWhiteSpace(phase.Note))
            {
                var note = InterventionText(""); note.Tag = "UserContent";
                note.Inlines.Add(new Run { Text = N("Notizen zur Phase: ", "Phase notes: "), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                note.Inlines.Add(new Run { Text = phase.Note }); details.Children.Add(note);
            }
            phaseGroup.Children.Add(details);
        }
        if (phases.Count == 0)
        {
            var empty = InterventionText(N("Keine Phasen erfasst", "No phases recorded"));
            empty.Tag = "LocalizedContent"; phaseGroup.Children.Add(empty);
        }
        InterventionContextRight.Children.Add(phaseGroup);
    }

    // These labels are already localized by N/T or contain user-entered text.
    private static TextBlock InterventionText(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Tag = "LocalizedContent" };

    private void InterventionDate_Changed(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args) => InvalidateInterventionResult();
    private void InterventionLag_Changed(object sender, SelectionChangedEventArgs args) => InvalidateInterventionResult();
    private void InvalidateInterventionResult()
    {
        if (InterventionResults is null || refreshingInterventions) return;
        InterventionMessage.Visibility = Visibility.Collapsed;
        InterventionMessage.Text = "";
        if (!SaveInterventionSelection()) InterventionMessage.Text += "\n" + InterventionSaveWarning;
        QueueInterventionEvaluation();
    }

    private void QueueInterventionEvaluation()
    {
        if (refreshingInterventions || interventionPreferences is null || InterventionDetails is null) return;
        if (!interventionEvaluationTimerInitialized)
        {
            interventionEvaluationTimer.Tick += (_, _) => { interventionEvaluationTimer.Stop(); EvaluateIntervention(); };
            interventionEvaluationTimerInitialized = true;
        }
        interventionEvaluationTimer.Stop(); interventionEvaluationTimer.Start();
    }

    private void EvaluateIntervention()
    {
        var expansion = InterventionDetails.Children.OfType<Expander>()
            .Where(e => e.Header is string).ToDictionary(e => (string)e.Header, e => e.IsExpanded);
        InterventionResults.Children.Clear();
        InterventionPemResults.Children.Clear(); InterventionDetails.Children.Clear();
        unavailableInterventionComparisons.Clear();
        var selectionSaved = SaveInterventionSelection();
        InterventionMessage.Visibility = Visibility.Visible;
        if (InterventionMeasure.SelectedItem is not InterventionChoice choice || InterventionBaselineFrom.Date is null ||
            InterventionBaselineTo.Date is null || InterventionFollowFrom.Date is null || InterventionFollowTo.Date is null)
        { InterventionMessage.Text = N("Bitte Maßnahme und alle vier Datumsgrenzen auswählen.", "Select an intervention and all four date bounds."); return; }
        var a = InterventionBaselineFrom.Date.Value.Date; var b = InterventionBaselineTo.Date.Value.Date;
        var c = InterventionFollowFrom.Date.Value.Date; var d = InterventionFollowTo.Date.Value.Date;
        if (a > b || c > d || b >= c || d > DateTime.Today || (b - a).TotalDays > 3660 || (d - c).TotalDays > 3660)
        { InterventionMessage.Text = N("Ausgangszeitraum muss vor dem Vergleich liegen, ohne Überschneidung. Keine Zukunftsdaten; höchstens 3661 Tage je Zeitraum.",
            "Baseline must precede comparison without overlap. No future dates; at most 3661 days per period."); return; }
        var lag = InterventionLag.SelectedIndex switch { 0 => (24, 48), 2 => (0, 24), _ => (24, 72) };
        var baseline = InterventionAnalysis.Build(entries, a, b, lag.Item1, lag.Item2, DateTime.Now);
        var follow = InterventionAnalysis.Build(entries, c, d, lag.Item1, lag.Item2, DateTime.Now);
        InterventionMessage.Text = "";
        if (baseline.Days.Count(x => x.Overall.HasValue) < 7 || follow.Days.Count(x => x.Overall.HasValue) < 7)
            InterventionMessage.Text = N("Weniger als sieben dokumentierte Zustandstage in mindestens einem Zeitraum: Der Vergleich ist lückenhaft und kann stark von einzelnen Tagen abhängen.",
                "Fewer than seven recorded condition days in at least one period: the comparison is sparse and may depend heavily on individual days.");
        if (!selectionSaved) InterventionMessage.Text += (InterventionMessage.Text.Length > 0 ? "\n" : "") + InterventionSaveWarning;
        InterventionMessage.Visibility = InterventionMessage.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        interventionComparisonGrid = new Grid { ColumnSpacing = 12, RowSpacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        for (int column = 0; column < 3; column++) interventionComparisonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        InterventionResults.Children.Add(interventionComparisonGrid);
        AddInterventionComparison(N("Allgemeinzustand", "Overall condition"), N("Tagesmittel (0–4 Punkte; niedriger = besser)", "Daily mean (0–4 points; lower = better)"), baseline, follow, x => x.Overall, N("Punkte", "points"));
        foreach (var symptom in baseline.Days.Concat(follow.Days).SelectMany(x => x.Symptoms.Keys).Distinct().OrderBy(x => x))
            AddInterventionComparison(T(symptom), N("Tagesmittel (0–4 Punkte)", "Daily mean (0–4 points)"), baseline, follow, x => x.Symptoms.TryGetValue(symptom, out var value) ? value : null, N("Punkte", "points"));
        AddInterventionComparison(N("Dokumentierte Aktivität", "Recorded activity"), N("Aktivitätsdauer (Minuten pro Tag)", "Activity duration (minutes per day)"), baseline, follow, x => x.ActivityMinutes, "min");
        AddInterventionComparison(N("PEM nach Aktivität", "PEM after activity"), N("PEM erkannt / Crash · Tagesanteil (%)", "Recognized PEM / crash · daily proportion (%)"), baseline, follow, x => x.DelayedPemPercent, N("Prozentpunkte", "percentage points"), InterventionPemResults);
        if (InterventionPemResults.Children.Count == 0)
            InterventionPemResults.Children.Add(InterventionText(N("Kein PEM-Vergleich möglich: In mindestens einem Zeitraum fehlen auswertbare Aktivitäten mit späteren Zustandseinträgen. Details stehen unter Hinweise.",
                "PEM cannot be compared: at least one period lacks usable activities with follow-up condition records. See Notes for details.")));
        if (interventionComparisonGrid.Children.Count == 0) interventionComparisonGrid.Visibility = Visibility.Collapsed;
        var coverage = new StackPanel { Spacing = 12 };
        foreach (var period in new[] { baseline, follow })
        {
            var n = period.Days.Sum(x => x.PemObserved); var positive = period.Days.Sum(x => x.PemPositive); var total = period.Days.Sum(x => x.ActivityCount);
            coverage.Children.Add(InterventionText($"{period.Start:d}–{period.End:d}: " +
                N($"PEM erkannt / Crash bei {positive}/{n} Aktivitäten mit späteren Zustandseinträgen; {total - n}/{total} ohne auswertbares oder abgeschlossenes Folgefenster.",
                  $"Recognized PEM / crash in {positive}/{n} activities with follow-up records; {total - n}/{total} without usable or completed follow-up.")));
        }
        var hints = new StackPanel { Spacing = 20 };
        void HintSection(string title, UIElement content)
        {
            var section = new StackPanel { Spacing = 8 };
            var heading = InterventionText(title);
            heading.FontSize = 18; heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            section.Children.Add(heading); section.Children.Add(content); hints.Children.Add(section);
        }
        HintSection(N("Datenbasis und Hinweise", "Data coverage and notes"), coverage);
        var methodology = new StackPanel { Spacing = 12 };
        methodology.Children.Add(InterventionText(N(
            $"Methodik: Tagesmittel für Symptome und Allgemeinzustand; Tage gleich gewichtet. Schwankungsbreite = kleinster bis größter dokumentierter Tageswert, kein Vertrauensintervall. Aktivitätsminuten werden an Tagesgrenzen geteilt und Überschneidungen nur einmal gezählt; laufende Aktivitäten bleiben unberücksichtigt. Kein Eintrag bedeutet unbekannt, nicht null. PEM erkannt oder Crash in {lag.Item1}–{lag.Item2} Stunden nach Aktivitätsende; mindestens ein Zustandseintrag und vollständig verstrichenes Zeitfenster erforderlich. Bereits bestehende PEM ist eingeschlossen. Dieselbe PEM kann mehreren Aktivitäten zugeordnet sein. Folgeeinträge dürfen außerhalb des Vergleichszeitraums liegen. Das beschreibt zeitliche Nähe, keine Verursachung.",
            $"Method: daily means for symptoms and overall condition; days weighted equally. Range = minimum to maximum recorded daily value, not a confidence interval. Activity minutes are split at midnight and overlaps counted once; ongoing activities excluded. No record means unknown, not zero. Recognized PEM or crash {lag.Item1}–{lag.Item2} hours after activity ends; requires a follow-up record and fully elapsed window. Includes pre-existing PEM. One PEM record may relate to several activities. Follow-up records may fall outside the comparison period. This describes timing, not causation.")));
        if (unavailableInterventionComparisons.Count > 0)
            methodology.Children.Add(InterventionText(N("Nicht berechenbar (nicht als Karte angezeigt):", "Not calculable (not shown as cards):") +
                "\n" + string.Join("\n", unavailableInterventionComparisons.Select(item => "• " + item))));
        HintSection(N("Methodik und nicht berechenbare Werte", "Method and unavailable comparisons"), methodology);
        HintSection(N("Weitere Einflüsse / Kontext", "Other influences / context"), InterventionConfounders(choice, baseline, follow));
        AddInterventionSection(N("Hinweise", "Notes"), hints);
        RenderInterventionTimeline(baseline, follow);
        foreach (var section in InterventionDetails.Children.OfType<Expander>())
            if (section.Header is string header && expansion.TryGetValue(header, out var wasExpanded)) section.IsExpanded = wasExpanded;
    }

    private void AddInterventionSection(string title, UIElement content)
    {
        InterventionDetails.Children.Add(new Expander {
            Header = title, Content = content, IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch });
    }

    private void AddInterventionComparison(string title, string subtitle, InterventionPeriod a, InterventionPeriod b, Func<InterventionDay, double?> value, string unit, Panel? destination = null)
    {
        var x = InterventionPeriod.Summarize(a.Days.Select(value)); var y = InterventionPeriod.Summarize(b.Days.Select(value));
        if (x is null || y is null)
        {
            var reason = x is null && y is null
                ? N("keine auswertbaren Daten in beiden Zeiträumen", "no usable data in either period")
                : x is null ? N("keine auswertbaren Daten im Ausgangszeitraum", "no usable baseline data")
                : N("keine auswertbaren Daten im Vergleichszeitraum", "no usable comparison data");
            unavailableInterventionComparisons.Add(title + ": " + reason);
            return;
        }
        string Describe(InterventionSummary s, int days) =>
            $"Ø {s.Mean:0.##} [{s.Min:0.##}–{s.Max:0.##}] · {s.Count}/{days} " + N("Tage", "days");
        var panel = new StackPanel { Spacing = 6 };
        var heading = InterventionText(title); heading.FontSize = 18; heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        panel.Children.Add(heading);
        var caption = InterventionText(subtitle); caption.FontSize = 12; caption.Opacity = 0.8;
        caption.Margin = new Thickness(0, 0, 0, 6); panel.Children.Add(caption);
        void ValueLine(string label, string text)
        {
            var line = InterventionText("");
            line.Inlines.Add(new Run { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            line.Inlines.Add(new Run { Text = text }); panel.Children.Add(line);
        }
        ValueLine(N("Ausgang: ", "Baseline: "), Describe(x, a.Days.Count));
        ValueLine(N("Vergleich: ", "Comparison: "), Describe(y, b.Days.Count));
        ValueLine(N("Differenz: ", "Difference: "), $"{y.Mean - x.Mean:+0.##;-0.##;0} {unit}");
        var card = new Border { Padding = new Thickness(16), BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Colors.LightGray), CornerRadius = new CornerRadius(6), Child = panel,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        if (destination is not null) { destination.Children.Add(card); return; }
        var grid = interventionComparisonGrid!;
        int index = grid.Children.Count;
        if (index % 3 == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(card, index % 3); Grid.SetRow(card, index / 3); grid.Children.Add(card);
    }

    private TextBlock InterventionConfounders(InterventionChoice choice, InterventionPeriod a, InterventionPeriod b)
    {
        var notes = new List<string>();
        var phases = (choice.Data.Phases ?? []).Where(p => p.Start < b.End.AddDays(1) && (p.End is null || p.End > a.Start)).ToList();
        if (phases.Count > 1 || phases.Any(p => p.Paused)) notes.Add(N("Mehrere Phasen oder Pausen liegen im betrachteten Bereich.", "Multiple phases or pauses fall in the selected span."));
        foreach (var selection in new[] { (Box: InterventionBaselinePhase, Period: a), (Box: InterventionFollowPhase, Period: b) })
            if ((selection.Box.SelectedItem as ComboBoxItem)?.Tag is InterventionPhase selected &&
                (selection.Period.Start < selected.Start || selected.End is { } end && selection.Period.End.AddDays(1) > end))
                notes.Add(N("Die Tagesauswertung umfasst angebrochene Tage der ausgewählten Phase; Einträge außerhalb ihrer Uhrzeitgrenzen können enthalten sein.",
                    "Daily analysis includes partial days of the selected phase; records outside its exact time bounds may be included."));
        foreach (var entry in entries.Where(e => e.Id != choice.Entry.Id && e.Kind == "Maßnahme" && e.Start < b.End.AddDays(1)))
        {
            var data = InterventionAnalysis.Read<MeasureData>(entry);
            if (data is null) continue;
            if (entry.Start >= a.Start || data.TrackProgress && data.Status != "completed" ||
                (data.Phases ?? []).Any(p => p.Start < b.End.AddDays(1) && (p.End is null || p.End > a.Start)))
                notes.Add($"{data.Name} {data.Dose} · {entry.Start:g}");
        }
        var intakeCount = entries.Count(e => e.Kind == "Einnahme" && e.Start >= a.Start && e.Start < b.End.AddDays(1));
        return InterventionText(
            (notes.Count > 0 ? string.Join("\n", notes) + "\n" : "") + N($"{intakeCount} Einnahmeeinträge im Gesamtzeitraum. Medikamente, Infekte und andere Änderungen bitte zusätzlich berücksichtigen; sie werden nicht rechnerisch herausgerechnet.",
                $"{intakeCount} intake records in the full span. Consider medication, infections and other changes; they are not statistically adjusted away."));
    }

    private void RenderInterventionTimeline(InterventionPeriod baseline, InterventionPeriod follow)
    {
        var timeline = new StackPanel { Spacing = 12 };
        timeline.Children.Add(InterventionText(N(
            "Jeder Punkt zeigt einen dokumentierten Tageswert. Linien verbinden nur aufeinanderfolgende dokumentierte Tage desselben Zeitraums; Lücken bleiben leer. Jede Zeile hat ihre eigene Werteskala.",
            "Each point is a recorded daily value. Lines connect only consecutive recorded days within the same period; gaps stay empty. Each panel has its own scale.")));
        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        var baselineLabel = InterventionText(N($"● Ausgang: {baseline.Start:d}–{baseline.End:d}", $"● Baseline: {baseline.Start:d}–{baseline.End:d}"));
        baselineLabel.Foreground = (Brush)AnalysisChart.Resources["ChartLineBrush"];
        var comparisonLabel = InterventionText(N($"● Vergleich: {follow.Start:d}–{follow.End:d}", $"● Comparison: {follow.Start:d}–{follow.End:d}"));
        comparisonLabel.Foreground = (Brush)AnalysisChart.Resources["ChartPemBrush"];
        legend.Children.Add(baselineLabel); legend.Children.Add(comparisonLabel); timeline.Children.Add(legend);
        var canvas = new Canvas { Width = Math.Max(760, AnalysisVisualTabs.ActualWidth - 72), Height = 570, Background = new SolidColorBrush(Colors.White) };
        canvas.SizeChanged += (_, _) => DrawInterventionTimeline(canvas, baseline, follow);
        var viewport = new ScrollViewer { Content = canvas, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 213, 226, 234)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8) };
        viewport.SizeChanged += (_, args) => canvas.Width = Math.Max(760, args.NewSize.Width - 20);
        DrawInterventionTimeline(canvas, baseline, follow);
        timeline.Children.Add(viewport);
        var days = baseline.Days.Concat(follow.Days).ToList();
        var details = new StackPanel { Spacing = 4 };
        foreach (var day in days)
        {
            string V(double? v) => v?.ToString("0.##", CultureInfo.CurrentCulture) ?? "—";
            details.Children.Add(InterventionText($"{day.Date:d} · " + N("Allgemein", "Overall") + $" {V(day.Overall)} · " +
                N("Aktivität", "Activity") + $" {V(day.ActivityMinutes)} min · PEM {day.PemPositive}/{day.PemObserved} · " +
                string.Join(" · ", day.Symptoms.OrderBy(x => x.Key).Select(x => $"{T(x.Key)} {x.Value:0.##}"))));
        }
        timeline.Children.Add(new Expander { Header = N("Tageswerte und Symptome", "Daily values and symptoms"), Content = details, IsExpanded = false, HorizontalAlignment = HorizontalAlignment.Stretch });
        AddInterventionSection(N("Gemeinsamer Tagesverlauf", "Combined daily timeline"), timeline);
    }

    private void DrawInterventionTimeline(Canvas canvas, InterventionPeriod baseline, InterventionPeriod follow)
    {
        canvas.Children.Clear();
        var axis = (Brush)AnalysisChart.Resources["ChartAxisBrush"];
        var normal = (Brush)AnalysisChart.Resources["ChartLineBrush"];
        var comparison = (Brush)AnalysisChart.Resources["ChartPemBrush"];
        double left = 64, right = Math.Max(left + 100, canvas.Width - 24);
        var days = baseline.Days.Concat(follow.Days).ToList();
        double span = Math.Max(1, (follow.End - baseline.Start).TotalDays);
        double X(DateTime date) => left + (date - baseline.Start).TotalDays / span * (right - left);
        var recordedActivity = days.Max(d => d.ActivityMinutes ?? 0);
        double activityStep = recordedActivity <= 60 ? 15 : 100;
        double activityMax = Math.Max(activityStep, Math.Ceiling(recordedActivity / activityStep) * activityStep);
        var lanes = new (string Title, string Unit, Func<InterventionDay, double?> Value, double Max)[] {
            (N("Allgemeinzustand · Tagesmittel (höher = schlechter)", "Overall condition · daily mean (higher = worse)"), N("Punkte", "points"), d => d.Overall, 4),
            (N("Dokumentierte Aktivität · Minuten pro Tag", "Recorded activity · minutes per day"), "min", d => d.ActivityMinutes, activityMax),
            (N("PEM nach Aktivität · Tagesanteil auswertbarer Aktivitäten mit PEM/Crash", "PEM after activity · daily fraction of observed activities with PEM/crash"), "%", d => d.DelayedPemPercent, 100) };
        void Label(string text, double x, double y, bool title = false)
        {
            var label = new TextBlock { Text = text, FontSize = title ? 14 : 11,
                FontWeight = title ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                Foreground = title ? new SolidColorBrush(Colors.Black) : normal, Tag = "LocalizedContent" };
            Canvas.SetLeft(label, x); Canvas.SetTop(label, y); canvas.Children.Add(label);
        }
        for (int lane = 0; lane < lanes.Length; lane++)
        {
            var metric = lanes[lane]; double top = lane * 190 + 32, bottom = lane * 190 + 140;
            double Y(double value) => bottom - Math.Clamp(value, 0, metric.Max) / metric.Max * (bottom - top);
            Label(metric.Title, left, lane * 190, true);
            for (int tick = 0; tick <= 4; tick++)
            {
                double value = metric.Max * tick / 4, y = Y(value);
                canvas.Children.Add(new Line { X1 = left, Y1 = y, X2 = right, Y2 = y, Stroke = axis, StrokeThickness = 1 });
                Label(value.ToString("0.#", CultureInfo.CurrentCulture), 12, y - 8);
            }
            canvas.Children.Add(new Line { X1 = left, Y1 = top, X2 = left, Y2 = bottom, Stroke = axis, StrokeThickness = 1 });
            var dateTicks = Enumerable.Range(0, 6).Select(i => baseline.Start.AddDays(Math.Round(span * i / 5))).Distinct();
            foreach (var date in dateTicks)
            {
                double x = X(date);
                canvas.Children.Add(new Line { X1 = x, Y1 = top, X2 = x, Y2 = bottom, Stroke = axis, StrokeThickness = 0.6, Opacity = 0.5 });
                Label(date.ToString(selectedLanguage == "en" ? "MM/dd/yy" : "dd.MM.yy"), Math.Clamp(x - 28, left, right - 62), bottom + 5);
            }
            Label(N("Datum", "Date"), (left + right) / 2 - 14, bottom + 23);
            foreach (var period in new[] { baseline, follow })
            {
                Brush color = ReferenceEquals(period, baseline) ? normal : comparison;
                InterventionDay? previous = null; double? previousValue = null;
                foreach (var day in period.Days)
                {
                    var value = metric.Value(day);
                    if (value is null) { previous = null; previousValue = null; continue; }
                    if (previous is not null && previousValue is { } last && (day.Date - previous.Date).Days == 1)
                        canvas.Children.Add(new Line { X1 = X(previous.Date), Y1 = Y(last), X2 = X(day.Date), Y2 = Y(value.Value), Stroke = color, StrokeThickness = 2.4 });
                    var point = new Ellipse { Width = 8, Height = 8, Fill = color };
                    Canvas.SetLeft(point, X(day.Date) - 4); Canvas.SetTop(point, Y(value.Value) - 4); canvas.Children.Add(point);
                    ToolTipService.SetToolTip(point, $"{day.Date:d} · {metric.Title}: {value:0.##} {metric.Unit}" +
                        (lane == 2 ? $" · {day.PemPositive}/{day.PemObserved}" : ""));
                    previous = day; previousValue = value;
                }
                if (period.Days.All(d => metric.Value(d) is null))
                    Label((ReferenceEquals(period, baseline) ? N("Ausgang", "Baseline") : N("Vergleich", "Comparison")) + N(": keine Daten", ": no data"),
                        ReferenceEquals(period, baseline) ? left + 12 : Math.Max(left + 190, right - 190), top + 6);
            }
        }
    }
}
