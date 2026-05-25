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

    /// <summary>会话生命周期事件</summary>
    public Action<ISession>? OnSessionStarted { get; set; }
    public Action? OnSessionEnded { get; set; }

    /// <summary>当前活跃会话（无则为 null）</summary>
    public ISession? CurrentSession => _currentSession;

    /// <summary>插件注册的配置定义</summary>
    public List<PluginConfigSection> PluginConfigs { get; } = new();

    /// <summary>配置变更事件</summary>
    public event EventHandler<string>? ConfigValueChanged;

    /// <summary>打开插件配置弹窗的回调（由 UI 层设置）</summary>
    public Func<PluginConfigSection, Task>? OnShowPluginConfig { get; set; }

    /// <summary>显示气泡文字的 UI 回调</summary>
    public Action<string, string>? OnShowThought { get; set; }

    /// <summary>显示反应 emoji 的回调</summary>
    public Action<string>? OnShowReaction { get; set; }

    /// <summary>动画控制回调</summary>
    public Action<PetAnimation>? OnStartAnimation { get; set; }
    public Action? OnStopAnimation { get; set; }

    /// <summary>任务状态回调</summary>
    public Action<bool>? OnTaskRunningChanged { get; set; }

    private CancellationTokenSource? _currentCts;
    private SessionImpl? _currentSession;

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

    // ── 会话管理 ──

    public ISession StartSession(string title)
    {
        // 如有现有会话，先结束
        _currentSession?.Cancel();

        var session = new SessionImpl(title);
        session.OnEndRequested = OnSessionEnd;

        _currentSession = session;

        // 自动激活同名的文件动作
        var match = FileActions.FirstOrDefault(a =>
            a.Name.Equals(title, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            ActivatedAction = match;

        OnSessionStarted?.Invoke(session);
        // 不设置 IsTaskRunning —— 进度环由 RunWithAnimation 控制
        return session;
    }

    private void OnSessionEnd()
    {
        ActivatedAction = null;
        _currentSession = null;
        // 不重置 IsTaskRunning —— 由 RunWithAnimation 的 finally 块负责
        OnSessionEnded?.Invoke();
    }

    // ── 插件配置 ──

    void IPluginHost.RegisterConfig(PluginConfigSection config, string? pluginName)
    {
        // 替换同名的已有配置（插件热加载场景）
        PluginConfigs.RemoveAll(c => c.Title == config.Title);
        PluginConfigs.Add(config);
    }

    void IPluginHost.ShowConfigDialog(string sectionTitle)
    {
        var section = PluginConfigs.FirstOrDefault(s => s.Title == sectionTitle);
        if (section != null && OnShowPluginConfig != null)
            _ = OnShowPluginConfig(section);
    }

    /// <summary>
    /// 批量保存插件配置值并通知插件。
    /// 由 PluginConfigDialog 保存后调用。
    /// </summary>
    public void SavePluginConfig(Dictionary<string, string?> values)
    {
        _config.SetPluginValuesBatch(values);
        foreach (var key in values.Keys)
            ConfigValueChanged?.Invoke(this, key);
    }

    public void ShowThought(string title, string text)
    {
        OnShowThought?.Invoke(title, text);
    }

    public void ShowReaction(string emoji, PetAnimation animation = PetAnimation.Jump)
    {
        OnStartAnimation?.Invoke(animation);
        OnShowReaction?.Invoke(emoji);
    }

    public void StartAnimation(PetAnimation animation)
    {
        OnStartAnimation?.Invoke(animation);
    }

    public void StopAnimation()
    {
        OnStopAnimation?.Invoke();
    }

    public async Task RunWithAnimation(PetAnimation animation, Func<CancellationToken, Task> action)
    {
        await RunWithAnimation(new[] { animation }, action);
    }

    public async Task RunWithAnimation(IEnumerable<PetAnimation> animations, Func<CancellationToken, Task> action)
    {
        var animList = animations.ToList();
        if (animList.Count == 0) return;

        _currentCts = new CancellationTokenSource();
        var token = _currentCts.Token;
        OnTaskRunningChanged?.Invoke(true);

        // 动画轮换循环
        _ = Task.Run(async () =>
        {
            var idx = 0;
            while (!token.IsCancellationRequested)
            {
                OnStartAnimation?.Invoke(animList[idx % animList.Count]);
                idx++;
                try { await Task.Delay(2000, token); }
                catch (TaskCanceledException) { break; }
            }
        }, token);

        try { await action(token); }
        finally
        {
            _currentCts?.Cancel();
            _currentCts?.Dispose();
            _currentCts = null;
            OnStopAnimation?.Invoke();
            OnTaskRunningChanged?.Invoke(false);
        }
    }

    public void CancelCurrentTask()
    {
        _currentCts?.Cancel();
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
