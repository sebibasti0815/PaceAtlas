using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;
using System.Drawing;
using System.Drawing.Imaging;

internal static class Program
{
    static string output = "";
    static Process? app;
    static int testCount;
    [MTAThread]
    static int Main(string[] args)
    {
        SetThreadDpiAwarenessContext(new IntPtr(-4));
        if(args.Length>0 && args[0]=="--host-cold-suite") return HostColdSuite(args);
        if(args.Length>0 && args[0]=="--live-action") return LiveAction(args);
        if(args.Length!=2) throw new ArgumentException("Expected app path and result folder.");
        output=args[1];Directory.CreateDirectory(output);
        if(!string.Equals(Environment.UserName,"WDAGUtilityAccount",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Default test mode requires disposable Windows Sandbox data.");
        File.WriteAllText(Path.Combine(output,"status.txt"),"RUNNING");
        try
        {
            foreach(bool direct in new[]{true,false})
                foreach(bool recordFirst in new[]{false,true}) RunScenario(args[0],direct,recordFirst);
            Log("PASS: all eight cold-start and repeated button clicks passed.");
            File.WriteAllText(Path.Combine(output,"status.txt"),"PASS 8/8");
            return 0;
        }
        catch(Exception ex)
        {
            Log("FAIL: "+ex);Capture("failure");
            var trace=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PaceAtlas.WinUI","reminder-input.log");
            if(File.Exists(trace)) File.Copy(trace,Path.Combine(output,"pointer-failure.log"),true);
            File.WriteAllText(Path.Combine(output,"status.txt"),"FAIL: "+ex.Message);
            return 1;
        }
        finally {if(app is {HasExited:false}) app.Kill(true);}
    }
    static int HostColdSuite(string[] args)
    {
        output=args[2];Directory.CreateDirectory(output);
        Require(Process.GetProcessesByName("PaceAtlas.WinUI").Length==0,"Existing app must be exited normally before host cold-start tests");
        string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PaceAtlas.WinUI");
        string tray=Path.Combine(settings,"tray-settings.json"), choice=Path.Combine(settings,"close-choice.json");
        string flag=Path.Combine(settings,"reminder-input-diagnostics.enabled");
        byte[]? originalTray=File.Exists(tray)?File.ReadAllBytes(tray):null;
        byte[]? originalChoice=File.Exists(choice)?File.ReadAllBytes(choice):null;
        bool flagExisted=File.Exists(flag);
        if(originalTray!=null) File.WriteAllBytes(Path.Combine(output,"original-tray-settings.json"),originalTray);
        if(originalChoice!=null) File.WriteAllBytes(Path.Combine(output,"original-close-choice.json"),originalChoice);
        try
        {
            File.WriteAllText(flag,"Temporary host cold-start input test");
            foreach(bool direct in new[]{true,false})
                foreach(bool recordFirst in new[]{false,true})
                {
                    File.WriteAllText(tray,JsonSerializer.Serialize(new{StartInTray=direct}));
                    if(!direct && File.Exists(choice)) File.Delete(choice);
                    string label=(direct?"host-direct":"host-close")+(recordFirst?"-record-first":"-later-first");
                    app=Process.Start(new ProcessStartInfo(args[1]){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(args[1])!})!;
                    var main=WaitHandleWindow("Pace Atlas",45000,false);Thread.Sleep(1500);
                    if(!direct)
                    {
                        Wait(()=>IsWindowVisible(main),8000,"Normal host startup did not open main");
                        SetForegroundWindow(main);Thread.Sleep(200);
                        keybd_event(0x12,0,0,UIntPtr.Zero);keybd_event(0x73,0,0,UIntPtr.Zero);
                        keybd_event(0x73,0,2,UIntPtr.Zero);keybd_event(0x12,0,2,UIntPtr.Zero);
                        var root=AutomationElement.FromHandle(main);
                        AutomationElement? button=null;
                        Wait(()=>(button=root.FindFirst(TreeScope.Descendants,new AndCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),
                            new PropertyCondition(AutomationElement.NameProperty,"Ins Tray"))))!=null,8000,"Host close dialog missing");
                        ClickBounds(button!.Current.BoundingRectangle);
                    }
                    Wait(()=>!IsWindowVisible(main),8000,"Host did not enter tray");
                    foreach(bool chooseRecord in new[]{recordFirst,!recordFirst})
                    {
                        if(IsWindowVisible(main)) { PostMessage(main,0x0112,new IntPtr(0xF020),IntPtr.Zero);Wait(()=>!IsWindowVisible(main),5000,"Host did not minimize to tray"); }
                        ShowLiveReminder(main);
                        TestButton(label,main,chooseRecord?"Zustand erfassen":"Später",chooseRecord);
                    }
                    ExitHost(main);app.Dispose();app=null;Thread.Sleep(600);
                }
            File.WriteAllText(Path.Combine(output,"status.txt"),"PASS 8/8 host cold starts, hover, clicks and passive focus");
            Log("PASS all host cold-start and reuse cases without Tab");
            return 0;
        }
        catch(Exception ex) {Log("Host cold-start failure: "+ex);Capture("host-cold-failure");File.WriteAllText(Path.Combine(output,"status.txt"),"FAIL: "+ex.Message);return 1;}
        finally
        {
            if(originalTray==null) {if(File.Exists(tray))File.Delete(tray);}else File.WriteAllBytes(tray,originalTray);
            if(originalChoice==null) {if(File.Exists(choice))File.Delete(choice);}else File.WriteAllBytes(choice,originalChoice);
            if(!flagExisted && File.Exists(flag)) File.Delete(flag);
            if(app is {HasExited:false})
                try {ExitHost(WaitHandleWindow("Pace Atlas",3000,false));}catch(Exception ex){Log("Host left running: "+ex.Message);}
        }
    }

    static void ExitHost(IntPtr main)
    {
        PostMessage(main,0x8000+47,IntPtr.Zero,new IntPtr(0x0205));
        ClickBounds(FindLiveMenu("Pace Atlas beenden").Current.BoundingRectangle);
        Wait(()=>app!.HasExited,8000,"Host did not exit normally");
    }

    static int LiveAction(string[] args)
    {
        output=args[2];Directory.CreateDirectory(output);
        app=Process.GetProcessById(int.Parse(args[1]));
        try
        {
            var main=WaitHandleWindow("Pace Atlas",5000,false);
            if(args[3]=="exit")
            {
                AutomationElement exit;
                try { exit=FindLiveMenu("Pace Atlas beenden"); }
                catch { PostMessage(main,0x8000+47,IntPtr.Zero,new IntPtr(0x0205)); exit=FindLiveMenu("Pace Atlas beenden"); }
                ClickBounds(exit.Current.BoundingRectangle);
                Wait(()=>app.HasExited,8000,"App did not exit through its tray menu");
                return 0;
            }
            if(args[3]=="show") { ShowLiveReminder(main);return 0; }
            PostMessage(main,0x0112,new IntPtr(0xF020),IntPtr.Zero);
            Wait(()=>!IsWindowVisible(main),5000,"Main did not enter tray");
            ShowLiveReminder(main);
            bool record=args[3]=="record";
            TestButton("host-updated",main,record?"Zustand erfassen":"Später",record);
            File.WriteAllText(Path.Combine(output,"live-"+args[3]+"-status.txt"),"PASS: drawn hover and actual click");
            return 0;
        }
        catch(Exception ex) {Log("Live action: "+ex);Capture("live-action-failure");return 1;}
    }

    static AutomationElement FindLiveMenu(string name, bool prefix = false)
    {
        AutomationElement? result = null;
        Wait(() => {
            var menus = AutomationElement.RootElement.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ProcessIdProperty, app!.Id));
            foreach (AutomationElement menu in menus)
                foreach (AutomationElement item in menu.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)))
                    if (prefix ? item.Current.Name.StartsWith(name, StringComparison.Ordinal) : item.Current.Name == name)
                    { result = item; return true; }
            return false;
        }, 5000, "Menu item not found: " + name);
        return result!;
    }

    static void ShowLiveReminder(IntPtr main)
    {
        PostMessage(main,0x8000+47,IntPtr.Zero,new IntPtr(0x0205));
        ClickBounds(FindLiveMenu("Zustandserinnerung").Current.BoundingRectangle);
        Thread.Sleep(250);
        ClickBounds(FindLiveMenu("Jetzt anzeigen").Current.BoundingRectangle);
    }

    static void RunScenario(string exe, bool direct, bool recordFirst)
    {
        string label = (direct ? "direct-tray" : "close-to-tray") + (recordFirst ? "-record-first" : "-later-first");
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string settings = Path.Combine(local, "PaceAtlas.WinUI");
        string data = Path.Combine(local, "PaceAtlas");
        Directory.CreateDirectory(settings); Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(settings, "reminder-input-diagnostics.enabled"), "test");
        File.WriteAllText(Path.Combine(settings, "language.txt"), "de");
        File.WriteAllText(Path.Combine(settings, "tray-settings.json"), JsonSerializer.Serialize(new { StartInTray = direct }));
        var closeChoice = Path.Combine(settings, "close-choice.json");
        if (File.Exists(closeChoice)) File.Delete(closeChoice);
        // One minute is supported by the settings reader and keeps the actual timer path.
        File.WriteAllText(Path.Combine(data, "state-reminder.json"), "{\"IntervalMinutes\":1,\"CustomInterval\":true}");
        app = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! })!;
        IntPtr main = WaitHandleWindow("Pace Atlas", 45000, false);
        Thread.Sleep(2500);
        if (!direct)
        {
            Require(IsWindowVisible(main), "Normal startup window not visible");
            SetForegroundWindow(main); Thread.Sleep(300);
            // A real keyboard close follows the same close-to-tray path as the titlebar button.
            keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x73, 0, 0, UIntPtr.Zero);
            keybd_event(0x73, 0, 2, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero);
            var mainRoot = AutomationElement.FromHandle(main);
            AutomationElement? trayButton = null;
            Wait(() => (trayButton = mainRoot.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.NameProperty, "Ins Tray")))) != null,
                6000, "Close dialog did not offer Ins Tray");
            Capture(label + "-close-dialog");
            ClickBounds(trayButton!.Current.BoundingRectangle);
            Log(label + ": clicked Ins Tray in the actual close dialog");
        }
        Wait(() => !IsWindowVisible(main), 10000, "Main window did not enter tray");
        Log(label + ": main window hidden");
        foreach (bool chooseRecord in new[] { recordFirst, !recordFirst })
        {
            if (IsWindowVisible(main))
            {
                PostMessage(main,0x0112,new IntPtr(0xF020),IntPtr.Zero);
                Wait(()=>!IsWindowVisible(main),5000,"Main did not return to tray");
            }
            ShowLiveReminder(main);
            TestButton(label,main,chooseRecord?"Zustand erfassen":"Später",chooseRecord);
        }
        File.Copy(Path.Combine(settings,"reminder-input.log"),Path.Combine(output,label+"-pointer.log"),true);
        app.Kill(true); app.WaitForExit(); app.Dispose(); app = null;
        Thread.Sleep(1500);
    }

    static void TestButton(string label, IntPtr main, string name, bool opensMain)
    {
        IntPtr popup = WaitHandleWindow("Zustandserinnerung", 85000, true);
        Require(!IsWindowVisible(main), "Main window unexpectedly visible before reminder click");
        // Detect the drawn button from screen pixels before touching accessibility.
        Require(DwmGetWindowAttribute(popup,9,out var frame,Marshal.SizeOf<RECT>())==0,"Physical reminder frame unavailable");
        MovePhysical(frame.Left-30,frame.Top+10); Thread.Sleep(300);
        using var pixels=CaptureArea(frame);
        var visibleBlue=FindBlue(pixels);
        double gap=12*GetDpiForWindow(popup)/96.0;
        var visible=new System.Windows.Rect(frame.Left+visibleBlue.Left,frame.Top+visibleBlue.Top,visibleBlue.Width,visibleBlue.Height);
        string traceEvent="record.enter";
        if(!opensMain) { visible.X += visibleBlue.Width+gap+3; traceEvent="later.enter"; }
        Require((GetWindowLongPtr(popup,-20).ToInt64() & 0x08000000L)!=0,"Reminder lacks WS_EX_NOACTIVATE");
        string traceFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PaceAtlas.WinUI","reminder-input.log");
        Require(File.Exists(traceFile),"Reminder trace unavailable");
        var traceLines=File.ReadAllLines(traceFile);
        Require(traceLines.Last(line=>line.Contains("create.noactivate=")).EndsWith("create.noactivate=True",StringComparison.Ordinal),"Reminder creation changed foreground or keyboard focus");
        Require(traceLines.Last(line=>line.Contains("show.noactivate=")).EndsWith("show.noactivate=True",StringComparison.Ordinal),"Reminder show changed foreground or keyboard focus");
        Log("PASS passive show: foreground and keyboard focus unchanged");
        int previous=File.Exists(traceFile)?File.ReadLines(traceFile).Count(line=>line.EndsWith(traceEvent,StringComparison.Ordinal)):0;
        MovePhysical((int)(visible.X+visible.Width/2),(int)(visible.Y+visible.Height/2));
        GetPhysicalCursorPos(out var hoverPoint);
        Log("Testing drawn hover center: "+visible+"; cursor="+hoverPoint.X+","+hoverPoint.Y+"; prior="+previous);
        pixels.Save(Path.Combine(output,"drawn-button-"+testCount+".png"),ImageFormat.Png);
        Wait(()=>File.Exists(traceFile) && File.ReadLines(traceFile).Count(line=>line.EndsWith(traceEvent,StringComparison.Ordinal))>previous,
            2500,"Visible button did not receive its pointer-enter event: "+name);
        Log("PASS hover at screen-pixel button center: "+name+"; visible="+visible);
        var root = AutomationElement.FromHandle(popup);
        AutomationElement? button = null;
        Wait(() => (button = root.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.NameProperty, name)))) != null, 4000, "Button not exposed: " + name);
        var bounds = button!.Current.BoundingRectangle;
        Log(label + ": " + name + "; enabled=" + button.Current.IsEnabled + "; rect=" + bounds);
        Require(button.Current.IsEnabled && !bounds.IsEmpty && bounds.Width > 0, "Button disabled or empty");
        Capture(label + "-before-" + testCount);
        // Send actual mouse input; never InvokePattern or a direct event handler call.
        var foregroundBeforeClick=GetForegroundWindow();
        var focusBeforeClick=ForegroundFocus(foregroundBeforeClick);
        ClickBounds(visible);
        var clickedAt = Stopwatch.StartNew();
        // Must react before the 20-second automatic dismiss can produce a false pass.
        Wait(() => !IsWindowVisible(popup), 3000, name + " did not dismiss the reminder after the mouse click");
        if (opensMain)
        {
            Wait(() => IsWindowVisible(main), 5000, "Record did not open the main window");
            VerifySelectedStateTab(main);
        }
        else
        {
            Require(!IsWindowVisible(main),"Later unexpectedly opened main window");
            Require(GetForegroundWindow()==foregroundBeforeClick && ForegroundFocus(GetForegroundWindow())==focusBeforeClick,
                "Later changed the foreground window or keyboard focus");
            Log("PASS Later: foreground and keyboard focus unchanged");
        }
        testCount++; Capture(label + "-after-" + testCount);
        Log("PASS " + testCount + ": " + label + " / " + name + " (" + clickedAt.ElapsedMilliseconds + " ms)");
    }

    static Bitmap CaptureArea(RECT rect)
    {
        var bitmap=new Bitmap(rect.Right-rect.Left,rect.Bottom-rect.Top);
        using var graphics=Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(rect.Left,rect.Top,0,0,bitmap.Size);
        return bitmap;
    }

    static System.Drawing.Rectangle FindBlue(Bitmap image)
    {
        // The reminder has exactly one large teal button. Ignore small icon pixels.
        int left=image.Width,top=image.Height,right=-1,bottom=-1;
        for(int y=image.Height/4;y<image.Height*3/4;y++)
            for(int x=10;x<image.Width/2;x++)
            {
                var c=image.GetPixel(x,y);
                if(c.R==22 && c.G==119 && c.B==137)
                { left=Math.Min(left,x); top=Math.Min(top,y); right=Math.Max(right,x); bottom=Math.Max(bottom,y); }
            }
        Require(right-left>80 && bottom-top>15,"Visible teal button not found in screen pixels");
        return System.Drawing.Rectangle.FromLTRB(left,top,right+1,bottom+1);
    }

    static bool MovePhysical(int x,int y)
    {
        int left=GetSystemMetrics(76),top=GetSystemMetrics(77),width=GetSystemMetrics(78),height=GetSystemMetrics(79);
        var input=new INPUT { type=0,mi=new MOUSEINPUT {
            dx=(int)Math.Round((x-left)*65535.0/Math.Max(1,width-1)),
            dy=(int)Math.Round((y-top)*65535.0/Math.Max(1,height-1)),
            dwFlags=0xC001 } };
        return SendInput(1,new[]{input},Marshal.SizeOf<INPUT>())==1;
    }

    static void ClickBounds(System.Windows.Rect bounds)
    {
        var center = new POINT { X = (int)(bounds.X + bounds.Width / 2), Y = (int)(bounds.Y + bounds.Height / 2) };
        var hit = WindowFromPhysicalPoint(center);
        GetWindowThreadProcessId(hit, out uint hitPid);
        var cls = new System.Text.StringBuilder(256); GetClassName(hit, cls, cls.Capacity);
        Log("Click target native class=" + cls + "; pid=" + hitPid + "; bounds=" + bounds + "; enabled=" + IsWindowEnabled(hit));
        Require(MovePhysical(center.X, center.Y), "Physical cursor positioning failed");
        GetPhysicalCursorPos(out var actual);
        Log("Cursor at " + actual.X + "," + actual.Y + "; intended " + center.X + "," + center.Y);
        INPUT[] input = { new() { type = 0, mi = new MOUSEINPUT { dwFlags = 0x0002 } }, new() { type = 0, mi = new MOUSEINPUT { dwFlags = 0x0004 } } };
        Require(SendInput((uint)input.Length, input, Marshal.SizeOf<INPUT>()) == 2, "Mouse injection failed");
    }

    static IntPtr WaitHandleWindow(string title, int timeout, bool visible)
    {
        IntPtr result = IntPtr.Zero;
        Wait(() => {
            EnumWindows((hwnd, _) => {
                GetWindowThreadProcessId(hwnd, out uint pid);
                var text = new System.Text.StringBuilder(512); GetWindowText(hwnd, text, 512);
                if (pid == app!.Id && text.ToString() == title && (!visible || IsWindowVisible(hwnd))) { result = hwnd; return false; }
                return true;
            }, IntPtr.Zero);
            return result != IntPtr.Zero;
        }, timeout, "Window not found: " + title);
        return result;
    }

    static void Wait(Func<bool> condition, int ms, string error)
    {
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < ms) { if (condition()) return; if (app is { HasExited: true }) throw new Exception("App exited: " + app.ExitCode); Thread.Sleep(100); }
        throw new Exception(error);
    }

    static void Require(bool pass, string error) { if (!pass) throw new Exception(error); }

    static void Log(string text) { File.AppendAllText(Path.Combine(output, "test.log"), DateTime.Now.ToString("O") + " " + text + Environment.NewLine); }

    static void Capture(string name)
    {
        try
        {
            if(app==null) return;
            IntPtr target=IntPtr.Zero; bool reminder=false;
            EnumWindows((hwnd,_)=>{
                GetWindowThreadProcessId(hwnd,out uint pid);
                if(pid!=app.Id || !IsWindowVisible(hwnd)) return true;
                var title=new System.Text.StringBuilder(128);GetWindowText(hwnd,title,128);
                if(title.ToString()=="Zustandserinnerung") { target=hwnd;reminder=true;return false; }
                if(title.ToString()=="Pace Atlas") target=hwnd;
                return true;
            },IntPtr.Zero);
            if(target==IntPtr.Zero) return;
            GetWindowRect(target,out var rect);
            rect.Right=Math.Min(rect.Right,rect.Left+2400);
            rect.Bottom=Math.Min(rect.Bottom,rect.Top+(reminder?900:600));
            using var image=CaptureArea(rect);
            image.Save(Path.Combine(output,name+".png"),ImageFormat.Png);
        }
        catch(Exception ex) {Log("Capture: "+ex.Message);}
    }

    static void VerifySelectedStateTab(IntPtr main)
    {
        var root=AutomationElement.FromHandle(main);
        string[] names=Array.Empty<string>();
        Wait(()=>{
            names=root.FindAll(TreeScope.Descendants,new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.TabItem),
                new PropertyCondition(SelectionItemPattern.IsSelectedProperty,true)))
                .Cast<AutomationElement>().Select(e=>e.Current.Name).ToArray();
            return names.Any(n=>n.Contains("Zustand",StringComparison.OrdinalIgnoreCase));
        },8000,"Condition tab not selected after record click");
        Log("Verified selected tab after layout: "+string.Join(", ",names));
    }

    static IntPtr ForegroundFocus(IntPtr window)
    {
        uint id=GetWindowThreadProcessId(window,out _);
        var info=new GuiInfo { Size=(uint)Marshal.SizeOf<GuiInfo>() };
        return GetGUIThreadInfo(id,ref info)?info.Focus:IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] struct GuiInfo { public uint Size,Flags; public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret; public RECT CaretBounds; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx,dy; public uint mouseData,dwFlags,time; public UIntPtr dwExtraInfo; }
    delegate bool EnumProc(IntPtr window,IntPtr data);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] static extern uint SendInput(uint count,INPUT[] inputs,int size);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out RECT rect);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out RECT rect,int size);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("user32.dll")] static extern bool GetPhysicalCursorPos(out POINT point);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPhysicalPoint(POINT point);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,System.Text.StringBuilder text,int max);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread,ref GuiInfo info);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,System.Text.StringBuilder text,int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    [DllImport("user32.dll")] static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
}
