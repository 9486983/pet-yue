using MyPersonalTool.Sdk;

namespace SamplePlugin;

/// <summary>
/// 示例插件 —— 演示如何为桌面宠物添加自定义功能
/// 构建后自动复制到 plugins/ 目录，重启应用即可生效
/// </summary>
[Plugin("每日运势", Version = "1.0.0", Description = "每天一句治愈小语 + 星座运势")]
public class FortunePlugin : PluginBase
{
    public override string Name => "每日运势";
    public override string Description => "每天一句治愈小语 + 星座运势";

    private static readonly string[] Fortunes =
    [
        "✨ 今天会有好事发生～",
        "🌈 你笑起来的样子真好看",
        "🌻 今天也是被宇宙偏爱的一天",
        "🍀 好运正在赶来的路上",
        "💫 你比你以为的更强大",
        "🌸 保持可爱，世界会温柔待你",
        "🌟 今天的主角就是你",
        "🎈 放轻松，一切都会刚刚好",
    ];

    public override async Task InitializeAsync(IPluginHost host)
    {
        // 注册右键菜单动作
        host.RegisterAction("抽运势", "🔮", "🎴", "看看今天的幸运签");
        host.RegisterAction("夸夸我", "🌟", "💖", "让宠物夸你一句");
        host.RegisterAction("治愈小语", "🌿", "📖", "听一句温暖的话");

        host.Log("每日运势插件已加载，祝你好运！🍀");

        await Task.CompletedTask;
    }
}
