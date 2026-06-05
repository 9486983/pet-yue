using static PinTopPlugin.Win32Native;
using static PinTopPlugin.Win32Const;

namespace PinTopPlugin;

/// <summary>Overlay 管理器 —— 协调策略选择、生命周期、事件响应</summary>
internal class OverlayManager
{
    private IOverlayStyle _style = new BorderStyle();
    private string _styleName = "border";
    private OverlayConfig _cfg = new();
    private readonly HashSet<IntPtr> _pinned;
    private readonly Dictionary<IntPtr, IntPtr[]> _overlays = new();
    private IntPtr _msgWnd;

    // ── 事件钩子引用（需保持存活） ──
    public WinEventDelegate? DestroyHandler { get; private set; }
    public WinEventDelegate? LocationHandler { get; private set; }
    public WinEventDelegate? ForegroundHandler { get; private set; }
    public WinEventDelegate? FocusHandler { get; private set; }
    public IntPtr DestroyHook { get; set; }
    public IntPtr LocationHook { get; set; }
    public IntPtr ForegroundHook { get; set; }
    public IntPtr FocusHook { get; set; }

    public string OverlayClassName { get; set; } = "";

    public OverlayManager(HashSet<IntPtr> pinned)
    {
        _pinned = pinned;
    }

    public void RegisterEventHandlers()
    {
        DestroyHandler = OnWindowDestroyed;
        LocationHandler = OnLocationChanged;
        ForegroundHandler = OnForegroundChanged;
        FocusHandler = OnFocus;

        DestroyHook = SetWinEventHook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_DESTROY,
            IntPtr.Zero, DestroyHandler, 0, 0, WINEVENT_OUTOFCONTEXT);
        LocationHook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, LocationHandler, 0, 0, WINEVENT_OUTOFCONTEXT);
        // 窗口激活（Alt+Tab / 点击标题栏）
        ForegroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, ForegroundHandler, 0, 0, WINEVENT_OUTOFCONTEXT);
        // 键盘聚焦（点击窗口内编辑框等控件）
        FocusHook = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS,
            IntPtr.Zero, FocusHandler, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    public void RefreshAll(OverlayConfig cfg, string styleName, IntPtr msgWnd)
    {
        _cfg = cfg;
        _styleName = styleName;
        _msgWnd = msgWnd;

        if (_cfg.Brush != IntPtr.Zero)
        {
            DeleteObject(_cfg.Brush);
            _cfg.Brush = IntPtr.Zero;
        }
        _cfg.Brush = CreateSolidBrush(_cfg.ColorBgr);

        _style = styleName switch
        {
            "corner" => new CornerStyle(),
            "flash" => new FlashStyle(),
            _ => new BorderStyle(),
        };

        var snapshot = _pinned.ToList();
        foreach (var h in snapshot)
        {
            Remove(h);
            Create(h);
        }
    }

    public void Create(IntPtr targetHwnd)
    {
        if (_overlays.ContainsKey(targetHwnd)) return;

        var overlays = _style.Apply(targetHwnd, _cfg);
        if (_style.IsPersistent && overlays != null)
        {
            _overlays[targetHwnd] = overlays;
            _style.Update(targetHwnd, overlays, _cfg);
        }
    }

    public void Update(IntPtr targetHwnd)
    {
        if (_overlays.TryGetValue(targetHwnd, out var overlays))
            _style.Update(targetHwnd, overlays, _cfg);
    }

    public void Remove(IntPtr targetHwnd)
    {
        if (!_overlays.Remove(targetHwnd, out var overlays)) return;
        _style.Remove(overlays);
    }

    public void Cleanup()
    {
        if (DestroyHook != IntPtr.Zero) { UnhookWinEvent(DestroyHook); DestroyHook = IntPtr.Zero; }
        if (LocationHook != IntPtr.Zero) { UnhookWinEvent(LocationHook); LocationHook = IntPtr.Zero; }
        if (ForegroundHook != IntPtr.Zero) { UnhookWinEvent(ForegroundHook); ForegroundHook = IntPtr.Zero; }
        if (FocusHook != IntPtr.Zero) { UnhookWinEvent(FocusHook); FocusHook = IntPtr.Zero; }

        foreach (var kv in _overlays.ToList())
        {
            _style.Remove(kv.Value);
            _overlays.Remove(kv.Key);
        }
        if (_cfg.Brush != IntPtr.Zero) { DeleteObject(_cfg.Brush); _cfg.Brush = IntPtr.Zero; }
    }

    // ── 事件回调 ──

    private void OnWindowDestroyed(IntPtr hHook, uint evt, IntPtr hwnd, int idObj, int idChild, uint dwThread, uint dwTime)
    {
        if (idObj != 0) return;
        if (_pinned.Remove(hwnd))
        {
            Remove(hwnd);
            if (_msgWnd != IntPtr.Zero)
                PostMessage(_msgWnd, WM_REMOVE_OVERLAY, hwnd, IntPtr.Zero);
        }
    }

    private void OnLocationChanged(IntPtr hHook, uint evt, IntPtr hwnd, int idObj, int idChild, uint dwThread, uint dwTime)
    {
        if (idObj != 0) return;
        try { Update(hwnd); }
        catch { }
    }

    private void OnForegroundChanged(IntPtr hHook, uint evt, IntPtr hwnd, int idObj, int idChild, uint dwThread, uint dwTime)
    {
        if (idObj != 0) return;
        ReTopmost(hwnd);
    }

    private void OnFocus(IntPtr hHook, uint evt, IntPtr hwnd, int idObj, int idChild, uint dwThread, uint dwTime)
    {
        // idObj 可能为 0（窗口聚焦）或子元素 ID（控件聚焦），都视为该窗口被激活
        ReTopmost(hwnd);
    }

    private void ReTopmost(IntPtr hwnd)
    {
        if (_pinned.Contains(hwnd))
        {
            try
            {
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            catch { }
        }
    }
}
