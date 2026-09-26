using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace SBtools.Services;

public static class AutoStartManager
{
    private const string RegKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "XiYue";

    public static void Apply(bool enable)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKey, writable: true);
                if (key == null) return;

                if (enable)
                {
                    var exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    // ★ 加 --autostart 参数
                    key.SetValue(AppName, $"\"{exe}\" --autostart");
                    LogService.Log($"已注册开机自启动: {exe} --autostart", "自启动");
                }
                else
                {
                    if (key.GetValue(AppName) != null)
                        key.DeleteValue(AppName);
                    LogService.Log("已取消开机自启动", "自启动");
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                var plist = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library/LaunchAgents/com.schoolbusytools.xiyue.plist");

                if (enable)
                {
                    var exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    var content = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0""><dict>
<key>Label</key><string>com.schoolbusytools.xiyue</string>
<key>ProgramArguments</key><array>
    <string>{exe}</string>
    <string>--autostart</string>
</array>
<key>RunAtLoad</key><true/>
</dict></plist>";
                    File.WriteAllText(plist, content);
                }
                else
                {
                    if (File.Exists(plist)) File.Delete(plist);
                }
            }
            else
            {
                var desktop = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config/autostart/xiyue.desktop");
                Directory.CreateDirectory(Path.GetDirectoryName(desktop)!);

                if (enable)
                {
                    var exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    var content = $"[Desktop Entry]\nType=Application\nName=汐月\nExec={exe} --autostart\nX-GNOME-Autostart-enabled=true\n";
                    File.WriteAllText(desktop, content);
                }
                else
                {
                    if (File.Exists(desktop)) File.Delete(desktop);
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"自启动配置失败: {ex.Message}", "自启动");
            throw;
        }
    }
}