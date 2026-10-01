using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Runtime.InteropServices;

namespace SBtools.Services;

/// <summary>
/// 窗口背景材质工具。支持 None / Acrylic / Mica 三种模式。
/// 云母和亚克力仅在深色模式下生效，浅色模式下自动回退到不透明背景。
/// 亚克力/云母模式下强制让 Windows 标题栏保持不透明。
/// </summary>
public static class WindowBackdropService
{
    // ★ DWM 标题栏 API
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR    = 36;
    private const int DWMWA_COLOR_DEFAULT = -1;  // 0xFFFFFFFF 作为 int

    public static void Apply(Window window, string mode)
    {
        if (window == null) return;

        try
        {
            bool isDark = window.ActualThemeVariant == ThemeVariant.Dark;
            var solidColor = isDark ? Color.Parse("#FF202020") : Color.Parse("#FFF3F3F3");

            // 浅色模式下：云母/亚克力不支持，自动回退到不透明
            bool transparentSupported = isDark;
            if (!transparentSupported && (mode == "Acrylic" || mode == "Mica"))
            {
                LogService.Log($"浅色模式下 {mode} 不受支持，回退到不透明背景", "主题");
                mode = "None";
            }

            switch (mode)
            {
                case "Acrylic":
                    window.TransparencyBackgroundFallback = new SolidColorBrush(solidColor);
                    window.TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur };
                    window.Background = new SolidColorBrush(Color.Parse("#01000000"));
                    break;

                case "Mica":
                    window.TransparencyBackgroundFallback = new SolidColorBrush(solidColor);
                    window.TransparencyLevelHint = new[] { WindowTransparencyLevel.Mica };
                    window.Background = new SolidColorBrush(Color.Parse("#01000000"));
                    break;

                case "None":
                default:
                    window.TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
                    window.Background = new SolidColorBrush(solidColor);
                    break;
            }

            // ★ 亚克力/云母模式下：强制标题栏为不透明
            ApplyTitleBarColor(window, mode, isDark);

            LogService.Log($"窗口背景材质已切换为: {mode}（isDark={isDark}）", "主题");
        }
        catch (Exception ex)
        {
            LogService.Log($"应用窗口材质失败: {ex.Message}", "主题");
        }
    }

    /// <summary>强制标题栏使用不透明颜色。</summary>
    private static void ApplyTitleBarColor(Window window, string mode, bool isDark)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var handle = window.TryGetPlatformHandle();
            if (handle == null || handle.Handle == IntPtr.Zero) return;

            if (mode == "Acrylic" || mode == "Mica")
            {
                // 亚克力/云母：标题栏用不透明颜色
                // COLORREF = 0x00BBGGRR
                int captionBgr = isDark ? 0x00202020 : 0x00F3F3F3;
                int textBgr    = isDark ? 0x00FFFFFF : 0x00000000;

                DwmSetWindowAttribute(handle.Handle, DWMWA_CAPTION_COLOR,
                    ref captionBgr, sizeof(int));
                DwmSetWindowAttribute(handle.Handle, DWMWA_TEXT_COLOR,
                    ref textBgr, sizeof(int));

                LogService.Log("标题栏已设为不透明", "主题");
            }
            else
            {
                // 恢复默认标题栏颜色
                int defaultColor = DWMWA_COLOR_DEFAULT;
                DwmSetWindowAttribute(handle.Handle, DWMWA_CAPTION_COLOR,
                    ref defaultColor, sizeof(int));
                DwmSetWindowAttribute(handle.Handle, DWMWA_TEXT_COLOR,
                    ref defaultColor, sizeof(int));
            }
        }
        catch (Exception ex)
        {
            // DWM API 在 Win10 或老版本 Win11 上可能不支持，忽略
            LogService.Log($"设置标题栏颜色失败（可能系统不支持）: {ex.Message}", "主题");
        }
    }

    public static bool IsTransparentModeSupported(Window window)
    {
        if (window == null) return false;
        return window.ActualThemeVariant == ThemeVariant.Dark;
    }

    public static void RefreshAll()
    {
        try
        {
            var mode = Models.ConfigManager.Instance.WindowBackdrop;

            if (Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow is Window main)
                    Apply(main, mode);

                foreach (var w in desktop.Windows)
                {
                    if (w != desktop.MainWindow)
                        Apply(w, mode);
                }
            }
        }
        catch { }
    }
}