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
    private readonly List<MealIngredient> mealIngredients = new();
    private long editingFoodId;
    private long editingRuleId;
    private long editingMealId;
    private int editingIngredientIndex = -1;
    private bool refreshingNutrition;

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
        ReloadNutrition();
    }

    private void ReloadNutrition()
    {
        try
        {
            refreshingNutrition = true;
            nutritionFoods.Clear(); nutritionFoods.AddRange(store.Foods());
            nutritionRules.Clear(); nutritionRules.AddRange(store.FoodRules());
            nutritionMeals.Clear(); nutritionMeals.AddRange(store.Meals());
            RenderFoodCatalog(); RenderFoodRules(); RenderMealHistory(); RenderMealIngredients(); RenderFoodAnalysis();
            if (EntryList is not null) DisplayEntries();
        }
        catch (Exception ex) { MealStatusText.Text = N("Ernährungsdaten konnten nicht geladen werden: ", "Could not load nutrition data: ") + ex.Message; }
        finally { refreshingNutrition = false; }
    }

    private static Grid NutritionRow(string content)
    {
        var row = new Grid { MinHeight = 32,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        row.Children.Add(new TextBlock { Text = content, VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(8, 4, 8, 4) });
        return row;
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
        var filtered = nutritionFoods.Where(food => MatchesTableFilters("foods", Cells(food)));
        visibleNutritionFoods.AddRange(descending ? filtered.OrderByDescending(Key, StringComparer.CurrentCultureIgnoreCase)
            : filtered.OrderBy(Key, StringComparer.CurrentCultureIgnoreCase));
        FoodCatalogList.ItemsSource = visibleNutritionFoods.Select(food => NutritionTableRow("foods", food.Name, Nutrient(food.CarbsPer100G),
            Nutrient(food.GlycemicIndex), Nutrient(food.GlycemicLoadPer100G), food.Source, food.Note)).ToArray();
        ConfigureListFeedback(FoodCatalogList);
        var index = visibleNutritionFoods.FindIndex(food => food.Id == selected);
        if (index >= 0) FoodCatalogList.SelectedIndex = index;
    }

    private void FoodCatalog_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = FoodCatalogList.SelectedIndex;
        if (DeleteFoodButton is not null) DeleteFoodButton.IsEnabled = index >= 0 && index < visibleNutritionFoods.Count;
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
    }

    private void NewFood_Click(object sender, RoutedEventArgs e)
    {
        editingFoodId = 0; FoodCatalogList.SelectedIndex = -1;
        DeleteFoodButton.IsEnabled = false;
        FoodName.Text = FoodSource.Text = FoodNote.Text = "";
        FoodCarbs.Value = FoodGi.Value = FoodGl.Value = double.NaN;
        FoodEditorStatus.Text = N("Neues Lebensmittel vorbereitet.", "New food ready.");
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
        try
        {
            store.SaveFood(new FoodItem { Id = editingFoodId, Name = name,
                CarbsPer100G = carbs, GlycemicIndex = gi, GlycemicLoadPer100G = gl,
                Source = string.IsNullOrWhiteSpace(FoodSource.Text) ? N("Eigener Eintrag", "User entry") : FoodSource.Text.Trim(),
                Note = FoodNote.Text.Trim() });
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
        sender.ItemsSource = term.Length < 2 ? [] : nutritionFoods.Where(food =>
            food.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)).Take(25).Select(food => food.Name).ToArray();
    }

    private void MealFoodSearch_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) =>
        sender.Text = args.SelectedItem?.ToString() ?? "";

    private void MealAddFood_Click(object sender, RoutedEventArgs e)
    {
        var food = nutritionFoods.FirstOrDefault(item => item.Name.Equals(MealFoodSearch.Text.Trim(), StringComparison.CurrentCultureIgnoreCase));
        var existing = editingIngredientIndex >= 0 && editingIngredientIndex < mealIngredients.Count
            ? mealIngredients[editingIngredientIndex] : null;
        if (food is null && (existing is null || !existing.Name.Equals(MealFoodSearch.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)))
        { MealStatusText.Text = N("Bitte ein Lebensmittel aus der Liste auswählen oder unter „Lebensmittel“ anlegen.",
            "Choose a food from the list or add it under Foods."); return; }
        if (double.IsNaN(MealFoodGrams.Value) || MealFoodGrams.Value <= 0)
        { MealStatusText.Text = N("Bitte eine positive Menge eingeben.", "Enter a positive amount."); return; }
        var ingredient = new MealIngredient { FoodId = food?.Id ?? existing!.FoodId,
            Name = food?.Name ?? existing!.Name, Grams = MealFoodGrams.Value,
            CarbsPer100G = food is null ? existing?.CarbsPer100G : food.CarbsPer100G,
            GlycemicLoadPer100G = food is null ? existing?.GlycemicLoadPer100G : food.GlycemicLoadPer100G };
        var wasEditing = existing is not null;
        if (existing is null) mealIngredients.Add(ingredient);
        else mealIngredients[editingIngredientIndex] = ingredient;
        editingIngredientIndex = -1;
        MealFoodSearch.Text = ""; MealFoodGrams.Value = 100;
        UpdateMealIngredientEditor(); RenderMealIngredients();
        MealStatusText.Text = wasEditing ? N("Zutat geändert.", "Ingredient updated.") :
            N("Zutat hinzugefügt.", "Ingredient added.");
    }

    private void MealIngredients_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var index = MealIngredientsList.SelectedIndex;
        if (index < 0 || index >= mealIngredients.Count) return;
        editingIngredientIndex = index;
        MealFoodSearch.Text = mealIngredients[index].Name;
        MealFoodGrams.Value = mealIngredients[index].Grams;
        UpdateMealIngredientEditor();
        MealStatusText.Text = N($"Du bearbeitest „{mealIngredients[index].Name}“.",
            $"You are editing “{mealIngredients[index].Name}”.");
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
        if (index < 0 || index >= mealIngredients.Count) return;
        mealIngredients.RemoveAt(index);
        if (editingIngredientIndex == index) MealCancelFoodEdit_Click(sender, e);
        else if (editingIngredientIndex > index) editingIngredientIndex--;
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
        MealIngredientsList.ItemsSource = mealIngredients.Select(item => NutritionRow(
            $"{item.Name}  ·  {item.Grams:0.#} g  ·  KH {Nutrient(item.CarbsPer100G * item.Grams / 100)} g  ·  " +
            $"GL {Nutrient(item.GlycemicLoadPer100G * item.Grams / 100)}  ·  " +
            (MatchingRule(item.Name) is { } rule ? RuleLabel(rule.Decision) + ": " + rule.Note + "  ·  " : "") +
            (item.GlycemicLoadPer100G is { } gl ? gl > 20 ? N("GL/100 g > 20: meiden", "GL/100 g > 20: avoid") :
                gl >= 10 ? N("GL/100 g 10–20: vermeiden", "GL/100 g 10–20: avoid") : N("GL/100 g < 10", "GL/100 g < 10") :
                N("GL unbekannt", "GL unknown")))).ToArray();
        ConfigureListFeedback(MealIngredientsList);
        MealRemoveFoodButton.IsEnabled = false;
        var carbsKnown = mealIngredients.All(item => item.CarbsPer100G is not null);
        var glKnown = mealIngredients.All(item => item.GlycemicLoadPer100G is not null);
        var exclusions = mealIngredients.Where(item => MatchingRule(item.Name)?.Decision == "exclude").ToArray();
        var advice = mealIngredients.Where(item => MatchingRule(item.Name)?.Decision is "conditional" or "avoid").ToArray();
        MealAssessment.Text = N("Mahlzeit: ", "Meal: ") +
            (carbsKnown ? $"{mealIngredients.Sum(item => item.CarbsPer100G!.Value * item.Grams / 100):0.#} g KH" :
                N("KH unvollständig", "carbs incomplete")) + "  ·  " +
            (glKnown ? $"GL ≈ {mealIngredients.Sum(item => item.GlycemicLoadPer100G!.Value * item.Grams / 100):0.#}" :
                N("GL unvollständig", "GL incomplete")) + "\n" +
            (exclusions.Length > 0 ? N("Ausschluss: ", "Excluded: ") + string.Join(", ", exclusions.Select(item => item.Name)) :
                advice.Length > 0 ? N("Bedingungen prüfen: ", "Check conditions: ") + string.Join(", ", advice.Select(item => item.Name)) :
                N("Keine passende Ausschlussregel gefunden. Unbekannte Zutaten müssen geprüft werden.",
                  "No matching exclusion found. Check unknown ingredients.")) + "\n" +
            N("Die GL-Ampel gilt je Lebensmittel pro 100 g; die Mahlzeiten-GL ist nur eine Schätzung.",
              "GL thresholds apply to each food per 100 g; meal GL is only an estimate.");
        MealAssessment.Foreground = new SolidColorBrush(exclusions.Length > 0 ?
            Windows.UI.Color.FromArgb(255, 160, 33, 33) : Windows.UI.Color.FromArgb(255, 32, 65, 80));
    }

    private void SaveMeal(string status)
    {
        if (mealIngredients.Count == 0 || string.IsNullOrWhiteSpace(MealName.Text))
        { MealStatusText.Text = N("Bitte Bezeichnung und mindestens eine Zutat eintragen.",
            "Enter a name and at least one ingredient."); return; }
        if (MealDate.Date is not { } day || !TimeOnly.TryParseExact(MealTime.Text.Trim(), "HH:mm",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        { MealStatusText.Text = N("Bitte Datum und Uhrzeit prüfen.", "Check date and time."); return; }
        try
        {
            store.SaveMeal(new MealRecord { Id = editingMealId, Name = MealName.Text.Trim(),
                At = day.Date.Add(time.ToTimeSpan()), Status = status,
                Ingredients = mealIngredients.Select(item => new MealIngredient { FoodId = item.FoodId,
                    Name = item.Name, Grams = item.Grams, CarbsPer100G = item.CarbsPer100G,
                    GlycemicLoadPer100G = item.GlycemicLoadPer100G }).ToList(), Note = MealNote.Text.Trim() });
            ResetMeal(); ReloadNutrition();
            MealStatusText.Text = N("Mahlzeit gespeichert.", "Meal saved.");
        }
        catch (Exception ex) { MealStatusText.Text = N("Speichern fehlgeschlagen: ", "Save failed: ") + ex.Message; }
    }

    private void SavePlannedMeal_Click(object sender, RoutedEventArgs e) => SaveMeal("planned");
    private void SaveConsumedMeal_Click(object sender, RoutedEventArgs e) => SaveMeal("consumed");
    private void SaveMealTemplate_Click(object sender, RoutedEventArgs e) => SaveMeal("template");
    private void ResetMeal_Click(object sender, RoutedEventArgs e) => ResetMeal();

    private void ResetMeal()
    {
        editingMealId = 0; MealHistoryList.SelectedIndex = -1;
        editingIngredientIndex = -1; UpdateMealIngredientEditor();
        mealIngredients.Clear(); MealDate.Date = DateTimeOffset.Now;
        MealTime.Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        MealName.Text = MealNote.Text = MealFoodSearch.Text = "";
        MealFoodGrams.Value = 100; RenderMealIngredients();
    }

    private void RenderMealHistory()
    {
        if (MealHistoryList is null) return;
        MealHistoryList.ItemsSource = nutritionMeals.Select(meal => NutritionRow(
            $"{(meal.Status == "template" ? N("Vorlage", "Template") : meal.At.ToString("dd.MM.yyyy HH:mm"))}  ·  " +
            $"{meal.Name}  ·  {meal.Ingredients.Count} {N("Zutaten", "ingredients")}  ·  " +
            (meal.Status == "consumed" ? N("gegessen", "consumed") : meal.Status == "planned" ? N("geplant", "planned") : ""))).ToArray();
        ConfigureListFeedback(MealHistoryList);
    }

    private void MealHistory_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var index = MealHistoryList.SelectedIndex;
        if (index < 0 || index >= nutritionMeals.Count) return;
        EditMeal(nutritionMeals[index]);
    }

    private void EditMeal(MealRecord meal)
    {
        SetEntriesExpanded(false);
        MainTabs.SelectedItem = NutritionTab;
        NutritionTabs.SelectedIndex = 0;
        editingMealId = meal.Id;
        editingIngredientIndex = -1; UpdateMealIngredientEditor();
        MealFoodSearch.Text = ""; MealFoodGrams.Value = 100;
        MealName.Text = meal.Name; MealDate.Date = new DateTimeOffset(meal.At);
        MealTime.Text = meal.At.ToString("HH:mm", CultureInfo.InvariantCulture);
        MealNote.Text = meal.Note; mealIngredients.Clear(); mealIngredients.AddRange(meal.Ingredients);
        RenderMealIngredients(); MealStatusText.Text = N("Gespeicherte Mahlzeit wird bearbeitet.", "Editing saved meal.");
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

    private void ConsumeSelectedMeal_Click(object sender, RoutedEventArgs e)
    {
        var index = MealHistoryList.SelectedIndex;
        if (index < 0 || index >= nutritionMeals.Count) return;
        var original = nutritionMeals[index];
        try
        {
            store.SaveMeal(new MealRecord { Id = original.Status == "planned" ? original.Id : 0,
                Name = original.Name, At = DateTime.Now, Status = "consumed",
                Ingredients = original.Ingredients, Note = original.Note });
            ReloadNutrition(); MealStatusText.Text = N("Als gegessen erfasst.", "Recorded as consumed.");
        }
        catch (Exception ex) { MealStatusText.Text = ex.Message; }
    }

    private void DeleteSelectedMeal_Click(object sender, RoutedEventArgs e)
    {
        var index = MealHistoryList.SelectedIndex;
        if (index < 0 || index >= nutritionMeals.Count) return;
        try { store.DeleteMeal(nutritionMeals[index].Id); ResetMeal(); ReloadNutrition(); }
        catch (Exception ex) { MealStatusText.Text = ex.Message; }
    }

    private void RenderFoodAnalysis()
    {
        if (FoodAnalysisRows is null) return;
        FoodAnalysisRows.Children.Clear();
        var consumed = nutritionMeals.Where(meal => meal.Status == "consumed").Take(80).ToArray();
        if (consumed.Length == 0)
        {
            FoodAnalysisRows.Children.Add(new TextBlock { Text = N("Noch keine gegessenen Mahlzeiten erfasst.",
                "No consumed meals recorded yet.") }); return;
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
