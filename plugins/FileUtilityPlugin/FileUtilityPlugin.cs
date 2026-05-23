using MyPersonalTool.Core.Models;
using MyPersonalTool.Sdk;

namespace FileUtilityPlugin;

[Plugin("文件工具", Version = "1.0.0", Description = "根据拖入类型自动显示文件/文件夹/文本信息")]
public class FileUtilityPlugin : PluginBase
{
    private static readonly HashSet<string> TextExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".csv", ".json", ".xml", ".yaml", ".yml",
            ".ini", ".cfg", ".conf", ".log", ".bat", ".cmd", ".ps1",
            ".sh", ".py", ".js", ".ts", ".jsx", ".tsx", ".css", ".html",
            ".cs", ".cpp", ".c", ".h", ".hpp", ".java", ".rs", ".go",
            ".rb", ".php", ".sql", ".r", ".swift", ".kt", ".dart",
        };

    public override string Name => "文件工具";

    public override async Task InitializeAsync(IPluginHost host)
    {
        host.RegisterAction(new PluginAction
        {
            Name = "查看详情",
            Emoji = "🔍",
            Description = "自动识别类型：文件夹信息 / 文件信息 / 文本预览",
            Target = ActionTarget.RadialMenu,
            AcceptType = ItemType.Both,
            CanActivate = true,
            FileCallback = async (paths) =>
            {
                try
                {
                    var path = paths[0];

                    // 文件夹
                    if (Directory.Exists(path))
                    {
                        var dir = new DirectoryInfo(path);
                        var subDirs = dir.GetDirectories().Length;
                        var files = dir.GetFiles();
                        var totalSize = files.Sum(f => f.Length);

                        host.ShowThought("📁 文件夹详情",
                            $"名称: {dir.Name}\n" +
                            $"位置: {dir.FullName}\n" +
                            $"子文件夹: {subDirs} 个\n" +
                            $"文件: {files.Length} 个\n" +
                            $"总大小: {FormatSize(totalSize)}");
                        return;
                    }

                    // 文件
                    var fi = new FileInfo(path);
                    if (!fi.Exists) { host.ShowThought("❌ 错误", "文件不存在"); return; }

                    var ext = fi.Extension.ToLowerInvariant();

                    // 文本文件 → 预览内容
                    if (TextExtensions.Contains(ext))
                    {
                        var text = await File.ReadAllTextAsync(path);
                        var preview = text.Length > 500 ? text[..500] + "\n\n…（仅显示前 500 字符）" : text;
                        host.ShowThought("📄 文本预览",
                            $"文件: {fi.Name}\n" +
                            $"大小: {FormatSize(fi.Length)}\n" +
                            $"修改: {fi.LastWriteTime:yyyy-MM-dd HH:mm}\n\n" +
                            $"─── 内容预览 ───\n{preview}");
                        return;
                    }

                    // 普通文件
                    host.ShowThought("📄 文件详情",
                        $"名称: {fi.Name}\n" +
                        $"类型: {fi.Extension} 文件\n" +
                        $"大小: {FormatSize(fi.Length)}\n" +
                        $"位置: {fi.DirectoryName}\n" +
                        $"修改: {fi.LastWriteTime:yyyy-MM-dd HH:mm}\n" +
                        $"创建: {fi.CreationTime:yyyy-MM-dd HH:mm}");
                }
                catch (Exception ex) { host.ShowThought("❌ 错误", ex.Message); }
            },
        });

        await Task.CompletedTask;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB",
    };
}
