using System.Text.Json;
using MyPersonalTool.Sdk;

namespace ClaudeCodeMonitor;

[Plugin("Claude Code 监测", Version = "2.2.0", Description = "监测所有活跃 Claude Code 会话，无需 hooks 配置")]
public class ClaudeCodeMonitorPlugin : PluginBase
{
    private const string KeyEnabled = "ccd_monitor_enabled";
    private static readonly string SessionsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "sessions");

    private static readonly string ProjectsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "projects");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private IPluginHost? _host;
    private CancellationTokenSource? _cts;
    private HashSet<string> _knownSessions = new();
    private readonly HashSet<string> _seenUuids = new();
    private readonly Dictionary<string, string> _trackedJsonls = new();
    private readonly Dictionary<string, long> _filePositions = new();
    private DateTime _startupTime;

    public override string Name => "Claude Code 监测";

    public override async Task InitializeAsync(IPluginHost host)
    {
        _host = host;
        _startupTime = DateTime.UtcNow;
        await base.InitializeAsync(host);

        // 注册切换开关（名称根据当前状态）
        host.RegisterAction(new PluginAction
        {
            Name = host.GetConfig(KeyEnabled) != "false" ? "切换监测" : "已关闭 · 点击开启",
            Emoji = "🔄",
            Description = "开启或关闭 Claude Code 活动监测",
            Group = "💬 Claude Code 监测",
            Target = ActionTarget.ContextMenu,
            Callback = ToggleMonitoring,
        });

        LoadKnownSessions();

        if (host.GetConfig(KeyEnabled) != "false")
        {
            _cts = new CancellationTokenSource();
            _ = PollAsync(_cts.Token);
            host.Log("Claude Code 监测已启动");
        }
        else
        {
            host.Log("Claude Code 监测已关闭");
        }
    }

    private Task ToggleMonitoring()
    {
        if (_host == null) return Task.CompletedTask;
        var currentlyEnabled = _host.GetConfig(KeyEnabled) != "false";

        if (currentlyEnabled)
        {
            _host.SetConfig(KeyEnabled, "false");
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _host.CancelCurrentTask();
            _host.StopAnimation();
            _host.UpdateActionName("切换监测", "已关闭 · 点击开启");
            _host.ShowThought("⏹️ 已关闭", "Claude Code 监测已停止");
        }
        else
        {
            _host.SetConfig(KeyEnabled, "true");
            _startupTime = DateTime.UtcNow;
            _knownSessions.Clear();
            _trackedJsonls.Clear();
            _filePositions.Clear();
            _seenUuids.Clear();
            LoadKnownSessions();
            _cts = new CancellationTokenSource();
            _ = PollAsync(_cts.Token);
            _host.UpdateActionName("已关闭 · 点击开启", "切换监测");
            _host.ShowThought("▶️ 已开启", "Claude Code 监测已启动");
        }

        return Task.CompletedTask;
    }

    private void LoadKnownSessions()
    {
        try
        {
            if (Directory.Exists(SessionsDir))
            {
                foreach (var file in Directory.GetFiles(SessionsDir, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var s = JsonSerializer.Deserialize<SessionFile>(json, JsonOptions);
                        if (s != null) _knownSessions.Add(s.SessionId);
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        try { Directory.CreateDirectory(SessionsDir); } catch { }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1500, ct);
                UpdateSessions(ct);
                await ScanAllTranscriptsAsync(ct);
            }
            catch (TaskCanceledException) { break; }
            catch { }
        }
    }

    private void UpdateSessions(CancellationToken ct)
    {
        if (!Directory.Exists(SessionsDir)) return;

        var currentIds = new HashSet<string>();
        SessionFile? newestSession = null;
        long newestTime = 0;
        var wasEmpty = _knownSessions.Count == 0;

        foreach (var file in Directory.GetFiles(SessionsDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var s = JsonSerializer.Deserialize<SessionFile>(json, JsonOptions);
                if (s == null || string.IsNullOrEmpty(s.SessionId)) continue;
                currentIds.Add(s.SessionId);

                if (s.StartedAt > newestTime) { newestTime = s.StartedAt; newestSession = s; }
            }
            catch { }
        }

        foreach (var id in currentIds)
        {
            if (!_knownSessions.Contains(id))
            {
                _knownSessions.Add(id);
                OnSessionStarted(ct);
            }
        }

        foreach (var id in _knownSessions.ToList())
        {
            if (!currentIds.Contains(id))
            {
                _knownSessions.Remove(id);
                _trackedJsonls.Remove(id);
                OnSessionEnded();
            }
        }

        if (wasEmpty && newestSession != null && currentIds.Contains(newestSession.SessionId))
        {
            OnSessionStarted(ct);
        }
    }

    private async Task ScanAllTranscriptsAsync(CancellationToken ct)
    {
        if (!Directory.Exists(ProjectsDir)) return;
        var scanned = false;

        // 只在有未知会话或首次运行时扫描 project 目录
        foreach (var sessionId in _knownSessions)
        {
            if (_trackedJsonls.ContainsKey(sessionId)) continue;
            if (!scanned) { scanned = true; WarmProjectsCache(); }

            foreach (var subDir in _cachedProjectDirs)
            {
                var file = Directory.GetFiles(subDir, $"*{sessionId}*").FirstOrDefault();
                if (file != null) { _trackedJsonls[sessionId] = file; break; }
            }
        }

        // 也扫描近期修改过的 JSONL（没有 session 文件的活跃会话）
        if (_lastProjectsScan.AddSeconds(5) < DateTime.UtcNow)
        {
            if (!scanned) { scanned = true; WarmProjectsCache(); }
            _lastProjectsScan = DateTime.UtcNow;

            foreach (var subDir in _cachedProjectDirs)
            {
                foreach (var file in Directory.GetFiles(subDir, "*.jsonl"))
                {
                    try
                    {
                        var lastWrite = File.GetLastWriteTimeUtc(file);
                        if (lastWrite <= _startupTime) continue;
                        if (_trackedJsonls.ContainsValue(file)) continue;
                        var id = Path.GetFileNameWithoutExtension(file);
                        _trackedJsonls[id] = file;
                    }
                    catch { }
                }
            }
        }

        foreach (var kv in _trackedJsonls)
        {
            if (ct.IsCancellationRequested) break;
            await ScanJsonlAsync(kv.Key, kv.Value, ct);
        }
    }

    // 缓存 project 目录，加速后续查找
    private DateTime _lastProjectsScan;
    private string[] _cachedProjectDirs = [];

    private void WarmProjectsCache()
    {
        try { _cachedProjectDirs = Directory.GetDirectories(ProjectsDir); }
        catch { _cachedProjectDirs = []; }
    }

    private async Task ScanJsonlAsync(string sessionId, string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return;

        try
        {
            var fileLen = new FileInfo(path).Length;
            var lastPos = _filePositions.GetValueOrDefault(sessionId, -1L);

            if (lastPos < 0)
            {
                // 首次遇到：记录末尾位置，跳过所有已有内容，后续只读增量
                _filePositions[sessionId] = fileLen;
            }

            if (fileLen <= _filePositions[sessionId])
                return;

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(_filePositions[sessionId], SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            var newContent = await reader.ReadToEndAsync();

            _filePositions[sessionId] = fileLen;
            if (string.IsNullOrEmpty(newContent)) return;

            foreach (var line in newContent.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<TranscriptEntry>(line, JsonOptions);
                    if (entry?.Type == null) continue;

                    var key = entry.Uuid ?? entry.PromptId ?? line.GetHashCode().ToString();
                    if (!_seenUuids.Add(key)) continue;

                    switch (entry.Type)
                    {
                        case "user" when entry.Message?.Content is JsonElement je &&
                                        je.ValueKind == JsonValueKind.String:
                            OnUserPrompt(je.GetString() ?? "");
                            break;

                        case "thinking":
                            OnThinking();
                            break;

                        case "assistant":
                            OnAssistantResponse(entry);
                            break;
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    // ── 事件处理（通过 SDK 队列排队，不打断）──

    private void OnSessionStarted(CancellationToken ct)
    {
        if (_host == null) return;
        _host.EnqueueThought(new ThoughtMessage
        {
            Title = "💬 Claude Code", Text = "Claude Code 会话已开始",
            DurationMs = 3000,
        });
    }

    private void OnSessionEnded()
    {
        if (_host == null) return;
        _host.ClearThoughtQueue();
        _host.CancelCurrentTask();
        _host.StopAnimation();
        _host.ShowThought("💬 Claude Code", "Claude Code 会话已结束");
    }

    private void OnUserPrompt(string content)
    {
        if (_host == null) return;
        var preview = content.Length > 120 ? content[..120] + "…" : content;
        _host.EnqueueThought(new ThoughtMessage
        {
            Title = "💬 Claude Code", Text = preview,
            DurationMs = 3000,
        });
    }

    private void OnThinking() { }

    private void OnAssistantResponse(TranscriptEntry? entry)
    {
        if (entry?.Message?.Content == null) return;
        var textParts = new List<string>();
        if (entry.Message.Content is JsonElement arr && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var t) && t.GetString() == "text" &&
                    item.TryGetProperty("text", out var txt))
                {
                    textParts.Add(txt.GetString() ?? "");
                }
            }
        }
        var content = string.Join("\n", textParts);
        if (string.IsNullOrEmpty(content)) return;

        _host?.EnqueueThought(new ThoughtMessage
        {
            Title = "💬 Claude Code",
            Text = content.Length > 200 ? content[..200] + "…" : content,
            DurationMs = 6000,
        });
    }

    // ── 模型 ──

    private class SessionFile
    {
        public string SessionId { get; set; } = "";
        public long StartedAt { get; set; }
    }

    private class TranscriptEntry
    {
        public string? Type { get; set; }
        public string? Uuid { get; set; }
        public string? PromptId { get; set; }
        public TranscriptMessage? Message { get; set; }
    }

    private class TranscriptMessage
    {
        public string? Role { get; set; }
        public object? Content { get; set; }
    }

    public override Task CleanupAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        return base.CleanupAsync();
    }
}
