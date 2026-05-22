using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MyPersonalTool.Views;

/// <summary>
/// 简易输入对话框（供插件调用）
/// </summary>
public static class InputDialog
{
    /// <summary>弹出输入框，返回用户输入的文本（null = 取消）</summary>
    public static async Task<string?> ShowAsync(Window owner, string title,
        string placeholder, string? initialValue = null)
    {
        var bgColor = TryGetColor("BgPage", 0xFFF0ECE3);
        var fgColor = TryGetColor("TextPrimary", 0xFF794f27);

        var textBox = new TextBox
        {
            PlaceholderText = placeholder,
            Text = initialValue ?? "",
            MinWidth = 280,
            Height = 36,
            Margin = new Thickness(0, 8, 0, 0),
        };

        var okBtn = new Button
        {
            Content = "✅ 确定",
            Width = 100,
            Height = 34,
            Margin = new Thickness(0, 0, 8, 0),
        };
        var cancelBtn = new Button
        {
            Content = "取消",
            Width = 100,
            Height = 34,
        };

        var dialog = new Window
        {
            Title = title,
            Width = 380,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(bgColor),
            Content = new Border
            {
                Padding = new Thickness(20),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 16,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(fgColor),
                        },
                        textBox,
                        new StackPanel
                        {
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Orientation = Orientation.Horizontal,
                            Children = { okBtn, cancelBtn },
                        },
                    },
                },
            },
        };

        string? result = null;

        okBtn.Click += (_, _) =>
        {
            result = textBox.Text;
            dialog.Close();
        };
        cancelBtn.Click += (_, _) => dialog.Close();

        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                result = textBox.Text;
                dialog.Close();
            }
        };

        await dialog.ShowDialog(owner);
        return result;
    }

    private static Color TryGetColor(string key, uint fallback)
    {
        if (Application.Current?.TryFindResource(key, out var value) == true && value is Color c)
            return c;
        return Color.Parse($"#{fallback:X8}");
    }
}
