using System.Runtime.InteropServices;

namespace PaceAtlas.WinUI;

// A Windows shell icon attached to the WinUI window. No second UI framework is loaded.
internal sealed class PacingTrayIcon : IDisposable
{
    private const uint TrayMessage = 0x8000 + 47;
    private const uint NifMessage = 1, NifIcon = 2, NifTip = 4;
    private const uint NIM_ADD = 0, NIM_DELETE = 2;
    private const uint WM_RBUTTONUP = 0x0205, WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_SIZE = 0x0005, SizeMinimized = 1;
    private const uint WM_SYSCOMMAND = 0x0112, SC_MINIMIZE = 0xF020;
    private const uint MF_STRING = 0, MF_SEPARATOR = 0x800, MF_GRAYED = 1, MF_CHECKED = 8, MF_POPUP = 0x10;
    private const uint TPM_RETURNCMD = 0x100, TPM_RIGHTBUTTON = 2;
    private const uint ImageIcon = 1, LrLoadFromFile = 0x10, LrDefaultSize = 0x40;
    private readonly IntPtr hwnd;
    private readonly IntPtr icon;
    private readonly bool ownsIcon;
    private readonly SubclassProc callback;
    private readonly Func<bool> isRunning, isResting, startInTray, reminderCustom;
    private readonly Func<int> reminderMinutes;
    private readonly Func<string> language;
    private readonly Action open, start, pause, stop, toggleStartInTray, exit, minimize, showAbout;
    private readonly Action<int, bool> setReminder;
    private readonly Action configureReminder;
    private NotifyIconData data;
    private bool disposed;

    public PacingTrayIcon(IntPtr hwnd, string iconFile, Func<bool> isRunning, Func<bool> isResting,
        Func<bool> startInTray, Func<string> language, Action open, Action start, Action pause, Action stop,
        Action toggleStartInTray, Action exit, Action minimize, Action showAbout,
        Func<int> reminderMinutes, Func<bool> reminderCustom, Action<int, bool> setReminder,
        Action configureReminder)
    {
        this.hwnd = hwnd;
        this.isRunning = isRunning; this.isResting = isResting; this.startInTray = startInTray;
        this.language = language;
        this.open = open; this.start = start; this.pause = pause; this.stop = stop;
        this.toggleStartInTray = toggleStartInTray; this.exit = exit; this.minimize = minimize; this.showAbout = showAbout;
        this.reminderMinutes = reminderMinutes; this.reminderCustom = reminderCustom;
        this.setReminder = setReminder; this.configureReminder = configureReminder;
        icon = LoadImageW(IntPtr.Zero, iconFile, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);
        ownsIcon = icon != IntPtr.Zero;
        if (icon == IntPtr.Zero) icon = LoadIconW(IntPtr.Zero, (IntPtr)32512);
        data = new NotifyIconData { cbSize = (uint)Marshal.SizeOf<NotifyIconData>(), hWnd = hwnd,
            uID = 1, uFlags = NifMessage | NifIcon | NifTip, uCallbackMessage = TrayMessage,
            hIcon = icon, szTip = "Pace Atlas · ME/CFS", szInfo = "", szInfoTitle = "" };
        callback = WindowProc;
        if (!SetWindowSubclass(hwnd, callback, (UIntPtr)1, IntPtr.Zero))
            throw new InvalidOperationException("Tray-Fensternachrichten konnten nicht eingerichtet werden.");
        if (!Shell_NotifyIconW(NIM_ADD, ref data))
        {
            RemoveWindowSubclass(hwnd, callback, (UIntPtr)1);
            throw new InvalidOperationException("Das Tray-Symbol konnte nicht erstellt werden.");
        }
    }

    private IntPtr WindowProc(IntPtr window, uint message, IntPtr wparam, IntPtr lparam,
        UIntPtr id, IntPtr reference)
    {
        if (message == TrayMessage)
        {
            switch ((uint)lparam.ToInt64() & 0xFFFF)
            {
                case WM_LBUTTONDBLCLK: open(); break;
                case WM_RBUTTONUP: ShowMenu(); break;
            }
            return IntPtr.Zero;
        }
        if (message == WM_SYSCOMMAND && ((ulong)wparam.ToInt64() & 0xFFF0) == SC_MINIMIZE)
        {
            // Prevent Windows from creating a minimized taskbar button at all.
            minimize();
            return IntPtr.Zero;
        }
        if (message == WM_SIZE && wparam.ToInt64() == SizeMinimized)
        {
            var result = DefSubclassProc(window, message, wparam, lparam);
            minimize(); // Covers programmatic minimization that did not use SC_MINIMIZE.
            return result;
        }
        return DefSubclassProc(window, message, wparam, lparam);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        var reminders = CreatePopupMenu();
        bool submenuAttached = false;
        try
        {
            bool english = language() == "en";
            AppendMenuW(menu, MF_STRING, 1, english ? "Open Pace Atlas" : "Pace Atlas öffnen");
            AppendMenuW(menu, MF_SEPARATOR, 0, null);
            AppendMenuW(menu, MF_STRING | (isRunning() ? MF_GRAYED : 0), 2, english ? "Start pacing" : "Pacing starten");
            AppendMenuW(menu, MF_STRING | (isRunning() ? 0 : MF_GRAYED), 3,
                isResting() ? (english ? "End break" : "Pause beenden") : (english ? "Start break" : "Pause starten"));
            AppendMenuW(menu, MF_STRING | (isRunning() ? 0 : MF_GRAYED), 4, english ? "Stop pacing" : "Pacing stoppen");
            AppendMenuW(menu, MF_SEPARATOR, 0, null);
            if (reminders != IntPtr.Zero)
            {
                int[] intervals = [0, 15, 30, 60, 120];
                for (int i = 0; i < intervals.Length; i++)
                {
                    int value = intervals[i];
                    string caption = value switch
                    {
                        0 => english ? "Off" : "Aus",
                        15 or 30 => $"{value} " + (english ? "minutes" : "Minuten"),
                        60 => english ? "1 hour" : "1 Stunde",
                        _ => english ? "2 hours" : "2 Stunden"
                    };
                    AppendMenuW(reminders, MF_STRING | (reminderMinutes() == value &&
                        (value == 0 || !reminderCustom()) ? MF_CHECKED : 0), (uint)(20 + i), caption);
                }
                AppendMenuW(reminders, MF_SEPARATOR, 0, null);
                AppendMenuW(reminders, MF_STRING | (reminderCustom() ? MF_CHECKED : 0), 25,
                    (english ? "Custom interval ..." : "Eigenes Intervall ...") +
                    (reminderCustom() ? $" ({reminderMinutes()} " +
                        (english ? "minutes)" : "Minuten)") : ""));
                submenuAttached = AppendMenuPopupW(menu, MF_POPUP, reminders,
                    english ? "Condition reminder" : "Zustandserinnerung");
            }
            AppendMenuW(menu, MF_SEPARATOR, 0, null);
            AppendMenuW(menu, MF_STRING | (startInTray() ? MF_CHECKED : 0), 5,
                english ? "Open in tray at startup" : "Beim Start im Tray öffnen");
            AppendMenuW(menu, MF_STRING, 7, english ? "About ..." : "Info ...");
            AppendMenuW(menu, MF_STRING, 6, english ? "Exit Pace Atlas" : "Pace Atlas beenden");
            SetForegroundWindow(hwnd);
            GetCursorPos(out var point);
            var command = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, point.X, point.Y, hwnd, IntPtr.Zero);
            switch (command)
            {
                case 1: open(); break;
                case 2: start(); break;
                case 3: pause(); break;
                case 4: stop(); break;
                case 5: toggleStartInTray(); break;
                case 6: exit(); break;
                case 7: showAbout(); break;
                case >= 20 and <= 24: setReminder(new[] { 0, 15, 30, 60, 120 }[command - 20], false); break;
                case 25: configureReminder(); break;
            }
        }
        finally
        {
            DestroyMenu(menu); // Destroys its attached submenu as well.
            if (reminders != IntPtr.Zero && !submenuAttached) DestroyMenu(reminders);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Shell_NotifyIconW(NIM_DELETE, ref data);
        RemoveWindowSubclass(hwnd, callback, (UIntPtr)1);
        if (ownsIcon && icon != IntPtr.Zero) DestroyIcon(icon);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize; public IntPtr hWnd; public uint uID, uFlags, uCallbackMessage; public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags; public Guid guidItem; public IntPtr hBalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SubclassProc(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam,
        UIntPtr id, IntPtr reference);
    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr id, IntPtr reference);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    private static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadImageW(IntPtr instance, string name, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr LoadIconW(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenuW(IntPtr menu, uint flags, uint id, string? text);
    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuPopupW(IntPtr menu, uint flags, IntPtr submenu, string text);
    [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr parameters);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
}
