namespace MyPersonalTool.Sdk;

/// <summary>插件宿主接口 —— 插件通过此接口与主程序交互</summary>
public interface IPluginHost
{
    /// <summary>注册一个动作（右键菜单 / 径向菜单均可）</summary>
    void RegisterAction(PluginAction action);

    /// <summary>在宠物气泡中显示文字（标题 + 内容）</summary>
    void ShowThought(string title, string text);

    /// <summary>弹出输入框让用户输入文本（返回 null 表示取消）</summary>
    Task<string?> ShowInputDialog(string title, string placeholder, string? initialValue = null);

    /// <summary>获取/设置插件配置值（保存在主程序配置中）</summary>
    string? GetConfig(string key);
    void SetConfig(string key, string value);

    /// <summary>输出日志</summary>
    void Log(string message);
}
