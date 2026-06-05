using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;

namespace MyPersonalTool.Controls;

/// <summary>
/// 精灵图表动画控件 —— 实时裁切帧，无需预缓存全部 Bitmap。
/// 只保留一张完整精灵图，渲染时 GPU 裁切，内存省 ~10MB。
/// </summary>
public class SpritesheetView : Control
{
    private Bitmap? _sheet;          // 完整精灵图（唯一大头内存）
    private int[] _framesPerRow = []; // 每行有效帧数
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

    /// <summary>第一帧中第一行非空白像素在帧坐标系中的 Y 坐标</summary>
    public int ContentTopY { get; private set; } = -1;

    public SpritesheetView()
    {
        ClipToBounds = true;
    }

    static SpritesheetView()
    {
        AffectsRender<SpritesheetView>(SpritesheetProperty, CurrentRowProperty);
    }

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
            if (_timer != null)
                _timer.Interval = TimeSpan.FromMilliseconds(FrameDurationMs);
        }
    }

    private void OnSpritesheetChanged()
    {
        StopAnimation();
        FreeSheet();
        _currentFrame = 0;
        _framesPerRow = [];

        var path = Spritesheet;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        try
        {
            using var src = SKBitmap.Decode(path);
            if (src == null) return;

            // 只保留一张完整精灵图
            using var ms = new MemoryStream();
            src.Encode(ms, SKEncodedImageFormat.Png, 100);
            ms.Position = 0;
            _sheet = new Bitmap(ms);

            // 检测每行有效帧数
            _framesPerRow = ComputeFramesPerRow(src, FrameWidth, FrameHeight);

            // 检测可见内容顶部 Y
            ContentTopY = FindContentTopY(src, 0, 0, FrameWidth, FrameHeight);

            InvalidateVisual();
            StartAnimation();
        }
        catch { }
    }

    /// <summary>计算每行有效帧数（跳过尾部空白帧）</summary>
    private static int[] ComputeFramesPerRow(SKBitmap src, int fw, int fh)
    {
        var result = new int[9];

        for (var r = 0; r < result.Length; r++)
        {
            var lastNonBlank = -1;
            for (var c = 0; c < 8; c++)
            {
                using var frame = new SKBitmap(fw, fh);
                src.ExtractSubset(frame, new SKRectI(c * fw, r * fh, (c + 1) * fw, (r + 1) * fh));
                if (HasContent(frame))
                    lastNonBlank = c;
            }
            result[r] = Math.Max(1, lastNonBlank + 1);
        }
        return result;
    }

    private static int FindContentTopY(SKBitmap src, int col, int row, int fw, int fh)
    {
        using var frame = new SKBitmap(fw, fh);
        src.ExtractSubset(frame, new SKRectI(col * fw, row * fh, (col + 1) * fw, (row + 1) * fh));

        var step = Math.Max(1, fh / 20);
        for (var y = 0; y < fh; y += step)
            for (var x = 0; x < fw; x += Math.Max(1, fw / 10))
                if (frame.GetPixel(x, y).Alpha > 30)
                    return y;
        return -1;
    }

    private static bool HasContent(SKBitmap bmp)
    {
        var step = Math.Max(1, Math.Min(bmp.Width, bmp.Height) / 8);
        for (var y = 0; y < bmp.Height; y += step)
            for (var x = 0; x < bmp.Width; x += step)
                if (bmp.GetPixel(x, y).Alpha > 30)
                    return true;
        return false;
    }

    private void FreeSheet()
    {
        _sheet?.Dispose();
        _sheet = null;
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
        if (_sheet == null) return;
        var count = CurrentRowFrameCount;
        _currentFrame = (_currentFrame + 1) % count;
        InvalidateVisual();
    }

    // ── 渲染（实时裁切，无预缓存帧）──

    public override void Render(DrawingContext context)
    {
        if (_sheet == null) return;

        var fw = FrameWidth;
        var fh = FrameHeight;
        var sx = _currentFrame * fw;
        var sy = CurrentRow * fh;

        context.DrawImage(_sheet,
            new Rect(sx, sy, fw, fh),
            new Rect(Bounds.Size));
    }

    // ── 清理 ──

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopAnimation();
        FreeSheet();
    }
}
