using Avalonia;
using LibVLCSharp.Shared;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools;

class Program
{
    private const string MutexName = "Global\\XiYue_SingleInstance_Mutex";
    private static Mutex? _mutex;

    [STAThread]
    public static void Main(string[] args)
    {
        // 全局异常兜底
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogException("AppDomain", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogException("Task", e.Exception);
            e.SetObserved();
        };

        // 单实例
        bool createdNew;
        _mutex = new Mutex(true, MutexName, out createdNew);
        if (!createdNew)
        {
            BringExistingWindowToFront();
            return;
        }

        try { Core.Initialize(); }
        catch (Exception ex) { LogException("VLC", ex); }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void LogException(string source, Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SchoolBusytools", "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, $"error_{DateTime.Now:yyyyMMdd}.log"),
                $"[{DateTime.Now:HH:mm:ss}] [{source}] {ex}\n\n");
        }
        catch { }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void BringExistingWindowToFront()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
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
        }
        catch { }
    }

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    private const int SW_RESTORE = 9;
}