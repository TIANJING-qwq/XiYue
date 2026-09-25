using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace SBtools.Services;

public static class AutoStartManager
{
    private const string RegKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "XiYue";

    public static void Apply(bool enable)
    {
        if (OperatingSystem.IsWindows())
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKey, writable: true);
            if (key == null) throw new Exception("无法打开注册表");

            if (enable)
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName
                          ?? throw new Exception("无法获取程序路径");
                key.SetValue(AppName, $"\"{exe}\"");
            }
            else
            {
                if (key.GetValue(AppName) != null)
                    key.DeleteValue(AppName);
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            // macOS：写入 LaunchAgents plist
            var plist = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library/LaunchAgents/com.schoolbusytools.xiyue.plist");
            if (enable)
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                var content = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0""><dict>
<key>Label</key><string>com.schoolbusytools.xiyue</string>
<key>ProgramArguments</key><array><string>{exe}</string></array>
<key>RunAtLoad</key><true/>
</dict></plist>";
                System.IO.File.WriteAllText(plist, content);
            }
            else
            {
                if (System.IO.File.Exists(plist)) System.IO.File.Delete(plist);
            }
        }
        else
        {
            // Linux：写入 ~/.config/autostart
            var desktop = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config/autostart/xiyue.desktop");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(desktop)!);

            if (enable)
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                var content = $"[Desktop Entry]\nType=Application\nName=汐月\nExec={exe}\nX-GNOME-Autostart-enabled=true\n";
                System.IO.File.WriteAllText(desktop, content);
            }
            else
            {
                if (System.IO.File.Exists(desktop)) System.IO.File.Delete(desktop);
            }
        }
    }
}