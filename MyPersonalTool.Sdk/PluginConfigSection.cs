namespace MyPersonalTool.Sdk;

/// <summary>插件配置分区 —— 插件在初始化时通过 <see cref="IPluginHost.RegisterConfig"/> 注册</summary>
public class PluginConfigSection
{
    /// <summary>分区标题（如 "DeepSeek API 配置"）</summary>
    public string Title { get; set; } = "";

    /// <summary>标题前的 Emoji 图标</summary>
    public string? Emoji { get; set; }

    /// <summary>配置字段列表</summary>
    public List<PluginConfigField> Fields { get; set; } = new();
}
