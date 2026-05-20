namespace MyPersonalTool.Core.Models;

/// <summary>跨程序集事件总线，用于 MainViewModel ↔ PetViewModel 通信</summary>
public static class PetEvents
{
    /// <summary>配置已保存（设置页点击保存时触发）</summary>
    public static event Action? ConfigSaved;

    public static void NotifyConfigSaved() => ConfigSaved?.Invoke();
}
