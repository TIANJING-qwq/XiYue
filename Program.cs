using Avalonia;
using LibVLCSharp.Shared;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SBtools;

class Program
{
    // ★ 全局唯一标识
    private const string MutexName = "Global\\XiYue_SingleInstance_Mutex";
    private const string AppTitle = "汐月 · XiYue";

    private static Mutex? _mutex;

    [STAThread]
    public static void Main(string[] args)
    {
        // ★ 单实例检测
        bool createdNew;
        _mutex = new Mutex(true, MutexName, out createdNew);

        if (!createdNew)
        {
            // 已有实例在运行 → 尝试把已有窗口带到前台，然后退出
            BringExistingWindowToFront();
            return;
        }

        try
        {
            Core.Initialize();
        }
        catch { }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    // ============================================================
    // 把已运行的窗口带到前台（跨平台）
    // ============================================================
    private static void BringExistingWindowToFront()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows：遍历进程找同名窗口
                var current = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcessesByName(current.ProcessName))
                {
                    if (p.Id == current.Id) continue;
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(p.MainWindowHandle, SW_RESTORE);
                        SetForegroundWindow(p.MainWindowHandle);
                        break;
                    }
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                // macOS：用 open -a 激活
                Process.Start(new ProcessStartInfo("open", "-a XiYue") { UseShellExecute = false });
            }
            else
            {
                // Linux：用 wmctrl（需安装）
                try
                {
                    Process.Start(new ProcessStartInfo("wmctrl", "-a \"汐月\"") { UseShellExecute = false });
                }
                catch { }
            }
        }
        catch { }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_RESTORE = 9;
}