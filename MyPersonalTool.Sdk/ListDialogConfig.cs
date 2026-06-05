namespace MyPersonalTool.Sdk;

/// <summary>列表弹窗配置 —— 插件通过此对象定义列表的数据、列、工具栏</summary>
public class ListDialogConfig
{
    /// <summary>弹窗标题</summary>
    public string Title { get; set; } = "";

    /// <summary>标题前的 Emoji 图标</summary>
    public string? Emoji { get; set; }

    /// <summary>静态数据源（行数据列表，每行为键值对字典）</summary>
    public List<Dictionary<string, string>>? Items { get; set; }

    /// <summary>异步数据源回调（与 Items 互斥，优先级更高）</summary>
    public Func<Task<List<Dictionary<string, string>>>>? DataSource { get; set; }

    /// <summary>列定义</summary>
    public List<ListColumn> Columns { get; set; } = new();

    /// <summary>工具栏按钮列表</summary>
    public List<ListToolbarAction> ToolbarActions { get; set; } = new();

    /// <summary>数据变更时触发，列表弹窗订阅后自动刷新</summary>
    public event EventHandler? DataChanged;

    /// <summary>通知列表弹窗数据已变更（可从任意线程调用）</summary>
    public void NotifyDataChanged() => DataChanged?.Invoke(this, EventArgs.Empty);
}
