using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MyPersonalTool.Views;

/// <summary>
/// 气泡样式的确认弹窗 —— 遵循 InputDialog 模式。
/// 返回 true=确认，false=取消。
/// </summary>
public static class ConfirmDialog
{
    public static async Task<bool> ShowAsync(Window owner, string title, string text)
    {
        var overlayColor = TryGetColor("BgOverlay", 0xCCF0ECE3);
        var borderColor = TryGetColor("BorderColor", 0xFFc4b89e);
        var fgColor = TryGetColor("TextPrimary", 0xFF794f27);
        var mutedColor = TryGetColor("TextMuted", 0xFF9f927d);
        var accentColor = TryGetColor("AccentPrimary", 0xFF19c8b9);

        var yesBtn = new Button
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

        var noBtn = new Button
        {
            Content = "❌ 取消",
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

        var popup = new Window
        {
            Width = 360,
            Height = 0,
            SizeToContent = SizeToContent.Height,
            WindowDecorations = WindowDecorations.None,
            CanResize = false,
            ShowInTaskbar = false,
            Background = Brushes.Transparent,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
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
                    RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
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
                            TextWrapping = TextWrapping.Wrap,
                        }.WithGridRow(0),
                        new TextBlock
                        {
                            Text = text,
                            FontSize = 13,
                            Foreground = new SolidColorBrush(fgColor),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 8, 0, 0),
                        }.WithGridRow(1),
                        new StackPanel
                        {
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Margin = new Thickness(0, 12, 0, 0),
                            Children = { noBtn, yesBtn },
                        }.WithGridRow(2),
                    },
                },
            },
        };

        var result = false;

        yesBtn.Click += (_, _) => { result = true; popup.Close(); };
        noBtn.Click += (_, _) => popup.Close();

        popup.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) popup.Close();
            if (e.Key == Key.Enter) { result = true; popup.Close(); }
        };

        await popup.ShowDialog(owner);
        return result;
    }

    private static Color TryGetColor(string key, uint fallback)
    {
        if (Application.Current?.TryFindResource(key, out var value) == true && value is Color c)
            return c;
        return Color.Parse($"#{fallback:X8}");
    }

    private static T WithGridRow<T>(this T element, int row) where T : Control
    {
        Grid.SetRow(element, row);
        return element;
    }
}
