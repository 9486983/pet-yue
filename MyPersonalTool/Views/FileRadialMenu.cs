using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MyPersonalTool.Core.Models;

namespace MyPersonalTool.Views;

/// <summary>
/// 文件拖放径向菜单 —— 拖文件到宠物时即时弹出，可直接拖到选项上释放。
/// </summary>
public class FileRadialMenu : Window
{
    private readonly List<FileActionConfig> _actions;
    private readonly PixelPoint _anchorCenter;
    private readonly List<Border> _items = [];
    private readonly bool[] _itemReady = [];
    private bool _closed;
    private int _hoveredIndex = -1;

    private const double MenuSize = 300;
    private const double Radius = 88;
    private const double ItemSize = 72;

    // ═══ 自定义区域：调整选项排列位置 ═══
    // 全圆分 10 份，每份 36°；ArcPosition 指定从第几份开始
    // ArcPosition : 0=正上  1=右上  2=右  3=右下  4=下  5=左下  6=左  7=左上  8,9=其他
    private const int TotalParts = 10;
    private const int ArcPosition = 0;   // 弧段起点 (0~9)
    private const int ArcSpan = 3;       // 弧段覆盖的份数（选项越多此值应越大）
    // ═══════════════════════════════════

    private static double ArcAngle => ArcSpan * (2 * Math.PI / TotalParts);
    private static double ArcCenterAngle => -Math.PI / 2 + ArcPosition * (2 * Math.PI / TotalParts);

    // 主题色缓存
    private static readonly Lazy<Color> BgOverlay = new(() => GetColor("BgOverlay", 0xCC2C2420));
    private static readonly Lazy<Color> BorderColor = new(() => GetColor("BorderColor", 0xFF5D4F45));
    private static readonly Lazy<Color> TextPrimary = new(() => GetColor("TextPrimary", 0xFFF0E6D3));
    private static readonly Lazy<Color> AccentPrimary = new(() => GetColor("AccentPrimary", 0xFF19c8b9));
    private static readonly Lazy<Color> BgHover = new(() => GetColor("BgHover", 0xFF4D3F37));

    private FileRadialMenu(List<FileActionConfig> actions, PixelPoint anchorCenter)
    {
        _actions = actions;
        _anchorCenter = anchorCenter;

        Title = "";
        Width = MenuSize;
        Height = MenuSize;
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Topmost = true;
        Opacity = 0;
        DragDrop.SetAllowDrop(this, true);

        // Width/Height 是 DIP，Position 是物理像素，需用 scaling 换算
        var scaling = this.RenderScaling;
        Position = new PixelPoint(
            _anchorCenter.X - (int)(MenuSize * scaling / 2),
            _anchorCenter.Y - (int)(MenuSize * scaling / 2));

        var canvas = new Canvas { Width = MenuSize, Height = MenuSize };
        Content = canvas;

        var count = actions.Count;
        var baseBg = new SolidColorBrush(BgOverlay.Value);
        var baseBorder = new SolidColorBrush(BorderColor.Value);
        var textClr = new SolidColorBrush(TextPrimary.Value);
        var hoverBg = new SolidColorBrush(BgHover.Value);
        var hoverBorder = new SolidColorBrush(AccentPrimary.Value);

        var halfItem = ItemSize / 2;
        var startAngle = ArcCenterAngle - ArcAngle / 2;
        var angleStep = count > 1 ? ArcAngle / (count - 1) : 0;

        for (var i = 0; i < count; i++)
        {
            var action = actions[i];
            var angle = startAngle + angleStep * i;
            var cx = MenuSize / 2 + Radius * Math.Cos(angle);
            var cy = MenuSize / 2 + Radius * Math.Sin(angle);

            var btn = new Border
            {
                Width = ItemSize,
                Height = ItemSize,
                CornerRadius = new CornerRadius(halfItem),
                Background = baseBg,
                BorderThickness = new Thickness(2),
                BorderBrush = baseBorder,
                Cursor = new Cursor(StandardCursorType.Hand),
                Opacity = 0,
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                RenderTransform = new ScaleTransform(0.6, 0.6),
                Transitions = new Transitions
                {
                    new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(250), Easing = new CubicEaseOut() },
                },
                Child = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = action.Emoji, FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock
                        {
                            Text = action.Name, FontSize = 11,
                            Foreground = textClr,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            FontWeight = FontWeight.SemiBold,
                        },
                    },
                },
            };

            Canvas.SetLeft(btn, cx - halfItem);
            Canvas.SetTop(btn, cy - halfItem);
            canvas.Children.Add(btn);
            _items.Add(btn);
        }
        _itemReady = new bool[_items.Count];

        // 窗口自身处理拖放（根据鼠标位置确定 hover 项）
        AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = DragDropEffects.Copy;
            var pt = e.GetPosition(canvas);
            HighlightNearest(pt);
        });

        AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            e.DragEffects = DragDropEffects.Copy;
            var pt = e.GetPosition(canvas);
            var idx = FindNearestIndex(pt);
            if (idx >= 0 && idx < _actions.Count)
            {
                var files = await ReadFiles(e);
                if (files.Length > 0)
                    await ExecuteAction(_actions[idx], files);
            }
            Close();
        });

        AddHandler(DragDrop.DragLeaveEvent, (_, _) => _ = DelayClose());

        Deactivated += (_, _) => Close();

        this.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(150) },
        };
    }

    public void Open()
    {
        Show();
        Opacity = 1;
        _ = AnimateItemsIn();
    }

    private async Task AnimateItemsIn()
    {
        // 预先启动浮动 timer，每个选项入场后自动加入
        StartFloating();

        for (var i = 0; i < _items.Count; i++)
        {
            await Task.Delay(60);
            _items[i].Opacity = 1;
            // 入场缩放动画（弹性缓出）
            await AnimateScale(_items[i], 0.6, 1.0, 300, new ElasticEaseOut());
            // 标记为已入场，浮动 timer 接管
            _itemReady[i] = true;
        }
    }

    /// <summary>逐帧平滑缩放</summary>
    private static async Task AnimateScale(Border item, double from, double to, int ms, Easing? easing)
    {
        easing ??= new CubicEaseOut();
        var frames = ms / 16;
        for (var f = 0; f <= frames; f++)
        {
            var t = easing.Ease((double)f / frames);
            var s = from + (to - from) * t;
            item.RenderTransform = new ScaleTransform(s, s);
            await Task.Delay(16);
        }
        item.RenderTransform = new ScaleTransform(to, to);
    }

    /// <summary>所有已入场选项持续上下浮动（顺时针相位错开）</summary>
    private void StartFloating()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        var startTime = DateTime.UtcNow;
        timer.Tick += (_, _) =>
        {
            var t = (DateTime.UtcNow - startTime).TotalSeconds;
            for (var i = 0; i < _items.Count; i++)
            {
                if (!_itemReady[i]) continue; // 尚未入场，跳过
                var phase = t * 2 * Math.PI / 1.8 + i * Math.PI / 3;
                var offset = Math.Sin(phase) * 5;
                _items[i].RenderTransform = new TransformGroup
                {
                    Children = new Transforms
                    {
                        new ScaleTransform(1, 1),
                        new TranslateTransform(0, offset),
                    }
                };
            }
        };
        timer.Start();
    }

    private void HighlightNearest(Point pt)
    {
        var idx = FindNearestIndex(pt);
        if (idx == _hoveredIndex) return;
        if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count)
        {
            _items[_hoveredIndex].Background = new SolidColorBrush(BgOverlay.Value);
            _items[_hoveredIndex].BorderBrush = new SolidColorBrush(BorderColor.Value);
        }
        if (idx >= 0 && idx < _items.Count)
        {
            _items[idx].Background = new SolidColorBrush(BgHover.Value);
            _items[idx].BorderBrush = new SolidColorBrush(AccentPrimary.Value);
        }
        _hoveredIndex = idx;
    }

    private int FindNearestIndex(Point pt)
    {
        var halfItem = ItemSize / 2;
        var best = -1;
        var bestDist = double.MaxValue;
        for (var i = 0; i < _items.Count; i++)
        {
            var left = Canvas.GetLeft(_items[i]) + halfItem;
            var top = Canvas.GetTop(_items[i]) + halfItem;
            var dx = pt.X - left;
            var dy = pt.Y - top;
            var dist = dx * dx + dy * dy;
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        return bestDist <= 55 * 55 ? best : -1;
    }

    private static async Task<string[]> ReadFiles(DragEventArgs e)
    {
        try
        {
            var items = e.DataTransfer?.TryGetFiles();
            if (items != null) return items.Select(i => i.Path.LocalPath).ToArray();
        }
        catch { }
        return [];
    }

    private async Task ExecuteAction(FileActionConfig action, string[] files)
    {
        if (_closed) return;
        _closed = true;
        try
        {
            if (action.ActionCallback != null) await action.ActionCallback(files);
        }
        catch { }
        Close();
    }

    private async Task DelayClose()
    {
        await Task.Delay(800);
        if (!_closed) Close();
    }

    public static void ShowDuringDrag(Window owner, List<FileActionConfig> actions, PixelPoint anchorCenter)
    {
        if (actions.Count == 0) return;
        var menu = new FileRadialMenu(actions, anchorCenter);
        menu.Open();
    }

    private static Color GetColor(string key, uint fallback)
    {
        if (Application.Current?.TryFindResource(key, out var value) == true && value is Color c)
            return c;
        return Color.Parse($"#{fallback:X8}");
    }
}
