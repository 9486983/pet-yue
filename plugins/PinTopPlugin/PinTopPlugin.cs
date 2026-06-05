using System.Runtime.InteropServices;
using System.Text;
using MyPersonalTool.Sdk;

namespace PinTopPlugin;

[Plugin("窗口置顶器", Version = "1.0.0", Description = "Ctrl+Alt+T 置顶/取消置顶当前窗口")]
public class PinTopPlugin : PluginBase
{
    private IPluginHost? _host;
    private Thread? _pump;
    private IntPtr _msgWnd;
    private int _hotKeyId;
    private readonly HashSet<IntPtr> _pinned = new();
    private WndProcDelegate? _wndProc;
    private ListDialogConfig? _activeListConfig;
    private IntPtr _winEventHook;
    private WinEventDelegate? _winEventDelegate;

    private delegate IntPtr WndProcDelegate(IntPtr h, int m, IntPtr w, IntPtr l);
    private delegate void WinEventDelegate(IntPtr hHook, uint evt, IntPtr hwnd, int idObj, int idChild, uint dwThread, uint dwTime);

    const int WM_HOTKEY = 0x0312;
    const uint EVENT_OBJECT_DESTROY = 0x8001;
    const uint WINEVENT_OUTOFCONTEXT = 0;

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr ha, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint m, uint v);
    [DllImport("user32.dll")] static extern IntPtr CreateWindowEx(int ex, string cn, string wn, int s, int x, int y, int w, int h, IntPtr p, IntPtr m, IntPtr i, IntPtr d);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern ushort RegisterClassW(ref WNDCLASSW wc);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProcW(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, int f, int t);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG m);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint tid, int m, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string? n);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmod, WinEventDelegate lpfn, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hHook);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WNDCLASSW { public uint style; public IntPtr lpfnWndProc; public int cbClsExtra; public int cbWndExtra; public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor; public IntPtr hbrBackground; public string lpszMenuName; public string lpszClassName; }
    [StructLayout(LayoutKind.Sequential)]
    struct MSG { public IntPtr hwnd; public int message; public IntPtr wParam; public IntPtr lParam; public int time; public int pt_x; public int pt_y; }

    const int SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001;

    public override string Name => "窗口置顶器";

    public override Task InitializeAsync(IPluginHost host)
    {
        _host = host;
        host.RegisterConfig(new PluginConfigSection { Title = "窗口置顶器", Emoji = "📌",
            Fields = new() {
                new() { Key = "pt_enabled", Label = "启用", Type = PluginConfigFieldType.Boolean, DefaultValue = "true" },
                new() { Key = "pt_modifiers", Label = "快捷键修饰键", Type = PluginConfigFieldType.String, DefaultValue = "Ctrl+Alt" },
                new() { Key = "pt_key", Label = "快捷键主键", Type = PluginConfigFieldType.String, DefaultValue = "T" },
            }}, Name);
        host.RegisterAction(new PluginAction { Name = "设置", Emoji = "📌", Group = "📌 窗口置顶器", Target = ActionTarget.ContextMenu,
            Callback = () => { host.ShowConfigDialog("窗口置顶器"); return Task.CompletedTask; } });

        host.RegisterAction(new PluginAction
        {
            Name = "已置顶窗口",
            Emoji = "📌",
            Group = "📌 窗口置顶器",
            Target = ActionTarget.ContextMenu,
            Callback = () => ShowPinnedList(host),
        });

        if (host.GetConfig("pt_enabled") != "false")
        {
            var mod = ParseMod(host.GetConfig("pt_modifiers") ?? "Ctrl+Alt");
            var key = ParseKey(host.GetConfig("pt_key") ?? "T");
            StartPump(mod, key);
        }
        host.Log("窗口置顶器插件已加载");
        return Task.CompletedTask;
    }

    private void StartPump(uint mod, uint key)
    {
        _hotKeyId = GetHashCode();
        _wndProc = WndProc;
        _winEventDelegate = OnWindowDestroyed;
        var host = _host;
        var pinned = _pinned;
        var cn = $"PinTop_{_hotKeyId}";
        var hi = GetModuleHandle(null);
        var wndProcPtr = Marshal.GetFunctionPointerForDelegate(_wndProc);

        _pump = new Thread(() =>
        {
            try
            {
                var wc = new WNDCLASSW { lpfnWndProc = wndProcPtr, hInstance = hi, lpszClassName = cn };
                RegisterClassW(ref wc);
                _msgWnd = CreateWindowEx(0, cn, "", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hi, IntPtr.Zero);
                if (_msgWnd == IntPtr.Zero) return;
                RegisterHotKey(_msgWnd, _hotKeyId, mod, key);

                // 监听窗口销毁事件，自动清理已关闭的置顶窗口
                _winEventHook = SetWinEventHook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_DESTROY,
                    IntPtr.Zero, _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

                while (GetMessage(out var m, IntPtr.Zero, 0, 0) != 0)
                {
                    try { TranslateMessage(ref m); DispatchMessage(ref m); }
                    catch { }
                }
            }
            catch { }
        }) { IsBackground = true, Name = "PinTop" };
        _pump.Start();
    }

    private IntPtr WndProc(IntPtr h, int m, IntPtr w, IntPtr l)
    {
        if (m == WM_HOTKEY && (int)w == _hotKeyId)
        {
            try { OnHotKey(); }
            catch { }
            return IntPtr.Zero;
        }
        return DefWindowProcW(h, m, w, l);
    }

    private void OnHotKey()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        var host = _host;
        var pinned = _pinned;
        var listConfig = _activeListConfig;
        if (host == null) return;

        if (pinned.Contains(hwnd))
        {
            SetWindowPos(hwnd, new IntPtr(-2), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            pinned.Remove(hwnd);
            host.ShowThought("📌 已取消", GetTitle(hwnd));
        }
        else
        {
            SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            pinned.Add(hwnd);
            host.ShowThought("📌 已置顶", GetTitle(hwnd));
        }
        listConfig?.NotifyDataChanged();
    }

    private void OnWindowDestroyed(IntPtr hHook, uint evt, IntPtr hwnd, int idObj, int idChild, uint dwThread, uint dwTime)
    {
        if (idObj != 0) return; // 只关心窗口本身
        if (_pinned.Remove(hwnd))
            _activeListConfig?.NotifyDataChanged();
    }

    private static string GetTitle(IntPtr h)
    {
        var sb = new StringBuilder(256);
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }

    private async Task ShowPinnedList(IPluginHost host)
    {
        var pinned = _pinned;
        if (pinned.Count == 0)
        {
            host.ShowThought("📌 置顶列表", "暂无已置顶的窗口");
            return;
        }

        if (_activeListConfig != null) return; // 已打开

        var config = new ListDialogConfig
        {
            Title = "已置顶窗口",
            Emoji = "📌",
            DataSource = () =>
            {
                var valid = pinned.Where(IsWindow).ToList();
                foreach (var h in pinned.Except(valid).ToList())
                    pinned.Remove(h);

                return Task.FromResult(valid.Select(h => new Dictionary<string, string>
                {
                    { "hwnd", h.ToString() },
                    { "title", GetTitle(h) },
                }).ToList());
            },
            Columns = new()
            {
                new() { Key = "title", Header = "窗口标题" },
                new()
                {
                    Key = "actions", Header = "",
                    Type = ListColumnType.Action,
                    RowActions = new()
                    {
                        new()
                        {
                            Label = "取消", Emoji = "🔓",
                            Callback = row =>
                            {
                                if (IntPtr.TryParse(row["hwnd"], out var h))
                                {
                                    SetWindowPos(h, new IntPtr(-2), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                                    pinned.Remove(h);
                                    host.ShowThought("📌 已取消", GetTitle(h));
                                }
                                return Task.CompletedTask;
                            },
                        },
                    },
                },
            },
        };

        _activeListConfig = config;
        await host.ShowListDialog(config);
        _activeListConfig = null;
    }

    static uint ParseMod(string s) { uint m = 0; foreach (var p in s.Split('+', StringSplitOptions.TrimEntries)) m |= p.ToLowerInvariant() switch { "ctrl" => 0x0002u, "alt" => 0x0001u, "shift" => 0x0004u, "win" => 0x0008u, _ => 0 }; return m; }
    static uint ParseKey(string s) { s = s.Trim().ToUpperInvariant(); if (s.Length == 1 && s[0] >= 'A' && s[0] <= 'Z') return (uint)s[0]; return 0x54u; }

    public override Task CleanupAsync()
    {
        if (_winEventHook != IntPtr.Zero)
            UnhookWinEvent(_winEventHook);
        foreach (var h in _pinned.ToList())
            SetWindowPos(h, new IntPtr(-2), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        _pinned.Clear();
        if (_pump?.ManagedThreadId > 0)
            PostThreadMessage((uint)_pump.ManagedThreadId, 0x0012, IntPtr.Zero, IntPtr.Zero);
        return base.CleanupAsync();
    }
}
