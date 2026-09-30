using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private readonly List<ActivityTemplate> activityTemplates = new();
    private bool refreshingActivityTemplates;

    private void LoadActivityTemplates()
    {
        activityTemplates.Clear();
        activityTemplates.AddRange(store.ActivityTemplates());
        RefreshActivityTemplateChoice();
    }

    private void RefreshActivityTemplateChoice(string? selected = null)
    {
        if (ActivityTemplateChoice is null) return;
        refreshingActivityTemplates = true;
        try
        {
            selected ??= (ActivityTemplateChoice.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            ActivityTemplateChoice.Items.Clear();
            ActivityTemplateChoice.Items.Add(new ComboBoxItem { Content = selectedLanguage == "en" ? "No preset" : "Ohne Vorlage" });
            foreach (var template in activityTemplates.OrderBy(t => t.Name,
                         StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true)))
                ActivityTemplateChoice.Items.Add(new ComboBoxItem { Content = template.Name, Tag = template.Name });
            ActivityTemplateChoice.SelectedItem = ActivityTemplateChoice.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), selected, StringComparison.OrdinalIgnoreCase))
                ?? ActivityTemplateChoice.Items[0];
        }
        finally { refreshingActivityTemplates = false; }
    }

    private void ActivityTemplateChoice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (refreshingActivityTemplates || IntervalKind?.SelectedIndex != 0) return;
        var name = (ActivityTemplateChoice.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var template = activityTemplates.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        if (template is null) return;
        foreach (var check in intervalDimensionChecks)
            check.IsChecked = template.Dimensions.Contains(check.Tag?.ToString(), StringComparer.OrdinalIgnoreCase);
        IntervalIntensity.SelectedIndex = Math.Clamp(template.Intensity - 1, 0, 3);
        SetHearingProtection(template.HearingProtection);
    }

    private async void ManageActivityTemplates_Click(object sender, RoutedEventArgs e)
    {
        var english = selectedLanguage == "en";
        var draft = activityTemplates.Select(t => new ActivityTemplate { Name = t.Name,
            Dimensions = t.Dimensions.ToList(), Intensity = t.Intensity,
            HearingProtection = t.HearingProtection.ToList() }).ToList();
        var list = new ListView { ItemsSource = draft.OrderBy(t => t.Name).Select(t => t.Name).ToArray() };
        var empty = new TextBlock { Text = english ? "No activities yet" : "Noch keine Aktivitäten",
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 93, 111, 126)),
            Margin = new Thickness(12), VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center };
        var name = new TextBox { Header = english ? "Name" : "Bezeichnung" };
        var dimensions = choices.Activities.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .Select(n => new CheckBox { Content = T(n), Tag = n }).ToArray();
        var protectionNames = choices.HearingProtection.ToList();
        var protections = new List<CheckBox>();
        var intensity = new ComboBox { Header = T("Intensität"), MinWidth = 170,
            ItemsSource = new[] { "gering", "mittel", "hoch", "sehr hoch" }.Select(T).ToArray(), SelectedIndex = 1 };
        var add = new Button { Content = english ? "+ New activity" : "+ Neue Aktivität" };
        var save = new Button { Content = english ? "Save activity" : "Aktivität speichern",
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 119, 137)),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) };
        var remove = new Button { Content = english ? "Remove" : "Entfernen" };
        void RefreshList(string? selected = null)
        {
            list.ItemsSource = draft.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).Select(t => t.Name).ToArray();
            list.SelectedItem = selected;
            empty.Visibility = draft.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        list.SelectionChanged += (_, _) =>
        {
            var template = draft.FirstOrDefault(t => t.Name == list.SelectedItem as string);
            if (template is null) return;
            name.Text = template.Name;
            intensity.SelectedIndex = Math.Clamp(template.Intensity - 1, 0, 3);
            foreach (var check in dimensions) check.IsChecked = template.Dimensions.Contains(check.Tag?.ToString() ?? "");
            foreach (var check in protections) check.IsChecked = template.HearingProtection.Contains(check.Tag?.ToString() ?? "");
        };
        add.Click += (_, _) =>
        {
            list.SelectedItem = null;
            name.Text = "";
            intensity.SelectedIndex = 1;
            foreach (var check in dimensions) check.IsChecked = false;
            foreach (var check in protections) check.IsChecked = false;
            name.Focus(FocusState.Programmatic);
        };
        save.Click += (_, _) =>
        {
            var value = name.Text.Trim();
            if (value.Length == 0) return;
            var selected = list.SelectedItem as string;
            if (draft.Any(t => t.Name.Equals(value, StringComparison.OrdinalIgnoreCase) && t.Name != selected)) return;
            var template = draft.FirstOrDefault(t => t.Name == selected) ?? new ActivityTemplate();
            if (!draft.Contains(template)) draft.Add(template);
            template.Name = value;
            template.Intensity = Math.Clamp(intensity.SelectedIndex + 1, 1, 4);
            template.Dimensions = dimensions.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();
            template.HearingProtection = protections.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();
            RefreshList(value);
        };
        remove.Click += (_, _) =>
        {
            if (list.SelectedItem is not string selected) return;
            draft.RemoveAll(t => t.Name == selected);
            RefreshList();
            name.Text = "";
        };
        var fields = new StackPanel { Spacing = 10 };
        fields.Children.Add(name);
        fields.Children.Add(new TextBlock { Text = english ? "Activity types" : "Belastungsarten", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var dimensionGrid = new Grid { ColumnSpacing = 8, RowSpacing = 2 };
        dimensionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dimensionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var index = 0; index < dimensions.Length; index++)
        {
            if (index % 2 == 0) dimensionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(dimensions[index], index / 2);
            Grid.SetColumn(dimensions[index], index % 2);
            dimensionGrid.Children.Add(dimensions[index]);
        }
        fields.Children.Add(dimensionGrid);
        fields.Children.Add(intensity);
        fields.Children.Add(new TextBlock { Text = T("Schutzmaßnahmen"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var protectionGrid = new Grid { ColumnSpacing = 8, RowSpacing = 2 };
        protectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        protectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void RefreshProtections(IEnumerable<string>? checkedNames = null)
        {
            var selected = checkedNames?.ToHashSet(StringComparer.OrdinalIgnoreCase) ??
                protections.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
            protectionGrid.Children.Clear(); protectionGrid.RowDefinitions.Clear(); protections.Clear();
            foreach (var value in protectionNames.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
                protections.Add(new CheckBox { Content = T(value), Tag = value, IsChecked = selected.Contains(value) });
            for (var index = 0; index < protections.Count; index++)
            {
                if (index % 2 == 0) protectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(protections[index], index / 2);
                Grid.SetColumn(protections[index], index % 2);
                protectionGrid.Children.Add(protections[index]);
            }
        }
        RefreshProtections();
        fields.Children.Add(protectionGrid);
        var actions = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 };
        save.HorizontalAlignment = HorizontalAlignment.Stretch;
        remove.HorizontalAlignment = HorizontalAlignment.Stretch;
        actions.Children.Add(save); actions.Children.Add(remove);
        var left = new Grid { RowSpacing = 8, Width = 200 };
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.Children.Add(add);
        var listSurface = new Grid { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 248, 250)) };
        listSurface.Children.Add(empty);
        listSurface.Children.Add(list);
        Grid.SetRow(listSurface, 1); left.Children.Add(listSurface);
        Grid.SetRow(actions, 2); left.Children.Add(actions);
        var body = new Grid { ColumnSpacing = 18, Width = 760, Height = 440 };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(left);
        var scroll = new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetColumn(scroll, 1); body.Children.Add(scroll);
        empty.Visibility = draft.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var dialog = new ContentDialog { Title = english ? "Manage activities" : "Aktivitäten verwalten",
            Content = body, PrimaryButtonText = english ? "Apply" : "Übernehmen",
            CloseButtonText = english ? "Cancel" : "Abbrechen",
            MinWidth = 810, XamlRoot = (Content as FrameworkElement)?.XamlRoot };
        dialog.Resources["ContentDialogMaxWidth"] = 900d;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            store.SetActivityTemplates(draft);
            activityTemplates.Clear(); activityTemplates.AddRange(draft);
            RefreshActivityTemplateChoice();
        }
        catch (Exception ex) { IntervalStatus.Text = (english ? "Could not save activities: " : "Aktivitäten konnten nicht gespeichert werden: ") + ex.Message; }
    }
}
