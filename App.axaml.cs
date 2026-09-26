using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SBtools.Controls;
using SBtools.Models;
using System;
using System.Linq;

namespace SBtools;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // 应用通知配置
        ToastHost.AutoCollapseOnNew = ConfigManager.Instance.AutoCollapseOnNewToast;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ★ 检测是否由自启动触发
            bool startMinimized = Environment.GetCommandLineArgs()
                .Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase));

            desktop.MainWindow = new MainWindow(startMinimized);
        }

        base.OnFrameworkInitializationCompleted();
    }
}