using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SBtools.Controls;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Linq;

namespace SBtools;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ToastHost.AutoCollapseOnNew = ConfigManager.Instance.AutoCollapseOnNewToast;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ★ 只有显式 Shutdown 才退出
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            bool isAutoStart = Environment.GetCommandLineArgs()
                .Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase));

            bool startMinimized = isAutoStart && ConfigManager.Instance.AutoStartMinimized;

            var window = new MainWindow(startMinimized);
            desktop.MainWindow = window;

            // ★ 显式 Show()，让 Opened 事件触发
            window.Show();

            LogService.Log($"App 初始化完成，startMinimized={startMinimized}", "启动");
        }

        base.OnFrameworkInitializationCompleted();
    }
}