using MyPersonalTool.Core.Interfaces;
using MyPersonalTool.Core.Models;
using MyPersonalTool.Sdk;

namespace MyPersonalTool.Services;

/// <summary>插件宿主实现 —— 插件通过此对象与主程序交互</summary>
public class PluginHostImpl : IPluginHost
{
    private readonly IConfigService _config;

    /// <summary>插件注册的动作（右键菜单）</summary>
    public List<PetActionConfig> PluginActions { get; } = new();

    /// <summary>插件注册的文件动作（径向菜单）</summary>
    public List<FileActionConfig> FileActions { get; } = new();

    /// <summary>当前被激活的默认文件操作（激活后拖文件直接执行，不弹菜单）</summary>
    public FileActionConfig? ActivatedAction { get; set; }

    /// <summary>显示气泡文字的 UI 回调</summary>
    public Action<string, string>? OnShowThought { get; set; }

    /// <summary>输入框回调</summary>
    public Func<string, string, string?, Task<string?>>? OnShowInputDialog { get; set; }

    /// <summary>日志输出</summary>
    public event Action<string>? LogEmitted;

    public PluginHostImpl(IConfigService config)
    {
        _config = config;
    }

    public void RegisterAction(PluginAction action)
    {
        if (action.Target == ActionTarget.ContextMenu)
        {
            // 右键菜单
            PluginActions.Add(new PetActionConfig
            {
                Name = action.Name,
                Emoji = action.Emoji,
                Reaction = action.Emoji,
                Description = action.Description,
                Group = action.Group,
                ActionCallback = action.Callback,
            });
        }
        else
        {
            // 径向菜单（文件拖放）
            FileActions.Add(new FileActionConfig
            {
                Name = action.Name,
                Emoji = action.Emoji,
                Description = action.Description,
                FileExtensions = action.FileExtensions,
                AcceptType = action.AcceptType,
                ActionCallback = action.FileCallback,
            });
        }
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

    public string? GetConfig(string key) => _config.GetPluginValue(key);
    public void SetConfig(string key, string value) { _config.SetPluginValue(key, value); }

    public void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[Plugin] {message}");
        LogEmitted?.Invoke(message);
    }
}
