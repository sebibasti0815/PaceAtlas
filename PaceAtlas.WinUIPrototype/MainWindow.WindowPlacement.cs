using System.Text.Json;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace PaceAtlas.WinUIPrototype;

public sealed partial class MainWindow
{
    private sealed class WindowPlacementSettings
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    private static string WindowPlacementPath => Path.Combine(PrototypeFolder, "window-placement.json");
    private RectInt32? lastNormalBounds;

    private void RestoreWindowPlacement()
    {
        try
        {
            if (!File.Exists(WindowPlacementPath)) return;
            var saved = JsonSerializer.Deserialize<WindowPlacementSettings>(File.ReadAllText(WindowPlacementPath));
            if (saved is null || saved.Width < 420 || saved.Height < 320 ||
                saved.Width > 16000 || saved.Height > 16000) return;

            var center = new PointInt32(saved.X + saved.Width / 2, saved.Y + saved.Height / 2);
            var work = DisplayArea.GetFromPoint(center, DisplayAreaFallback.Primary).WorkArea;
            int width = Math.Min(saved.Width, work.Width);
            int height = Math.Min(saved.Height, work.Height);
            int x = Math.Clamp(saved.X, work.X, work.X + work.Width - width);
            int y = Math.Clamp(saved.Y, work.Y, work.Y + work.Height - height);
            AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Use the default window size and position if the saved values are unavailable.
        }
    }

    private void TrackWindowPlacement()
    {
        RememberNormalBounds();
        AppWindow.Changed += (_, _) => RememberNormalBounds();
        AppWindow.Closing += (_, args) =>
        {
            if (args.Cancel) return;
            RememberNormalBounds();
            if (lastNormalBounds is not RectInt32 bounds) return;
            try
            {
                SaveJson(WindowPlacementPath, new WindowPlacementSettings
                {
                    X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Window closing must remain possible even when preferences cannot be saved.
            }
        };
    }

    private void RememberNormalBounds()
    {
        if (hiddenToTray || AppWindow.Presenter is not OverlappedPresenter presenter ||
            presenter.State != OverlappedPresenterState.Restored) return;
        var position = AppWindow.Position;
        var size = AppWindow.Size;
        if (size.Width >= 420 && size.Height >= 320)
            lastNormalBounds = new RectInt32(position.X, position.Y, size.Width, size.Height);
    }
}
