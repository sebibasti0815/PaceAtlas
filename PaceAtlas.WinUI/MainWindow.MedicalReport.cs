using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private bool medicalReportBusy;

    private async void CreateMedicalReport_Click(object sender, RoutedEventArgs e)
    {
        if (medicalReportBusy) return;
        bool english = selectedLanguage == "en";
        var start = new CalendarDatePicker { Header = english ? "From" : "Von",
            Date = DateTimeOffset.Now.AddDays(-30), Width = 170 };
        var end = new CalendarDatePicker { Header = english ? "Through" : "Bis einschließlich",
            Date = DateTimeOffset.Now, Width = 170 };
        var dates = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        dates.Children.Add(start);
        dates.Children.Add(end);
        var nutrition = new CheckBox { Content = english ? "Include nutrition" : "Ernährung aufnehmen", IsChecked = true };
        var medication = new CheckBox { Content = english ? "Include medication and interventions" : "Medikamente und Maßnahmen aufnehmen", IsChecked = true };
        var history = new CheckBox { Content = english ? "Include short daily history" : "Kurzen Tagesverlauf aufnehmen", IsChecked = true };
        var personalNotes = new CheckBox { Content = english ? "Include personal entry notes" : "Persönliche Eintragsnotizen aufnehmen",
            IsEnabled = true };
        history.Checked += (_, _) => personalNotes.IsEnabled = true;
        history.Unchecked += (_, _) => { personalNotes.IsChecked = false; personalNotes.IsEnabled = false; };
        var topic = new TextBox { Header = english ? "For the appointment (optional)" : "Für den Termin (optional)",
            PlaceholderText = english ? "Questions or topics to discuss" : "Fragen oder Themen für das Gespräch",
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 90 };
        var fields = new StackPanel { Spacing = 7, Width = 465 };
        fields.Children.Add(dates);
        fields.Children.Add(nutrition);
        fields.Children.Add(medication);
        fields.Children.Add(history);
        fields.Children.Add(personalNotes);
        fields.Children.Add(topic);
        fields.Children.Add(new TextBlock { Text = english
            ? "The preview below summarizes the selected period after you choose the dates. Only eaten meals count. PDF files are saved where you select; no report is uploaded."
            : "Nach der Zeitraumwahl zeigt die Vorschau den Umfang. Nur als gegessen gespeicherte Mahlzeiten zählen. Das PDF wird am gewählten Ort gespeichert und nicht hochgeladen.",
            TextWrapping = TextWrapping.Wrap });
        var preview = new TextBlock { TextWrapping = TextWrapping.Wrap };
        fields.Children.Add(preview);
        void UpdatePreview()
        {
            if (start.Date is not { } from || end.Date is not { } to || from.Date > to.Date)
            {
                preview.Text = english ? "Choose a valid date range." : "Bitte einen gültigen Zeitraum wählen.";
                return;
            }
            var stateCount = entries.Count(item => item.Kind == "Zustand" && item.Start.Date >= from.Date && item.Start.Date <= to.Date);
            preview.Text = english
                ? $"Preview: {stateCount} condition entries · {Math.Max(1, (to.Date - from.Date).Days + 1)} calendar days · " +
                    (nutrition.IsChecked == true ? "nutrition included" : "without nutrition")
                : $"Vorschau: {stateCount} Zustandseinträge · {Math.Max(1, (to.Date - from.Date).Days + 1)} Kalendertage · " +
                    (nutrition.IsChecked == true ? "mit Ernährung" : "ohne Ernährung");
        }
        start.DateChanged += (_, _) => UpdatePreview();
        end.DateChanged += (_, _) => UpdatePreview();
        nutrition.Checked += (_, _) => UpdatePreview();
        nutrition.Unchecked += (_, _) => UpdatePreview();
        UpdatePreview();

        var dialog = new ContentDialog { Title = english ? "Create medical report" : "Arztbericht erstellen",
            Content = new ScrollViewer { Content = fields, MaxHeight = 560 },
            PrimaryButtonText = english ? "Continue to PDF" : "Weiter zum PDF",
            CloseButtonText = english ? "Cancel" : "Abbrechen",
            XamlRoot = ((FrameworkElement)Content).XamlRoot };
        // Keep the dialog open for invalid or inverted ranges.
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (start.Date is null || end.Date is null || start.Date.Value.Date > end.Date.Value.Date)
            {
                args.Cancel = true;
                UpdatePreview();
            }
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || start.Date is null || end.Date is null) return;
        var options = new MedicalReportOptions(start.Date.Value.Date, end.Date.Value.Date,
            english, nutrition.IsChecked == true, medication.IsChecked == true,
            history.IsChecked == true, personalNotes.IsChecked == true, topic.Text.Trim());
        var suggested = $"PaceAtlas-Arztbericht-{options.End:yyyy-MM-dd}";
        var path = await PickFileAsync(true, ".pdf", suggested);
        if (path is null) return;
        medicalReportBusy = true;
        CreateMedicalReportButton.IsEnabled = false;
        MedicalReportStatus.Text = english ? "Creating the PDF ..." : "PDF wird erstellt ...";
        try
        {
            await Task.Run(() => MedicalReportPdf.Write(path, options, store.All(),
                options.Nutrition ? store.Meals() : [],
                options.Medication ? store.MedicationPlans() : []));
            MedicalReportStatus.Text = english ? "Medical report saved." : "Arztbericht gespeichert.";
        }
        catch (Exception ex)
        {
            MedicalReportStatus.Text = (english ? "PDF could not be created: " : "PDF konnte nicht erstellt werden: ") + ex.Message;
        }
        finally
        {
            medicalReportBusy = false;
            CreateMedicalReportButton.IsEnabled = true;
        }
    }
}
