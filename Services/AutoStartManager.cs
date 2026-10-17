using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace SBtools.Services;

/// <summary>
/// 开机自启动管理。注册的启动命令必须带 --autostart 参数，
/// 否则 App.axaml.cs 无法区分"用户手动启动"和"系统开机启动"。
/// </summary>
public static class AutoStartManager
{
    private const string AppName = "Vael";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>应用路径（publish 后的 exe）</summary>
    private static string ExePath
    {
        get
        {
            try
            {
                var path = Process.GetCurrentProcess().MainModule?.FileName;
                return string.IsNullOrEmpty(path)
                    ? Path.Combine(AppContext.BaseDirectory, "SBtools.exe")
                    : path;
            }
            catch
            {
                return Path.Combine(AppContext.BaseDirectory, "SBtools.exe");
            }
        }
    }

    /// <summary>应用是否已注册开机自启动</summary>
    public static bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>启用或禁用开机自启动</summary>
    public static void Apply(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (enabled)
            {
                // ★ 关键：带 --autostart 参数
                var command = $"\"{ExePath}\" --autostart";
                key.SetValue(AppName, command, RegistryValueKind.String);

                LogService.Log($"已启用开机自启动: {command}", "启动");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                LogService.Log("已禁用开机自启动", "启动");
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"设置开机自启动失败: {ex.Message}", "启动");
            throw;
        }
    }

    /// <summary>修复已存在的注册表项（去掉旧版没带参数的值）</summary>
    public static void RepairIfNeeded()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            var current = key.GetValue(AppName) as string;
            if (string.IsNullOrEmpty(current)) return;

            // 如果已存在但没有 --autostart 参数，修正它
            if (!current.Contains("--autostart", StringComparison.OrdinalIgnoreCase))
            {
                var command = $"\"{ExePath}\" --autostart";
                key.SetValue(AppName, command, RegistryValueKind.String);
                LogService.Log($"已修复自启动命令: {command}", "启动");
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"修复自启动命令失败: {ex.Message}", "启动");
        }
    }
}