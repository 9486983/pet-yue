using System.Runtime.InteropServices;
using System.Text;

namespace PinTopPlugin;

// ── Delegates ──
internal delegate IntPtr WndProcDelegate(IntPtr h, int m, IntPtr w, IntPtr l);
internal delegate void WinEventDelegate(IntPtr hHook, uint evt, IntPtr hwnd, int idObj, int idChild, uint dwThread, uint dwTime);

// ── Constants ──
internal static class Win32Const
{
    public const int WM_HOTKEY = 0x0312;
    public const int WM_ERASEBKGND = 0x0014;
    public const int WM_USER = 0x0400;
    public const int WM_REMOVE_OVERLAY = WM_USER + 1;
    public const int WM_UNPIN = WM_USER + 2;
    public const int WM_REFRESH_OVERLAYS = WM_USER + 3;
    public const uint EVENT_OBJECT_DESTROY = 0x8001;
    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    public const uint EVENT_OBJECT_FOCUS = 0x8005;
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0;
    public const uint FLASHW_CAPTION = 1;
    public const uint FLASHW_TIMERNOFG = 12;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x80000;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WS_EX_TOOLWINDOW = 0x80;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOPMOST = 0x8;
    public const uint WS_POPUP = 0x80000000;
    public const int LWA_ALPHA = 0x2;
    public const int SWP_NOMOVE = 0x0002;
    public const int SWP_NOSIZE = 0x0001;
    public const int SWP_NOACTIVATE = 0x0010;
    public const int SWP_SHOWWINDOW = 0x0040;
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);
}

// ── P/Invoke ──
internal static class Win32Native
{
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr ha, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint m, uint v);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateWindowExW(int ex, string cn, string? wn, uint s, int x, int y, int w, int h, IntPtr p, IntPtr m, IntPtr i, IntPtr d);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern ushort RegisterClassW(ref WNDCLASSW wc);
    [DllImport("user32.dll")] public static extern IntPtr DefWindowProcW(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetMessage(out MSG m, IntPtr h, int f, int t);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref MSG m);
    [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint tid, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, int flags);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32.dll")] public static extern int FillRect(IntPtr dc, ref RECT r, IntPtr brush);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string? n);
    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmod, WinEventDelegate lpfn, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hHook);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateSolidBrush(uint colorRef);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool FlashWindowEx(ref FLASHWINFO p);
}

// ── Structs ──
[StructLayout(LayoutKind.Sequential)]
internal struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WNDCLASSW { public uint style; public IntPtr lpfnWndProc; public int cbClsExtra; public int cbWndExtra; public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor; public IntPtr hbrBackground; public string? lpszMenuName; public string lpszClassName; }

[StructLayout(LayoutKind.Sequential)]
internal struct MSG { public IntPtr hwnd; public int message; public IntPtr wParam; public IntPtr lParam; public int time; public int pt_x; public int pt_y; }

[StructLayout(LayoutKind.Sequential)]
internal struct RECT { public int Left, Top, Right, Bottom; }
