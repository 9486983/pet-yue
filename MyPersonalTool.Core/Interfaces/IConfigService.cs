using MyPersonalTool.Core.Models;

namespace MyPersonalTool.Core.Interfaces;

/// <summary>配置服务 —— 加载/保存应用配置</summary>
public interface IConfigService
{
    AppConfig Config { get; }
    void Save();

    /// <summary>插件读取配置值</summary>
    string? GetPluginValue(string key);

    /// <summary>插件写入配置值</summary>
    void SetPluginValue(string key, string value);
}
