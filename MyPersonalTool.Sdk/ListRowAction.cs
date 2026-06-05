namespace MyPersonalTool.Sdk;

/// <summary>列表行操作按钮 —— 显示在 Action 列的每一行中</summary>
public class ListRowAction
{
    /// <summary>按钮文字</summary>
    public string Label { get; set; } = "";

    /// <summary>按钮 Emoji 图标</summary>
    public string Emoji { get; set; } = "";

    /// <summary>悬停提示</summary>
    public string? Tooltip { get; set; }

    /// <summary>点击回调，参数为当前行数据</summary>
    public Func<Dictionary<string, string>, Task>? Callback { get; set; }
}
