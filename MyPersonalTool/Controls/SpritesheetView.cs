using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;

namespace MyPersonalTool.Controls;

/// <summary>
/// 精灵图表动画控件 —— 自动跳过空白帧，消除因空单元格导致的闪烁
/// </summary>
public class SpritesheetView : Control
{
    private Bitmap[] _frames = [];
    private int[] _framesPerRow = []; // 每行实际有效帧数
    private int _currentFrame;
    private DispatcherTimer? _timer;

    // ── 依赖属性 ──

    public static readonly StyledProperty<string> SpritesheetProperty =
        AvaloniaProperty.Register<SpritesheetView, string>(nameof(Spritesheet));

    public static readonly StyledProperty<int> CurrentRowProperty =
        AvaloniaProperty.Register<SpritesheetView, int>(nameof(CurrentRow), 0);

    public static readonly StyledProperty<int> FrameWidthProperty =
        AvaloniaProperty.Register<SpritesheetView, int>(nameof(FrameWidth), 192);

    public static readonly StyledProperty<int> FrameHeightProperty =
        AvaloniaProperty.Register<SpritesheetView, int>(nameof(FrameHeight), 208);

    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<SpritesheetView, int>(nameof(Columns), 8);

    public static readonly StyledProperty<int> FrameDurationMsProperty =
        AvaloniaProperty.Register<SpritesheetView, int>(nameof(FrameDurationMs), 100);

    public string Spritesheet
    {
        get => GetValue(SpritesheetProperty);
        set => SetValue(SpritesheetProperty, value);
    }

    public int CurrentRow
    {
        get => GetValue(CurrentRowProperty);
        set => SetValue(CurrentRowProperty, value);
    }

    public int FrameWidth
    {
        get => GetValue(FrameWidthProperty);
        set => SetValue(FrameWidthProperty, value);
    }

    public int FrameHeight
    {
        get => GetValue(FrameHeightProperty);
        set => SetValue(FrameHeightProperty, value);
    }

    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public int FrameDurationMs
    {
        get => GetValue(FrameDurationMsProperty);
        set => SetValue(FrameDurationMsProperty, value);
    }

    /// <summary>当前行实际帧数（跳过空白帧后的有效值）</summary>
    private int CurrentRowFrameCount
    {
        get
        {
            var row = CurrentRow;
            if (row >= 0 && row < _framesPerRow.Length && _framesPerRow[row] > 0)
                return _framesPerRow[row];
            return Columns;
        }
    }

    // ── 构造 ──

    public SpritesheetView()
    {
        ClipToBounds = true;
    }

    static SpritesheetView()
    {
        AffectsRender<SpritesheetView>(SpritesheetProperty, CurrentRowProperty);
    }

    // ── 属性变化 ──

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SpritesheetProperty)
            OnSpritesheetChanged();
        else if (change.Property == CurrentRowProperty)
        {
            _currentFrame = 0;
            InvalidateVisual();
        }
        else if (change.Property == FrameDurationMsProperty)
        {
            // 直接改定时器间隔，不反复停启
            if (_timer != null)
                _timer.Interval = TimeSpan.FromMilliseconds(FrameDurationMs);
        }
    }

    private void OnSpritesheetChanged()
    {
        StopAnimation();
        ClearFrames();
        _currentFrame = 0;
        _framesPerRow = [];

        var path = Spritesheet;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        try
        {
            using var src = SKBitmap.Decode(path);
            if (src == null) return;
            PrecacheFrames(src);
            InvalidateVisual();
            StartAnimation();
        }
        catch { }
    }

    /// <summary>
    /// 用 SkiaSharp 提取每帧并检测空白帧。
    /// 只有含非透明像素的帧才计入有效帧数，尾部的空白帧被跳过。
    /// </summary>
    private void PrecacheFrames(SKBitmap src)
    {
        var fw = FrameWidth;
        var fh = FrameHeight;
        var cols = Columns;
        var rows = 9;

        // 提取所有帧并检测内容
        var allFrameBitmaps = new Bitmap[rows * cols];
        var rowFrameCounts = new int[rows];

        for (var r = 0; r < rows; r++)
            rowFrameCounts[r] = cols; // 默认全部有效

        // 第一遍：提取 + 检测空白
        for (var r = 0; r < rows; r++)
        {
            var lastNonBlank = -1;
            for (var c = 0; c < cols; c++)
            {
                using var frameSk = new SKBitmap(fw, fh);
                src.ExtractSubset(frameSk, new SKRectI(c * fw, r * fh, (c + 1) * fw, (r + 1) * fh));

                // 像素级检测：是否有非透明内容
                if (HasContent(frameSk))
                    lastNonBlank = c;

                // 编码为 PNG 内存流 → Avalonia Bitmap
                using var ms = new MemoryStream();
                frameSk.Encode(ms, SKEncodedImageFormat.Png, 100);
                ms.Position = 0;
                allFrameBitmaps[r * cols + c] = new Bitmap(ms);
            }

            // 有效帧数 = 最后一个有内容的列 + 1（至少 1 帧）
            rowFrameCounts[r] = Math.Max(1, lastNonBlank + 1);
        }

        _frames = allFrameBitmaps;
        _framesPerRow = rowFrameCounts;
    }

    /// <summary>检查 SKBitmap 中是否有非透明像素</summary>
    private static bool HasContent(SKBitmap bmp)
    {
        // 快速检查：隔点采样，加速检测
        var step = Math.Max(1, Math.Min(bmp.Width, bmp.Height) / 8);
        for (var y = 0; y < bmp.Height; y += step)
        {
            for (var x = 0; x < bmp.Width; x += step)
            {
                var pixel = bmp.GetPixel(x, y);
                if (pixel.Alpha > 30) // 有可见内容
                    return true;
            }
        }
        return false;
    }

    private void ClearFrames()
    {
        foreach (var f in _frames)
            f?.Dispose();
        _frames = [];
        _framesPerRow = [];
    }

    // ── 定时器 ──

    private void StartAnimation()
    {
        StopAnimation();
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(FrameDurationMs),
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void StopAnimation()
    {
        if (_timer != null)
        {
            _timer.Tick -= OnTimerTick;
            _timer.Stop();
            _timer = null;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_frames.Length == 0) return;
        var count = CurrentRowFrameCount;
        _currentFrame = (_currentFrame + 1) % count;
        InvalidateVisual();
    }

    // ── 渲染 ──

    public override void Render(DrawingContext context)
    {
        var row = CurrentRow;
        var cols = Columns;
        var idx = row * cols + _currentFrame;
        if (idx < 0 || idx >= _frames.Length || _frames[idx] == null) return;
        context.DrawImage(_frames[idx], new Rect(Bounds.Size));
    }

    // ── 清理 ──

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopAnimation();
        ClearFrames();
    }
}
