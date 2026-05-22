using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using MyPersonalTool.Services;
using MyPersonalTool.ViewModels;

namespace MyPersonalTool;

public partial class App : Application
{
    public static Window? SettingsWindow { get; private set; }
    public static PetViewModel? PetViewModel { get; private set; }

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
            var configService = new ConfigService();
            var dispatcher = new AvaloniaDispatcherService();
            var petdexService = new PetdexService();
            var activityMonitor = new ActivityMonitor();
            var healthService = new HealthReminderService(configService, dispatcher);
            var config = configService.Config;

            // ── 加载主题资源 ──
            LoadThemeResources(config.IsDarkTheme);

            // ── 监听主题切换 ──
            Core.Models.PetEvents.ThemeChanged += isDark =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => LoadThemeResources(isDark));

            // ── 加载插件 ──
            var pluginHost = new PluginHostImpl(configService);
            var pluginLoader = new PluginLoader();
            pluginLoader.LoadFromDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins"));
            _ = pluginLoader.InitializeAllAsync(pluginHost);

            // ── 主设置窗口 ──
            var mainVm = new MainViewModel(configService, dispatcher, pluginLoader);
            var mainWindow = new MainWindow { DataContext = mainVm };
            SettingsWindow = mainWindow;

            // ── 宠物窗作为主窗口 ──
            var petVm = new PetViewModel(configService, dispatcher, petdexService,
                activityMonitor, healthService, pluginHost.PluginActions);
            PetViewModel = petVm;
            var petWindow = new PetWindow
            {
                DataContext = petVm,
                Position = new PixelPoint((int)config.PetWindowX, (int)config.PetWindowY),
            };

            // ── 连接插件气泡回调 ──
            pluginHost.OnShowThought = (title, text) =>
                petVm.ShowFileDropInfo(title, text);

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

        // 移除旧主题资源
        Resources.MergedDictionaries.Clear();

        // 加载新主题颜色资源
        var uri = isDark ? DarkThemeUri : LightThemeUri;
        Resources.MergedDictionaries.Add(
            (ResourceDictionary)AvaloniaXamlLoader.Load(uri));
    }
}
