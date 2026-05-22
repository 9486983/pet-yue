namespace MyPersonalTool.Core.Models;

/// <summary>文件拖放动作配置（径向菜单选项）</summary>
public class FileActionConfig
{
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>执行回调，参数为拖放的文件路径列表</summary>
    public Func<string[], Task>? ActionCallback { get; set; }
}
