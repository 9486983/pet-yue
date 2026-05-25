using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using MyPersonalTool.Core.Interfaces;
using MyPersonalTool.Services;
using MyPersonalTool.ViewModels;

namespace MyPersonalTool;

public partial class App : Application
{
    public static Window? SettingsWindow { get; private set; }
    public static PetViewModel? PetViewModel { get; private set; }

    private ServiceProvider? _serviceProvider;

    private static readonly Uri DarkThemeUri = new("avares://MyPersonalTool/Styles/Themes/Dark.axaml");
    private static readonly Uri LightThemeUri = new("avares://MyPersonalTool/Styles/Themes/Light.axaml");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ── 构建 DI 容器 ──
            var services = new ServiceCollection();

            // 基础设施（Singleton）
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<IDispatcherService, AvaloniaDispatcherService>();
            services.AddSingleton<IPetdexService, PetdexService>();
            services.AddSingleton<IActivityMonitor, ActivityMonitor>();
            services.AddSingleton<HealthReminderService>();
            services.AddSingleton<PluginHostImpl>();
            services.AddSingleton<PluginLoader>();

            // ViewModels（Singleton）
            services.AddSingleton<MainViewModel>();

            _serviceProvider = services.BuildServiceProvider();

            // ── 从容器解析服务 ──
            var configService = _serviceProvider.GetRequiredService<IConfigService>();
            var dispatcher = _serviceProvider.GetRequiredService<IDispatcherService>();
            var petdexService = _serviceProvider.GetRequiredService<IPetdexService>();
            var activityMonitor = _serviceProvider.GetRequiredService<IActivityMonitor>();
            var healthService = _serviceProvider.GetRequiredService<HealthReminderService>();
            var pluginHost = _serviceProvider.GetRequiredService<PluginHostImpl>();
            var pluginLoader = _serviceProvider.GetRequiredService<PluginLoader>();
            var config = configService.Config;

            // ── 加载主题资源 ──
            LoadThemeResources(config.IsDarkTheme);

            // ── 监听主题切换 ──
            Core.Models.PetEvents.ThemeChanged += isDark =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => LoadThemeResources(isDark));

            // ── 加载并初始化插件 ──
            pluginLoader.LoadFromDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins"));
            _ = pluginLoader.InitializeAllAsync(pluginHost);

            // ── 主设置窗口（MainViewModel 由 DI 构建，自动注入 PluginLoader） ──
            var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVm };
            SettingsWindow = mainWindow;

            // ── 宠物窗口 ──
            var petVm = new PetViewModel(configService, dispatcher, petdexService,
                activityMonitor, healthService, pluginHost.PluginActions,
                pluginHost.FileActions);
            PetViewModel = petVm;
            var petWindow = new PetWindow
            {
                DataContext = petVm,
                Position = new PixelPoint((int)config.PetWindowX, (int)config.PetWindowY),
            };

            // ── 连接插件气泡回调 ──
            pluginHost.OnShowThought = (title, text) =>
                petVm.ShowFileDropInfo(title, text);
            pluginHost.OnShowReaction = emoji =>
                petVm.ShowReaction(emoji);
            pluginHost.OnStartAnimation = anim =>
                petVm.AnimCurrentRow = (int)anim;
            pluginHost.OnStopAnimation = () =>
                petVm.AnimCurrentRow = 0;
            pluginHost.OnTaskRunningChanged = running =>
                petVm.IsTaskRunning = running;
            petVm.CancelTaskCallback = () =>
                pluginHost.CancelCurrentTask();

            // ── 连接会话事件 ──
            pluginHost.OnSessionStarted = session =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => petVm.OnSessionStarted(session));
            pluginHost.OnSessionEnded = () =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => petVm.OnSessionEnded());
            petVm.EndSessionCallback = () =>
                pluginHost.CurrentSession?.Cancel();

            // ── 连接插件配置弹窗 ──
            pluginHost.OnShowPluginConfig = async section =>
            {
                await Views.PluginConfigDialog.ShowAsync(petWindow, section,
                    key => configService.GetPluginValue(key),
                    values => pluginHost.SavePluginConfig(values));
            };

            // ── 连接剪贴板 ──
            petVm.ClipboardSetText = text =>
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-Command \"Set-Clipboard -Value '{text.Replace("'", "''")}'\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch { }
            };

            // ── 连接插件输入框回调 ──
            pluginHost.OnShowInputDialog = async (title, placeholder, initial) =>
                await Views.InputDialog.ShowAsync(petWindow, title, placeholder, initial);

            desktop.MainWindow = petWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>加载主题资源 + 设置 Fluent 主题</summary>
    private void LoadThemeResources(bool isDark)
    {
        RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;

        Resources.MergedDictionaries.Clear();

        var uri = isDark ? DarkThemeUri : LightThemeUri;
        Resources.MergedDictionaries.Add(
            (ResourceDictionary)AvaloniaXamlLoader.Load(uri));
    }
}
