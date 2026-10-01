using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private sealed class CarbBucket
    {
        public DateTime Start { get; init; }
        public double KnownGrams { get; set; }
        public int MealCount { get; set; }
        public int MissingIngredients { get; set; }
    }

    private void FoodCarbChartViewport_SizeChanged(object sender, SizeChangedEventArgs e) => RenderFoodCarbChart();

    private void RenderFoodCarbChart()
    {
        if (FoodCarbChart is null || FoodCarbChartSummary is null || AnalysisPeriod is null ||
            AnalysisPeriod.SelectedIndex < 0) return;

        var chart = FoodCarbChart;
        chart.Children.Clear();
        var today = DateTime.Today;
        var threshold = AnalysisThreshold();
        var consumed = nutritionMeals.Where(meal => meal.Status == "consumed" &&
            meal.At >= threshold && meal.At.Date <= today).ToArray();
        if (consumed.Length == 0)
        {
            FoodCarbChartSummary.Text = N("Keine gegessenen Mahlzeiten im gewählten Zeitraum.",
                "No consumed meals in the selected period.");
            chart.Width = Math.Max(320, FoodCarbChartViewport.ActualWidth - 4);
            return;
        }

        // Short periods retain individual days; longer periods group days into calendar weeks or months.
        var period = AnalysisPeriod.SelectedIndex;
        var grouping = period <= 1 ? 0 : period == 2 ? 1 : 2;
        var first = period == 4 ? consumed.Min(meal => meal.At).Date : threshold.Date;
        if (grouping == 1) first = first.AddDays(-((int)first.DayOfWeek + 6) % 7);
        if (grouping == 2) first = new DateTime(first.Year, first.Month, 1);
        DateTime Next(DateTime date) => grouping switch
        {
            0 => date.AddDays(1), 1 => date.AddDays(7), _ => date.AddMonths(1)
        };
        var buckets = new List<CarbBucket>();
        for (var date = first; date <= today; date = Next(date))
            buckets.Add(new CarbBucket { Start = date });
        var byStart = buckets.ToDictionary(bucket => bucket.Start);
        foreach (var meal in consumed)
        {
            var date = meal.At.Date;
            if (grouping == 1) date = date.AddDays(-((int)date.DayOfWeek + 6) % 7);
            if (grouping == 2) date = new DateTime(date.Year, date.Month, 1);
            var bucket = byStart[date];
            bucket.MealCount++;
            foreach (var ingredient in meal.Ingredients)
            {
                if (ingredient.CarbsPer100G is { } carbs)
                    bucket.KnownGrams += carbs * ingredient.Grams / 100;
                else bucket.MissingIngredients++;
            }
        }

        var missing = buckets.Sum(bucket => bucket.MissingIngredients);
        var complete = buckets.Where(bucket => bucket.MealCount > 0 && bucket.MissingIngredients == 0).ToArray();
        var average = complete.Length == 0 ? 0 : complete.Average(bucket => bucket.KnownGrams);
        var unit = grouping switch { 0 => N("Tag", "day"), 1 => N("Woche", "week"), _ => N("Monat", "month") };
        FoodCarbChartSummary.Text = N(
            $"{consumed.Length} gegessene Mahlzeiten · {buckets.Sum(bucket => bucket.KnownGrams):0.#} g bekannte KH · Ø {average:0.#} g pro {unit} mit vollständigen KH-Werten." +
            (missing > 0 ? $" Bei {missing} Zutaten fehlt der KH-Wert; betroffene Balken sind Mindestwerte und bleiben beim Durchschnitt außen vor." : ""),
            $"{consumed.Length} consumed meals · {buckets.Sum(bucket => bucket.KnownGrams):0.#} g known carbs · average {average:0.#} g per {unit} with complete carb values." +
            (missing > 0 ? $" {missing} ingredients lack a carb value; affected bars are lower bounds and excluded from the average." : ""));

        const double left = 55, top = 22, bottom = 242, right = 24;
        var available = Math.Max(320, FoodCarbChartViewport.ActualWidth - 4);
        chart.Width = Math.Max(available, left + right + buckets.Count * 36);
        var plotWidth = chart.Width - left - right;
        var slot = plotWidth / buckets.Count;
        var highest = Math.Max(1, buckets.Max(bucket => bucket.KnownGrams));
        var scale = Math.Ceiling(Math.Max(highest, average) / 10) * 10;
        var culture = CultureInfo.GetCultureInfo(selectedLanguage == "en" ? "en-US" : "de-DE");
        var axis = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 202, 218, 227));
        var ink = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 45, 65, 78));
        var blue = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 119, 137));
        var amber = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 191, 105, 25));
        void Label(string value, double x, double y, Brush color, double width = 52)
        {
            var text = new TextBlock { Text = value, Width = width, TextAlignment = TextAlignment.Center,
                FontSize = 11, Foreground = color };
            Canvas.SetLeft(text, x); Canvas.SetTop(text, y); chart.Children.Add(text);
        }
        foreach (var fraction in new[] { 0.0, 0.5, 1.0 })
        {
            var y = bottom - fraction * (bottom - top);
            chart.Children.Add(new Line { X1 = left, X2 = chart.Width - right, Y1 = y, Y2 = y,
                Stroke = axis, StrokeThickness = 1 });
            Label((scale * fraction).ToString("0.#", culture), 0, y - 9, ink, 48);
        }
        if (complete.Length > 0)
        {
            var y = bottom - average / scale * (bottom - top);
            chart.Children.Add(new Line { X1 = left, X2 = chart.Width - right, Y1 = y, Y2 = y,
                Stroke = amber, StrokeThickness = 2 });
            Label("Ø", chart.Width - right, y - 10, amber, 20);
        }
        for (var i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[i];
            var x = left + (i + 0.5) * slot;
            var height = bucket.KnownGrams / scale * (bottom - top);
            var bar = new Rectangle { Width = Math.Min(28, slot * 0.65), Height = Math.Max(2, height),
                Fill = bucket.MissingIngredients > 0 ? amber : blue,
                Opacity = bucket.MealCount == 0 ? 0 : 1 };
            Canvas.SetLeft(bar, x - bar.Width / 2); Canvas.SetTop(bar, bottom - bar.Height);
            ToolTipService.SetToolTip(bar, N(
                $"{bucket.Start:dd.MM.yyyy} · {bucket.KnownGrams:0.#} g bekannte KH · {bucket.MealCount} Mahlzeiten · {bucket.MissingIngredients} Zutaten ohne KH-Wert",
                $"{bucket.Start:yyyy-MM-dd} · {bucket.KnownGrams:0.#} g known carbs · {bucket.MealCount} meals · {bucket.MissingIngredients} ingredients without a carb value"));
            chart.Children.Add(bar);
            if (bucket.MissingIngredients > 0)
                Label("?", x - 10, Math.Max(top, bottom - bar.Height - 22), amber, 20);
            if (i % Math.Max(1, (int)Math.Ceiling(54 / slot)) == 0)
            {
                var dateLabel = grouping == 2 ? bucket.Start.ToString("MM/yy", culture) :
                    bucket.Start.ToString("dd.MM.", culture);
                Label(dateLabel, x - 26, bottom + 8, ink);
            }
        }
    }
}
