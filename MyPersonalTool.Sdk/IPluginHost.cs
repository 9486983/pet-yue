namespace MyPersonalTool.Sdk;

/// <summary>插件宿主接口 —— 插件通过此接口与主程序交互</summary>
public interface IPluginHost
{
    /// <summary>注册一个宠物反应（在交互时触发）</summary>
    void RegisterReaction(string trigger, string emoji);

    /// <summary>注册一个右键菜单动作</summary>
    void RegisterAction(string name, string emoji, string reaction, string description);

    /// <summary>输出日志</summary>
    void Log(string message);
}
