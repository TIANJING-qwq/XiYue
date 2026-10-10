using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FluentAvalonia.Styling;
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
        ApplyTheme(ConfigManager.Instance.ThemeMode);
        ToastHost.AutoCollapseOnNew = ConfigManager.Instance.AutoCollapseOnNewToast;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            bool isAutoStart = Environment.GetCommandLineArgs()
                .Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase));

            bool startMinimized = isAutoStart && ConfigManager.Instance.AutoStartMinimized;

            // ★ 无论如何都创建窗口，但只有非静默模式才 Show
            var window = new MainWindow(startMinimized);
            desktop.MainWindow = window;

            if (!startMinimized)
            {
                // 普通启动：立即显示
                window.Show();
            }
            else
            {
                // 自启动静默：不显示窗口，只运行托盘
                // 窗口保持未显示状态（永远不会闪一帧）
                LogService.Log("自启动静默模式：主窗口未显示", "启动");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(string mode)
    {
        if (Current == null) return;

        var variant = mode switch
        {
            "Light" => ThemeVariant.Light,
            "Dark"  => ThemeVariant.Dark,
            _       => ThemeVariant.Default,
        };

        Current.RequestedThemeVariant = variant;

        foreach (var style in Current.Styles)
        {
            if (style is FluentAvaloniaTheme fa)
                fa.PreferSystemTheme = (mode == "Default");
        }

        LogService.Log($"主题已切换为: {mode}", "主题");
    }
}