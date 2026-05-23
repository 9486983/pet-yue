using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPersonalTool.Core.Interfaces;
using MyPersonalTool.Core.Models;
using MyPersonalTool.Services;

namespace MyPersonalTool.ViewModels;

public partial class PetViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly IPetdexService _petdexService;
    private readonly IDispatcherService _dispatcher;
    private readonly IActivityMonitor? _activityMonitor;
    private readonly HealthReminderService? _healthService;
    private readonly Random _random = new();
    private CancellationTokenSource? _activityCts;

    // ── 精灵图属性 ──

    [ObservableProperty]
    private string _petName = "";

    [ObservableProperty]
    private string _spritesheetPath = "";

    [ObservableProperty]
    private int _animFrameWidth = 192;

    [ObservableProperty]
    private int _animFrameHeight = 208;

    [ObservableProperty]
    private int _animColumns = 8;

    [ObservableProperty]
    private int _animRows = 9;

    [ObservableProperty]
    private double _animFrameDurationMs = 100.0;

    [ObservableProperty]
    private int _animCurrentRow;

    // ── 反应气泡 ──

    [ObservableProperty]
    private string _currentReaction = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyBubbleVisible))]
    private bool _isReacting;

    // ── Agent 对话监测气泡 ──

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyBubbleVisible))]
    private bool _isShowingThought;

    [ObservableProperty]
    private string _thoughtText = "";

    [ObservableProperty]
    private string _thoughtAssistant = "";

    /// <summary>任一气泡可见（统一外部 Popup 绑定）</summary>
    public bool IsAnyBubbleVisible => IsReacting || IsShowingThought;

    /// <summary>当前宠物定义</summary>
    public PetDefinition? CurrentPet { get; private set; }

    /// <summary>已安装的 Petdex 宠物列表</summary>
    public List<PetDefinition> PetdexPets { get; private set; } = [];

    /// <summary>默认交互动作（右键菜单用）</summary>
    public List<PetActionConfig> Actions { get; } =
    [
        new() { Name = "喂食", Emoji = "🍔", Reaction = "😋", Description = "喂好吃的" },
        new() { Name = "玩耍", Emoji = "🎮", Reaction = "🎉", Description = "一起玩" },
        new() { Name = "摸摸", Emoji = "❤️", Reaction = "🥰", Description = "轻轻抚摸" },
    ];

    /// <summary>文件拖放动作（径向菜单用）</summary>
    public List<FileActionConfig> FileActions => _fileActions;
    private readonly List<FileActionConfig> _fileActions = [];

    /// <summary>是否处于激活模式（拖文件直接执行默认操作）</summary>
    public bool IsActivated => ActivatedFileAction != null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActivated))]
    private FileActionConfig? _activatedFileAction;

    /// <summary>剪贴板写入回调（由 UI 层设置）</summary>
    public Action<string>? ClipboardSetText { get; set; }

    private void LoadBuiltinFileActions()
    {
        _fileActions.Add(new FileActionConfig
        {
            Name = "查看信息",
            Emoji = "📄",
            Description = "查看文件名称、大小、修改时间",
            ActionCallback = async (files) =>
            {
                var sb = new System.Text.StringBuilder();
                foreach (var f in files.Take(5))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        var size = FormatFileSize(fi.Length);
                        var time = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                        sb.AppendLine($"📄 {fi.Name}");
                        sb.AppendLine($"  大小 {size} · {time}");
                    }
                    catch { }
                }
                if (files.Length > 5)
                    sb.AppendLine($"  …以及 {files.Length - 5} 个文件");
                if (sb.Length > 0)
                    ShowFileDropInfo("📁 文件信息", sb.ToString().TrimEnd());
                ShowReaction("📂");
            },
        });
        _fileActions.Add(new FileActionConfig
        {
            Name = "打开位置",
            Emoji = "📂",
            Description = "在资源管理器中打开文件所在文件夹",
            ActionCallback = async (files) =>
            {
                if (files.Length > 0)
                {
                    var dir = Path.GetDirectoryName(files[0]);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{files[0]}\"");
                        ShowReaction("📂");
                    }
                }
            },
        });
        _fileActions.Add(new FileActionConfig
        {
            Name = "复制路径",
            Emoji = "📋",
            Description = "复制文件完整路径到剪贴板",
            ActionCallback = async (files) =>
            {
                if (files.Length > 0)
                {
                    ClipboardSetText?.Invoke(files[0]);
                    ShowReaction("📋");
                }
            },
        });
    }

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB",
    };

    public PetViewModel(IConfigService configService, IDispatcherService dispatcher,
        IPetdexService petdexService, IActivityMonitor? activityMonitor = null,
        HealthReminderService? healthService = null,
        List<PetActionConfig>? pluginActions = null,
        List<FileActionConfig>? fileActions = null)
    {
        _configService = configService;
        _dispatcher = dispatcher;
        _petdexService = petdexService;
        _activityMonitor = activityMonitor;
        _healthService = healthService;

        // 加载内置文件拖放动作
        LoadBuiltinFileActions();
        if (fileActions != null && fileActions.Count > 0)
            _fileActions.AddRange(fileActions);

        // 扫描已安装宠物
        ReloadPetdexPets();

        // 加载上次使用的宠物
        var cfg = configService.Config;
        ApplyPetById(cfg.CurrentPetId);
        _petName = string.IsNullOrEmpty(cfg.PetName) && CurrentPet != null
            ? CurrentPet.Name
            : cfg.PetName;

        // 合并插件注册的动作
        if (pluginActions != null && pluginActions.Count > 0)
        {
            var merged = new List<PetActionConfig>(Actions);
            merged.AddRange(pluginActions);
            Actions = merged;
        }

        // 恢复上次激活的默认操作
        RestoreActivatedAction();

        // 启动 Agent 监测
        if (activityMonitor != null)
            StartActivityMonitoring();

        // 启动健康提醒
        if (healthService != null)
        {
            healthService.ReminderTriggered += OnHealthReminder;
            healthService.Start();
        }

        // 监听配置保存事件（动画速度立即生效）
        PetEvents.ConfigSaved += OnConfigSaved;
    }

    private void OnHealthReminder(string type, string message)
    {
        ThoughtText = message;
        ThoughtAssistant = type switch
        {
            "sit" => "🧘 久坐提醒",
            "eye" => "👀 用眼提醒",
            "drink" => "💧 喝水提醒",
            _ => "⏰ 提醒",
        };
        IsShowingThought = true;

        // 6 秒后自动隐藏
        Task.Delay(6000).ContinueWith(_ =>
            _dispatcher.Post(() => IsShowingThought = false));
    }

    private void OnConfigSaved()
    {
        AnimFrameDurationMs = _configService.Config.AnimFrameDurationMs;
    }

    /// <summary>启动 Agent 事件后台轮询</summary>
    private void StartActivityMonitoring()
    {
        _activityCts = new CancellationTokenSource();
        var ct = _activityCts.Token;

        Task.Run(async () =>
        {
            _activityMonitor!.Start();

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(3000, ct);

                    // 随机切换动作（~10% 概率，自动适配当前宠物的行数）
                    if (_random.Next(10) == 0 && AnimRows > 1)
                    {
                        var row = _random.Next(1, AnimRows); // 跳过 idle(0)
                        _dispatcher.Post(() => AnimCurrentRow = row);
                        await Task.Delay(2000, ct);
                        _dispatcher.Post(() =>
                        {
                            if (AnimCurrentRow == row)
                                AnimCurrentRow = 0;
                        });
                    }

                    // 读取新事件 → 显示气泡
                    var events = _activityMonitor.GetNewEvents();
                    foreach (var ev in events)
                    {
                        if (ev.Type == "response" && !string.IsNullOrEmpty(ev.Content))
                        {
                            var preview = ev.Content.Length > 120
                                ? ev.Content[..120] + "…"
                                : ev.Content;
                            _dispatcher.Post(() =>
                            {
                                ThoughtText = preview;
                                ThoughtAssistant = ev.Assistant;
                                IsShowingThought = true;
                            });
                            await Task.Delay(6000, ct);
                            _dispatcher.Post(() => IsShowingThought = false);
                        }
                    }
                }
                catch (TaskCanceledException) { break; }
                catch { }
            }
        }, ct);
    }

    /// <summary>重新扫描 ~/.codex/pets/ + ~/.petdex/pets/</summary>
    [RelayCommand]
    public void ReloadPetdexPets()
    {
        PetdexPets = _petdexService.GetInstalledPetIds()
            .Select(id => _petdexService.ToPetDefinition(id))
            .Where(p => p != null)
            .Cast<PetDefinition>()
            .ToList();
        OnPropertyChanged(nameof(PetdexPets));
    }

    /// <summary>按 petdex:xxx 格式 ID 切换到宠物</summary>
    public void ApplyPetById(string petId)
    {
        if (!petId.StartsWith("petdex:"))
        {
            // 无有效 ID 时选第一个已安装宠物
            var first = PetdexPets.FirstOrDefault();
            if (first != null) { ApplyPetDefinition(first); }
            return;
        }

        var slug = petId["petdex:".Length..];
        var def = _petdexService.ToPetDefinition(slug);
        if (def != null) ApplyPetDefinition(def);
    }

    private void ApplyPetDefinition(PetDefinition pet)
    {
        CurrentPet = pet;
        PetName = pet.Name;
        SpritesheetPath = pet.SpritesheetPath;
        AnimFrameWidth = pet.FrameWidth;
        AnimFrameHeight = pet.FrameHeight;
        AnimColumns = pet.Columns;
        AnimRows = pet.Rows;
        AnimFrameDurationMs = _configService.Config.AnimFrameDurationMs;
        AnimCurrentRow = 0;

        _configService.Config.CurrentPetId = pet.Id;
        _configService.Config.PetName = pet.Name;
        _configService.Save();
    }

    /// <summary>保存宠物窗口位置（下次启动恢复）</summary>
    public void SavePosition(double x, double y)
    {
        _configService.Config.PetWindowX = x;
        _configService.Config.PetWindowY = y;
        _configService.Save();
    }

    // ── 交互 ──

    [RelayCommand]
    private void SingleClick()
    {
        AnimCurrentRow = 3; // waving
        ResetAnimRowAfterDelay();
    }

    [RelayCommand]
    private async Task PerformAction(PetActionConfig? action)
    {
        if (action == null) return;

        // 如果有异步回调，执行它（插件 API 查询等）
        if (action.ActionCallback != null)
        {
            AnimCurrentRow = 4; // jumping
            try { await action.ActionCallback(); }
            catch (Exception ex) { ShowFileDropInfo("⚠️ 插件错误", ex.Message); }
            ResetAnimRowAfterDelay();
            return;
        }

        ShowReaction(action.Reaction);
        AnimCurrentRow = 4; // jumping
        ResetAnimRowAfterDelay();
    }

    /// <summary>激活一个文件动作为默认拖放操作</summary>
    public void ActivateAction(FileActionConfig action)
    {
        ActivatedFileAction = action;
        _configService.Config.ActivatedFileActionName = action.Name;
        _configService.Save();
        ShowFileDropInfo("📌 已锁定", $"「{action.Emoji} {action.Name}」\n拖文件将直接执行此操作，右键可解锁。");
    }

    /// <summary>解锁默认拖放操作</summary>
    public void DeactivateAction()
    {
        if (ActivatedFileAction == null) return;
        var name = ActivatedFileAction.Name;
        ActivatedFileAction = null;
        _configService.Config.ActivatedFileActionName = null;
        _configService.Save();
        ShowFileDropInfo("🔓 已解锁", $"「{name}」已取消锁定，拖文件将恢复弹出选项菜单。");
    }

    /// <summary>重启后恢复上次激活的操作</summary>
    private void RestoreActivatedAction()
    {
        var savedName = _configService.Config.ActivatedFileActionName;
        if (string.IsNullOrEmpty(savedName)) return;
        var match = _fileActions.FirstOrDefault(a =>
            a.Name.Equals(savedName, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            ActivatedFileAction = match;
            // 不弹气泡，安静恢复
        }
    }

    [RelayCommand]
    private void SelectPet(PetDefinition? pet)
    {
        if (pet == null) return;
        ApplyPetDefinition(pet);
        ShowReaction("✨");
    }

    private async void ResetAnimRowAfterDelay()
    {
        await Task.Delay(1500);
        _dispatcher.Post(() => AnimCurrentRow = 0);
    }

    public void ShowReaction(string reaction)
    {
        _dispatcher.Post(() =>
        {
            CurrentReaction = reaction;
            IsReacting = true;
        });
        Task.Delay(2000).ContinueWith(_ =>
            _dispatcher.Post(() => IsReacting = false));
    }

    /// <summary>在对话气泡中显示文件拖放信息</summary>
    public void ShowFileDropInfo(string title, string info)
    {
        _dispatcher.Post(() =>
        {
            ThoughtAssistant = title;
            ThoughtText = info;
            IsShowingThought = true;
        });
        Task.Delay(8000).ContinueWith(_ =>
            _dispatcher.Post(() => IsShowingThought = false));
    }

    public void Cleanup()
    {
        PetEvents.ConfigSaved -= OnConfigSaved;
        if (_healthService != null)
        {
            _healthService.ReminderTriggered -= OnHealthReminder;
            _healthService.Stop();
        }
        _activityCts?.Cancel();
        _activityCts?.Dispose();
        (_activityMonitor as IDisposable)?.Dispose();
    }
}
