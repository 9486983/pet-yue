using MyPersonalTool.Core.Interfaces;
using MyPersonalTool.Core.Models;
using MyPersonalTool.Sdk;

namespace MyPersonalTool.Services;

/// <summary>插件宿主实现 —— 插件通过此对象与主程序交互</summary>
public class PluginHostImpl : IPluginHost
{
    private readonly IConfigService _config;

    /// <summary>插件注册的动作列表</summary>
    public List<PetActionConfig> PluginActions { get; } = new();

    /// <summary>日志输出</summary>
    public event Action<string>? LogEmitted;

    public PluginHostImpl(IConfigService config)
    {
        _config = config;
    }

    public void RegisterReaction(string trigger, string emoji)
    {
        // 暂存，供 ViewModel 使用
    }

    public void RegisterAction(string name, string emoji, string reaction, string description)
    {
        PluginActions.Add(new PetActionConfig
        {
            Name = name,
            Emoji = emoji,
            Reaction = reaction,
            Description = description,
        });
    }

    public void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[Plugin] {message}");
        LogEmitted?.Invoke(message);
    }
}
