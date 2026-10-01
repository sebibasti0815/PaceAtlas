using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PaceAtlas;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private readonly List<FoodItem> nutritionFoods = new();
    private readonly List<FoodItem> visibleNutritionFoods = new();
    private readonly List<FoodRule> nutritionRules = new();
    private readonly List<FoodRule> displayedNutritionRules = new();
    private readonly List<MealRecord> nutritionMeals = new();
    private readonly List<MealRecord> displayedNutritionMeals = new();
    private readonly List<MealIngredient> mealIngredients = new();
    private readonly List<int> displayedIngredientIndices = new();
    private long editingFoodId;
    private long editingRuleId;
    private long editingMealId;
    private string editingMealStatus = "";
    private int editingIngredientIndex = -1;
    private bool refreshingNutrition;
    private bool nutritionInitialized;
    private bool nutritionFoodsLoaded;
    private bool nutritionFoodsLoading;
    private CancellationTokenSource? offImportCancellation;
    private bool closeAfterOffImport;
    private bool OffImportRunning => offImportCancellation is not null;

    private void CancelOpenFoodFactsImport_Click(object sender, RoutedEventArgs e)
    {
        offImportCancellation?.Cancel();
        CancelOffImportButton.IsEnabled = false;
        FoodEditorStatus.Text = N("Import wird abgebrochen; bisherige Daten bleiben erhalten.",
            "Stopping import; existing data will be preserved.");
    }
    private int mealFeedbackGeneration;
    private bool savingMeal;
    private bool confirmingPendingMealIngredient;
    private Button? activeMealSaveButton;
    private object? activeMealSaveLabel;

    private async Task BeginMealSaveAsync(string? status = null)
    {
        savingMeal = true;
        activeMealSaveButton = status switch
        {
            "planned" => SavePlannedMealButton,
            "consumed" => SaveConsumedMealButton,
            "template" => SaveMealTemplateButton,
            _ => null
        };
        activeMealSaveLabel = activeMealSaveButton?.Content;
        if (activeMealSaveButton is not null)
            activeMealSaveButton.Content = N("Wird gespeichert …", "Saving …");
        SavePlannedMealButton.IsEnabled = false;
        SaveConsumedMealButton.IsEnabled = false;
        SaveMealTemplateButton.IsEnabled = false;
        MealSaveProgress.Visibility = Visibility.Visible;
        MealSaveProgress.IsActive = true;
        MealStatusText.Text = N("Mahlzeit wird gespeichert …", "Saving meal …");
        await Task.Delay(50); // Let the UI paint before the catalog and history are refreshed.
    }

    private void EndMealSave()
    {
        savingMeal = false;
        if (activeMealSaveButton is not null) activeMealSaveButton.Content = activeMealSaveLabel;
        activeMealSaveButton = null;
        activeMealSaveLabel = null;
        MealSaveProgress.IsActive = false;
        MealSaveProgress.Visibility = Visibility.Collapsed;
        SavePlannedMealButton.IsEnabled = true;
        SaveConsumedMealButton.IsEnabled = true;
        SaveMealTemplateButton.IsEnabled = true;
    }

    private string N(string german, string english) => selectedLanguage == "en" ? english : german;
    private static double? NumberOrNull(NumberBox box) => double.IsNaN(box.Value) ? null : box.Value;
    private static string Nutrient(double? value) => value?.ToString("0.#", CultureInfo.CurrentCulture) ?? "?";

    private void InitializeNutrition()
    {
        foreach (var list in new[] { MealIngredientsList, MealHistoryList, FoodCatalogList, FoodRulesList })
            list.SelectionChanged += (_, _) => UpdateListFeedback(list);
        MealDate.Date = DateTimeOffset.Now;
        MealTime.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        MealFoodGrams.Value = 100;
        FoodCarbs.Value = FoodGi.Value = FoodGl.Value = double.NaN;
        RuleDecision.SelectedIndex = 0;
        MealFoodSearch.IsEnabled = false;
        nutritionInitialized = true;
        ReloadNutrition();
    }

    private async Task EnsureNutritionFoodsLoadedAsync()
    {
        if (!nutritionInitialized || nutritionFoodsLoaded || nutritionFoodsLoading) return;
        nutritionFoodsLoading = true;
        FoodCatalogCount.Text = N("Lebensmittel werden geladen …", "Loading foods …");
        MealFoodSearch.PlaceholderText = N("Lebensmittel werden geladen …", "Loading foods …");
        try
        {
            var foods = await Task.Run(() => store.Foods());
            nutritionFoods.Clear();
            nutritionFoods.AddRange(foods);
            nutritionFoodsLoaded = true;
            MealFoodSearch.IsEnabled = true;
            MealFoodSearch.PlaceholderText = N("Lebensmittel suchen", "Search foods");
            RenderFoodCatalog();
        }
        catch (Exception ex)
        {
            FoodCatalogCount.Text = N("Lebensmittel konnten nicht geladen werden: ",
                "Could not load foods: ") + ex.Message;
        }
        finally { nutritionFoodsLoading = false; }
    }

    private void ReloadMealData()
    {
        nutritionMeals.Clear(); nutritionMeals.AddRange(store.Meals());
        RenderMealHistory(); RenderFoodCarbChart(); RenderFoodAnalysis(); RefreshMealDueIndicators();
        if (EntryList is not null) DisplayEntries();
    }

    private void ReloadNutrition(bool reloadFoods = true)
    {
        try
        {
            refreshingNutrition = true;
            if (reloadFoods && nutritionFoodsLoaded)
            {
                nutritionFoods.Clear(); nutritionFoods.AddRange(store.Foods());
            }
            nutritionRules.Clear(); nutritionRules.AddRange(store.FoodRules());
            nutritionMeals.Clear(); nutritionMeals.AddRange(store.Meals());
            if (nutritionFoodsLoaded) RenderFoodCatalog();
            RenderFoodRules(); RenderMealHistory(); RenderMealIngredients(); RenderFoodCarbChart(); RenderFoodAnalysis();
            RefreshMealDueIndicators();
            if (EntryList is not null) DisplayEntries();
        }
        catch (Exception ex) { MealStatusText.Text = N("Ernährungsdaten konnten nicht geladen werden: ", "Could not load nutrition data: ") + ex.Message; }
        finally { refreshingNutrition = false; }
    }

    private Grid NutritionTableRow(string table, params string[] cells)
    {
        var row = new Grid { MinHeight = 27,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        foreach (var width in columnWidths[table])
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = new TextBlock { Text = cells[i], VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(8, 1, 8, 1) };
            ToolTipService.SetToolTip(cell, cells[i]);
            Grid.SetColumn(cell, i);
            row.Children.Add(cell);
        }
        return row;
    }

    private void RenderFoodCatalog()
    {
        if (FoodCatalogList is null) return;
        var selected = editingFoodId;
        visibleNutritionFoods.Clear();
        var (column, descending) = tableSort["foods"];
        string[] Cells(FoodItem food) => [food.Name, Nutrient(food.CarbsPer100G),
            Nutrient(food.GlycemicIndex), Nutrient(food.GlycemicLoadPer100G), food.Source, food.Note];
        string Key(FoodItem food) => column switch
        {
            1 => Numeric(food.CarbsPer100G ?? -1), 2 => Numeric(food.GlycemicIndex ?? -1),
            3 => Numeric(food.GlycemicLoadPer100G ?? -1), _ => Cells(food)[column]
        };
        var hasFoodFilter = tableFilters.TryGetValue("foods", out var foodFilters) && foodFilters.Count > 0;
        var fastDefault = column == 0 && !descending && !hasFoodFilter;
        if (fastDefault)
            visibleNutritionFoods.AddRange(nutritionFoods.Take(300)); // Foods() is already ordered by name.
        else
        {
            var filtered = nutritionFoods.Where(food => MatchesTableFilters("foods", Cells(food)));
            visibleNutritionFoods.AddRange((descending
                ? filtered.OrderByDescending(Key, StringComparer.CurrentCultureIgnoreCase)
                : filtered.OrderBy(Key, StringComparer.CurrentCultureIgnoreCase)).Take(300));
        }
        var matchingCount = !hasFoodFilter ? nutritionFoods.Count :
            nutritionFoods.Count(food => MatchesTableFilters("foods", Cells(food)));
        FoodCatalogCount.Text = !store.HasBlsCatalog()
            ? N($"{matchingCount} Lebensmittel · BLS-Offlinedaten fehlen für diesen Benutzer; vollständiges Paket einmal installieren.",
                $"{matchingCount} foods · BLS offline data missing for this user; install the complete package once.")
            : matchingCount > 300 && !hasFoodFilter
                ? N($"{matchingCount} Lebensmittel · erste 300 angezeigt; über die Lupe in den Spalten suchen.",
                    $"{matchingCount} foods · first 300 shown; use column search to narrow the list.")
                : N($"{matchingCount} Lebensmittel", $"{matchingCount} foods");
        FoodCatalogList.ItemsSource = visibleNutritionFoods.Select(food => NutritionTableRow("foods", food.Name, Nutrient(food.CarbsPer100G),
            Nutrient(food.GlycemicIndex), Nutrient(food.GlycemicLoadPer100G), food.Source, food.Note)).ToArray();
        ConfigureListFeedback(FoodCatalogList);
        var index = visibleNutritionFoods.FindIndex(food => food.Id == selected);
        if (index >= 0) FoodCatalogList.SelectedIndex = index;
    }

    private void FoodCatalog_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = FoodCatalogList.SelectedIndex;
        if (DeleteFoodButton is not null) DeleteFoodButton.IsEnabled = index >= 0 && index < visibleNutritionFoods.Count
            && !visibleNutritionFoods[index].IsBlsBase && !visibleNutritionFoods[index].IsOffBase;
    }

    private void FoodCatalog_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var index = FoodCatalogList.SelectedIndex;
        if (refreshingNutrition || index < 0 || index >= visibleNutritionFoods.Count) return;
        var food = visibleNutritionFoods[index];
        editingFoodId = food.Id;
        FoodName.Text = food.Name; FoodCarbs.Value = food.CarbsPer100G ?? double.NaN;
        FoodGi.Value = food.GlycemicIndex ?? double.NaN; FoodGl.Value = food.GlycemicLoadPer100G ?? double.NaN;
        FoodSource.Text = food.Source; FoodNote.Text = food.Note;
        FoodEditorStatus.Text = food.IsBlsBase || food.IsOffBase
            ? N("Quellwert. Speichern legt eine persönliche Ergänzung an; die Quelldaten bleiben erhalten.",
                "Source value. Saving creates a personal override; the source data remains available.") : "";
    }

    private void NewFood_Click(object sender, RoutedEventArgs e)
    {
        editingFoodId = 0; FoodCatalogList.SelectedIndex = -1;
        DeleteFoodButton.IsEnabled = false;
        FoodName.Text = FoodSource.Text = FoodNote.Text = "";
        FoodCarbs.Value = FoodGi.Value = FoodGl.Value = double.NaN;
        FoodEditorStatus.Text = N("Neues Lebensmittel vorbereitet.", "New food ready.");
    }

    private async void ImportOpenFoodFacts_Click(object sender, RoutedEventArgs e)
    {
        var english = selectedLanguage == "en";
        var instructions = new StackPanel { Spacing = 12 };
        instructions.Children.Add(new TextBlock
        {
            Text = english
                ? "Download the tab-separated CSV export from Open Food Facts, preferably the compressed .csv.gz file. The import keeps products sold in Germany with a German name and carbohydrate values, and combines obvious duplicates. JSONL and MongoDB exports cannot be imported here."
                : "Lade den tabulatorgetrennten CSV-Export von Open Food Facts herunter, am besten die komprimierte Datei mit der Endung .csv.gz. Der Import übernimmt Produkte mit Deutschlandbezug, deutschem Namen und Kohlenhydratwerten und fasst offensichtliche Doppelungen zusammen. JSONL und MongoDB-Dumps können hier nicht importiert werden.",
            TextWrapping = TextWrapping.Wrap
        });
        instructions.Children.Add(new HyperlinkButton
        {
            Content = english ? "Open Food Facts data and downloads" : "Open Food Facts: Daten und Downloads",
            NavigateUri = new Uri("https://world.openfoodfacts.org/data"),
            Padding = new Thickness(0)
        });
        var dialog = new ContentDialog
        {
            XamlRoot = ((FrameworkElement)Content).XamlRoot,
            Title = english ? "Import Open Food Facts" : "Open Food Facts importieren",
            Content = instructions,
            PrimaryButtonText = english ? "Select file" : "Datei auswählen",
            CloseButtonText = english ? "Cancel" : "Abbrechen",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".csv"); picker.FileTypeFilter.Add(".tsv"); picker.FileTypeFilter.Add(".gz");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        ImportOffButton.IsEnabled = false;
        CancelOffImportButton.Visibility = Visibility.Visible;
        CancelOffImportButton.IsEnabled = true;
        OffImportProgress.Visibility = Visibility.Visible;
        OffImportProgress.IsActive = true;
        offImportCancellation = new CancellationTokenSource();
        FoodEditorStatus.Text = N("Open Food Facts wird importiert. Das kann bei der vollständigen Exportdatei einige Zeit dauern.",
            "Importing Open Food Facts. The full export may take some time.");
        try
        {
            var token = offImportCancellation.Token;
            var result = await Task.Run(() => store.ImportOpenFoodFacts(file.Path, token, rows =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (OffImportRunning && !token.IsCancellationRequested)
                        FoodEditorStatus.Text = N($"Open Food Facts: {rows:N0} Zeilen geprüft …",
                            $"Open Food Facts: {rows:N0} rows checked …");
                });
            }));
            if (result.Imported == 0)
                FoodEditorStatus.Text = N(
                    $"Keine geeigneten Produkte gefunden: {result.Rows:N0} Zeilen geprüft, {result.Germany:N0} mit Deutschlandbezug, {result.Named:N0} mit Namen, {result.Nutrition:N0} mit Kohlenhydratwert. Der bisherige Bestand bleibt erhalten.",
                    $"No matching products: {result.Rows:N0} rows checked, {result.Germany:N0} sold in Germany, {result.Named:N0} with names, {result.Nutrition:N0} with carbohydrate values. Existing data remains unchanged.");
            else
            {
                ReloadNutrition();
                FoodEditorStatus.Text = N($"{result.Imported} Open-Food-Facts-Produkte importiert oder aktualisiert.",
                    $"{result.Imported} Open Food Facts products imported or updated.");
            }
        }
        catch (OperationCanceledException)
        {
            FoodEditorStatus.Text = N("Import abgebrochen. Die bisherigen Daten bleiben erhalten.",
                "Import cancelled. Existing data remains unchanged.");
        }
        catch (Exception ex)
        {
            FoodEditorStatus.Text = N("Open-Food-Facts-Import fehlgeschlagen: ",
                "Open Food Facts import failed: ") + ex.Message;
        }
        finally
        {
            offImportCancellation?.Dispose();
            offImportCancellation = null;
            ImportOffButton.IsEnabled = true;
            CancelOffImportButton.Visibility = Visibility.Collapsed;
            OffImportProgress.IsActive = false;
            OffImportProgress.Visibility = Visibility.Collapsed;
            if (closeAfterOffImport)
            {
                closeAfterOffImport = false;
                Close();
            }
        }
    }

    private void SaveFood_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FoodName.Text))
        { FoodEditorStatus.Text = N("Bitte eine Bezeichnung eingeben.", "Enter a name."); return; }
        var name = FoodName.Text.Trim();
        var duplicate = nutritionFoods.FirstOrDefault(food => food.Id != editingFoodId &&
            food.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
        { FoodEditorStatus.Text = DuplicateFoodMessage(duplicate.Name); return; }
        var carbs = NumberOrNull(FoodCarbs); var gi = NumberOrNull(FoodGi); var gl = NumberOrNull(FoodGl);
        if (gl is null && carbs is not null && gi is not null) gl = Math.Round(carbs.Value * gi.Value / 100, 1);
        var baseFood = editingFoodId < 0 ? nutritionFoods.FirstOrDefault(food => food.Id == editingFoodId) : null;
        try
        {
            store.SaveFood(new FoodItem { Id = editingFoodId, Name = name,
                CarbsPer100G = baseFood is not null && carbs == baseFood.CarbsPer100G ? null : carbs,
                GlycemicIndex = gi, GlycemicLoadPer100G = gl,
                Source = baseFood is not null && FoodSource.Text == baseFood.Source
                    ? N("Eigene Ergänzung · ", "Personal override · ") + baseFood.Source
                    : string.IsNullOrWhiteSpace(FoodSource.Text) ? N("Eigener Eintrag", "User entry") : FoodSource.Text.Trim(),
                Note = FoodNote.Text.Trim(), BlsCode = baseFood?.BlsCode ?? "", OffCode = baseFood?.OffCode ?? "" });
            editingFoodId = 0; ReloadNutrition(); NewFood_Click(sender, e);
            FoodEditorStatus.Text = N("Lebensmittel gespeichert.", "Food saved.");
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19 &&
            ex.Message.Contains("foods.name", StringComparison.OrdinalIgnoreCase))
        { FoodEditorStatus.Text = DuplicateFoodMessage(name); }
        catch (Exception ex) { FoodEditorStatus.Text = N("Speichern fehlgeschlagen: ", "Save failed: ") + ex.Message; }
    }

    private string DuplicateFoodMessage(string name) => N(
        $"„{name}“ ist bereits vorhanden. Wähle es unten in der Tabelle aus, um den bestehenden Eintrag zu bearbeiten, oder vergib einen anderen Namen. Deine Eingaben bleiben erhalten.",
        $"“{name}” already exists. Select it in the table below to edit the existing entry, or choose another name. Your entries are still here.");

    private async void DeleteFood_Click(object sender, RoutedEventArgs e)
    {
        var index = FoodCatalogList.SelectedIndex;
        if (index < 0 || index >= visibleNutritionFoods.Count) return;
        var food = visibleNutritionFoods[index];
        if (food.IsBlsBase || food.IsOffBase) return;
        var dialog = new ContentDialog
        {
            Title = N("Lebensmittel entfernen?", "Remove food?"),
            Content = N($"„{food.Name}“ wird aus dem Lebensmittelkatalog entfernt. Bereits gespeicherte Mahlzeiten behalten ihre erfassten Zutaten und Werte.",
                $"“{food.Name}” will be removed from the food catalog. Saved meals keep their recorded ingredients and values."),
            PrimaryButtonText = N("Entfernen", "Remove"),
            CloseButtonText = N("Abbrechen", "Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = ((FrameworkElement)Content).XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            store.DeleteFood(food.Id);
            editingFoodId = 0;
            ReloadNutrition(); NewFood_Click(sender, e);
            FoodEditorStatus.Text = N($"„{food.Name}“ wurde entfernt.", $"“{food.Name}” was removed.");
        }
        catch (Exception ex) { FoodEditorStatus.Text = N("Entfernen fehlgeschlagen: ", "Could not remove food: ") + ex.Message; }
    }

    private void RenderFoodRules()
    {
        if (FoodRulesList is null) return;
        var selected = editingRuleId;
        var (column, descending) = tableSort["rules"];
        string[] Cells(FoodRule rule) => [rule.Pattern, RuleLabel(rule.Decision),
            rule.Priority.ToString(CultureInfo.CurrentCulture), rule.Note, rule.Source];
        string Key(FoodRule rule) => column == 2 ? Numeric(rule.Priority) : Cells(rule)[column];
        var filtered = nutritionRules.Where(rule => MatchesTableFilters("rules", Cells(rule)));
        displayedNutritionRules.Clear();
        displayedNutritionRules.AddRange(descending ? filtered.OrderByDescending(Key, StringComparer.CurrentCultureIgnoreCase)
            : filtered.OrderBy(Key, StringComparer.CurrentCultureIgnoreCase));
        FoodRulesList.ItemsSource = displayedNutritionRules.Select(rule => NutritionTableRow("rules", rule.Pattern, RuleLabel(rule.Decision),
            rule.Priority.ToString(CultureInfo.CurrentCulture), rule.Note, rule.Source)).ToArray();
        ConfigureListFeedback(FoodRulesList);
        var index = displayedNutritionRules.FindIndex(rule => rule.Id == selected);
        if (index >= 0) FoodRulesList.SelectedIndex = index;
    }

    private string RuleLabel(string decision) => decision switch
    {
        "exclude" => N("Verboten", "Excluded"), "avoid" => N("Vermeiden", "Avoid"),
        "conditional" => N("Bedingt erlaubt", "Conditional"), "allow" => N("Erlaubt", "Allowed"),
        _ => N("Unbekannt", "Unknown")
    };

    private void FoodRule_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (refreshingNutrition || FoodRulesList.SelectedIndex < 0 || FoodRulesList.SelectedIndex >= displayedNutritionRules.Count) return;
        var rule = displayedNutritionRules[FoodRulesList.SelectedIndex];
        editingRuleId = rule.Id; RulePattern.Text = rule.Pattern; RuleNote.Text = rule.Note;
        RuleSource.Text = rule.Source; RulePriority.Value = rule.Priority;
        RuleDecision.SelectedIndex = rule.Decision switch
        { "exclude" => 0, "avoid" => 1, "conditional" => 2, "allow" => 3, _ => 0 };
    }

    private void NewFoodRule_Click(object sender, RoutedEventArgs e)
    {
        editingRuleId = 0; FoodRulesList.SelectedIndex = -1;
        RulePattern.Text = RuleNote.Text = RuleSource.Text = "";
        RuleDecision.SelectedIndex = 0; RulePriority.Value = 10;
        RuleEditorStatus.Text = N("Neue Regel vorbereitet.", "New rule ready.");
    }

    private void SaveFoodRule_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RulePattern.Text))
        { RuleEditorStatus.Text = N("Bitte einen Suchbegriff eingeben.", "Enter a matching term."); return; }
        try
        {
            store.SaveFoodRule(new FoodRule { Id = editingRuleId, Pattern = RulePattern.Text.Trim(),
                Decision = (RuleDecision.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "exclude",
                Note = RuleNote.Text.Trim(), Priority = (int)(double.IsNaN(RulePriority.Value) ? 10 : RulePriority.Value),
                Source = string.IsNullOrWhiteSpace(RuleSource.Text) ? N("Eigene Regel", "User rule") : RuleSource.Text.Trim() });
            editingRuleId = 0; ReloadNutrition(); NewFoodRule_Click(sender, e);
            RuleEditorStatus.Text = N("Regel gespeichert.", "Rule saved.");
        }
        catch (Exception ex) { RuleEditorStatus.Text = N("Speichern fehlgeschlagen: ", "Save failed: ") + ex.Message; }
    }

    private void DeleteFoodRule_Click(object sender, RoutedEventArgs e)
    {
        if (editingRuleId == 0) return;
        try { store.DeleteFoodRule(editingRuleId); editingRuleId = 0; ReloadNutrition(); NewFoodRule_Click(sender, e); }
        catch (Exception ex) { RuleEditorStatus.Text = ex.Message; }
    }

    private void MealFoodSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var term = sender.Text.Trim();
        sender.ItemsSource = term.Length < 2 ? [] : nutritionFoods.Where(food => FoodNameMatches(food.Name, term))
            .OrderBy(food => food.Name.StartsWith(term, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1)
            .ThenBy(food => food.Name.Length).Take(25).Select(food => food.Name).ToArray();
    }

    private void MealFoodSearch_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) =>
        sender.Text = args.SelectedItem?.ToString() ?? "";

    private void MealAddFood_Click(object sender, RoutedEventArgs e) => TryAddMealIngredient();

    private bool TryAddMealIngredient()
    {
        var food = nutritionFoods.FirstOrDefault(item => item.Name.Equals(MealFoodSearch.Text.Trim(), StringComparison.CurrentCultureIgnoreCase));
        var existing = editingIngredientIndex >= 0 && editingIngredientIndex < mealIngredients.Count
            ? mealIngredients[editingIngredientIndex] : null;
        if (food is null && (existing is null || !existing.Name.Equals(MealFoodSearch.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)))
        { MealStatusText.Text = N("Bitte ein Lebensmittel aus der Liste auswählen oder unter „Lebensmittel“ anlegen.",
            "Choose a food from the list or add it under Foods."); return false; }
        if (double.IsNaN(MealFoodGrams.Value) || MealFoodGrams.Value <= 0)
        { MealStatusText.Text = N("Bitte eine positive Menge eingeben.", "Enter a positive amount."); return false; }
        var ingredient = new MealIngredient { FoodId = food?.Id ?? existing!.FoodId,
            Name = food?.Name ?? existing!.Name, Grams = MealFoodGrams.Value,
            CarbsPer100G = food is null ? existing?.CarbsPer100G : food.CarbsPer100G,
            GlycemicLoadPer100G = food is null ? existing?.GlycemicLoadPer100G : food.GlycemicLoadPer100G,
            BlsCode = food?.BlsCode ?? existing?.BlsCode ?? "",
            NutrientSource = food?.Source ?? existing?.NutrientSource ?? "" };
        var wasEditing = existing is not null;
        if (existing is null) mealIngredients.Add(ingredient);
        else mealIngredients[editingIngredientIndex] = ingredient;
        editingIngredientIndex = -1;
        MealFoodSearch.Text = ""; MealFoodGrams.Value = 100;
        UpdateMealIngredientEditor(); RenderMealIngredients();
        MealStatusText.Text = wasEditing ? N("Zutat geändert.", "Ingredient updated.") :
            N("Zutat hinzugefügt.", "Ingredient added.");
        return true;
    }

    private void MealIngredients_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var index = MealIngredientsList.SelectedIndex;
        if (index < 0 || index >= displayedIngredientIndices.Count) return;
        editingIngredientIndex = displayedIngredientIndices[index];
        MealFoodSearch.Text = mealIngredients[editingIngredientIndex].Name;
        MealFoodGrams.Value = mealIngredients[editingIngredientIndex].Grams;
        UpdateMealIngredientEditor();
        MealStatusText.Text = N($"Du bearbeitest „{mealIngredients[editingIngredientIndex].Name}“.",
            $"You are editing “{mealIngredients[editingIngredientIndex].Name}”.");
    }

    private void MealCancelFoodEdit_Click(object sender, RoutedEventArgs e)
    {
        editingIngredientIndex = -1;
        MealFoodSearch.Text = ""; MealFoodGrams.Value = 100;
        UpdateMealIngredientEditor();
        MealStatusText.Text = "";
    }

    private void UpdateMealIngredientEditor()
    {
        MealAddFoodButton.Visibility = editingIngredientIndex >= 0 ? Visibility.Collapsed : Visibility.Visible;
        MealUpdateFoodButton.Visibility = editingIngredientIndex >= 0 ? Visibility.Visible : Visibility.Collapsed;
        MealCancelFoodEditButton.Visibility = editingIngredientIndex >= 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MealRemoveFood_Click(object sender, RoutedEventArgs e)
    {
        var index = MealIngredientsList.SelectedIndex;
        if (index < 0 || index >= displayedIngredientIndices.Count) return;
        var originalIndex = displayedIngredientIndices[index];
        mealIngredients.RemoveAt(originalIndex);
        if (editingIngredientIndex == originalIndex) MealCancelFoodEdit_Click(sender, e);
        else if (editingIngredientIndex > originalIndex) editingIngredientIndex--;
        RenderMealIngredients();
    }

    private void MealIngredients_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MealRemoveFoodButton is not null)
            MealRemoveFoodButton.IsEnabled = MealIngredientsList.SelectedIndex >= 0;
    }

    private FoodRule? MatchingRule(string name) => nutritionRules
        .Where(rule => name.Contains(rule.Pattern, StringComparison.CurrentCultureIgnoreCase))
        .OrderByDescending(rule => rule.Priority).ThenByDescending(rule => rule.Pattern.Length).FirstOrDefault();

    private void RenderMealIngredients()
    {
        if (MealIngredientsList is null) return;
        var (column, descending) = tableSort["ingredients"];
        string[] Cells(MealIngredient item) => [item.Name, $"{item.Grams:0.#} g",
            item.CarbsPer100G is { } carbs ? $"{carbs * item.Grams / 100:0.#} g" : "?"];
        string Key(MealIngredient item) => column switch
        {
            1 => Numeric(item.Grams), 2 => Numeric(item.CarbsPer100G is { } carbs
                ? carbs * item.Grams / 100 : -1), _ => item.Name
        };
        var filtered = Enumerable.Range(0, mealIngredients.Count)
            .Where(index => MatchesTableFilters("ingredients", Cells(mealIngredients[index])));
        displayedIngredientIndices.Clear();
        displayedIngredientIndices.AddRange(descending
            ? filtered.OrderByDescending(index => Key(mealIngredients[index]), StringComparer.CurrentCultureIgnoreCase)
            : filtered.OrderBy(index => Key(mealIngredients[index]), StringComparer.CurrentCultureIgnoreCase));
        MealIngredientsList.ItemsSource = displayedIngredientIndices.Select(index =>
        {
            var item = mealIngredients[index];
            var row = NutritionTableRow("ingredients", Cells(item));
            var rule = MatchingRule(item.Name);
            ToolTipService.SetToolTip(row.Children.OfType<TextBlock>().First(), $"GL: {Nutrient(item.GlycemicLoadPer100G * item.Grams / 100)} · " +
                (rule is null ? N("Keine passende Regel", "No matching rule") : RuleLabel(rule.Decision) + ": " + rule.Note));
            return row;
        }).ToArray();
        FillMealIngredientsWidth();
        ConfigureListFeedback(MealIngredientsList);
        MealRemoveFoodButton.IsEnabled = false;
        var carbsKnown = mealIngredients.All(item => item.CarbsPer100G is not null);
        var glKnown = mealIngredients.All(item => item.GlycemicLoadPer100G is not null);
        var exclusions = mealIngredients.Where(item => MatchingRule(item.Name)?.Decision == "exclude").ToArray();
        var advice = mealIngredients.Where(item => MatchingRule(item.Name)?.Decision is "conditional" or "avoid").ToArray();
        var ruleWarning = exclusions.Length > 0
            ? N("Ausschluss: ", "Excluded: ") + string.Join(", ", exclusions.Select(item => item.Name))
            : advice.Length > 0
                ? N("Bedingungen prüfen: ", "Check conditions: ") + string.Join(", ", advice.Select(item => item.Name))
                : "";
        MealAssessment.Text = N("Mahlzeit: ", "Meal: ") +
            (carbsKnown ? $"{mealIngredients.Sum(item => item.CarbsPer100G!.Value * item.Grams / 100):0.#} g KH" :
                N("KH unvollständig", "carbs incomplete")) + "  ·  " +
            (glKnown ? $"GL ≈ {mealIngredients.Sum(item => item.GlycemicLoadPer100G!.Value * item.Grams / 100):0.#}" :
                N("GL unvollständig", "GL incomplete")) + "\n" +
            (ruleWarning.Length > 0 ? ruleWarning + "\n" : "") +
            N("Die GL-Ampel gilt je Lebensmittel pro 100 g; die Mahlzeiten-GL ist nur eine Schätzung.",
              "GL thresholds apply to each food per 100 g; meal GL is only an estimate.");
        MealAssessment.Foreground = new SolidColorBrush(exclusions.Length > 0 ?
            Windows.UI.Color.FromArgb(255, 160, 33, 33) : Windows.UI.Color.FromArgb(255, 32, 65, 80));
    }

    private async Task SaveMealAsync(string status)
    {
        if (savingMeal || confirmingPendingMealIngredient) return;
        if (status == "consumed" && !string.IsNullOrWhiteSpace(MealFoodSearch.Text))
        {
            var pendingName = MealFoodSearch.Text.Trim();
            var food = nutritionFoods.FirstOrDefault(item => item.Name.Equals(pendingName, StringComparison.CurrentCultureIgnoreCase));
            var existing = editingIngredientIndex >= 0 && editingIngredientIndex < mealIngredients.Count
                ? mealIngredients[editingIngredientIndex] : null;
            if (food is not null || existing?.Name.Equals(pendingName, StringComparison.CurrentCultureIgnoreCase) == true)
            {
                if (double.IsNaN(MealFoodGrams.Value) || MealFoodGrams.Value <= 0)
                { MealStatusText.Text = N("Bitte eine positive Menge eingeben.", "Enter a positive amount."); return; }
                confirmingPendingMealIngredient = true;
                ContentDialogResult answer;
                try
                {
                    var dialog = new ContentDialog
                    {
                        Title = N("Zutat übernehmen?", "Add ingredient?"),
                        Content = N($"„{pendingName}“ ({MealFoodGrams.Value:0.#} g) ist noch nicht in der Zutatenliste. Zur Mahlzeit hinzufügen und als gegessen speichern?",
                            $"“{pendingName}” ({MealFoodGrams.Value:0.#} g) is not yet in the ingredient list. Add it and save the meal as consumed?"),
                        PrimaryButtonText = N("Hinzufügen und speichern", "Add and save"),
                        CloseButtonText = N("Abbrechen", "Cancel"),
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = ((FrameworkElement)Content).XamlRoot
                    };
                    answer = await dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    MealStatusText.Text = N("Nachfrage konnte nicht angezeigt werden: ",
                        "Could not show confirmation: ") + ex.Message;
                    return;
                }
                finally { confirmingPendingMealIngredient = false; }
                if (answer != ContentDialogResult.Primary) return;
                if (!TryAddMealIngredient()) return;
                if (string.IsNullOrWhiteSpace(MealName.Text)) MealName.Text = food?.Name ?? existing!.Name;
            }
        }
        if (mealIngredients.Count == 0 || string.IsNullOrWhiteSpace(MealName.Text))
        { MealStatusText.Text = N("Bitte Bezeichnung und mindestens eine Zutat eintragen.",
            "Enter a name and at least one ingredient."); return; }
        if (MealDate.Date is not { } day || !TimeOnly.TryParseExact(MealTime.Text.Trim(), "HH:mm",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        { MealStatusText.Text = N("Bitte Datum und Uhrzeit prüfen.", "Check date and time."); return; }
        var preserveOriginal = editingMealStatus == "template" && status != "template"
            || editingMealStatus != "" && editingMealStatus != "template" && status == "template";
        var meal = new MealRecord { Id = preserveOriginal ? 0 : editingMealId, Name = MealName.Text.Trim(),
            At = day.Date.Add(time.ToTimeSpan()), Status = status,
            Ingredients = mealIngredients.Select(item => new MealIngredient { FoodId = item.FoodId,
                Name = item.Name, Grams = item.Grams, CarbsPer100G = item.CarbsPer100G,
                GlycemicLoadPer100G = item.GlycemicLoadPer100G }).ToList(), Note = MealNote.Text.Trim() };
        await BeginMealSaveAsync(status);
        try
        {
            await Task.Run(() => store.SaveMeal(meal));
            ResetMeal(); ReloadMealData();
            MealStatusText.Text = N("Mahlzeit gespeichert.", "Meal saved.");
        }
        catch (Exception ex) { MealStatusText.Text = N("Speichern fehlgeschlagen: ", "Save failed: ") + ex.Message; }
        finally { EndMealSave(); }
    }

    private async void SavePlannedMeal_Click(object sender, RoutedEventArgs e) => await SaveMealAsync("planned");
    private async void SaveConsumedMeal_Click(object sender, RoutedEventArgs e) => await SaveMealAsync("consumed");
    private async void SaveMealTemplate_Click(object sender, RoutedEventArgs e) => await SaveMealAsync("template");
    private void ResetMeal_Click(object sender, RoutedEventArgs e) => ResetMeal();

    private void ResetMeal()
    {
        editingMealId = 0; editingMealStatus = ""; MealHistoryList.SelectedIndex = -1;
        MealEditingBanner.Visibility = Visibility.Collapsed;
        UpdateMealSaveButtonColors("planned");
        editingIngredientIndex = -1; UpdateMealIngredientEditor();
        mealIngredients.Clear(); MealDate.Date = DateTimeOffset.Now;
        MealTime.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        MealName.Text = MealNote.Text = MealFoodSearch.Text = "";
        MealFoodGrams.Value = 100; RenderMealIngredients();
    }

    private void RenderMealHistory()
    {
        if (MealHistoryList is null) return;
        var (column, descending) = tableSort["meals"];
        string Status(MealRecord meal) => meal.Status switch
        {
            "consumed" => N("gegessen", "consumed"), "planned" => N("geplant", "planned"),
            _ => N("Vorlage", "Template")
        };
        double? Carbs(MealRecord meal) => meal.Ingredients.All(item => item.CarbsPer100G is not null)
            ? meal.Ingredients.Sum(item => item.CarbsPer100G!.Value * item.Grams / 100) : null;
        string[] Cells(MealRecord meal) => [meal.Status == "template" ? "" : meal.At.ToString("dd.MM.yyyy HH:mm"),
            meal.Name, Status(meal), meal.Ingredients.Count.ToString(CultureInfo.CurrentCulture),
            Carbs(meal) is { } carbs ? $"{carbs:0.#} g" : "?", meal.Note];
        string Key(MealRecord meal) => column switch
        {
            0 => meal.Status == "template" ? "" : meal.At.ToString("O"),
            3 => Numeric(meal.Ingredients.Count), 4 => Numeric(Carbs(meal) ?? -1),
            _ => Cells(meal)[column]
        };
        var filtered = nutritionMeals.Where(meal => meal.Status is "template" or "planned" &&
            MatchesTableFilters("meals", Cells(meal)));
        displayedNutritionMeals.Clear();
        displayedNutritionMeals.AddRange(descending
            ? filtered.OrderByDescending(Key, StringComparer.CurrentCultureIgnoreCase)
            : filtered.OrderBy(Key, StringComparer.CurrentCultureIgnoreCase));
        MealHistoryList.ItemsSource = displayedNutritionMeals.Select(meal =>
        {
            var row = NutritionTableRow("meals", Cells(meal));
            if (MealIsDue(meal, DateTime.Now))
                row.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 229, 229));
            return row;
        }).ToArray();
        ConfigureListFeedback(MealHistoryList);
    }

    private static bool MealIsDue(MealRecord meal, DateTime now) =>
        meal.Status == "planned" && meal.At <= now;

    private void RefreshMealDueIndicators()
    {
        if (NutritionDueIcon is null || MealsDueIcon is null) return;
        var now = DateTime.Now;
        var due = nutritionMeals.Any(meal => MealIsDue(meal, now));
        NutritionDueIcon.Visibility = MealsDueIcon.Visibility = due ? Visibility.Visible : Visibility.Collapsed;
        if (MealHistoryList is not null && listFeedbackRows.TryGetValue(MealHistoryList, out var rows) &&
            displayedNutritionMeals.Count == rows.Count)
        {
            for (var index = 0; index < rows.Count; index++)
                rows[index] = (rows[index].Row, MealIsDue(displayedNutritionMeals[index], now)
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 229, 229))
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent));
            UpdateListFeedback(MealHistoryList);
        }
    }

    private void MealHistory_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (!SelectMealAt(e.OriginalSource as DependencyObject)) return;
        var index = MealHistoryList.SelectedIndex;
        if (index < 0 || index >= displayedNutritionMeals.Count) return;
        if (displayedNutritionMeals[index].Status == "template")
            ConsumeSelectedMeal_Click(sender, e);
        else
            EditMeal(displayedNutritionMeals[index]);
        e.Handled = true;
    }

    private bool SelectMealAt(DependencyObject? source)
    {
        var node = source;
        while (node is not null && node is not ListViewItem)
            node = VisualTreeHelper.GetParent(node);
        if (node is not ListViewItem item) return false;
        MealHistoryList.SelectedItem = item.Content;
        return true;
    }

    private void MealHistory_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e) =>
        SelectMealAt(e.OriginalSource as DependencyObject);

    private void MealHistoryMenu_Opening(object sender, object e)
    {
        var selected = MealHistoryList.SelectedIndex >= 0 && MealHistoryList.SelectedIndex < displayedNutritionMeals.Count;
        foreach (var item in new[] { EditMealMenu, PlanMealMenu, ConsumeMealMenu, DeleteMealMenu })
        {
            item.IsEnabled = selected;
            item.Text = T(item == EditMealMenu ? "Auswahl bearbeiten" : item == PlanMealMenu
                ? "Auswahl als geplant übernehmen" : item == ConsumeMealMenu
                    ? "Auswahl als gegessen übernehmen" : "Auswahl entfernen");
        }
    }

    private void EditSelectedMealTemplate_Click(object sender, RoutedEventArgs e)
    {
        var index = MealHistoryList.SelectedIndex;
        if (index >= 0 && index < displayedNutritionMeals.Count)
            EditMeal(displayedNutritionMeals[index]);
    }

    private void EditMeal(MealRecord meal)
    {
        SetEntriesExpanded(false);
        MainTabs.SelectedItem = NutritionTab;
        NutritionTabs.SelectedIndex = 0;
        editingMealId = meal.Id;
        editingMealStatus = meal.Status;
        MealEditingText.Text = meal.Status == "template"
            ? N("Du bearbeitest die Vorlage „", "You are editing the template “") + meal.Name + N("“.", "”.")
            : N("Du bearbeitest die Mahlzeit „", "You are editing the meal “") + meal.Name +
                N("“ vom ", "” from ") + meal.At.ToString("dd.MM.yyyy, HH:mm") + N(" Uhr.", ".");
        MealEditingBanner.Visibility = Visibility.Visible;
        UpdateMealSaveButtonColors(meal.Status);
        editingIngredientIndex = -1; UpdateMealIngredientEditor();
        MealFoodSearch.Text = ""; MealFoodGrams.Value = 100;
        MealName.Text = meal.Name; MealDate.Date = new DateTimeOffset(meal.At);
        MealTime.Text = meal.At.ToString("HH:mm", CultureInfo.InvariantCulture);
        MealNote.Text = meal.Note; mealIngredients.Clear(); mealIngredients.AddRange(meal.Ingredients);
        RenderMealIngredients(); MealStatusText.Text = N("Gespeicherte Mahlzeit wird bearbeitet.", "Editing saved meal.");
        DispatcherQueue.TryEnqueue(() => { MealName.Focus(FocusState.Programmatic); });
    }

    private void UpdateMealSaveButtonColors(string status)
    {
        var accent = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 119, 137));
        var white = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        var dark = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 31, 31, 31));
        SavePlannedMealButton.Background = status == "planned" ? accent : white;
        SavePlannedMealButton.Foreground = status == "planned" ? white : dark;
        SaveConsumedMealButton.Background = status == "consumed" ? accent : white;
        SaveConsumedMealButton.Foreground = status == "consumed" ? white : dark;
        SaveMealTemplateButton.Background = status == "template" ? accent : white;
        SaveMealTemplateButton.Foreground = status == "template" ? white : dark;
    }

    private Grid MealEntryRow(Entry entry, MealRecord meal)
    {
        var row = TableRow([(entry.Start.ToString("dd.MM.yyyy HH:mm"), 180), ("", 180),
            ("Mahlzeit", 115), (meal.Name, 400), ("", 340)], compact: true, table: "entries");
        var ingredients = string.Join(" · ", meal.Ingredients.Select(item =>
            $"{item.Name} ({item.Grams:0.#} g)"));
        var details = N("Zutaten: ", "Ingredients: ") + ingredients +
            (string.IsNullOrWhiteSpace(meal.Note) ? "" : "\n" + N("Notiz: ", "Note: ") + meal.Note);
        AddEntryDetails(row, details);
        return row;
    }

    private async void PlanSelectedMeal_Click(object sender, RoutedEventArgs e) => await CopySelectedMealAsync("planned");
    private async void ConsumeSelectedMeal_Click(object sender, RoutedEventArgs e) => await CopySelectedMealAsync("consumed");

    private async Task CopySelectedMealAsync(string status)
    {
        if (savingMeal) return;
        var index = MealHistoryList.SelectedIndex;
        if (index < 0 || index >= displayedNutritionMeals.Count) return;
        var original = displayedNutritionMeals[index];
        var at = DateTime.Now;
        var meal = new MealRecord { Id = status == "consumed" && original.Status == "planned" ? original.Id : 0,
            Name = original.Name, At = at, Status = status,
            Ingredients = original.Ingredients, Note = original.Note };
        await BeginMealSaveAsync();
        try
        {
            await Task.Run(() => store.SaveMeal(meal));
            ReloadMealData(); MealStatusText.Text = status == "planned"
                ? N("Als geplant übernommen.", "Copied as planned.")
                : N("Als gegessen erfasst.", "Recorded as consumed.");
            if (status == "consumed" && original.Status == "template")
                ShowMealTemplateFeedback(original.Name, at);
        }
        catch (Exception ex) { MealStatusText.Text = ex.Message; }
        finally { EndMealSave(); }
    }

    private async void ShowMealTemplateFeedback(string name, DateTime at)
    {
        var generation = ++mealFeedbackGeneration;
        MealTemplateFeedbackText.Text = N("„", "“") + name + N("“ wurde für heute um ",
            "” was recorded as consumed today at ") + at.ToString("HH:mm") +
            N(" Uhr als gegessen erfasst.", ".");
        MealTemplateFeedback.Visibility = Visibility.Visible;
        await Task.Delay(TimeSpan.FromSeconds(5));
        if (generation == mealFeedbackGeneration)
            MealTemplateFeedback.Visibility = Visibility.Collapsed;
    }

    private void DeleteSelectedMeal_Click(object sender, RoutedEventArgs e)
    {
        var index = MealHistoryList.SelectedIndex;
        if (index < 0 || index >= displayedNutritionMeals.Count) return;
        try { store.DeleteMeal(displayedNutritionMeals[index].Id); ResetMeal(); ReloadMealData(); }
        catch (Exception ex) { MealStatusText.Text = ex.Message; }
    }

    private void RenderFoodAnalysis()
    {
        if (FoodAnalysisRows is null) return;
        FoodAnalysisRows.Children.Clear();
        var threshold = AnalysisPeriod?.SelectedIndex >= 0 ? AnalysisThreshold() : DateTime.MinValue;
        var consumed = nutritionMeals.Where(meal => meal.Status == "consumed" && meal.At >= threshold)
            .OrderByDescending(meal => meal.At).Take(80).ToArray();
        if (consumed.Length == 0)
        {
            FoodAnalysisRows.Children.Add(new TextBlock { Text = N("Keine gegessenen Mahlzeiten im gewählten Zeitraum.",
                "No consumed meals in the selected period.") }); return;
        }
        var states = entries.Where(entry => entry.Kind == "Zustand").OrderBy(entry => entry.Start).ToArray();
        foreach (var meal in consumed)
        {
            var parts = new List<string>();
            foreach (var (from, to) in new[] { (0, 6), (6, 24), (24, 48), (48, 72) })
            {
                var following = states.Where(entry => entry.Start > meal.At.AddHours(from) &&
                    entry.Start <= meal.At.AddHours(to)).ToArray();
                var severities = following.Select(entry =>
                {
                    try { return JsonSerializer.Deserialize<StateData>(entry.Data); }
                    catch (JsonException) { return null; }
                }).Where(data => data is not null).ToArray();
                parts.Add($"{from}–{to} h: " + (severities.Length == 0 ? "–" :
                    $"{severities.Length} {N("Zustände", "conditions")}, " +
                    $"Ø {N("Allgemein", "overall")} {severities.Average(data => data!.Overall):0.#}, " +
                    $"Ø {N("Symptomstärken", "symptom severity sum")} " +
                    $"{severities.Average(data => data!.Symptoms?.Values.Where(value => value > 0).Sum() ?? 0):0.#}, " +
                    $"{N("PEM", "PEM")} {severities.Count(data => data!.Pem > 0)}"));
            }
            var baseline = states.LastOrDefault(entry => entry.Start <= meal.At && entry.Start >= meal.At.AddHours(-24));
            var before = baseline is null ? N("kein Zustand davor", "no prior condition") :
                N("Zustand davor: ", "prior condition: ") + baseline.Start.ToString("dd.MM. HH:mm");
            FoodAnalysisRows.Children.Add(new TextBlock { Text =
                $"{meal.At:dd.MM.yyyy HH:mm} · {meal.Name} · {before}\n" + string.Join("    |    ", parts),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        }
    }
}
