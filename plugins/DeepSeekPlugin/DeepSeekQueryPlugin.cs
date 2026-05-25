using System.Text;
using System.Text.Json;
using MyPersonalTool.Sdk;

namespace DeepSeekPlugin;

/// <summary>
/// DeepSeek 余额查询 &amp; 缓存测试插件
/// 配置在设置页中管理（API Key、定时查询间隔）
/// </summary>
[Plugin("DeepSeek 查询", Version = "2.0.0", Description = "查询 DeepSeek API 余额、用量和缓存命中率，支持定时自动查询")]
public class DeepSeekQueryPlugin : PluginBase
{
    private const string KeyApiKey = "deepseek_api_key";
    private const string KeyInterval = "deepseek_interval";
    private const string KeyAutoQuery = "deepseek_auto_query";
    private const string KeySummaryUrl = "deepseek_summary_url";
    private const string BalanceUrl = "https://api.deepseek.com/user/balance";
    private const string ChatUrl = "https://api.deepseek.com/v1/chat/completions";

    private IPluginHost? _host;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _timerCts;

    public override string Name => "DeepSeek 查询";
    public override string Description => "查询 DeepSeek API 余额和缓存状态";

    public override async Task InitializeAsync(IPluginHost host)
    {
        _host = host;
        await base.InitializeAsync(host);

        // ── 注册配置定义 ──
        host.RegisterConfig(new PluginConfigSection
        {
            Title = "DeepSeek API",
            Emoji = "🔑",
            Fields = new()
            {
                new()
                {
                    Key = KeyApiKey,
                    Label = "API Key",
                    Type = PluginConfigFieldType.Password,
                    IsRequired = true,
                    Placeholder = "sk-...",
                    Description = "从 DeepSeek 控制台获取",
                },
                new()
                {
                    Key = KeyAutoQuery,
                    Label = "定时查询余额",
                    Type = PluginConfigFieldType.Boolean,
                    DefaultValue = "false",
                    Description = "开启后将按下方间隔自动查询余额并显示气泡",
                },
                new()
                {
                    Key = KeyInterval,
                    Label = "查询间隔（分钟）",
                    Type = PluginConfigFieldType.Number,
                    DefaultValue = "30",
                    MinValue = 1,
                    MaxValue = 180,
                    Description = "两次自动查询之间的间隔时间",
                },
                new()
                {
                    Key = KeySummaryUrl,
                    Label = "用量查询接口",
                    Type = PluginConfigFieldType.String,
                    DefaultValue = "https://platform.deepseek.com/api/v0/users/get_user_summary",
                    Description = "待后续接入，当前暂未使用",
                },
            },
        }, Name);

        // ── 监听配置变更 ──
        host.ConfigValueChanged += OnConfigChanged;

        // ── 注册动作 ──

        host.RegisterAction(new PluginAction
        {
            Name = "设置",
            Emoji = "⚙️",
            Description = "配置 DeepSeek API Key 和定时查询参数",
            Group = "DeepSeek",
            Target = ActionTarget.ContextMenu,
            Callback = () =>
            {
                host.ShowConfigDialog("DeepSeek API");
                return Task.CompletedTask;
            },
        });

        host.RegisterAction(new PluginAction
        {
            Name = "查询余额",
            Emoji = "💰",
            Description = "查询 DeepSeek API 余额",
            Group = "DeepSeek",
            Target = ActionTarget.ContextMenu,
            Callback = async () => await QueryBalance(),
        });

        host.RegisterAction(new PluginAction
        {
            Name = "缓存命中测试",
            Emoji = "⚡",
            Description = "发送两次请求对比缓存命中率",
            Group = "DeepSeek",
            Target = ActionTarget.ContextMenu,
            Callback = async () => await TestCache(),
        });

        // ── 启动定时查询（如果已启用） ──
        RestartTimerIfNeeded();

        host.Log("DeepSeek 查询插件 v2.0 已加载");
    }

    private string? GetApiKey() => _host?.GetConfig(KeyApiKey);
    private bool IsAutoQueryEnabled => _host?.GetConfig(KeyAutoQuery) == "true";
    private int GetQueryIntervalMinutes()
    {
        var val = _host?.GetConfig(KeyInterval);
        return int.TryParse(val, out var m) && m >= 1 ? m : 30;
    }

    // ── 定时查询 ──

    private void RestartTimerIfNeeded()
    {
        StopTimer();

        if (!IsAutoQueryEnabled) return;
        if (_host == null) return;

        var interval = GetQueryIntervalMinutes();
        _timerCts = new CancellationTokenSource();
        var ct = _timerCts.Token;
        _timer = new PeriodicTimer(TimeSpan.FromMinutes(interval));

        _ = Task.Run(async () =>
        {
            try
            {
                while (await _timer.WaitForNextTickAsync(ct))
                {
                    if (string.IsNullOrEmpty(GetApiKey())) continue;
                    await QueryBalance(silent: true);
                    if (!IsAutoQueryEnabled) break;
                }
            }
            catch (OperationCanceledException) { }
        }, ct);
    }

    private void StopTimer()
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = null;
        _timer?.Dispose();
        _timer = null;
    }

    private void OnConfigChanged(object? sender, string key)
    {
        if (key is KeyAutoQuery or KeyInterval)
            RestartTimerIfNeeded();
    }

    // ── 查询余额 ──

    private async Task QueryBalance(bool silent = false)
    {
        if (_host == null) return;
        var apiKey = GetApiKey();

        if (string.IsNullOrEmpty(apiKey))
        {
            if (!silent)
                _host.ShowThought("⚠️ 未配置", "请在设置 → DeepSeek API 中填写 API Key");
            return;
        }

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var json = await client.GetStringAsync(BalanceUrl);
            using var doc = JsonDocument.Parse(json);

            var balance = "";
            var isAvailable = false;
            if (doc.RootElement.TryGetProperty("balance_infos", out var infos) &&
                infos.ValueKind == JsonValueKind.Array && infos.GetArrayLength() > 0)
            {
                balance = infos[0].TryGetProperty("total_balance", out var b)
                    ? b.GetString() ?? "0" : "0";
            }
            if (doc.RootElement.TryGetProperty("is_available", out var avail))
                isAvailable = avail.GetBoolean();

            if (string.IsNullOrEmpty(balance) || balance == "0")
            {
                if (!silent)
                    _host.ShowThought("💰 余额", "暂未获取到余额信息");
                return;
            }

            var status = isAvailable ? "✅ 可用" : "❌ 不可用";
            var title = silent ? $"💰 ¥{balance}" : "💰 DeepSeek 余额";
            _host.ShowThought(title, $"{status}\n💰 余额: ¥{balance}");
            _host.ShowReaction(silent ? "💹" : "💰");
        }
        catch (HttpRequestException ex)
        {
            if (!silent)
                _host.ShowThought("❌ 查询失败",
                    $"网络错误: {ex.Message}\n\n请检查 API Key 是否正确");
        }
        catch (Exception ex)
        {
            if (!silent)
                _host.ShowThought("❌ 查询失败", ex.Message);
        }
    }

    // ── 缓存测试 ──

    private async Task TestCache()
    {
        if (_host == null) return;
        var apiKey = GetApiKey();

        if (string.IsNullOrEmpty(apiKey))
        {
            _host.ShowThought("⚠️ 未配置", "请在设置页 → DeepSeek API 中填写 API Key");
            return;
        }

        try
        {
            _host.ShowThought("⚡ 缓存测试", "正在发送第 1 次请求…");

            var body = JsonSerializer.Serialize(new
            {
                model = "deepseek-chat",
                messages = new[] { new { role = "user", content = "Hello" } },
                max_tokens = 1
            });

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var resp1 = await client.PostAsync(ChatUrl, content);
            var json1 = await resp1.Content.ReadAsStringAsync();
            var usage1 = ParseUsage(json1);

            _host.ShowThought("⚡ 缓存测试", "第 1 次请求完成，正在发送第 2 次…");

            var resp2 = await client.PostAsync(ChatUrl, content);
            var json2 = await resp2.Content.ReadAsStringAsync();
            var usage2 = ParseUsage(json2);

            var hitRate1 = usage1.TotalInput > 0
                ? (double)usage1.CacheHit / usage1.TotalInput * 100 : 0;
            var hitRate2 = usage2.TotalInput > 0
                ? (double)usage2.CacheHit / usage2.TotalInput * 100 : 0;

            var msg = $"【第 1 次请求】\n" +
                      $"  输入 tokens: {usage1.TotalInput}\n" +
                      $"  缓存命中: {usage1.CacheHit}\n" +
                      $"  缓存未命中: {usage1.CacheMiss}\n" +
                      $"  命中率: {hitRate1:F1}%\n\n" +
                      $"【第 2 次请求（相同前缀）】\n" +
                      $"  输入 tokens: {usage2.TotalInput}\n" +
                      $"  缓存命中: {usage2.CacheHit}\n" +
                      $"  缓存未命中: {usage2.CacheMiss}\n" +
                      $"  命中率: {hitRate2:F1}%\n\n" +
                      $"💡 第 2 次命中率应明显高于第 1 次\n" +
                      $"（前提是两次请求在缓存 TTL 内）";

            _host.ShowThought("⚡ DeepSeek 缓存测试", msg);
            _host.ShowReaction("⚡");
        }
        catch (HttpRequestException ex)
        {
            _host.ShowThought("❌ 测试失败",
                $"网络错误: {ex.Message}\n\n请检查 API Key 是否正确");
        }
        catch (Exception ex)
        {
            _host.ShowThought("❌ 测试失败", ex.Message);
        }
    }

    private static UsageInfo ParseUsage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var usage = doc.RootElement.GetProperty("usage");
            var hit = usage.TryGetProperty("prompt_cache_hit_tokens", out var h) ? h.GetInt64() : 0;
            var miss = usage.TryGetProperty("prompt_cache_miss_tokens", out var m) ? m.GetInt64() : 0;
            var input = usage.TryGetProperty("prompt_tokens", out var p) ? p.GetInt64() : 0;
            var output = usage.TryGetProperty("completion_tokens", out var c) ? c.GetInt64() : 0;
            return new UsageInfo(input, output, hit, miss);
        }
        catch
        {
            return new UsageInfo(0, 0, 0, 0);
        }
    }

    public override Task CleanupAsync()
    {
        StopTimer();
        return base.CleanupAsync();
    }

    private record UsageInfo(long Input, long Output, long CacheHit, long CacheMiss)
    {
        public long TotalInput => CacheHit + CacheMiss;
    }
}
