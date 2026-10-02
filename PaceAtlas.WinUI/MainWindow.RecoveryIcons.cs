using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private static readonly string[] RecoveryValues =
        ["nicht bewertet", "keine", "etwas", "mittel", "deutlich"];

    private string RecoveryDescription(int level) =>
        N("Erholung: ", "Recovery: ") + T(RecoveryValues[Math.Clamp(level, 0, 4)]);

    private FrameworkElement RecoveryBatteryIcon(int level)
    {
        level = Math.Clamp(level, 0, 4);
        var color = level switch
        {
            0 or 1 => Windows.UI.Color.FromArgb(255, 105, 119, 128),
            2 => Windows.UI.Color.FromArgb(255, 66, 143, 165),
            3 => Windows.UI.Color.FromArgb(255, 28, 126, 144),
            _ => Windows.UI.Color.FromArgb(255, 47, 136, 100)
        };
        var brush = new SolidColorBrush(color);
        var shell = new Grid { Width = 22, Height = 13 };
        shell.Children.Add(new Border { Width = 22, Height = 13, CornerRadius = new CornerRadius(2),
            BorderThickness = new Thickness(1.4), BorderBrush = brush });
        if (level > 1)
            shell.Children.Add(new Border { Width = (level - 1) * 6, Height = 9,
                Margin = new Thickness(2, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center, Background = brush,
                CornerRadius = new CornerRadius(1) });
        if (level == 0)
            shell.Children.Add(new TextBlock { Text = "?", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = brush, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -2, 0, 0) });
        var battery = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0,
            VerticalAlignment = VerticalAlignment.Center };
        battery.Children.Add(shell);
        battery.Children.Add(new Border { Width = 3, Height = 6, CornerRadius = new CornerRadius(0, 1, 1, 0),
            Background = brush, VerticalAlignment = VerticalAlignment.Center });
        ToolTipService.SetToolTip(battery, RecoveryDescription(level));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(battery, RecoveryDescription(level));
        return battery;
    }

    private void PopulateRecoveryChoices()
    {
        var selection = SleepRecovery.SelectedIndex;
        SleepRecovery.ItemsSource = RecoveryValues.Select((value, level) =>
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            row.Children.Add(RecoveryBatteryIcon(level));
            row.Children.Add(new TextBlock { Text = T(value), VerticalAlignment = VerticalAlignment.Center });
            return row;
        }).ToArray();
        SleepRecovery.SelectedIndex = Math.Clamp(selection, 0, RecoveryValues.Length - 1);
    }
}
