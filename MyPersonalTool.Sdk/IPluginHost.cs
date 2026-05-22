namespace MyPersonalTool.Sdk;

/// <summary>插件宿主接口 —— 插件通过此接口与主程序交互</summary>
public interface IPluginHost
{
    /// <summary>注册一个宠物反应（在交互时触发）</summary>
    void RegisterReaction(string trigger, string emoji);

    /// <summary>注册一个右键菜单动作</summary>
    void RegisterAction(string name, string emoji, string reaction, string description, string group = "");

    /// <summary>注册一个带异步回调的右键菜单动作（用于 API 查询等耗时操作）</summary>
    void RegisterAction(string name, string emoji, string description, string group, Func<Task> callback);

    /// <summary>注册一个文件拖放动作（在径向菜单中显示，拖文件到宠物时弹出）</summary>
    void RegisterFileAction(string name, string emoji, string description, Func<string[], Task> handler);

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
