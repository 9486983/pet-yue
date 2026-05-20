using MyPersonalTool.Core.Models;

namespace MyPersonalTool.Core.Interfaces;

/// <summary>配置服务 —— 加载/保存应用配置</summary>
public interface IConfigService
{
    AppConfig Config { get; }
    void Save();
}
