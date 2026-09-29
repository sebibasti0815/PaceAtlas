using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private static readonly string[] MedicationForms = ["Kapsel", "Tablette", "mg", "g", "ml", "Beutel", "Spray", "Pflaster", "Gel"];
    private static readonly SolidColorBrush MedicineInk = new(Windows.UI.Color.FromArgb(255, 33, 57, 76));
    private static readonly SolidColorBrush MedicineFill = new(Windows.UI.Color.FromArgb(255, 50, 154, 173));
    private static readonly SolidColorBrush MedicinePale = new(Windows.UI.Color.FromArgb(255, 204, 235, 240));

    private void PopulatePlanForms(string? selectedForm = null)
    {
        selectedForm ??= (PlanForm.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? MedicationForms[0];
        var forms = MedicationForms.Contains(selectedForm, StringComparer.OrdinalIgnoreCase)
            ? MedicationForms : MedicationForms.Append(selectedForm).ToArray(); // Preserve historical custom values.
        PlanForm.ItemsSource = forms.Select(form =>
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            content.Children.Add(MedicationFormIcon(form));
            content.Children.Add(new TextBlock { Text = T(form), VerticalAlignment = VerticalAlignment.Center });
            return new ComboBoxItem { Tag = form, Content = content };
        }).ToArray();
        PlanForm.SelectedIndex = Math.Max(0, Array.FindIndex(forms, form =>
            string.Equals(form, selectedForm, StringComparison.OrdinalIgnoreCase)));
    }

    private FrameworkElement MedicationFormCell(string form)
    {
        var icon = MedicationFormIcon(form);
        icon.Margin = new Thickness(8, 1, 8, 1);
        icon.VerticalAlignment = VerticalAlignment.Center;
        var description = T(form);
        ToolTipService.SetToolTip(icon, description);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(icon, description);
        return icon;
    }

    private static FrameworkElement MedicationFormIcon(string form)
    {
        var canvas = new Canvas { Width = 32, Height = 25 };
        static void Put(Canvas parent, UIElement child, double x, double y)
        {
            Canvas.SetLeft(child, x);
            Canvas.SetTop(child, y);
            parent.Children.Add(child);
        }
        static Microsoft.UI.Xaml.Shapes.Rectangle Box(double width, double height, double radius = 2,
            SolidColorBrush? fill = null) => new()
            {
                Width = width, Height = height, RadiusX = radius, RadiusY = radius,
                Stroke = MedicineInk, StrokeThickness = 1.5, Fill = fill ?? MedicinePale
            };
        static Line Stroke(double x1, double y1, double x2, double y2) => new()
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = MedicineInk, StrokeThickness = 1.5
        };
        switch (form.Trim().ToLowerInvariant())
        {
            case "tablette" or "tablet":
                Put(canvas, new Ellipse { Width = 22, Height = 16, Stroke = MedicineInk,
                    StrokeThickness = 1.5, Fill = MedicinePale }, 5, 4);
                canvas.Children.Add(Stroke(11, 6, 21, 18)); // Score on a tablet.
                break;
            case "kapsel" or "capsule":
                var capsule = new Canvas { Width = 27, Height = 16,
                    RenderTransform = new Microsoft.UI.Xaml.Media.RotateTransform { Angle = -25, CenterX = 13, CenterY = 8 } };
                Put(capsule, Box(26, 13, 7, MedicinePale), 0, 1);
                Put(capsule, new Microsoft.UI.Xaml.Shapes.Rectangle { Width = 11, Height = 11,
                    RadiusX = 5, RadiusY = 5, Fill = MedicineFill }, 1, 2);
                capsule.Children.Add(Stroke(12, 2, 12, 13));
                Put(canvas, capsule, 3, 5);
                break;
            case "spray":
                Put(canvas, Box(14, 14, 2, MedicinePale), 9, 9);
                Put(canvas, Box(5, 5, 1, MedicineFill), 13, 4);
                Put(canvas, Box(11, 3, 1, MedicineFill), 13, 2);
                canvas.Children.Add(Stroke(24, 3, 28, 3));
                break;
            case "pflaster" or "patch":
                Put(canvas, Box(24, 18, 3, MedicinePale), 4, 3);
                Put(canvas, Box(9, 8, 1, MedicineFill), 11, 8);
                break;
            case "gel":
                Put(canvas, Box(18, 17, 2, MedicinePale), 7, 4);
                Put(canvas, Box(22, 3, 1, MedicineFill), 5, 20);
                canvas.Children.Add(Stroke(11, 8, 21, 8));
                canvas.Children.Add(Stroke(12, 11, 20, 11));
                break;
            case "mg":
                foreach (var (x, y) in new[] { (6d, 7d), (14d, 4d), (22d, 8d), (10d, 15d), (20d, 16d) })
                    Put(canvas, new Ellipse { Width = 5, Height = 5, Fill = MedicineFill }, x, y);
                break;
            case "g":
                Put(canvas, Box(23, 21, 4, MedicinePale), 4, 2);
                Put(canvas, new TextBlock { Text = "g", FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = MedicineInk }, 11, 2);
                break;
            case "ml":
                Put(canvas, new Ellipse { Width = 13, Height = 16, Fill = MedicineFill,
                    Stroke = MedicineInk, StrokeThickness = 1 }, 10, 6);
                Put(canvas, new Ellipse { Width = 6, Height = 8, Fill = MedicineFill }, 13.5, 2);
                break;
            case "beutel" or "sachet":
                Put(canvas, Box(21, 20, 1, MedicinePale), 5, 3);
                Put(canvas, Box(21, 4, 1, MedicineFill), 5, 3);
                canvas.Children.Add(Stroke(9, 12, 22, 12));
                canvas.Children.Add(Stroke(11, 16, 20, 16));
                break;
            default:
                Put(canvas, new TextBlock { Text = "?", FontSize = 18, Foreground = MedicineInk }, 11, 0);
                break;
        }
        return canvas;
    }

    private static string IntakeStatusLabel(int index) => index switch
    {
        1 => "Genommen", 2 => "Ausgelassen", _ => "Offen"
    };

    private static string IntakeStatusIcon(int index) => index switch
    {
        1 => "✓", 2 => "✕", _ => "○"
    };

    private static SolidColorBrush IntakeStatusBrush(int index) => index switch
    {
        1 => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 126, 82)),
        2 => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 181, 64, 72)),
        _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 103, 119, 132))
    };

    private void PopulateIntakeStatus(ComboBox box, int selectedIndex)
    {
        box.ItemsSource = Enumerable.Range(0, 3).Select(index =>
        {
            var item = new ComboBoxItem
            {
                Content = new TextBlock { Text = IntakeStatusIcon(index), FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = IntakeStatusBrush(index) },
                Tag = IntakeStatusLabel(index)
            };
            ToolTipService.SetToolTip(item, T(IntakeStatusLabel(index)));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, T(IntakeStatusLabel(index)));
            return item;
        }).ToArray();
        box.SelectedIndex = selectedIndex;
        void SetDescription()
        {
            var description = T(IntakeStatusLabel(box.SelectedIndex));
            ToolTipService.SetToolTip(box, description);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box, description);
        }
        box.SelectionChanged += (_, _) => SetDescription();
        SetDescription();
    }
}
