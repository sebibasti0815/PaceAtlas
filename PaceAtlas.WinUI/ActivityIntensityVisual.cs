using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PaceAtlas.WinUI;

/// <summary>Shared visual representation of the four activity intensity levels.
/// Used by the activity editor and the history list so neither can drift.</summary>
internal static class ActivityIntensityVisual
{
    private static readonly string[] Colors = ["#187C90", "#A66A16", "#B75125", "#B75125"];

    internal static string Marker(int level)
    {
        var count = Math.Clamp(level, 0, 4);
        return new string('▰', count) + new string('▱', 4 - count);
    }

    // Render four genuine shapes instead of font characters. The same control is
    // used in both the input selector and the history list.
    internal static StackPanel Create(int level)
    {
        level = Math.Clamp(level, 1, 4);
        var color = Brush(level);
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < 4; i++)
        {
            panel.Children.Add(new Border
            {
                Width = 14, Height = 9, BorderThickness = new Thickness(1),
                BorderBrush = color,
                Background = i < level ? color : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                CornerRadius = new CornerRadius(0)
            });
        }
        return panel;
    }

    internal static SolidColorBrush Brush(int level)
    {
        var hex = Colors[Math.Clamp(level, 1, 4) - 1];
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255,
            Convert.ToByte(hex[1..3], 16), Convert.ToByte(hex[3..5], 16),
            Convert.ToByte(hex[5..7], 16)));
    }
}
