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

    /// <summary>插件注册的文件拖放动作</summary>
    public List<FileActionConfig> FileActions { get; } = new();

    /// <summary>显示气泡文字的 UI 回调（由 ViewModel 设置）</summary>
    public Action<string, string>? OnShowThought { get; set; }

    /// <summary>输入框回调（由 App 设置，返回 null 表示取消）</summary>
    public Func<string, string, string?, Task<string?>>? OnShowInputDialog { get; set; }

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

    public void RegisterAction(string name, string emoji, string reaction, string description, string group = "")
    {
        PluginActions.Add(new PetActionConfig
        {
            Name = name,
            Emoji = emoji,
            Reaction = reaction,
            Description = description,
            Group = group,
        });
    }

    public void RegisterAction(string name, string emoji, string description, string group, Func<Task> callback)
    {
        PluginActions.Add(new PetActionConfig
        {
            Name = name,
            Emoji = emoji,
            Reaction = "🔍",
            Description = description,
            Group = group,
            ActionCallback = callback,
        });
    }

    public void RegisterFileAction(string name, string emoji, string description, Func<string[], Task> handler)
    {
        FileActions.Add(new FileActionConfig
        {
            Name = name,
            Emoji = emoji,
            Description = description,
            ActionCallback = handler,
        });
    }

    public void ShowThought(string title, string text)
    {
        OnShowThought?.Invoke(title, text);
    }

    public Task<string?> ShowInputDialog(string title, string placeholder, string? initialValue = null)
    {
        if (OnShowInputDialog != null)
            return OnShowInputDialog(title, placeholder, initialValue);
        return Task.FromResult<string?>(null);
    }

    public string? GetConfig(string key)
    {
        // 用统一前缀避免冲突
        return _config.GetPluginValue(key);
    }

    public void SetConfig(string key, string value)
    {
        _config.SetPluginValue(key, value);
    }

    public void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[Plugin] {message}");
        LogEmitted?.Invoke(message);
    }
}
