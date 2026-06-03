using System.Text;
using System.Text.Json;
using MyPersonalTool.Sdk;

namespace ClaudeCodeMonitor;

/// <summary>
/// 基于 Hooks 的实时监测插件 —— 在 ~/.claude/settings.json 注册 hooks，
/// hook 触发时写日志文件，本插件监测日志实时展示气泡。
/// </summary>
[Plugin("Claude Code 钩子", Version = "1.0.0",
    Description = "通过系统 Hooks 实时监测 Claude Code：命令、改文件、回复内容等")]
public class ClaudeCodeHookPlugin : PluginBase
{
    private static readonly string UserProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static readonly string LogFile =
        Path.Combine(UserProfile, ".claude", "pet-hooks.jsonl");

    private static readonly string SettingsFile =
        Path.Combine(UserProfile, ".claude", "settings.json");

    private static readonly string ScriptDir =
        Path.Combine(UserProfile, ".petdex", "hooks");

    private static readonly string ScriptFile =
        Path.Combine(ScriptDir, "pet-hook.ps1");

    private const string HookMarker = "PET_HOOK_";
    private const string KeyEnabled = "cch_enabled";
    private const string KeyShowBash = "cch_show_bash";

    private sealed record HookDef(string Event, string Matcher, string Id);

    private static readonly HookDef[] MyHooks =
    [
        new("SessionStart", "", "session_start"),
        new("SessionEnd", "", "session_end"),
        new("Stop", "", "stop"),
        new("Notification", "", "notification"),
        new("PostToolUse", "Bash", "bash"),
        new("PostToolUse", "Write|Edit", "file"),
    ];

    private const string ScriptContent = @"param([string]$Event = """")
$logDir = ""$env:USERPROFILE\.claude""
$logFile = ""$logDir\pet-hooks.jsonl""
if (-not (Test-Path $logDir)) { [System.IO.Directory]::CreateDirectory($logDir) | Out-Null }

$extra = """"
$cwd = """"
try {
    $lines = @($input)
    if ($lines.Count -gt 0) {
        $stdin = $lines -join ""`n""
        $json = $stdin | ConvertFrom-Json -ErrorAction SilentlyContinue
        if ($json) {
            if ($json.cwd) { $cwd = $json.cwd }
            if ($json.tool_input.command) { $extra = $json.tool_input.command }
            elseif ($json.tool_input.file_path) { $extra = $json.tool_input.file_path }

            if ($Event -eq ""PET_HOOK_stop"" -and $json.transcript_path) {
                try {
                    $lastLine = [System.IO.File]::ReadAllLines($json.transcript_path, [System.Text.Encoding]::UTF8) | Select-Object -Last 1
                    if ($lastLine) {
                        $lastEntry = $lastLine | ConvertFrom-Json -ErrorAction SilentlyContinue
                        if ($lastEntry -and $lastEntry.message -and $lastEntry.message.content) {
                            $c = $lastEntry.message.content
                            if ($c -is [System.Object[]]) {
                                $texts = $c | Where-Object { $_.type -eq ""text"" } | ForEach-Object { $_.text }
                                if ($texts) { $extra = $texts -join [Environment]::NewLine }
                            } elseif ($c -is [string]) { $extra = $c }
                        }
                    }
                } catch {}
            }
        }
    }
} catch {}

if ($extra.Length -gt 500) { $extra = $extra.Substring(0, 500) + ""..."" }
$proj = """"
if ($cwd) { $proj = [System.IO.Path]::GetFileName($cwd) }

$timestamp = [DateTime]::Now.ToString(""O"")
[System.IO.File]::AppendAllText($logFile, ""$timestamp`t$Event`t$proj`t$extra`r`n"", [System.Text.Encoding]::UTF8)
";

    private IPluginHost? _host;
    private CancellationTokenSource? _cts;
    private long _lastLogPosition;
    private int _bashEventCount;
    private const int BashEventThrottle = 5;

    public override string Name => "Claude Code 钩子";

    public override async Task InitializeAsync(IPluginHost host)
    {
        _host = host;
        await base.InitializeAsync(host);

        host.RegisterConfig(new PluginConfigSection
        {
            Title = "Claude Code 钩子",
            Emoji = "🪝",
            Fields = new()
            {
                new()
                {
                    Key = KeyEnabled, Label = "启用钩子监测",
                    Type = PluginConfigFieldType.Boolean,
                    DefaultValue = "true",
                    Description = "安装系统 hooks，实时监测 Claude Code 事件",
                },
                new()
                {
                    Key = KeyShowBash, Label = "显示命令执行",
                    Type = PluginConfigFieldType.Boolean,
                    DefaultValue = "false",
                    Description = "每次 Claude 执行 Shell 命令时显示气泡",
                },
            },
        }, Name);

        host.RegisterAction(new PluginAction
        {
            Name = "钩子设置",
            Emoji = "🪝",
            Description = "配置 Claude Code 钩子监测",
            Group = "🪝 Claude Code 钩子",
            Target = ActionTarget.ContextMenu,
            Callback = () =>
            {
                host.ShowConfigDialog("Claude Code 钩子");
                return Task.CompletedTask;
            },
        });

        host.RegisterAction(new PluginAction
        {
            Name = host.GetConfig(KeyEnabled) != "false" ? "切换钩子" : "已关闭 · 点击开启",
            Emoji = "🔄",
            Description = "开启或关闭 Claude Code 钩子监测",
            Group = "🪝 Claude Code 钩子",
            Target = ActionTarget.ContextMenu,
            Callback = ToggleMonitoring,
        });

        if (host.GetConfig(KeyEnabled) != "false")
        {
            EnsureScriptInstalled();
            InstallHooks();
            _ = StartMonitorAsync();
        }

        host.Log("Claude Code 钩子插件已加载");
    }

    private Task ToggleMonitoring()
    {
        if (_host == null) return Task.CompletedTask;
        var enabled = _host.GetConfig(KeyEnabled) != "false";

        if (enabled)
        {
            _host.SetConfig(KeyEnabled, "false");
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            RemoveHooks();
            _host.UpdateActionName("切换钩子", "已关闭 · 点击开启");
            _host.ShowThought("⏹️ 钩子已关闭", "系统钩子已移除");
        }
        else
        {
            _host.SetConfig(KeyEnabled, "true");
            EnsureScriptInstalled();
            InstallHooks();
            _ = StartMonitorAsync();
            _host.UpdateActionName("已关闭 · 点击开启", "切换钩子");
            _host.ShowThought("▶️ 钩子已开启", "系统钩子已安装");
        }

        return Task.CompletedTask;
    }

    private void EnsureScriptInstalled()
    {
        try
        {
            if (!Directory.Exists(ScriptDir))
                Directory.CreateDirectory(ScriptDir);
            File.WriteAllText(ScriptFile, ScriptContent, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _host?.Log($"写入钩子脚本失败: {ex.Message}");
        }
    }

    // ── 安装/移除 Hooks ──

    private void InstallHooks()
    {
        try
        {
            var json = ReadSettingsWithRetry();
            using var doc = JsonDocument.Parse(json);
            var root = new Dictionary<string, object?>();

            foreach (var prop in doc.RootElement.EnumerateObject())
                if (prop.Name != "hooks")
                    root[prop.Name] = CloneElement(prop.Value);

            var hookGroups = new Dictionary<string, List<Dictionary<string, object?>>>();
            foreach (var def in MyHooks)
            {
                if (!hookGroups.ContainsKey(def.Event))
                    hookGroups[def.Event] = new List<Dictionary<string, object?>>();

                var entry = new Dictionary<string, object?>
                {
                    ["hooks"] = new List<Dictionary<string, object?>>
                    {
                        new()
                        {
                            ["type"] = "command",
                            ["command"] = BuildHookCommand(def),
                        },
                    },
                };
                if (!string.IsNullOrEmpty(def.Matcher))
                    entry["matcher"] = def.Matcher;

                hookGroups[def.Event].Add(entry);
            }

            var existingHooks = new Dictionary<string, List<Dictionary<string, object?>>>();
            if (doc.RootElement.TryGetProperty("hooks", out var hooksEl))
            {
                foreach (var eventProp in hooksEl.EnumerateObject())
                {
                    var entries = new List<Dictionary<string, object?>>();
                    foreach (var entryEl in eventProp.Value.EnumerateArray())
                    {
                        var entry = JsonToDict(entryEl);
                        if (!ContainsHookMarker(entry))
                            entries.Add(entry);
                    }
                    existingHooks[eventProp.Name] = entries;
                }
            }

            foreach (var kv in hookGroups)
            {
                if (existingHooks.ContainsKey(kv.Key))
                    existingHooks[kv.Key].AddRange(kv.Value);
                else
                    existingHooks[kv.Key] = kv.Value;
            }

            root["hooks"] = existingHooks;
            WriteSettingsWithRetry(JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _host?.Log($"安装钩子失败: {ex.Message}");
        }
    }

    private void RemoveHooks()
    {
        try
        {
            var json = ReadSettingsWithRetry();
            using var doc = JsonDocument.Parse(json);
            var root = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                if (prop.Name != "hooks")
                    root[prop.Name] = CloneElement(prop.Value);

            if (doc.RootElement.TryGetProperty("hooks", out var hooksEl))
            {
                var cleaned = new Dictionary<string, List<Dictionary<string, object?>>>();
                foreach (var eventProp in hooksEl.EnumerateObject())
                {
                    var entries = new List<Dictionary<string, object?>>();
                    foreach (var entryEl in eventProp.Value.EnumerateArray())
                    {
                        var entry = JsonToDict(entryEl);
                        if (!ContainsHookMarker(entry))
                            entries.Add(entry);
                    }
                    if (entries.Count > 0)
                        cleaned[eventProp.Name] = entries;
                }
                if (cleaned.Count > 0)
                    root["hooks"] = cleaned;
            }

            WriteSettingsWithRetry(JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _host?.Log($"移除钩子失败: {ex.Message}");
        }
    }

    private string BuildHookCommand(HookDef def) =>
        $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{ScriptFile}\" -Event \"{HookMarker}{def.Id}\"";

    private static bool ContainsHookMarker(Dictionary<string, object?> entry)
    {
        if (!entry.TryGetValue("hooks", out var hooksObj) || hooksObj is not List<object?> hooksList)
            return false;
        return hooksList.OfType<Dictionary<string, object?>>()
            .Any(h => h.TryGetValue("command", out var cmd) && cmd is string s && s.Contains(HookMarker));
    }

    // ── 日志监测 ──

    private async Task StartMonitorAsync()
    {
        try
        {
            var logDir = Path.GetDirectoryName(LogFile);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);
            if (!File.Exists(LogFile))
                File.WriteAllText(LogFile, "", Encoding.UTF8);
            _lastLogPosition = new FileInfo(LogFile).Length;
        }
        catch { _lastLogPosition = 0; }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);
                PollLogFile(ct);
            }
        }
        catch (TaskCanceledException) { }
        catch { }
    }

    private void PollLogFile(CancellationToken ct)
    {
        if (_host == null) return;
        try
        {
            var fi = new FileInfo(LogFile);
            if (!fi.Exists || fi.Length <= _lastLogPosition) return;

            using var fs = new FileStream(LogFile, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            fs.Seek(_lastLogPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var newContent = reader.ReadToEnd();
            _lastLogPosition = fs.Length;
            if (string.IsNullOrEmpty(newContent)) return;

            _bashEventCount = 0;
            foreach (var line in newContent.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (ct.IsCancellationRequested) break;
                ProcessLogLine(line.Trim('\r'));
            }
        }
        catch { }
    }

    private void ProcessLogLine(string line)
    {
        if (_host == null) return;

        var parts = line.Split('\t', 4);
        if (parts.Length < 2) return;

        var eventId = parts[1].Replace(HookMarker, "");
        var project = parts.Length > 2 ? parts[2] : "";
        var extra = parts.Length > 3 ? parts[3] : "";

        switch (eventId)
        {
            case "session_start":
                _host.EnqueueThought(new ThoughtMessage
                {
                    Title = "💬 Claude Code",
                    Text = string.IsNullOrEmpty(project) ? "开始工作" : $"在 {project} 开工",
                    DurationMs = 3000,
                });
                break;

            case "session_end":
                _host.ClearThoughtQueue();
                _host.CancelCurrentTask();
                _host.StopAnimation();
                _host.ShowThought("💬 Claude Code", "收工");
                break;

            case "stop":
                if (string.IsNullOrEmpty(extra)) break;
                _host.EnqueueThought(new ThoughtMessage
                {
                    Title = "💬 Claude Code",
                    Text = Truncate(extra, 200),
                    DurationMs = 6000,
                });
                break;

            case "notification":
                _host.EnqueueThought(new ThoughtMessage
                {
                    Title = "💬 Claude Code",
                    Text = "在等你",
                    DurationMs = 3000,
                });
                break;

            case "bash":
                if (_host.GetConfig(KeyShowBash) != "true") break;
                _bashEventCount++;
                if (_bashEventCount > BashEventThrottle || string.IsNullOrEmpty(extra)) break;
                _host.EnqueueThought(new ThoughtMessage
                {
                    Title = "💬 Claude Code",
                    Text = Truncate(extra, 80),
                    DurationMs = 4000,
                });
                break;

            case "file":
                if (string.IsNullOrEmpty(extra)) break;
                _host.EnqueueThought(new ThoughtMessage
                {
                    Title = "💬 Claude Code",
                    Text = Truncate(extra, 80),
                    DurationMs = 3000,
                });
                break;
        }
    }

    // ── JSON 工具 ──

    private static string ReadSettingsWithRetry(int retries = 3)
    {
        for (var i = 0; i < retries; i++)
        {
            try
            {
                if (!File.Exists(SettingsFile))
                    return "{}";
                return File.ReadAllText(SettingsFile, Encoding.UTF8);
            }
            catch (IOException) { Thread.Sleep(200); }
        }
        return "{}";
    }

    private static void WriteSettingsWithRetry(string content, int retries = 3)
    {
        for (var i = 0; i < retries; i++)
        {
            try
            {
                File.WriteAllText(SettingsFile, content, Encoding.UTF8);
                return;
            }
            catch (IOException) { Thread.Sleep(200); }
        }
    }

    private static object? CloneElement(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Object => JsonToDict(el),
        JsonValueKind.Array => el.EnumerateArray().Select(CloneElement).ToList(),
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? (object)l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.GetRawText(),
    };

    private static Dictionary<string, object?> JsonToDict(JsonElement el)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in el.EnumerateObject())
            dict[prop.Name] = CloneElement(prop.Value);
        return dict;
    }

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "…";

    public override Task CleanupAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        return base.CleanupAsync();
    }
}
