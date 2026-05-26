using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MyPersonalTool.Views;

/// <summary>
/// PetdexDialog 风格的输入弹窗（无边框、半透明背景浮层）
/// </summary>
public static class InputDialog
{
    /// <summary>弹出输入框，返回用户输入的文本（null = 取消）</summary>
    public static async Task<string?> ShowAsync(Window owner, string title,
        string placeholder, string? initialValue = null)
    {
        var overlayColor = TryGetColor("BgOverlay", 0xCCF0ECE3);
        var borderColor = TryGetColor("BorderColor", 0xFFc4b89e);
        var fgColor = TryGetColor("TextPrimary", 0xFF794f27);
        var mutedColor = TryGetColor("TextMuted", 0xFF9f927d);
        var accentColor = TryGetColor("AccentPrimary", 0xFF19c8b9);

        var textBox = new TextBox
        {
            PlaceholderText = placeholder,
            Text = initialValue ?? "",
            MinWidth = 260,
            Height = 40,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6),
            FontSize = 13,
        };

        var okBtn = new Button
        {
            Content = "✅ 确定",
            Width = 90,
            Height = 34,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(accentColor),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize = 13,
            Padding = new Thickness(6, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        var cancelBtn = new Button
        {
            Content = "取消",
            Width = 90,
            Height = 34,
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(borderColor),
            Foreground = new SolidColorBrush(mutedColor),
            FontSize = 13,
            Padding = new Thickness(6, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        // ── PetdexDialog 风格弹窗 ──
        var popup = new Window
        {
            Width = 360,
            Height = 220,
            WindowDecorations = WindowDecorations.None,
            CanResize = false,
            ShowInTaskbar = false,
            Background = Brushes.Transparent,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = new Border
            {
                Background = new SolidColorBrush(overlayColor),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(8),
                BoxShadow = new BoxShadows(new BoxShadow
                {
                    OffsetX = 0, OffsetY = 4, Blur = 20,
                    Color = Color.Parse("#40000000"),
                }),
                Child = new Grid
                {
                    RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                    Margin = new Thickness(16),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 18,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(fgColor),
                            VerticalAlignment = VerticalAlignment.Center,
                        }.WithGridRow(0),
                        textBox.WithGridRow(1),
                        new StackPanel
                        {
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Children = { cancelBtn, okBtn },
                        }.WithGridRow(2),
                    },
                },
            },
        };

        string? result = null;

        okBtn.Click += (_, _) =>
        {
            result = textBox.Text;
            popup.Close();
        };
        cancelBtn.Click += (_, _) => popup.Close();

        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                result = textBox.Text;
                popup.Close();
            }
        };

        // 可拖拽
        popup.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(popup).Properties.IsLeftButtonPressed)
                popup.BeginMoveDrag(e);
        };

        DialogHelper.PositionAboveOwner(popup, owner, aboveOffset: 280, dialogWidth: 360);

        await popup.ShowDialog(owner);
        return result;
    }

    private static Color TryGetColor(string key, uint fallback)
    {
        if (Application.Current?.TryFindResource(key, out var value) == true && value is Color c)
            return c;
        return Color.Parse($"#{fallback:X8}");
    }

    /// <summary>附加属性扩展：设置 Grid.Row</summary>
    private static T WithGridRow<T>(this T element, int row) where T : Control
    {
        Grid.SetRow(element, row);
        return element;
    }
}
