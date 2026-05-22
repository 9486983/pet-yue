using System.Text.Json;
using MyPersonalTool.Core.Interfaces;
using MyPersonalTool.Core.Models;

namespace MyPersonalTool.Services;

public class ConfigService : IConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MyPersonalTool");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public AppConfig Config { get; private set; }

    public ConfigService()
    {
        Config = Load();
    }

    private static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null)
                {
                    if (cfg.PetActions.Count == 0)
                        cfg.PetActions = DefaultActions();
                    return cfg;
                }
            }
        }
        catch { }
        var defaults = new AppConfig();
        defaults.PetActions = DefaultActions();
        return defaults;
    }

    private static List<PetActionConfig> DefaultActions() => new()
    {
        new() { Name = "喂食", Emoji = "🍔", Reaction = "😋", Description = "喂好吃的" },
        new() { Name = "玩耍", Emoji = "🎮", Reaction = "🎉", Description = "一起玩" },
        new() { Name = "摸摸", Emoji = "❤️", Reaction = "🥰", Description = "轻轻抚摸" },
    };

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(Config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
        }
    }

    public string? GetPluginValue(string key)
    {
        Config.PluginValues.TryGetValue(key, out var val);
        return val;
    }

    public void SetPluginValue(string key, string value)
    {
        Config.PluginValues[key] = value;
        Save();
    }
}
