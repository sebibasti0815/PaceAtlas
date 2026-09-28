using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace PaceAtlas.WinUIPrototype;

// WinUI can lose routed wheel events on some Windows mouse configurations.
// Handle the window message only while the pointer is over the form scroller.
internal sealed class MouseWheelBridge : IDisposable
{
    private const uint WmMouseWheel = 0x020A;
    private const uint WmPointerWheel = 0x024E;
    private const int WhGetMessage = 3;
    private readonly IntPtr hwnd;
    private readonly UIElement root;
    private readonly ScrollViewer scroller;
    private readonly SubclassProc callback;
    private readonly HookProc messageHook;
    private readonly Action<string> report;
    private IntPtr hookHandle;
    private bool installed;

    internal MouseWheelBridge(Window window, ScrollViewer scroller, Action<string> report)
    {
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        root = window.Content;
        this.scroller = scroller;
        this.report = report;
        callback = HandleMessage;
        messageHook = InspectMessage;
        installed = SetWindowSubclass(hwnd, callback, new UIntPtr(1), IntPtr.Zero);
        hookHandle = SetWindowsHookEx(WhGetMessage, messageHook, IntPtr.Zero, GetCurrentThreadId());
        report($"Mausrad bereit: Nachrichtenfilter {(hookHandle != IntPtr.Zero ? "aktiv" : "inaktiv")}");
    }

    private IntPtr InspectMessage(int code, IntPtr wparam, IntPtr lparam)
    {
        if (code >= 0 && wparam != IntPtr.Zero)
        {
            try
            {
                var message = Marshal.PtrToStructure<NativeMessage>(lparam);
                if (message.Message is WmMouseWheel or WmPointerWheel
                    && (message.Hwnd == hwnd || IsChild(hwnd, message.Hwnd)))
                {
                    int delta = unchecked((short)((message.WParam.ToInt64() >> 16) & 0xffff));
                    if (TryScroll(delta, "Windows"))
                        Marshal.WriteInt32(lparam, IntPtr.Size, 0); // WM_NULL: nicht zweimal scrollen.
                }
            }
            catch (Exception) { /* Native callbacks must never throw. */ }
        }
        return CallNextHookEx(hookHandle, code, wparam, lparam);
    }

    private IntPtr HandleMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam,
        UIntPtr subclassId, IntPtr referenceData)
    {
        if (message == WmMouseWheel)
        {
            try
            {
                int delta = unchecked((short)((wparam.ToInt64() >> 16) & 0xffff));
                if (TryScroll(delta, "Fenster")) return IntPtr.Zero;
            }
            catch (Exception) { /* Unmanaged callbacks must never throw. */ }
        }
        return DefSubclassProc(window, message, wparam, lparam);
    }

    private bool TryScroll(int delta, string route)
    {
        double maximum = Math.Max(0, scroller.ExtentHeight - scroller.ViewportHeight);
        if (delta == 0 || !GetCursorPos(out var cursor) || !ScreenToClient(hwnd, ref cursor))
            return false;
        double scale = root.XamlRoot?.RasterizationScale ?? 1;
        var bounds = scroller.TransformToVisual(root).TransformBounds(
            new Rect(0, 0, scroller.ActualWidth, scroller.ActualHeight));
        var x = cursor.X / scale;
        var y = cursor.Y / scale;
        if (x < bounds.Left || x >= bounds.Right || y < bounds.Top || y >= bounds.Bottom)
            return false;
        if (maximum <= 0)
        {
            report($"Mausrad über Formular ({route}), Scrollweg 0");
            return false;
        }
        double target = Math.Clamp(scroller.VerticalOffset - delta / 120.0 * 72, 0, maximum);
        bool moved = scroller.ChangeView(null, target, null, true);
        report($"Mausrad über Formular ({route}), Ziel {target:0}/{maximum:0}, Änderung {(moved ? "ja" : "nein")}");
        return moved;
    }

    public void Dispose()
    {
        if (hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(hookHandle);
            hookHandle = IntPtr.Zero;
        }
        if (!installed) return;
        RemoveWindowSubclass(hwnd, callback, new UIntPtr(1));
        installed = false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Cursor;
        public uint Private;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SubclassProc(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam,
        UIntPtr subclassId, IntPtr referenceData);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr HookProc(int code, IntPtr wparam, IntPtr lparam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wparam, IntPtr lparam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsChild(IntPtr parent, IntPtr child);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc callback,
        UIntPtr subclassId, IntPtr referenceData);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(IntPtr hwnd, ref Point point);
}
