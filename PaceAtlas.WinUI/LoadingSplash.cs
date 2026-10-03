using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PaceAtlas.WinUI;

// A native window on its own UI thread remains responsive during WinUI initialization.
internal sealed class LoadingSplash : IDisposable
{
    private const uint WsPopup = 0x80000000;
    private const uint WsExTopmost = 0x00000008, WsExToolWindow = 0x00000080, WsExNoActivate = 0x08000000;
    private const uint WmPaint = 0x000F, WmEraseBackground = 0x0014, WmClose = 0x0010;
    private const uint WmTimer = 0x0113, WmFinish = 0x8001;
    private const uint MinimumDisplayMs = 1500;
    private const int WindowProcedureIndex = -4;
    private const uint ImageIcon = 1, LoadFromFile = 0x10, DrawNormal = 3;
    private const uint TextCenter = 0x1, TextSingleLine = 0x20, TextVCenter = 0x4;
    private const uint HeaderColor = 0x004C3320; // RGB #20334C, matching MainWindow.xaml
    private static readonly WindowProc PaintProcedure = HandleMessage;
    private static LoadingSplash? active;

    private readonly ManualResetEventSlim finished = new(false);
    private readonly Thread thread;
    private IntPtr window, originalProcedure, icon, font, brush;
    private long visibleAt;
    private int width, height;
    private double scale;

    private LoadingSplash()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "Pace Atlas loading screen" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    internal static LoadingSplash Start() => new();

    private void Run()
    {
        if (finished.Wait(500)) return;
        try
        {
            scale = Math.Max(1, GetDpiForSystem() / 96.0);
            width = Px(680);
            height = Px(244);
            window = CreateWindowExW(WsExTopmost | WsExToolWindow | WsExNoActivate,
                "STATIC", "", WsPopup, (GetSystemMetrics(0) - width) / 2,
                (GetSystemMetrics(1) - height) / 2, width, height,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (window == IntPtr.Zero) return;

            icon = LoadImageW(IntPtr.Zero, Path.Combine(AppContext.BaseDirectory, "app.ico"),
                ImageIcon, Px(156), Px(156), LoadFromFile);
            font = CreateFontW(-Px(52), 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 0, 0, "Segoe UI");
            brush = CreateSolidBrush(HeaderColor);
            Volatile.Write(ref active, this);
            originalProcedure = SetWindowLongPtrW(window, WindowProcedureIndex,
                Marshal.GetFunctionPointerForDelegate(PaintProcedure));

            if (finished.IsSet) PostMessageW(window, WmClose, IntPtr.Zero, IntPtr.Zero);
            else
            {
                Volatile.Write(ref visibleAt, Stopwatch.GetTimestamp());
                ShowWindow(window, 4); // SW_SHOWNOACTIVATE
                if (finished.IsSet) PostMessageW(window, WmFinish, IntPtr.Zero, IntPtr.Zero);
            }

            while (GetMessageW(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessageW(ref message);
            }
        }
        catch (Exception) { /* The loading screen must never block application startup. */ }
        finally
        {
            if (window != IntPtr.Zero) DestroyWindow(window);
            Volatile.Write(ref window, IntPtr.Zero);
            Volatile.Write(ref active, null);
            if (icon != IntPtr.Zero) DestroyIcon(icon);
            if (font != IntPtr.Zero) DeleteObject(font);
            if (brush != IntPtr.Zero) DeleteObject(brush);
        }
    }

    private int Px(int value) => (int)Math.Round(value * scale);

    private static IntPtr HandleMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        var splash = Volatile.Read(ref active);
        if (splash is not null)
        {
            if (message == WmEraseBackground) return new IntPtr(1);
            if (message == WmPaint)
            {
                splash.Paint(hwnd);
                return IntPtr.Zero;
            }
            if (message == WmFinish)
            {
                var shownAt = Volatile.Read(ref splash.visibleAt);
                var remaining = shownAt == 0 ? 0 :
                    MinimumDisplayMs - Stopwatch.GetElapsedTime(shownAt).TotalMilliseconds;
                if (remaining > 0)
                {
                    if (SetTimer(hwnd, new IntPtr(1), (uint)Math.Ceiling(remaining), IntPtr.Zero) == IntPtr.Zero)
                        PostMessageW(hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
                }
                else
                    PostMessageW(hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
                return IntPtr.Zero;
            }
            if (message == WmTimer && wParam == new IntPtr(1))
            {
                KillTimer(hwnd, new IntPtr(1));
                PostMessageW(hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
                return IntPtr.Zero;
            }
            if (message == WmClose)
            {
                DestroyWindow(hwnd);
                PostQuitMessage(0);
                return IntPtr.Zero;
            }
            if (splash.originalProcedure != IntPtr.Zero)
                return CallWindowProcW(splash.originalProcedure, hwnd, message, wParam, lParam);
        }
        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private void Paint(IntPtr hwnd)
    {
        // PAINTSTRUCT occupies 72 bytes on x64; this project targets x64 exclusively.
        var paint = Marshal.AllocHGlobal(72);
        try
        {
            var dc = BeginPaint(hwnd, paint);
            try
            {
                var background = new Rect { Right = width, Bottom = height };
                FillRect(dc, ref background, brush);
                if (icon != IntPtr.Zero)
                    DrawIconEx(dc, Px(44), Px(44), icon, Px(156), Px(156),
                        0, IntPtr.Zero, DrawNormal);
                SetBkMode(dc, 1); // Transparent text background
                SetTextColor(dc, 0x00FFFFFF);
                var oldFont = font == IntPtr.Zero ? IntPtr.Zero : SelectObject(dc, font);
                var title = new Rect { Left = Px(220), Top = 0,
                    Right = width - Px(28), Bottom = height };
                DrawTextW(dc, "Pace Atlas", -1, ref title, TextCenter | TextSingleLine | TextVCenter);
                if (oldFont != IntPtr.Zero) SelectObject(dc, oldFont);
            }
            finally { EndPaint(hwnd, paint); }
        }
        finally { Marshal.FreeHGlobal(paint); }
    }

    public void Dispose()
    {
        finished.Set();
        var hwnd = Volatile.Read(ref window);
        if (hwnd != IntPtr.Zero) PostMessageW(hwnd, WmFinish, IntPtr.Zero, IntPtr.Zero);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Hwnd;
        public uint Id;
        public IntPtr WParam, LParam;
        public uint Time;
        public int X, Y;
        public uint Private;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(uint exStyle, string className, string title, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImageW(IntPtr instance, string name, uint type, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern IntPtr CallWindowProcW(IntPtr previous, IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern bool PostMessageW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr SetTimer(IntPtr hwnd, IntPtr id, uint milliseconds, IntPtr callback);
    [DllImport("user32.dll")] private static extern bool KillTimer(IntPtr hwnd, IntPtr id);
    [DllImport("user32.dll")] private static extern int GetMessageW(out Message message, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessageW(ref Message message);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hwnd, IntPtr paint);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hwnd, IntPtr paint);
    [DllImport("user32.dll")] private static extern int FillRect(IntPtr dc, ref Rect rect, IntPtr brush);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DrawTextW(IntPtr dc, string text, int length, ref Rect rect, uint format);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon,
        int width, int height, uint step, IntPtr brush, uint flags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(IntPtr dc, int mode);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(IntPtr dc, uint color);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr handle);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation,
        int weight, uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision,
        uint clipPrecision, uint quality, uint pitchAndFamily, string faceName);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
}
