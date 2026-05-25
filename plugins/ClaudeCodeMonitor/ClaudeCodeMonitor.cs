using System.Text.Json;
using MyPersonalTool.Sdk;

namespace ClaudeCodeMonitor;

[Plugin("Claude Code 监测", Version = "2.1.0", Description = "监测所有活跃 Claude Code 会话，无需 hooks 配置")]
public class ClaudeCodeMonitorPlugin : PluginBase
{
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
    /// <summary>sessionId → JSONL 文件路径</summary>
    private readonly Dictionary<string, string> _trackedJsonls = new();
    /// <summary>sessionId → 上次读取的字节位置</summary>
    private readonly Dictionary<string, long> _filePositions = new();
    private DateTime _startupTime;

    public override string Name => "Claude Code 监测";

    public override async Task InitializeAsync(IPluginHost host)
    {
        _host = host;
        _startupTime = DateTime.UtcNow;
        await base.InitializeAsync(host);

        LoadKnownSessions();

        _cts = new CancellationTokenSource();
        _ = PollAsync(_cts.Token);

        host.Log("Claude Code 监测插件 v2.1 已加载");
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
                await Task.Delay(3000, ct);
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
        var newestSession = (SessionFile?)null;
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

        // 新会话
        foreach (var id in currentIds)
        {
            if (!_knownSessions.Contains(id))
            {
                _knownSessions.Add(id);
                OnSessionStarted(ct);
            }
        }

        // 已结束的会话
        foreach (var id in _knownSessions.ToList())
        {
            if (!currentIds.Contains(id))
            {
                _knownSessions.Remove(id);
                _trackedJsonls.Remove(id);
                OnSessionEnded();
            }
        }

        // 首次启动时已有活跃会话，触发一次事件
        if (wasEmpty && newestSession != null && currentIds.Contains(newestSession.SessionId))
        {
            OnSessionStarted(ct);
        }
    }

    private async Task ScanAllTranscriptsAsync(CancellationToken ct)
    {
        if (!Directory.Exists(ProjectsDir)) return;

        // 为每个已知会话查找并跟踪 JSONL
        foreach (var sessionId in _knownSessions)
        {
            if (_trackedJsonls.ContainsKey(sessionId)) continue;

            foreach (var subDir in Directory.GetDirectories(ProjectsDir))
            {
                var file = Directory.GetFiles(subDir, $"*{sessionId}*").FirstOrDefault();
                if (file != null)
                {
                    _trackedJsonls[sessionId] = file;
                    break;
                }
            }
        }

        // 扫描所有已跟踪的 JSONL
        foreach (var kv in _trackedJsonls)
        {
            if (ct.IsCancellationRequested) break;
            await ScanJsonlAsync(kv.Key, kv.Value, ct);
        }
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
                // 首次遇到此文件：记录长度，只看修改时间晚于启动时间的文件
                _filePositions[sessionId] = fileLen;

                // 如果文件在启动后被修改过，从尾部读取新内容
                var lastWrite = File.GetLastWriteTimeUtc(path);
                if (lastWrite <= _startupTime || fileLen == 0)
                    return; // 未修改过或空文件，跳过
            }

            if (fileLen <= _filePositions[sessionId])
                return; // 没有新内容

            // 从上次位置开始读取新内容
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

                    // UUID 去重
                    var key = entry.Uuid ?? entry.PromptId ?? line.GetHashCode().ToString();
                    if (!_seenUuids.Add(key)) continue;

                    switch (entry.Type)
                    {
                        case "user" when entry.Message?.Content is JsonElement je &&
                                        je.ValueKind == JsonValueKind.String:
                            OnUserPrompt(je.GetString() ?? "");
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

    // ── 事件处理 ──

    private void OnSessionStarted(CancellationToken ct)
    {
        if (_host == null) return;
        _host.ShowThought("💬 Claude Code", "Claude Code 会话已开始");
        _ = _host.RunWithAnimation(PetAnimation.Wave, async token =>
        {
            await Task.Delay(2000, token);
        });
    }

    private void OnSessionEnded()
    {
        if (_host == null) return;
        _host.ShowThought("💬 Claude Code", "Claude Code 会话已结束");
    }

    private void OnUserPrompt(string content)
    {
        if (_host == null) return;
        var preview = content.Length > 120 ? content[..120] + "…" : content;
        _host.ShowThought("💬 Claude Code", preview);
        _ = _host.RunWithAnimation(PetAnimation.Think, async token =>
        {
            await Task.Delay(3000, token);
        });
    }

    private void OnAssistantResponse(TranscriptEntry? entry)
    {
        if (_host == null || entry?.Message?.Content == null) return;

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

        var preview = content.Length > 200 ? content[..200] + "…" : content;
        _host.ShowThought("💬 Claude Code", preview);
        _ = _host.RunWithAnimation(PetAnimation.Happy, async token =>
        {
            await Task.Delay(2000, token);
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
