using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyPersonalTool.Sdk;

namespace DeepSeekPlugin;

/// <summary>
/// DeepSeek 余额查询 &amp; 缓存测试插件
/// 首次使用需通过右键菜单设置 API Key
/// </summary>
[Plugin("DeepSeek 查询", Version = "1.1.0", Description = "查询 DeepSeek API 余额、用量和缓存命中率")]
public class DeepSeekQueryPlugin : PluginBase
{
    private const string ConfigKey = "deepseek_api_key";
    private const string BalanceUrl = "https://api.deepseek.com/user/balance";
    private const string ChatUrl = "https://api.deepseek.com/v1/chat/completions";

    private IPluginHost? _host;

    public override string Name => "DeepSeek 查询";
    public override string Description => "查询 DeepSeek API 余额和缓存状态";

    public override async Task InitializeAsync(IPluginHost host)
    {
        _host = host;
        await base.InitializeAsync(host);

        // 设置 API Key
        host.RegisterAction(new PluginAction
        {
            Name = "设置 API Key",
            Emoji = "🔑",
            Description = "配置 DeepSeek API Key",
            Group = "DeepSeek",
            Target = ActionTarget.ContextMenu,
            Callback = async () =>
            {
                var currentKey = host.GetConfig(ConfigKey) ?? "";
                var key = await host.ShowInputDialog(
                    "🔑 DeepSeek API Key",
                    "输入你的 DeepSeek API Key (sk-...)",
                    currentKey);
                if (string.IsNullOrEmpty(key)) return;

                host.SetConfig(ConfigKey, key);
                host.ShowThought("✅ 配置已保存",
                    "DeepSeek API Key 已保存，现在可以使用查询功能。");
            },
        });

        // 查询余额
        host.RegisterAction(new PluginAction
        {
            Name = "查询余额",
            Emoji = "💰",
            Description = "查询 DeepSeek API 余额和账户状态",
            Group = "DeepSeek",
            Target = ActionTarget.ContextMenu,
            Callback = async () => await QueryBalance(),
        });

        // 缓存测试
        host.RegisterAction(new PluginAction
        {
            Name = "缓存命中测试",
            Emoji = "⚡",
            Description = "发送两次请求对比缓存命中率",
            Group = "DeepSeek",
            Target = ActionTarget.ContextMenu,
            Callback = async () => await TestCache(),
        });

        host.Log("DeepSeek 查询插件已加载");
    }

    private string? GetApiKey() => _host?.GetConfig(ConfigKey);

    private async Task QueryBalance()
    {
        if (_host == null) return;
        var apiKey = GetApiKey();

        if (string.IsNullOrEmpty(apiKey))
        {
            _host.ShowThought("⚠️ 未配置", "请先右键 → DeepSeek → 设置 API Key");
            return;
        }

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var json = await client.GetStringAsync(BalanceUrl);
            var resp = JsonSerializer.Deserialize<BalanceResponse>(json);

            if (resp?.BalanceInfos == null || resp.BalanceInfos.Count == 0)
            {
                _host.ShowThought("💰 余额", "暂未获取到余额信息");
                return;
            }

            var info = resp.BalanceInfos[0];
            var available = resp.IsAvailable ? "✅ 可用" : "❌ 不可用";
            var msg = $"状态: {available}\n" +
                      $"余额: ¥{info.TotalBalance}\n" +
                      (string.IsNullOrEmpty(info.ToppedUpBalance) || info.ToppedUpBalance == "0.00"
                          ? ""
                          : $"充值余额: ¥{info.ToppedUpBalance}\n") +
                      (string.IsNullOrEmpty(info.GrantedBalance) || info.GrantedBalance == "0.00"
                          ? ""
                          : $"赠送余额: ¥{info.GrantedBalance}");

            var total = double.Parse(info.TotalBalance);
            if (total <= 0)
                msg += "\n\n⚠️ 余额不足，请及时充值";

            _host.ShowThought("💰 DeepSeek 余额", msg.TrimEnd());
        }
        catch (HttpRequestException ex)
        {
            _host.ShowThought("❌ 查询失败",
                $"网络错误: {ex.Message}\n\n请检查 API Key 是否正确");
        }
        catch (Exception ex)
        {
            _host.ShowThought("❌ 查询失败", ex.Message);
        }
    }

    /// <summary>发送两次极短请求来演示缓存命中率</summary>
    private async Task TestCache()
    {
        if (_host == null) return;
        var apiKey = GetApiKey();

        if (string.IsNullOrEmpty(apiKey))
        {
            _host.ShowThought("⚠️ 未配置", "请先右键 → DeepSeek → 设置 API Key");
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

            // 第 1 次请求（缓存未命中）
            var resp1 = await client.PostAsync(ChatUrl, content);
            var json1 = await resp1.Content.ReadAsStringAsync();
            var usage1 = ParseUsage(json1);

            _host.ShowThought("⚡ 缓存测试",
                "第 1 次请求完成，正在发送第 2 次…");

            // 第 2 次请求（相同前缀，应命中缓存）
            var resp2 = await client.PostAsync(ChatUrl, content);
            var json2 = await resp2.Content.ReadAsStringAsync();
            var usage2 = ParseUsage(json2);

            // 计算缓存命中率
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

    /// <summary>从 chat completion 响应中提取 usage 字段</summary>
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

    private record UsageInfo(long Input, long Output, long CacheHit, long CacheMiss)
    {
        public long TotalInput => CacheHit + CacheMiss;
    }
}

// ── API 响应模型 ──

public class BalanceResponse
{
    [JsonPropertyName("is_available")]
    public bool IsAvailable { get; set; }

    [JsonPropertyName("balance_infos")]
    public List<BalanceInfo>? BalanceInfos { get; set; }
}

public class BalanceInfo
{
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "CNY";

    [JsonPropertyName("total_balance")]
    public string TotalBalance { get; set; } = "0.00";

    [JsonPropertyName("granted_balance")]
    public string GrantedBalance { get; set; } = "0.00";

    [JsonPropertyName("topped_up_balance")]
    public string ToppedUpBalance { get; set; } = "0.00";
}
