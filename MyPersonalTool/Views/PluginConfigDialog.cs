using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MyPersonalTool.Sdk;

namespace MyPersonalTool.Views;

/// <summary>
/// 气泡样式的插件配置弹窗 —— 每个插件拥有独立的配置弹窗。
/// 完全遵循消息气泡的视觉风格：半透明浮层、圆角、主题色。
/// </summary>
public static class PluginConfigDialog
{
    /// <summary>弹出插件配置窗口，用户保存后值已持久化</summary>
    public static async Task ShowAsync(Window owner, PluginConfigSection section,
        Func<string, string?> getValue, Action<Dictionary<string, string?>> onSave)
    {
        var overlayColor = TryGetColor("BgOverlay", 0xCCF0ECE3);
        var borderColor = TryGetColor("BorderColor", 0xFFc4b89e);
        var fgColor = TryGetColor("TextPrimary", 0xFF794f27);
        var mutedColor = TryGetColor("TextMuted", 0xFF9f927d);
        var accentColor = TryGetColor("AccentPrimary", 0xFF19c8b9);
        var bgPage = TryGetColor("BgPage", 0xFFF5F0E8);

        // ── 动态生成字段控件 ──
        var fieldControls = new List<(PluginConfigField Field, Control Control)>();
        var stack = new StackPanel { Spacing = 10, Margin = new Thickness(0, 8, 0, 0) };

        foreach (var field in section.Fields)
        {
            var currentValue = getValue(field.Key) ?? field.DefaultValue ?? "";

            // 字段标签
            var label = new TextBlock
            {
                Text = field.Label,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(fgColor),
                VerticalAlignment = VerticalAlignment.Center,
            };

            Control? input = null;
            string? editedValue = null;

            // 值控件统一右侧对齐
            void AlignRight(Control c)
            {
                c.HorizontalAlignment = HorizontalAlignment.Right;
            }

            switch (field.Type)
            {
                case PluginConfigFieldType.Password:
                {
                    var tb = new TextBox
                    {
                        Text = currentValue,
                        PasswordChar = '•',
                        PlaceholderText = field.Placeholder ?? "",
                        MinWidth = 200,
                        Height = 34,
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(10, 4),
                        FontSize = 13,
                        Foreground = new SolidColorBrush(fgColor),
                        Background = new SolidColorBrush(bgPage),
                        BorderBrush = new SolidColorBrush(borderColor),
                    };
                    input = tb;
                    AlignRight(tb);
                    editedValue = currentValue;
                    tb.TextChanged += (_, _) => editedValue = tb.Text;
                    break;
                }

                case PluginConfigFieldType.Number:
                {
                    var tb = new TextBox
                    {
                        Text = currentValue,
                        PlaceholderText = field.Placeholder ?? "",
                        MinWidth = 80,
                        Width = 100,
                        Height = 34,
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(10, 4),
                        FontSize = 13,
                        Foreground = new SolidColorBrush(fgColor),
                        Background = new SolidColorBrush(bgPage),
                        BorderBrush = new SolidColorBrush(borderColor),
                    };
                    input = tb;
                    AlignRight(tb);
                    editedValue = currentValue;
                    tb.TextChanged += (_, _) => editedValue = tb.Text;
                    break;
                }

                case PluginConfigFieldType.Boolean:
                {
                    var ts = new ToggleSwitch
                    {
                        IsChecked = string.Equals(currentValue, "true", StringComparison.OrdinalIgnoreCase),
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                    };
                    input = ts;
                    AlignRight(ts);
                    editedValue = currentValue;
                    ts.IsCheckedChanged += (_, _) => editedValue = ts.IsChecked == true ? "true" : "false";
                    break;
                }

                case PluginConfigFieldType.Dropdown when field.Options?.Count > 0:
                {
                    var cb = new ComboBox
                    {
                        ItemsSource = field.Options,
                        SelectedValue = currentValue,
                        SelectedValueBinding = new global::Avalonia.Data.Binding("Value"),
                        DisplayMemberBinding = new global::Avalonia.Data.Binding("Label"),
                        MinWidth = 160,
                        Height = 34,
                        CornerRadius = new CornerRadius(8),
                        FontSize = 13,
                        Foreground = new SolidColorBrush(fgColor),
                        Background = new SolidColorBrush(bgPage),
                        BorderBrush = new SolidColorBrush(borderColor),
                    };
                    input = cb;
                    AlignRight(cb);
                    editedValue = currentValue;
                    cb.SelectedValue = currentValue;  // set again after binding
                    cb.SelectionChanged += (_, _) =>
                    {
                        if (cb.SelectedItem is PluginConfigOption opt)
                            editedValue = opt.Value;
                    };
                    break;
                }

                default: // String
                {
                    var tb = new TextBox
                    {
                        Text = currentValue,
                        PlaceholderText = field.Placeholder ?? "",
                        MinWidth = 200,
                        Height = 34,
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(10, 4),
                        FontSize = 13,
                        Foreground = new SolidColorBrush(fgColor),
                        Background = new SolidColorBrush(bgPage),
                        BorderBrush = new SolidColorBrush(borderColor),
                    };
                    input = tb;
                    AlignRight(tb);
                    editedValue = currentValue;
                    tb.TextChanged += (_, _) => editedValue = tb.Text;
                    break;
                }
            }

            if (input == null) continue;

            fieldControls.Add((field, input));

            // 单字段容器
            var fieldGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                Children =
                {
                    label,
                    input,
                },
            };
            Grid.SetColumn(input, 1);

            // 说明文字
            if (!string.IsNullOrEmpty(field.Description))
            {
                var desc = new TextBlock
                {
                    Text = field.Description,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(mutedColor),
                    Margin = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                };
                Grid.SetRow(desc, 1);
                Grid.SetColumnSpan(desc, 2);
                fieldGrid.Children.Add(desc);
            }

            stack.Children.Add(fieldGrid);
        }

        // ── 按钮 ──
        var saveBtn = new Button
        {
            Content = "💾 保存",
            Width = 100,
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
            Width = 100,
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

        // ── 构建窗口 ──
        var scrollViewer = new ScrollViewer
        {
            Content = stack,
            MaxHeight = 400,
        };

        var titleBlock = new TextBlock
        {
            Text = $"{section.Emoji ?? "⚙️"} {section.Title}",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(fgColor),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var popup = new Window
        {
            Width = 380,
            Height = 0, // auto-size via content
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
                    RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                    Margin = new Thickness(16),
                    Children =
                    {
                        titleBlock.WithGridRow(0),
                        scrollViewer.WithGridRow(1),
                        new StackPanel
                        {
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Margin = new Thickness(0, 8, 0, 0),
                            Children = { cancelBtn, saveBtn },
                        }.WithGridRow(2),
                    },
                },
            },
        };

        // ── 事件 ──
        saveBtn.Click += (_, _) =>
        {
            var changed = new Dictionary<string, string?>();
            foreach (var (field, ctrl) in fieldControls)
            {
                var newVal = ctrl switch
                {
                    TextBox tb => tb.Text,
                    ToggleSwitch ts => ts.IsChecked == true ? "true" : "false",
                    ComboBox cb => (cb.SelectedItem as PluginConfigOption)?.Value,
                    _ => null,
                };
                var oldVal = getValue(field.Key) ?? field.DefaultValue ?? "";
                if (newVal != oldVal)
                    changed[field.Key] = newVal;
            }
            if (changed.Count > 0)
                onSave(changed);
            popup.Close();
        };

        cancelBtn.Click += (_, _) => popup.Close();

        popup.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) popup.Close();
            if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
                saveBtn.RaiseEvent(new KeyEventArgs { Key = Key.Enter });
        };

        await popup.ShowDialog(owner);
    }

    private static Color TryGetColor(string key, uint fallback)
    {
        if (Application.Current?.TryFindResource(key, out var value) == true && value is Color c)
            return c;
        return Color.Parse($"#{fallback:X8}");
    }

    /// <summary>附加属性扩展：设置 Grid.Row / Grid.Column</summary>
    private static T WithGridRow<T>(this T element, int row) where T : Control
    {
        Grid.SetRow(element, row);
        return element;
    }

    private static (T Control, int Row, int Col) WithGrid<T>(this T element, int row, int col) where T : Control
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, col);
        return (element, row, col);
    }
}
