namespace MyPersonalTool.Sdk;

/// <summary>插件宿主接口 —— 插件通过此接口与主程序交互</summary>
public interface IPluginHost
{
    /// <summary>注册一个动作（右键菜单 / 径向菜单均可）</summary>
    void RegisterAction(PluginAction action);

    /// <summary>在宠物气泡中显示文字（标题 + 内容）</summary>
    void ShowThought(string title, string text);

    /// <summary>显示宠物反应 emoji（短暂弹出在宠物上方）</summary>
    void ShowReaction(string emoji, PetAnimation animation = PetAnimation.Jump);

    /// <summary>开始持续动画（长时间任务时展示状态，如"思考中"）</summary>
    void StartAnimation(PetAnimation animation);

    /// <summary>结束持续动画，恢复待机</summary>
    void StopAnimation();

    /// <summary>在指定动画状态下执行异步委托，执行完毕后自动恢复待机</summary>
    Task RunWithAnimation(PetAnimation animation, Func<CancellationToken, Task> action);

    /// <summary>在多个动画间轮换执行异步委托（适用于长时间任务），支持取消</summary>
    Task RunWithAnimation(IEnumerable<PetAnimation> animations, Func<CancellationToken, Task> action);

    /// <summary>取消当前正在执行的 RunWithAnimation 任务</summary>
    void CancelCurrentTask();

    /// <summary>弹出输入框让用户输入文本（返回 null 表示取消）</summary>
    Task<string?> ShowInputDialog(string title, string placeholder, string? initialValue = null);

    /// <summary>获取/设置插件配置值（保存在主程序配置中）</summary>
    string? GetConfig(string key);
    void SetConfig(string key, string value);

    /// <summary>输出日志</summary>
    void Log(string message);
}
