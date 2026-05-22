using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MyPersonalTool.Core.Models;
using MyPersonalTool.ViewModels;
using MyPersonalTool.Views;

namespace MyPersonalTool;

public partial class PetWindow : Window
{
    private PetViewModel? _vm;
    private DateTime _lastClick = DateTime.MinValue;

    public PetWindow()
    {
        InitializeComponent();

        // 定期重设 Topmost，防止被其他窗口覆盖后沉底
        var topmostTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3),
        };
        topmostTimer.Tick += (_, _) =>
        {
            if (IsVisible) Topmost = true;
        };
        topmostTimer.Start();
    }

    /// <summary>确保 ViewModel 就绪后绑定事件</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _vm = DataContext as PetViewModel;

        if (_vm != null)
        {
            // 直接同步动画速度（绕过 XAML 绑定，确保生效）
            SyncFrameDuration(_vm.AnimFrameDurationMs);

            // 监听 AnimFrameDurationMs 变化，直接设置到控件
            _vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(PetViewModel.AnimFrameDurationMs))
                    SyncFrameDuration(_vm.AnimFrameDurationMs);
                else if (args.PropertyName == nameof(PetViewModel.SpritesheetPath))
                    UpdateBubbleOffset();
            };

            Dispatcher.UIThread.Post(() => AdjustSize());
            Dispatcher.UIThread.Post(() => UpdateBubbleOffset(), DispatcherPriority.Loaded);
        }
    }

    private void SyncFrameDuration(double ms)
    {
        PetSprite.FrameDurationMs = (int)Math.Round(ms);
    }

    private void AdjustSize()
    {
        // 根据宠物表情调整窗口大小
        Width = 120;
        Height = 120;
    }

    /// <summary>根据宠物实际可见内容高度调整气泡偏移</summary>
    private void UpdateBubbleOffset()
    {
        // 等 SpritesheetView 完成帧提取
        Dispatcher.UIThread.Post(() =>
        {
            var topY = PetSprite.ContentTopY;
            if (topY < 0)
            {
                // 无有效检测 → 恢复默认偏移
                BubblePopup.VerticalOffset = -8;
                return;
            }

            // 精灵显示在 80×80，帧高度经缩放后，算出帧顶部空白占用了多少显示像素
            var scale = 80.0 / PetSprite.FrameHeight;
            var extraBlankInDisplay = topY * scale;

            // 把气泡下移「额外空白」的距离，但不超过 PetBody 顶部 (offset ≤ 0)
            BubblePopup.VerticalOffset = Math.Min(-8 + extraBlankInDisplay, 0);
        }, DispatcherPriority.Loaded);
    }

    // ── 拖动 ──

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    // ── 单击 / 双击 ──

    private void OnPetPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var now = DateTime.Now;
        if ((now - _lastClick).TotalMilliseconds < 350)
        {
            // 双击 — 打开宠物图鉴
            _lastClick = DateTime.MinValue;
            OpenPetdex();
        }
        else
        {
            _lastClick = now;
            // 延迟判断是否为单击（等待下一次点击超时）
            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(400);
                if ((DateTime.Now - _lastClick).TotalMilliseconds >= 380)
                {
                    _vm?.SingleClickCommand.Execute(null);
                }
            });
        }
    }

    // ── 右键菜单（宠物动作） ──

    /// <summary>获取当前主题色（带 fallback）</summary>
    private static SolidColorBrush ThemeBrush(string resourceKey, uint fallbackHex = 0xFFFFFFFF)
    {
        if (Application.Current?.TryFindResource(resourceKey, out var value) == true && value is Color c)
            return new SolidColorBrush(c);
        return new SolidColorBrush(Color.Parse($"#{fallbackHex:X8}"));
    }

    private void OnPetContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (_vm == null) return;

        var menu = new ContextMenu();

        // 宠物图鉴
        var dexItem = new MenuItem
        {
            Header = "📖 宠物图鉴",
            FontSize = 13,
            Foreground = ThemeBrush("TextPrimary"),
        };
        dexItem.Click += (_, _) => OpenPetdex();
        menu.Items.Add(dexItem);

        menu.Items.Add(new Separator());

        // 动作列表（按 Group 分组为二级菜单）
        var groups = _vm.Actions
            .GroupBy(a => string.IsNullOrEmpty(a.Group) ? "" : a.Group)
            .ToList();

        foreach (var group in groups)
        {
            if (string.IsNullOrEmpty(group.Key))
            {
                // 无分组 → 直接添加到根菜单
                foreach (var action in group)
                    menu.Items.Add(BuildMenuItem(action));
            }
            else
            {
                // 有分组 → 创建二级菜单
                var subMenu = new MenuItem
                {
                    Header = group.Key,
                    FontSize = 13,
                    Foreground = ThemeBrush("TextPrimary"),
                };
                foreach (var action in group)
                    subMenu.Items.Add(BuildMenuItem(action));
                menu.Items.Add(subMenu);
            }
        }

        menu.Items.Add(new Separator());

        // 打开主界面
        var settingsItem = new MenuItem
        {
            Header = "⚙️ 打开设置",
            FontSize = 13,
            Foreground = ThemeBrush("TextPrimary"),
        };
        settingsItem.Click += (_, _) =>
        {
            var settings = App.SettingsWindow;
            if (settings != null)
            {
                settings.Show();
                settings.WindowState = WindowState.Normal;
                settings.Activate();
            }
        };
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        // 关闭宠物（退出应用）
        var closeItem = new MenuItem
        {
            Header = "✕ 关闭宠物",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#E81123")),
        };
        closeItem.Click += (_, _) => Close();
        menu.Items.Add(closeItem);

        menu.Open(sender as Control);
        e.Handled = true;
    }

    // ── 宠物图鉴 ──

    private void OpenPetdex()
    {
        if (_vm == null) return;

        var dialog = new PetdexDialog();
        dialog.LoadPets(_vm);
        dialog.ShowDialog(this);
    }

    // ── 保存位置 ──

    // ── 文件拖放 ──

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (_vm == null) return;
        e.DragEffects = DragDropEffects.Copy;

        if (_vm.FileActions.Count > 0)
        {
            // 尝试提前读取文件扩展名，用于过滤
            var exts = TryGetExtensions(e);

            // 根据扩展名过滤可用动作
            var actions = _vm.FileActions
                .Where(a => a.MatchesExtension(exts))
                .ToList();

            if (actions.Count > 0)
            {
                var bodyBounds = PetBody.Bounds;
                var bodyCenterInWindow = new Point(
                    bodyBounds.X + bodyBounds.Width / 2,
                    bodyBounds.Y + bodyBounds.Height / 2);
                var screenPt = this.PointToScreen(bodyCenterInWindow);
                Views.FileRadialMenu.ShowDuringDrag(this, actions, screenPt);
            }
        }
    }

    /// <summary>从拖放数据中获取文件扩展名集合</summary>
    private static HashSet<string> TryGetExtensions(DragEventArgs e)
    {
        try
        {
            var items = e.DataTransfer?.TryGetFiles();
            if (items != null)
            {
                return items
                    .Select(i => Path.GetExtension(i.Path.LocalPath)?.ToLowerInvariant())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToHashSet()!;
            }
        }
        catch { }
        return [];
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        _vm?.ShowReaction("👋");
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        // 菜单已接管拖放，此处不再处理
    }

    /// <summary>格式化文件大小</summary>
    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB",
        };
    }

    /// <summary>构建右键菜单项</summary>
    private MenuItem BuildMenuItem(PetActionConfig action)
    {
        var item = new MenuItem
        {
            Header = $"{action.Emoji} {action.Name}",
            FontSize = 13,
            Foreground = ThemeBrush("TextPrimary"),
        };
        ToolTip.SetTip(item, action.Description);
        var captured = action;
        item.Click += (_, _) => _vm?.PerformActionCommand.Execute(captured);
        return item;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        var pos = Position;
        _vm?.SavePosition(pos.X, pos.Y);
        _vm?.Cleanup();
        base.OnClosing(e);
    }
}
