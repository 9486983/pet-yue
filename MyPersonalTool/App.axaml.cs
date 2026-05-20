using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MyPersonalTool.Services;
using MyPersonalTool.ViewModels;

namespace MyPersonalTool;

public partial class App : Application
{
    /// <summary>主设置窗口引用，供宠物窗右键菜单打开</summary>
    public static Window? SettingsWindow { get; private set; }

    /// <summary>宠物 ViewModel，供 MainViewModel 跨窗口同步配置</summary>
    public static PetViewModel? PetViewModel { get; private set; }

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

            // ── 加载插件 ──
            var pluginHost = new PluginHostImpl(configService);
            var pluginLoader = new PluginLoader();
            pluginLoader.LoadFromDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins"));
            _ = pluginLoader.InitializeAllAsync(pluginHost);

            // ── 主设置窗口（创建但不自动显示） ──
            var mainVm = new MainViewModel(configService, dispatcher, pluginLoader);
            var mainWindow = new MainWindow { DataContext = mainVm };
            SettingsWindow = mainWindow;

            // ── 宠物窗作为应用主窗口 ──
            var petVm = new PetViewModel(configService, dispatcher, petdexService,
                activityMonitor, healthService, pluginHost.PluginActions);
            PetViewModel = petVm;
            var petWindow = new PetWindow
            {
                DataContext = petVm,
                Position = new PixelPoint((int)config.PetWindowX, (int)config.PetWindowY),
            };

            desktop.MainWindow = petWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
