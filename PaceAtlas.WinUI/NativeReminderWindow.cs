using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace PaceAtlas.WinUI;

// An independent native input surface: no XAML focus bootstrap and no activation
// when shown or clicked. Only the explicit Record action opens the main window.
internal sealed class NativeReminderWindow : IDisposable
{
    private const uint ExStyle = 0x08000088; // NOACTIVATE | TOOLWINDOW | TOPMOST
    private const uint Style = 0x80800000; // POPUP | BORDER
    private const uint ShowMessage = 0x8001, HideMessage = 0x8002, StopMessage = 0x8003;
    private const int RecordId = 1, LaterId = 2;
    private static readonly ConcurrentDictionary<IntPtr, NativeReminderWindow> Owners = new();
    private static readonly WindowProc ParentCallback = ParentProc, ButtonCallback = ButtonProc;
    private readonly Action record;
    private readonly Action<string> trace;
    private readonly Action<Exception> reportError;
    private readonly Thread thread;
    private readonly Dictionary<IntPtr, IntPtr> buttonProcedures = new();
    private IntPtr window, recordButton, laterButton, parentProcedure;
    private IntPtr background, headerFont, textFont, buttonFont;
    private bool english, visible, disposed, showRequested, layingOut;
    private int hovered, pressed, width, height;
    private double scale = 1;
    private Rect titleBounds, descriptionBounds;

    public NativeReminderWindow(Action record, Action<string> trace, Action<Exception> reportError)
    {
        this.record = record; this.trace = trace; this.reportError = reportError;
        thread = new Thread(Run) { IsBackground = true, Name = "PaceAtlas reminder" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }
    public bool IsVisible => Volatile.Read(ref visible);
    public void Show(bool useEnglish)
    {
        Volatile.Write(ref english, useEnglish);
        Volatile.Write(ref showRequested, true);
        var hwnd = Volatile.Read(ref window);
        if (hwnd != IntPtr.Zero) PostMessageW(hwnd, ShowMessage, IntPtr.Zero, IntPtr.Zero);
    }
    public void Hide()
    {
        Volatile.Write(ref showRequested, false);
        var hwnd = Volatile.Read(ref window);
        if (hwnd != IntPtr.Zero) PostMessageW(hwnd, HideMessage, IntPtr.Zero, IntPtr.Zero);
    }
    private int Px(double value) => (int)Math.Round(value * scale);
    private void Run()
    {
        try
        {
            var previousForeground = GetForegroundWindow();
            var previousFocus = ForegroundFocus(previousForeground);
            SetThreadDpiAwarenessContext(new IntPtr(-4));
            var work = WorkArea();
            var hwnd = CreateWindowExW(ExStyle, "STATIC", "Zustandserinnerung", Style,
                work.Left + 18, work.Top + 18, 1, 1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (hwnd == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            Owners[hwnd] = this;
            parentProcedure = SetWindowLongPtrW(hwnd, -4, Marshal.GetFunctionPointerForDelegate(ParentCallback));
            Volatile.Write(ref window, hwnd);
            background = CreateSolidBrush(0x003E2B1F); // RGB 31,43,62
            recordButton = CreateButton(RecordId);
            laterButton = CreateButton(LaterId);
            trace("create.noactivate=" + (previousForeground == GetForegroundWindow() && previousFocus == ForegroundFocus(GetForegroundWindow())));
            if (Volatile.Read(ref disposed)) PostMessageW(hwnd, StopMessage, IntPtr.Zero, IntPtr.Zero);
            else if (Volatile.Read(ref showRequested)) ShowCore();
            while (GetMessageW(out var message, IntPtr.Zero, 0, 0) > 0)
            { TranslateMessage(ref message); DispatchMessageW(ref message); }
        }
        catch (Exception ex) { trace("native.error=" + ex.Message); reportError(ex); }
        finally
        {
            var hwnd = Volatile.Read(ref window);
            if (hwnd != IntPtr.Zero) DestroyWindow(hwnd);
            foreach (var child in buttonProcedures.Keys) Owners.TryRemove(child, out _);
            Owners.TryRemove(hwnd, out _);
            Volatile.Write(ref window, IntPtr.Zero);
            Volatile.Write(ref visible, false);
            foreach (var item in new[] { background, headerFont, textFont, buttonFont })
                if (item != IntPtr.Zero) DeleteObject(item);
        }
    }
    private IntPtr CreateButton(int id)
    {
        var child = CreateWindowExW(0, "BUTTON", "", 0x5001000B, 0, 0, 1, 1,
            window, new IntPtr(id), IntPtr.Zero, IntPtr.Zero); // CHILD | VISIBLE | TABSTOP | OWNERDRAW
        if (child == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        Owners[child] = this;
        buttonProcedures[child] = SetWindowLongPtrW(child, -4, Marshal.GetFunctionPointerForDelegate(ButtonCallback));
        return child;
    }
    private void ShowCore()
    {
        if (Volatile.Read(ref disposed) || !Volatile.Read(ref showRequested)) return;
        var foreground = GetForegroundWindow();
        var focus = ForegroundFocus(foreground);
        Layout();
        hovered = pressed = 0;
        SetWindowTextW(window, english ? "Condition reminder" : "Zustandserinnerung");
        SetWindowTextW(recordButton, english ? "Record condition" : "Zustand erfassen");
        SetWindowTextW(laterButton, english ? "Later" : "Später");
        Volatile.Write(ref visible, true);
        ShowWindow(window, 4); // SW_SHOWNOACTIVATE
        InvalidateRect(window, IntPtr.Zero, true);
        UpdateWindow(window);
        SetTimer(window, new IntPtr(1), 120000, IntPtr.Zero);
        trace("show.noactivate=" + (foreground == GetForegroundWindow() && focus == ForegroundFocus(GetForegroundWindow())));
    }
    private void HideCore()
    {
        KillTimer(window, new IntPtr(1));
        Volatile.Write(ref visible, false);
        Volatile.Write(ref showRequested, false);
        ShowWindow(window, 0);
    }
    private void Layout()
    {
        if (layingOut) return;
        layingOut = true;
        try
        {
            var work = WorkArea();
            SetWindowPos(window, IntPtr.Zero, work.Left + 18, work.Top + 18, 1, 1, 0x0015); // no size, z-order or activation
            scale = Math.Max(96, GetDpiForWindow(window)) / 96.0;
            foreach (var font in new[] { headerFont, textFont, buttonFont }) if (font != IntPtr.Zero) DeleteObject(font);
            headerFont = Font(19, 600); textFont = Font(16, 400); buttonFont = Font(14, 400);
            width = Math.Min(Px(450), Math.Max(1, work.Right - work.Left - 36));
            int margin = Px(26), gap = Px(12);
            var dc = GetDC(window);
            try
            {
                var old = SelectObject(dc, headerFont);
                titleBounds = new Rect { Left = margin, Top = Px(24), Right = width - margin };
                DrawTextW(dc, english ? "How are you feeling?" : "Wie geht es dir gerade?", -1, ref titleBounds, 0x410); // CALCRECT | WORDBREAK
                SelectObject(dc, textFont);
                descriptionBounds = new Rect { Left = margin, Top = titleBounds.Bottom + gap, Right = width - margin };
                DrawTextW(dc, Description, -1, ref descriptionBounds, 0x410);
                SelectObject(dc, old);
            }
            finally { ReleaseDC(window, dc); }
            int buttonY = descriptionBounds.Bottom + Px(20), buttonHeight = Px(40);
            int buttonWidth = Math.Max(1, (width - margin * 2 - gap) / 2);
            height = buttonY + buttonHeight + Px(24);
            var outer = new Rect { Right = width, Bottom = height };
            AdjustWindowRectEx(ref outer, Style, false, ExStyle);
            int outerWidth = outer.Right - outer.Left, outerHeight = outer.Bottom - outer.Top;
            SetWindowPos(window, new IntPtr(-1), Math.Max(work.Left, work.Right - outerWidth - 18), work.Top + 18,
                outerWidth, outerHeight, 0x0010); // NOACTIVATE
            SetWindowPos(recordButton, IntPtr.Zero, margin, buttonY, buttonWidth, buttonHeight, 0x0014);
            SetWindowPos(laterButton, IntPtr.Zero, margin + buttonWidth + gap, buttonY, buttonWidth, buttonHeight, 0x0014);
            SendMessageW(recordButton, 0x0030, buttonFont, new IntPtr(1));
            SendMessageW(laterButton, 0x0030, buttonFont, new IntPtr(1));
        }
        finally { layingOut = false; }
    }
    private string Description => english ? "A quick entry helps you notice changes over time." : "Ein kurzer Eintrag hilft dir, Veränderungen im Verlauf zu erkennen.";
    private IntPtr Font(int size, int weight) => CreateFontW(-Px(size), 0, 0, 0, weight, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
    private void Select(int id)
    {
        trace(id == RecordId ? "record.click" : "later.click");
        HideCore();
        if (id == RecordId) record();
    }
    private void Hover(int id)
    {
        if (hovered == id) return;
        if (hovered != 0) trace(hovered == RecordId ? "record.exit" : "later.exit");
        hovered = id;
        if (id != 0) trace(id == RecordId ? "record.enter" : "later.enter");
        InvalidateRect(recordButton, IntPtr.Zero, false); InvalidateRect(laterButton, IntPtr.Zero, false);
    }
    private static IntPtr ParentProc(IntPtr hwnd, uint message, IntPtr wp, IntPtr lp)
    {
        if (!Owners.TryGetValue(hwnd, out var owner)) return DefWindowProcW(hwnd, message, wp, lp);
        switch (message)
        {
            case ShowMessage: Volatile.Write(ref owner.showRequested, true); owner.ShowCore(); return IntPtr.Zero;
            case HideMessage: owner.HideCore(); return IntPtr.Zero;
            case StopMessage: owner.HideCore(); PostQuitMessage(0); return IntPtr.Zero;
            case 0x0021: return new IntPtr(3); // MA_NOACTIVATE, including on user click
            case 0x0010: owner.HideCore(); return IntPtr.Zero;
            case 0x0014: return new IntPtr(1);
            case 0x000F: owner.Paint(); return IntPtr.Zero;
            case 0x002B: owner.PaintButton(Marshal.PtrToStructure<DrawItem>(lp)); return new IntPtr(1);
            case 0x0111:
                if ((wp.ToInt64() >> 16 & 0xFFFF) == 0 && (wp.ToInt64() & 0xFFFF) is RecordId or LaterId)
                    owner.Select((int)(wp.ToInt64() & 0xFFFF));
                return IntPtr.Zero;
            case 0x02E0: owner.Layout(); return IntPtr.Zero;
            case 0x0113:
                GetCursorPos(out var point); GetWindowRect(hwnd, out var bounds);
                if (bounds.Contains(point.X, point.Y)) SetTimer(hwnd, new IntPtr(1), 120000, IntPtr.Zero);
                else owner.HideCore();
                return IntPtr.Zero;
        }
        return CallWindowProcW(owner.parentProcedure, hwnd, message, wp, lp);
    }
    private static IntPtr ButtonProc(IntPtr hwnd, uint message, IntPtr wp, IntPtr lp)
    {
        if (!Owners.TryGetValue(hwnd, out var owner)) return DefWindowProcW(hwnd, message, wp, lp);
        int id = hwnd == owner.recordButton ? RecordId : LaterId;
        switch (message)
        {
            case 0x0021: return new IntPtr(3);
            case 0x0200:
                owner.Hover(id);
                var tracking = new TrackMouse { Size = (uint)Marshal.SizeOf<TrackMouse>(), Flags = 2, Window = hwnd };
                TrackMouseEvent(ref tracking);
                return IntPtr.Zero;
            case 0x02A3: if (owner.hovered == id) owner.Hover(0); return IntPtr.Zero;
            case 0x0201:
                owner.pressed = id; SetCapture(hwnd); InvalidateRect(hwnd, IntPtr.Zero, false); return IntPtr.Zero;
            case 0x0202:
                bool pressed = owner.pressed == id;
                owner.pressed = 0; ReleaseCapture(); InvalidateRect(hwnd, IntPtr.Zero, false);
                GetClientRect(hwnd, out var rect);
                if (pressed && rect.Contains((short)(lp.ToInt64() & 0xFFFF), (short)(lp.ToInt64() >> 16 & 0xFFFF))) owner.Select(id);
                return IntPtr.Zero;
        }
        return CallWindowProcW(owner.buttonProcedures[hwnd], hwnd, message, wp, lp);
    }
    private void Paint()
    {
        var paint = Marshal.AllocHGlobal(128);
        try
        {
            var dc = BeginPaint(window, paint);
            try
            {
                GetClientRect(window, out var rect); FillRect(dc, ref rect, background);
                SetBkMode(dc, 1); SetTextColor(dc, 0x00FFFFFF);
                var old = SelectObject(dc, headerFont); var title = titleBounds;
                DrawTextW(dc, english ? "How are you feeling?" : "Wie geht es dir gerade?", -1, ref title, 0x10);
                SelectObject(dc, textFont); var description = descriptionBounds;
                DrawTextW(dc, Description, -1, ref description, 0x10);
                SelectObject(dc, old);
            }
            finally { EndPaint(window, paint); }
        }
        finally { Marshal.FreeHGlobal(paint); }
    }
    private void PaintButton(DrawItem item)
    {
        int id = (int)item.Id;
        uint color = id == RecordId ? (hovered == id ? 0x0097851Eu : 0x00897716u) : (hovered == id ? 0x00D8D3CFu : 0x00C5C0BCu);
        if (pressed == id) color = id == RecordId ? 0x00746312u : 0x00B5B0ACu;
        var brush = CreateSolidBrush(color);
        try { var rect = item.Bounds; FillRect(item.Dc, ref rect, brush); }
        finally { DeleteObject(brush); }
        SetBkMode(item.Dc, 1); SetTextColor(item.Dc, id == RecordId ? 0x00FFFFFFu : 0x00202020u);
        var old = SelectObject(item.Dc, buttonFont); var text = item.Bounds;
        DrawTextW(item.Dc, id == RecordId ? (english ? "Record condition" : "Zustand erfassen") : (english ? "Later" : "Später"), -1, ref text, 0x25);
        SelectObject(item.Dc, old);
        if ((item.State & 0x0010) != 0) { var focus = item.Bounds; focus.Left += 3; focus.Top += 3; focus.Right -= 3; focus.Bottom -= 3; DrawFocusRect(item.Dc, ref focus); }
    }
    private static Rect WorkArea()
    {
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        var primary = MonitorFromPoint(new Point(), 1);
        if (!GetMonitorInfoW(primary, ref info)) throw new System.ComponentModel.Win32Exception();
        return info.Work;
    }
    private static IntPtr ForegroundFocus(IntPtr foreground)
    {
        uint threadId = GetWindowThreadProcessId(foreground, out _);
        var info = new GuiInfo { Size = (uint)Marshal.SizeOf<GuiInfo>() };
        return GetGUIThreadInfo(threadId, ref info) ? info.Focus : IntPtr.Zero;
    }
    public void Dispose()
    {
        Volatile.Write(ref disposed, true); Hide();
        var hwnd = Volatile.Read(ref window);
        if (hwnd != IntPtr.Zero) PostMessageW(hwnd, StopMessage, IntPtr.Zero, IntPtr.Zero);
        if (Thread.CurrentThread != thread) thread.Join(500);
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wp, IntPtr lp);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; public readonly bool Contains(int x,int y) => x>=Left && x<Right && y>=Top && y<Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct TrackMouse { public uint Size, Flags; public IntPtr Window; public uint HoverTime; }
    [StructLayout(LayoutKind.Sequential)] private struct Message { public IntPtr Window; public uint Id; public IntPtr WParam, LParam; public uint Time; public int X,Y; public uint Private; }
    [StructLayout(LayoutKind.Sequential)] private struct DrawItem { public uint Type, Id, ItemId, Action, State; public IntPtr Window, Dc; public Rect Bounds; public UIntPtr Data; }
    [StructLayout(LayoutKind.Sequential)] private struct GuiInfo { public uint Size, Flags; public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret; public Rect CaretBounds; }
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateWindowExW(uint ex,string cls,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr data);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd,int index,IntPtr value);
    [DllImport("user32.dll",EntryPoint="CallWindowProcW")] private static extern IntPtr CallWindowProcW(IntPtr proc,IntPtr hwnd,uint message,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll",EntryPoint="DefWindowProcW")] private static extern IntPtr DefWindowProcW(IntPtr hwnd,uint message,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll")] private static extern bool PostMessageW(IntPtr hwnd,uint message,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll")] private static extern IntPtr SendMessageW(IntPtr hwnd,uint message,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll")] private static extern int GetMessageW(out Message message,IntPtr hwnd,uint min,uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessageW(ref Message message);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd,int command);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] private static extern bool AdjustWindowRectEx(ref Rect rect,uint style,bool menu,uint ex);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint id,ref GuiInfo info);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern IntPtr SetCapture(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern bool TrackMouseEvent(ref TrackMouse info);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr hwnd,IntPtr rect,bool erase);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr SetTimer(IntPtr hwnd,IntPtr id,uint interval,IntPtr callback);
    [DllImport("user32.dll")] private static extern bool KillTimer(IntPtr hwnd,IntPtr id);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern bool SetWindowTextW(IntPtr hwnd,string text);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern bool GetMonitorInfoW(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hwnd,IntPtr paint);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hwnd,IntPtr paint);
    [DllImport("user32.dll")] private static extern int FillRect(IntPtr dc,ref Rect rect,IntPtr brush);
    [DllImport("user32.dll")] private static extern bool DrawFocusRect(IntPtr dc,ref Rect rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int DrawTextW(IntPtr dc,string text,int length,ref Rect rect,uint flags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc,IntPtr handle);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(IntPtr dc,int mode);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(IntPtr dc,uint color);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr CreateFontW(int height,int width,int escape,int orientation,int weight,uint italic,uint underline,uint strike,uint charset,uint output,uint clip,uint quality,uint pitch,string face);
}
