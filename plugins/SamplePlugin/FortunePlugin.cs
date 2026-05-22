using MyPersonalTool.Sdk;

namespace SamplePlugin;

/// <summary>
/// 示例插件 —— 演示如何通过 PluginAction 注册右键菜单功能
/// </summary>
[Plugin("每日运势", Version = "2.0.0", Description = "每天一句治愈小语")]
public class FortunePlugin : PluginBase
{
    public override string Name => "每日运势";
    public override string Description => "每天一句治愈小语";

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

    private static readonly string[] Praises =
    [
        "你是全世界最棒的人！🎉",
        "今天的状态好到发光！✨",
        "你的存在让世界更美好了🌸",
        "智商和颜值并存的天才！🧠",
    ];

    public override async Task InitializeAsync(IPluginHost host)
    {
        var rng = new Random();

        // 右键菜单动作：使用 PluginAction 描述符
        host.RegisterAction(new PluginAction
        {
            Name = "抽运势",
            Emoji = "🔮",
            Description = "看看今天的幸运签",
            Group = "每日运势",
            Target = ActionTarget.ContextMenu,
            Callback = async () =>
            {
                var fortune = Fortunes[rng.Next(Fortunes.Length)];
                host.ShowThought("🔮 今日运势", fortune);
            },
        });

        host.RegisterAction(new PluginAction
        {
            Name = "夸夸我",
            Emoji = "🌟",
            Description = "让宠物夸你一句",
            Group = "每日运势",
            Target = ActionTarget.ContextMenu,
            Callback = async () =>
            {
                var praise = Praises[rng.Next(Praises.Length)];
                host.ShowThought("🌟 夸夸时间", praise);
            },
        });

        // 文件拖放动作：只对 .txt 文件生效
        host.RegisterAction(new PluginAction
        {
            Name = "读文本",
            Emoji = "📖",
            Description = "读取文本文件内容并在气泡中展示",
            FileExtensions = [".txt", ".md", ".csv"],
            Target = ActionTarget.RadialMenu,
            FileCallback = async (files) =>
            {
                try
                {
                    var text = await File.ReadAllTextAsync(files[0]);
                    var preview = text.Length > 300 ? text[..300] + "…" : text;
                    host.ShowThought("📖 文件内容", preview);
                }
                catch (Exception ex)
                {
                    host.ShowThought("❌ 读取失败", ex.Message);
                }
            },
        });

        host.Log("每日运势插件已加载，祝你好运！🍀");
        await Task.CompletedTask;
    }
}
